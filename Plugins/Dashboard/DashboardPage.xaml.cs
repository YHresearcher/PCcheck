using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Collections.Generic;
using YanJi.PluginSdk;

namespace YanJi.Plugin.Dashboard;

/// <summary>Trang Tổng quan: tiến độ + kết luận mua hiện tại + nút nhảy.
/// Kết luận mua tính đúng logic trang Tổng kết (Chặn = Không khuyến nghị mua,
/// Cảnh báo = Cần kiểm tra thêm, tất cả Đạt = Có thể mua, còn lại = Chưa đủ
/// dữ liệu), nhưng KHÔNG chọn câu nhận xét random ở đây — câu đó chỉ ở Tổng kết.
/// Nút Bắt đầu / Tiếp tục: vào wizard rồi nhảy tới bài chưa xong đầu tiên
/// (chưa xong bài nào thì là bài đầu tiên); đó chính là "tiếp tục phiên dang dở"
/// ở mức phiên test trong app (phiên đã lưu ra USB thì xem lại ở thư mục hồ sơ).</summary>
public partial class DashboardPage : UserControl
{
    readonly IHostContext _host;

    public DashboardPage(IHostContext host)
    {
        _host = host;
        InitializeComponent();
        _host.DoneChanged += Refresh;
        _host.VerdictChanged += Refresh;
        _host.SignalChanged += Refresh;
        _host.DriveSummariesChanged += Refresh;
        Loaded += (_, _) => Refresh();
    }

    void StartButton_Click(object sender, RoutedEventArgs e)
    {
        var passable = _host.Modules.Where(m => !m.Terminal).OrderBy(m => m.Order).ToList();
        // Đã xong hết thì nút này chỉ là lối tắt tới Tổng kết, không vào wizard nữa.
        // Dùng 1 handler duy nhất quyết định theo trạng thái lúc bấm, không đổi handler
        // theo Refresh (đổi handler theo Refresh sẽ gắn trùng handler khi Refresh chạy nhiều lần).
        if (passable.Count > 0 && passable.All(m => _host.IsDone(m.Id)))
        {
            _host.NavigateTo("summary");
            return;
        }
        string target = passable.FirstOrDefault(m => !_host.IsDone(m.Id))?.Id
            ?? passable.FirstOrDefault()?.Id ?? "summary";
        _host.StartWizard();
        if (!_host.NavigateTo(target))
        {
            // Không nhảy được (module bị xóa lúc chạy) thì thoát wizard để khỏi kẹt
            _host.ExitWizard();
        }
    }

    void ShopButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://vitinhantam.com") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Chưa mở được website: {ex.Message}", "Tham quan shop",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    void StopButton_Click(object sender, RoutedEventArgs e)
    {
        var survivors = _host.StopAllHeavyTests();
        if (survivors.Count == 0)
            MessageBox.Show("Không phát hiện tiến trình test nặng nào còn chạy. Nếu đang kiểm tra thủ công, bạn vẫn có thể tiếp tục nha!",
                "An Tâm PC check", MessageBoxButton.OK, MessageBoxImage.Information);
        else
            MessageBox.Show($"Còn tiến trình chưa dừng: {string.Join(", ", survivors)}. Hãy kiểm tra Task Manager nhé.",
                "Cần kiểm tra thêm", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    public sealed record TestLine(
        string Icon, string Name, string Status, Brush StatusBrush,
        Brush RowBrush, Brush IconBrush, Brush IconBackground, string Indicator, string Detail);

    static Brush HexBrush(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }

    // Các hàng đúng thứ tự và màu của bản thiết kế đã duyệt.
    // AV bao gồm 3 phép kiểm tra (loa / webcam / microphone) trong 1 plugin.
    // Trackpad chỉ được tính sau khi kỹ thuật viên đánh dấu xác minh ở Ngoại quan.
    static readonly (string Id, string Name, string Icon, string Row, string IconBg, string Fg)[] Rows =
    [
        ("screen",   "Màn hình",      "▣", "#E5F4FF", "#D2EAFF", "#1688ED"),
        ("av",       "Loa",           "♫", "#EEECFF", "#E1E0FF", "#536BFF"),
        ("av",       "Webcam",        "▣", "#F1E5FF", "#E5D2FF", "#8645E8"),
        ("keyboard", "Bàn phím",      "⌨", "#FFE7F2", "#FFD5EC", "#E83B9B"),
        ("trackpad", "Trackpad",      "▭", "#DFF8F0", "#C8F1E4", "#08BA87"),
        ("av",       "Microphone",    "♩", "#E6F8E7", "#D2F3D5", "#12AF37"),
        ("ports",    "Cổng kết nối",  "ϟ", "#FFE9E3", "#FFDAD0", "#F26427"),
        ("visual",   "Ngoại quan",    "▦", "#FFF2D4", "#FFE8B0", "#E8A600")
    ];


    static string IconFor(string id) => id.ToLowerInvariant() switch
    {
        "config" => "▣", "keyboard" => "⌨", "screen" => "▤",
        "av" => "♫", "stress" => "◉", "port" => "⌁", "visual" => "◈",
        "disk" => "▥", _ => "○"
    };

    static string ReadCpuName()
    {
        try
        {
            return Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0",
                "ProcessorNameString", null)?.ToString()?.Trim() ?? "Chưa đọc được";
        }
        catch { return "Chưa đọc được"; }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MemoryStatus
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile;
        public ulong TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);

    static string ReadPhysicalMemory()
    {
        try
        {
            var status = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
            return GlobalMemoryStatusEx(ref status)
                ? $"{status.TotalPhysical / 1073741824.0:0.#} GB"
                : "Chưa đọc được";
        }
        catch { return "Chưa đọc được"; }
    }

    void HardwareButton_Click(object sender, RoutedEventArgs e) => _host.NavigateTo("config");
    void HealthButton_Click(object sender, RoutedEventArgs e) => _host.NavigateTo("health");
    void SummaryButton_Click(object sender, RoutedEventArgs e) => _host.NavigateTo("summary");

    void ReportsButton_Click(object sender, RoutedEventArgs e)
    {
        string dir = YanJi.PluginSdk.Services.AppSettings.GetKetQuaDir();
        try
        {
            Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không mở được thư mục hồ sơ ({dir}): {ex.Message}",
                "Mở thư mục hồ sơ", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    void Refresh()
    {
        var passable = _host.Modules.Where(m => !m.Terminal).OrderBy(m => m.Order).ToList();
        int done = passable.Count(m => _host.IsDone(m.Id));
        int total = passable.Count;
        ProgressText.Text = total > 0
            ? $"{done}/{total} mục đã xong"
            : "Chưa có mục kiểm tra";

        var tests = new List<TestLine>();
        bool trackpadVerified = _host.IsDone("visual")
            && _host.Signals.TryGetValue("visual", out var visualSignals)
            && visualSignals.Contains("trackpadTested");
        bool trackpadFailed = _host.Signals.TryGetValue("visual", out var tpSignals)
            && tpSignals.Contains("trackpadFail");

        foreach (var spec in Rows)
        {
            ModuleVerdictLevel grade;
            if (spec.Id == "trackpad")
                grade = !trackpadVerified ? ModuleVerdictLevel.ChuaTest
                    : trackpadFailed ? ModuleVerdictLevel.CanhBao : ModuleVerdictLevel.Dat;
            else if (_host.Verdicts.TryGetValue(spec.Id, out var verdict))
                grade = verdict.Level;
            else
                grade = _host.IsDone(spec.Id) ? ModuleVerdictLevel.Dat : ModuleVerdictLevel.ChuaTest;

            string label = grade switch
            {
                ModuleVerdictLevel.Dat => "Đạt",
                ModuleVerdictLevel.CanhBao => "Cảnh báo",
                ModuleVerdictLevel.Chan => "Có lỗi",
                _ => "Chưa thử"
            };
            Brush color = grade switch
            {
                ModuleVerdictLevel.Dat => HexBrush("#18AD4D"),
                ModuleVerdictLevel.CanhBao => HexBrush("#DE870F"),
                ModuleVerdictLevel.Chan => HexBrush("#D33D42"),
                _ => HexBrush("#888080")
            };
            string indicator = grade switch
            {
                ModuleVerdictLevel.Dat => "●",
                ModuleVerdictLevel.CanhBao => "!",
                ModuleVerdictLevel.Chan => "✕",
                _ => "○"
            };
            string detail = spec.Id == "trackpad"
                ? "Đánh dấu đã kiểm tra trackpad ở trang Ngoại quan trước khi ghi nhận."
                : spec.Id == "av" ? "Trạng thái dùng chung từ module Camera - Mic - Loa."
                : "Trạng thái thực tế do module kiểm tra ghi nhận.";

            tests.Add(new TestLine(spec.Icon, spec.Name, label, color,
                HexBrush(spec.Row), HexBrush(spec.Fg), HexBrush(spec.IconBg), indicator, detail));
        }
        QuickTestList.ItemsSource = tests;
        ProgressText.Text = $"{tests.Count(t => t.Status != "Chưa thử")}/{tests.Count} mục đã thử";
        MachineText.Text = $"Thiết bị: {Environment.MachineName}";
        CpuText.Text = "CPU: " + ReadCpuName();
        RamText.Text = "RAM: " + ReadPhysicalMemory();
        GpuText.Text = "GPU: Xem mục Đối chiếu cấu hình";
        StorageText.Text = _host.DriveSummaries.Count > 0
            ? $"Ổ đĩa: {_host.DriveSummaries.Count} ổ được ghi nhận • Xem thông tin chi tiết"
            : "SSD / HDD: Chưa có dữ liệu kiểm tra";

        ModuleVerdictLevel autoLevel;
        var grades = new List<ModuleVerdictLevel>();
        string reason = "";
        foreach (var m in passable)
        {
            ModuleVerdictLevel grade;
            string note;
            if (_host.Verdicts.TryGetValue(m.Id, out var v)) { grade = v.Level; note = v.Note; }
            else if (_host.IsDone(m.Id)) { grade = ModuleVerdictLevel.Dat; note = "Đã cho qua"; }
            else { grade = ModuleVerdictLevel.ChuaTest; note = "Chưa cho qua"; }
            grades.Add(grade);
            if (grade == ModuleVerdictLevel.Chan && reason.Length == 0) reason = note;
            else if (grade == ModuleVerdictLevel.CanhBao && reason.Length == 0) reason = note;
        }
        if (grades.Any(g => g == ModuleVerdictLevel.Chan)) autoLevel = ModuleVerdictLevel.Chan;
        else if (grades.Any(g => g == ModuleVerdictLevel.CanhBao)) autoLevel = ModuleVerdictLevel.CanhBao;
        else if (total > 0 && grades.All(g => g == ModuleVerdictLevel.Dat)) autoLevel = ModuleVerdictLevel.Dat;
        else autoLevel = ModuleVerdictLevel.ChuaTest;

        (OverallText.Text, OverallText.Foreground) = autoLevel switch
        {
            ModuleVerdictLevel.Chan => ("KHÔNG KHUYẾN NGHỊ MUA", Application.Current?.TryFindResource("ErrorBrush") as Brush ?? Brushes.Red),
            ModuleVerdictLevel.CanhBao => ("CẦN KIỂM TRA THÊM / THƯƠNG LƯỢNG", Application.Current?.TryFindResource("AccentBrush") as Brush ?? Brushes.Orange),
            ModuleVerdictLevel.Dat => ("CÓ THỂ MUA — MUA ĐƯỢC", Brushes.ForestGreen),
            _ => ("CHƯA ĐỦ DỮ LIỆU — CHƯA KẾT LUẬN ĐƯỢC", Application.Current?.TryFindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray),
        };
        ReasonText.Text = autoLevel switch
        {
            ModuleVerdictLevel.Chan => $"Lý do chặn đầu tiên: {reason}" + (done < total ? $" • Còn {total - done} mục chưa hoàn thành." : ""),
            ModuleVerdictLevel.CanhBao => $"Cảnh báo đầu tiên: {reason}" + (done < total ? $" • Còn {total - done} mục chưa hoàn thành." : ""),
            ModuleVerdictLevel.Dat => "Tất cả bài test đã qua, chưa phát hiện vấn đề đáng kể. Câu nhận xét thân thiện đầy đủ ở trang Tổng kết.",
            _ => "Còn bài test chưa xong, hoặc phiên bị dừng giữa chừng — chưa đủ dữ liệu để kết luận mua hay không.",
        };
        RiskText.Text = autoLevel switch
        {
            ModuleVerdictLevel.Chan => $"⚠ Lỗi nghiêm trọng: {reason}. Mở Báo cáo để xem đầy đủ.",
            ModuleVerdictLevel.CanhBao => $"⚠ Cần lưu ý: {reason}. Nên kiểm tra và thương lượng.",
            ModuleVerdictLevel.Dat => "Các bài kiểm tra được ghi nhận hoàn tất. Không thay thế kiểm tra kỹ thuật chuyên sâu.",
            _ => "Chưa đủ kết quả. Các mục chưa kiểm tra không được xem là đạt."
        };
    }
}
