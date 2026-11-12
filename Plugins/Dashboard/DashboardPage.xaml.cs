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

/// <summary>Trang Tổng quan: tiến độ + nút nhảy.
/// Kết luận mua và thẻ Sức khỏe & Rủi ro đã dời sang trang Tổng kết (Báo cáo)
/// 2026-11-05 — Dashboard chỉ còn thẻ máy, kết quả nhanh và nút hành động.
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
        Lang.Changed += Refresh;
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

    void StopButton_Click(object sender, RoutedEventArgs e)
    {
        var survivors = _host.StopAllHeavyTests();
        if (survivors.Count == 0)
            Dialogs.Info(Window.GetWindow(this), Lang.Get("Dash.NoHeavy"));
        else
            Dialogs.Warn(Window.GetWindow(this), string.Format(Lang.Get("Dash.HeavyLeft"), string.Join(", ", survivors)));
    }

    public sealed record TestLine(
        string Icon, string Name, string Status, Brush StatusBrush,
        Brush RowBrush, Brush IconBrush, Brush IconBackground, string Indicator, string Detail,
        string? IconPath, string? StatusIcon);

    static Brush HexBrush(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }

    // Các hàng đúng thứ tự và màu của bản thiết kế đã duyệt.
    // AV bao gồm 3 phép kiểm tra (loa / webcam / microphone) trong 1 plugin.
    // Trackpad chỉ được tính sau khi kỹ thuật viên đánh dấu xác minh ở Ngoại quan.
    // Name là key chuỗi ngôn ngữ (Lang.Get lúc vẽ), không phải chữ hiện thẳng.
    // IconPath: ảnh icon thật trong bộ icon An Tâm Mac Check (Assets, file gốc
    // user gửi, pack URI host) — 24 icon đợt 1 + 5 icon kiểm tra đợt 2; đủ cả
    // 8 hàng. null = chưa có icon cho hàng này → giữ ký tự Icon như cũ.
    const string IconAssets = "pack://application:,,,/Assets/";
    static readonly (string Id, string NameKey, string Icon, string? IconPath, string Row, string IconBg, string Fg)[] Rows =
    [
        ("screen",   "Dash.Row.Screen",   "▣", IconAssets + "01_laptop_orange.png",     "#E5F4FF", "#D2EAFF", "#1688ED"),
        ("av",       "Dash.Row.Speakers", "♫", IconAssets + "loa_speaker.png",          "#EEECFF", "#E1E0FF", "#536BFF"),
        ("av",       "Dash.Row.Webcam",   "▣", IconAssets + "webcam_camera.png",        "#F1E5FF", "#E5D2FF", "#8645E8"),
        ("keyboard", "Dash.Row.Keyboard", "⌨", IconAssets + "ban_phim_keyboard.png",    "#FFE7F2", "#FFD5EC", "#E83B9B"),
        ("trackpad", "Dash.Row.Trackpad", "▭", IconAssets + "trackpad.png",             "#DFF8F0", "#C8F1E4", "#08BA87"),
        ("av",       "Dash.Row.Mic",      "♩", IconAssets + "microphone.png",           "#E6F8E7", "#D2F3D5", "#12AF37"),
        ("ports",    "Dash.Row.Ports",    "ϟ", IconAssets + "08_lightning_orange.png",  "#FFE9E3", "#FFDAD0", "#F26427"),
        ("visual",   "Dash.Row.Visual",   "▦", IconAssets + "15_search_gray.png",       "#FFF2D4", "#FFE8B0", "#E8A600")
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
                "ProcessorNameString", null)?.ToString()?.Trim() ?? Lang.Get("Dash.CpuUnread");
        }
        catch { return Lang.Get("Dash.CpuUnread"); }
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
                : Lang.Get("Dash.CpuUnread");
        }
        catch { return Lang.Get("Dash.CpuUnread"); }
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
            Dialogs.Warn(Window.GetWindow(this), string.Format(Lang.Get("Dash.FolderFailed"), dir, ex.Message));
        }
    }

    void Refresh()
    {
        var passable = _host.Modules.Where(m => !m.Terminal).OrderBy(m => m.Order).ToList();
        int done = passable.Count(m => _host.IsDone(m.Id));
        int total = passable.Count;
        ProgressText.Text = total > 0
            ? string.Format(Lang.Get("Dash.ProgressDone"), done, total)
            : Lang.Get("Dash.ProgressNone");

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
                ModuleVerdictLevel.Dat => Lang.Get("Dash.Status.Pass"),
                ModuleVerdictLevel.CanhBao => Lang.Get("Dash.Status.Warn"),
                ModuleVerdictLevel.Chan => Lang.Get("Dash.Status.Fail"),
                _ => Lang.Get("Dash.Status.Untried")
            };
            Brush color = grade switch
            {
                ModuleVerdictLevel.Dat => HexBrush("#18AD4D"),
                ModuleVerdictLevel.CanhBao => HexBrush("#DE870F"),
                ModuleVerdictLevel.Chan => HexBrush("#D33D42"),
                _ => HexBrush("#888080")
            };
            // Cột trạng thái: ảnh icon thật trong bộ 24 icon Mac. Bộ không có
            // icon cảnh báo nên CanhBao giữ dấu "!" chữ như cũ (không lấy nhầm
            // icon lỗi đỏ — cảnh báo khác lỗi).
            string? statusIcon = grade switch
            {
                ModuleVerdictLevel.Dat => IconAssets + "21_status_pass_green.png",
                ModuleVerdictLevel.Chan => IconAssets + "23_status_error_red.png",
                ModuleVerdictLevel.ChuaTest => IconAssets + "22_status_not_tested_gray.png",
                _ => null
            };
            string indicator = grade switch
            {
                ModuleVerdictLevel.Dat => "●",
                ModuleVerdictLevel.CanhBao => "!",
                ModuleVerdictLevel.Chan => "✕",
                _ => "○"
            };
            string detail = spec.Id == "trackpad"
                ? Lang.Get("Dash.Detail.Trackpad")
                : spec.Id == "av" ? Lang.Get("Dash.Detail.Av")
                : Lang.Get("Dash.Detail.Default");

            tests.Add(new TestLine(spec.Icon, Lang.Get(spec.NameKey), label, color,
                HexBrush(spec.Row), HexBrush(spec.Fg), HexBrush(spec.IconBg), indicator, detail,
                spec.IconPath, statusIcon));
        }
        QuickTestList.ItemsSource = tests;
        ProgressText.Text = string.Format(Lang.Get("Dash.Progress"),
            tests.Count(t => t.Status != Lang.Get("Dash.Status.Untried")), tests.Count);
        MachineText.Text = string.Format(Lang.Get("Dash.Device"), Environment.MachineName);
        CpuText.Text = "CPU: " + ReadCpuName();
        RamText.Text = "RAM: " + ReadPhysicalMemory();
        GpuText.Text = Lang.Get("Dash.GpuLine");
        StorageText.Text = _host.DriveSummaries.Count > 0
            ? string.Format(Lang.Get("Dash.StorageFound"), _host.DriveSummaries.Count)
            : Lang.Get("Dash.StorageEmpty");

    }
}
