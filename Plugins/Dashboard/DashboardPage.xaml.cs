using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Collections.Generic;
using YanJi.PluginSdk;
using YanJi.PluginSdk.Services;

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
    SmbiosService.SmbiosInfo? _smbios; // Hãng/Model/S/N đọc 1 lần lúc mở app, máy không đổi trong phiên

    public DashboardPage(IHostContext host)
    {
        _host = host;
        InitializeComponent();
        _smbios = SmbiosService.Read();
        _host.DoneChanged += Refresh;
        _host.VerdictChanged += Refresh;
        _host.SignalChanged += Refresh;
        _host.DriveSummariesChanged += Refresh;
        _host.BatteryWearChanged += Refresh;
        Lang.Changed += Refresh;
        Loaded += (_, _) => Refresh();
    }

    void StartButton_Click(object sender, RoutedEventArgs e)
    {
        // Vào wizard qua màn hình Bắt đầu (host quyết định: xong hết thì sang
        // Tổng kết, chưa thì mở intro rồi vào bài chưa xong đầu tiên).
        _host.BeginWizardCheck();
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

    static string? ReadOsName()
    {
        try
        {
            string? product = Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion",
                "ProductName", null)?.ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(product)) return null;
            string? display = Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion",
                "DisplayVersion", null)?.ToString()?.Trim();
            return string.IsNullOrWhiteSpace(display) ? product : $"{product} ({display})";
        }
        catch { return null; }
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
        // Hãng / Model / S/N từ SMBIOS thô (cùng nguồn với Đối chiếu cấu hình);
        // placeholder đã được SmbiosService lọc thành null — không đọc được thì ẩn dòng.
        string? brandModel = (_smbios?.SystemManufacturer, _smbios?.SystemProduct) switch
        {
            (string b, string m) => $"{b} • {m}",
            (string b, null) => b,
            (null, string m) => m,
            _ => null,
        };
        BrandModelText.Text = brandModel == null ? "" : string.Format(Lang.Get("Dash.BrandModel"), brandModel);
        BrandModelText.Visibility = brandModel == null ? Visibility.Collapsed : Visibility.Visible;
        SerialText.Text = _smbios?.SystemSerial == null ? "" : string.Format(Lang.Get("Dash.SerialLine"), _smbios.SystemSerial);
        SerialText.Visibility = _smbios?.SystemSerial == null ? Visibility.Collapsed : Visibility.Visible;
        RamText.Text = "RAM: " + ReadPhysicalMemory();
        GpuText.Text = Lang.Get("Dash.GpuLine");
        StorageText.Text = _host.DriveSummaries.Count > 0
            ? string.Format(Lang.Get("Dash.StorageFound"), _host.DriveSummaries.Count)
            : Lang.Get("Dash.StorageEmpty");

        // Dòng hệ điều hành (registry) + dòng pin (tín hiệu từ Đối chiếu cấu hình):
        // không có pin / không đọc được thì ẩn dòng, không đoán.
        string? os = ReadOsName();
        OsText.Text = os == null ? "" : string.Format(Lang.Get("Dash.OsLine"), os);
        OsText.Visibility = os == null ? Visibility.Collapsed : Visibility.Visible;
        string? battery = null;
        if (_host.Signals.TryGetValue("config", out var configSignals))
        {
            if (configSignals.Contains("batteryHealthy")) battery = Lang.Get("Dash.BatteryHealthy");
            else if (configSignals.Contains("batteryWorn20_30")) battery = Lang.Get("Dash.BatteryWorn");
            else if (configSignals.Contains("batteryWornOver40")) battery = Lang.Get("Dash.BatteryBad");
        }
        BatteryText.Text = battery ?? "";
        BatteryText.Visibility = battery == null ? Visibility.Collapsed : Visibility.Visible;

        RefreshDriveCard();
        RefreshBatteryCard();
    }

    /// <summary>Thẻ sức khỏe ổ: 1 hàng/ổ — tên + dung lượng, gauge tự vẽ theo
    /// % sức khỏe (màu theo mốc kết luận ổ: ≥90% xanh, 70–89% cam, &lt;70% đỏ,
    /// không rõ xám), lý do chặn/cảnh báo nếu có. Dữ liệu từ DriveSummaries do
    /// Đối chiếu cấu hình báo cáo (thu thập nền khi mở app).</summary>
    void RefreshDriveCard()
    {
        var drives = _host.DriveSummaries;
        DriveEmptyText.Visibility = drives.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        DriveList.Children.Clear();
        foreach (var d in drives)
        {
            var letters = d.Letters.Count > 0 ? $" ({string.Join(", ", d.Letters)})" : "";
            var row = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            row.Children.Add(new TextBlock
            {
                Text = $"{d.Model} • {d.Capacity}{letters}",
                FontSize = 13, FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 0, 4),
            });
            row.Children.Add(BuildDriveGauge(d));
            string reasons = string.Join("; ", d.BlockReasons.Concat(d.WarningReasons));
            if (!string.IsNullOrWhiteSpace(reasons))
                row.Children.Add(new TextBlock
                {
                    Text = reasons, FontSize = 11,
                    Foreground = HexBrush("#888080"), TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 4, 0, 0),
                });
            DriveList.Children.Add(row);
        }
    }

    /// <summary>Thẻ độ chai pin: viên pin 3D giống Đối chiếu cấu hình — mức đầy
    /// = dung lượng còn lại (100 - % chai), màu theo mốc chai (<20% xanh,
    /// 20–<40% cam, ≥40% đỏ), nhãn chỉ trạng thái + mức chai (không khuyên chi
    /// phí thay pin). Chưa thu thập thì hiện dòng chờ; đã thu thập mà không có
    /// pin (desktop / không đọc được) thì ẩn cả thẻ, không đoán.</summary>
    void RefreshBatteryCard()
    {
        double? wear = _host.BatteryWearPercent;
        if (wear == null && _host.Signals.ContainsKey("config"))
        {
            BatteryCard.Visibility = Visibility.Collapsed;
            return;
        }
        BatteryCard.Visibility = Visibility.Visible;
        BatteryEmptyText.Visibility = wear == null ? Visibility.Visible : Visibility.Collapsed;
        BatteryGaugeGrid.Visibility = wear == null ? Visibility.Collapsed : Visibility.Visible;
        if (wear == null) return;
        double remaining = 100 - wear.Value;
        BatteryFillColumn.Width = new GridLength(remaining, GridUnitType.Star);
        BatteryEmptyColumn.Width = new GridLength(wear.Value, GridUnitType.Star);
        Color color = wear < 20 ? Colors.ForestGreen
            : wear < 40 ? (Color)ColorConverter.ConvertFromString("#DE870F")
            : (Color)ColorConverter.ConvertFromString("#D33D42");
        BatteryFill.Fill = BandBrush(color);
        string band = wear < 20 ? Lang.Get("Dash.BatteryBandHealthy")
            : wear < 40 ? Lang.Get("Dash.BatteryBandWorn")
            : Lang.Get("Dash.BatteryBandBad");
        BatteryNoteText.Text = string.Format(Lang.Get("Dash.BatteryGaugeNote"),
            $"{remaining:0.#}", $"{wear.Value:0.#}", band);
    }

    Grid BuildDriveGauge(DriveSummary d)
    {
        // Màu theo mốc kết luận ổ (giống ConfigCheck): không rõ xám, ≥90 xanh,
        // 70–89 cam, <70 đỏ. Gauge đầy = % sức khỏe còn lại.
        Color color = d.HealthPercent == null ? (Color)ColorConverter.ConvertFromString("#888080")
            : d.HealthPercent >= 90 ? Colors.ForestGreen
            : d.HealthPercent >= 70 ? (Color)ColorConverter.ConvertFromString("#DE870F")
            : (Color)ColorConverter.ConvertFromString("#D33D42");
        // % sức khỏe hiện giữa vòng donut nên nhãn chỉ còn câu kết luận.
        return BuildGauge(d.HealthPercent, color, d.VerdictText);
    }

    /// <summary>Gauge tròn (donut) tự vẽ: vòng đầy = fill% sức khỏe, % hiện
    /// giữa vòng, nhãn kết luận bên phải. Không rõ % thì vòng xám + dấu "?".
    /// Chỉ dùng cho thẻ sức khỏe ổ (thẻ pin đã dùng viên pin 3D riêng).</summary>
    Grid BuildGauge(double? fill, Color color, string label)
    {
        const double size = 84;
        const double stroke = 10;
        double radius = size / 2 - stroke / 2; // 37: đường tâm của vòng
        var donut = new Grid { Width = size, Height = size };
        var track = new System.Windows.Shapes.Ellipse
        {
            Width = size, Height = size, StrokeThickness = stroke,
            Fill = Brushes.Transparent,
        };
        track.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "BorderBrush");
        donut.Children.Add(track);
        double pct = fill ?? 0;
        if (pct >= 100)
        {
            // Vòng tròn đầy: arc có điểm đầu = điểm cuối thì vẽ không ra gì,
            // nên vẽ hẳn 1 ellipse đầy thay cho arc.
            donut.Children.Add(new System.Windows.Shapes.Ellipse
            {
                Width = size, Height = size, StrokeThickness = stroke,
                Stroke = BandBrush(color), Fill = Brushes.Transparent,
            });
        }
        else if (pct > 0)
        {
            double angle = pct / 100 * 360;
            double rad = angle * Math.PI / 180;
            var end = new Point(
                size / 2 + radius * Math.Sin(rad),
                size / 2 - radius * Math.Cos(rad));
            var arc = new System.Windows.Shapes.Path
            {
                Stroke = BandBrush(color), StrokeThickness = stroke,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Data = new PathGeometry(new[]
                {
                    new PathFigure(new Point(size / 2, size / 2 - radius),
                        new[]
                        {
                            new ArcSegment(end, new Size(radius, radius), 0,
                                angle > 180, SweepDirection.Clockwise, true),
                        }, false),
                }),
            };
            donut.Children.Add(arc);
        }
        var centerText = new TextBlock
        {
            Text = fill == null ? "?" : $"{fill.Value:0.#}%",
            FontSize = 18, FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(color),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        donut.Children.Add(centerText);
        var labelText = new TextBlock
        {
            Text = label, FontSize = 11, FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(color), Margin = new Thickness(12, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap,
        };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(donut, 0);
        Grid.SetColumn(labelText, 1);
        grid.Children.Add(donut);
        grid.Children.Add(labelText);
        return grid;
    }

    /// <summary>Gradient dọc cho fill gauge (sáng trên -> màu gốc -> đậm dưới),
    /// giống viên pin ở Đối chiếu cấu hình để gauge nhìn nổi 3D.</summary>
    static LinearGradientBrush BandBrush(Color baseColor)
    {
        var brush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        brush.GradientStops.Add(new GradientStop(Lighten(baseColor, 0.5), 0));
        brush.GradientStops.Add(new GradientStop(baseColor, 0.5));
        brush.GradientStops.Add(new GradientStop(Darken(baseColor, 0.3), 1));
        brush.Freeze();
        return brush;
    }

    static Color Lighten(Color c, double amount) => Color.FromRgb(
        (byte)(c.R + (255 - c.R) * amount),
        (byte)(c.G + (255 - c.G) * amount),
        (byte)(c.B + (255 - c.B) * amount));

    static Color Darken(Color c, double amount) => Color.FromRgb(
        (byte)(c.R * (1 - amount)),
        (byte)(c.G * (1 - amount)),
        (byte)(c.B * (1 - amount)));
}
