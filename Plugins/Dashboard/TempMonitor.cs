using System.Diagnostics;
using LibreHardwareMonitor.Hardware;

namespace YanJi.Plugin.Dashboard;

/// <summary>Nhiệt độ live ở Dashboard, không cần vô phần test: mở Computer
/// LHM riêng, đọc 2 đường duy nhất — nhiệt độ CPU (Package) + nhiệt độ GPU —
/// mỗi 2 giây, báo về qua sự kiện Sampled. Chỉ hiển thị, không có ngưỡng,
/// không chặn, không đưa vào kết luận mua (ngưỡng CPU quá nhiệt vẫn là việc
/// của trang Tản nhiệt, xem SensorMonitorService).
/// Lifecycle: Dashboard Loaded mở, Unloaded đóng — mỗi trang tự mở Computer
/// của mình, giống SensorMonitorService, không dùng chung, không giữ driver
/// cả phiên. CPU cần driver PawnIO (Ring0); chưa cài thì CPU null, GPU
/// (NVAPI/ADL) vẫn có thể đọc được. Cảm biến không có / giá trị ≤0 (AMD
/// không PawnIO thì LHM trả 0 cố định) đều coi là không có.</summary>
public static class TempMonitor
{
    /// <summary>Một lần đọc nhiệt độ (null = đường đó không đọc được).
    /// Chạy trên thread nền — bên nhận tự đưa về UI thread.</summary>
    public static event Action<double?, double?>? Sampled;

    static readonly object _gate = new();
    static CancellationTokenSource? _cts;
    static Task? _loop;

    public static bool IsRunning { get { lock (_gate) return _loop is { IsCompleted: false }; } }

    /// <summary>Bắt đầu đọc (lũy đẳng: đang chạy thì trả về luôn).
    /// Computer.Open chậm nên toàn bộ chạy trên thread nền.</summary>
    public static void Start()
    {
        lock (_gate)
        {
            if (IsRunning) return;
            _cts = new CancellationTokenSource();
            _loop = Task.Run(() => Loop(_cts.Token));
        }
    }

    /// <summary>Dừng đọc và đóng Computer (đợi tối đa 3 giây).</summary>
    public static void Stop()
    {
        Task? loop;
        lock (_gate)
        {
            loop = _loop;
            _loop = null;
            try { _cts?.Cancel(); } catch { /* bỏ qua */ }
            _cts?.Dispose();
            _cts = null;
        }
        try { loop?.Wait(3000); } catch { /* lỗi thoát không ảnh hưởng dọn dẹp */ }
    }

    static void Loop(CancellationToken ct)
    {
        var computer = new Computer { IsCpuEnabled = true, IsGpuEnabled = true };
        try
        {
            EnsurePawnIoService(); // PawnIO đã cài nhưng service chưa chạy thì kéo lên
            computer.Open();

            var cpu = computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
            var cpuTemp = Find(cpu, SensorType.Temperature, "Package");
            var gpus = computer.Hardware.Where(h =>
                h.HardwareType is HardwareType.GpuAmd or HardwareType.GpuNvidia or HardwareType.GpuIntel &&
                h.Sensors.Any(s => s.SensorType == SensorType.Temperature)).ToList();
            // Ưu tiên card rời (cùng cách chọn mặc định của SensorMonitorService),
            // không có card rời thì lấy card đầu tiên.
            var gpu = gpus.FirstOrDefault(g => IsDiscreteGpu(g.HardwareType, g.Name)) ?? gpus.FirstOrDefault();
            var gpuTemp = Find(gpu, SensorType.Temperature, "GPU Core", "GPU");

            while (!ct.IsCancellationRequested)
            {
                try { cpu?.Update(); } catch { /* lần đọc lỗi thì giữ giá trị cũ */ }
                try { gpu?.Update(); } catch { /* lần đọc lỗi thì giữ giá trị cũ */ }
                try { Sampled?.Invoke(Valid(cpuTemp), Valid(gpuTemp)); } catch { /* bên nhận lỗi không làm chết vòng đọc */ }
                ct.WaitHandle.WaitOne(2000); // đọc mỗi 2 giây, giống trang Tản nhiệt
            }
        }
        catch (OperationCanceledException) { /* dừng bình thường */ }
        catch { /* LHM mở/đọc lỗi: lặng, Dashboard giữ dòng "chưa đọc được" */ }
        finally
        {
            try { computer.Close(); } catch { /* đóng lỗi không ảnh hưởng */ }
        }
    }

    static double? Valid(ISensor? s) =>
        s?.Value is float v && v > 0 ? v : null;

    /// <summary>Tìm cảm biến theo tên ưu tiên, không trúng thì lấy cái đầu tiên cùng loại.</summary>
    static ISensor? Find(IHardware? hw, SensorType type, params string[] keywords)
    {
        if (hw == null) return null;
        var all = hw.Sensors.Where(s => s.SensorType == type).ToList();
        foreach (string k in keywords)
        {
            var hit = all.FirstOrDefault(s => s.Name.Contains(k, StringComparison.OrdinalIgnoreCase));
            if (hit != null) return hit;
        }
        return all.FirstOrDefault();
    }

    /// <summary>Card rời hay card tích hợp (cùng cách nhận biết của
    /// SensorMonitorService): NVIDIA luôn rời; AMD tên có "RX" là rời, tên
    /// kết thúc bằng "Graphics" (APU) là tích hợp; Intel tên có "Arc" là rời.</summary>
    static bool IsDiscreteGpu(HardwareType type, string name) => type switch
    {
        HardwareType.GpuNvidia => true,
        HardwareType.GpuAmd =>
            name.Contains("RX", StringComparison.OrdinalIgnoreCase) ||
            !name.TrimEnd().EndsWith("Graphics", StringComparison.OrdinalIgnoreCase),
        HardwareType.GpuIntel => name.Contains("Arc", StringComparison.OrdinalIgnoreCase),
        _ => false,
    };

    /// <summary>PawnIO đã cài nhưng service chưa chạy (StartType=Manual, sau
    /// khi khởi động lại máy service không tự chạy) thì kéo lên — cùng cách
    /// SensorMonitorService làm, để CPU đọc được ngay mà không cần vô trang test.</summary>
    static void EnsurePawnIoService()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine
                .OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO");
            if (key?.GetValue("DisplayVersion") == null) return; // chưa cài driver thì thôi
            using var p = Process.Start(new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = "start PawnIO",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            })!;
            p.WaitForExit(15_000);
        }
        catch { /* kéo không lên thì cảm biến CPU coi như không có */ }
    }
}
