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
        // Nhiệt độ live ở thẻ máy: mở đọc khi vào Dashboard, dừng khi rời
        // trang (mỗi trang tự mở Computer LHM của mình, giống trang Tản nhiệt).
        TempMonitor.Sampled += OnTempSampled;
        Loaded += (_, _) => TempMonitor.Start();
        Unloaded += (_, _) => TempMonitor.Stop();
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

        RefreshHealthCard();
        RenderTemp();
    }

    double? _tempCpu, _tempGpu; // nhiệt độ mới nhất (null = đường đó không đọc được)
    bool _tempSampled;          // đã có lần đọc đầu tiên hay chưa

    void OnTempSampled(double? cpu, double? gpu)
    {
        // Sự kiện từ thread nền — đưa về UI thread (giống StressTestPage).
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => OnTempSampled(cpu, gpu));
            return;
        }
        _tempCpu = cpu;
        _tempGpu = gpu;
        _tempSampled = true;
        RenderTemp();
    }

    /// <summary>Dòng nhiệt độ ở thẻ máy: "Nhiệt độ: CPU 45°C • GPU 40°C"
    /// (đường nào không có thì bỏ, cả 2 đều không có thì "chưa đọc được").
    /// Chỉ hiển thị, không đưa vào kết luận hay tín hiệu nào.</summary>
    void RenderTemp()
    {
        if (!_tempSampled)
        {
            TempText.Text = Lang.Get("Dash.TempReading");
            return;
        }
        var parts = new List<string>();
        if (_tempCpu != null) parts.Add($"CPU {_tempCpu.Value:0.#}°C");
        if (_tempGpu != null) parts.Add($"GPU {_tempGpu.Value:0.#}°C");
        TempText.Text = parts.Count == 0
            ? Lang.Get("Dash.TempNone")
            : string.Format(Lang.Get("Dash.TempLine"), string.Join(" • ", parts));
    }

    /// <summary>Thẻ sức khỏe chung: 2 vòng donut lồng nhau (vòng ngoài =
    /// sức khỏe ổ, vòng trong = dung lượng pin còn lại) + chú giải + chi
    /// tiết từng ổ. Vòng ngoài lấy ổ yếu nhất (quyết định kết luận ổ);
    /// không có pin thì chỉ vẽ vòng ngoài, không có ổ thì chỉ vẽ vòng
    /// trong. Dữ liệu ổ từ DriveSummaries, % chai pin từ BatteryWearPercent
    /// (cùng nguồn Đối chiếu cấu hình thu thập nền khi mở app).</summary>
    void RefreshHealthCard()
    {
        var drives = _host.DriveSummaries;
        double? wear = _host.BatteryWearPercent;

        // Ổ yếu nhất (theo % sức khỏe) = vòng ngoài; ổ nào cũng không đọc
        // được % thì vòng ngoài xám + "?".
        var worst = drives
            .Where(d => d.HealthPercent != null)
            .OrderBy(d => d.HealthPercent)
            .FirstOrDefault();
        double? driveHealth = worst?.HealthPercent;
        Color driveColor = DriveBandColor(driveHealth);
        double? batteryRemaining = wear == null ? null : 100 - wear.Value;
        Color batteryColor = BatteryBandColor(wear);

        bool hasGauge = drives.Count > 0 || wear != null;
        HealthGaugeRow.Visibility = hasGauge ? Visibility.Visible : Visibility.Collapsed;
        DriveEmptyText.Visibility = drives.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        NestedGaugeHost.Children.Clear();
        if (hasGauge)
            NestedGaugeHost.Children.Add(BuildNestedGauge(
                drives.Count > 0, driveHealth, driveColor,
                wear != null, batteryRemaining, batteryColor));

        // Chú giải ổ: câu kết luận nguyên văn của ổ yếu nhất (đã có sẵn %
        // trong câu); ổ không đọc được % thì lấy ổ đầu tiên.
        DriveLegendRow.Visibility = drives.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (drives.Count > 0)
        {
            DriveDot.Fill = new SolidColorBrush(driveColor);
            DriveLegendText.Text = (worst ?? drives[0]).VerdictText;
            DriveLegendText.Foreground = new SolidColorBrush(driveColor);
        }

        // Chú giải pin: chưa thu thập xong → dòng chờ; có số → câu note
        // (còn % dung lượng • chai % — mức); đã thu thập mà không có pin
        // (desktop) → ẩn hẳn hàng.
        if (wear == null && _host.Signals.ContainsKey("config"))
        {
            BatteryLegendRow.Visibility = Visibility.Collapsed;
        }
        else
        {
            BatteryLegendRow.Visibility = Visibility.Visible;
            if (wear == null)
            {
                BatteryDot.Fill = HexBrush("#888080");
                BatteryLegendText.Text = Lang.Get("Dash.BatteryEmpty");
                BatteryLegendText.Foreground = HexBrush("#888080");
            }
            else
            {
                string band = wear < 20 ? Lang.Get("Dash.BatteryBandHealthy")
                    : wear < 40 ? Lang.Get("Dash.BatteryBandWorn")
                    : Lang.Get("Dash.BatteryBandBad");
                BatteryDot.Fill = new SolidColorBrush(batteryColor);
                BatteryLegendText.Text = string.Format(Lang.Get("Dash.BatteryGaugeNote"),
                    $"{batteryRemaining:0.#}", $"{wear.Value:0.#}", band);
                BatteryLegendText.Foreground = new SolidColorBrush(batteryColor);
            }
        }

        // Chi tiết từng ổ (chỉ chữ, không còn donut riêng): model • dung
        // lượng (ổ đĩa) + % sức khỏe + lý do chặn/cảnh báo nếu có.
        DriveList.Children.Clear();
        foreach (var d in drives)
        {
            var letters = d.Letters.Count > 0 ? $" ({string.Join(", ", d.Letters)})" : "";
            string health = d.HealthPercent == null ? "Không rõ" : $"{d.HealthPercent}%";
            var row = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            row.Children.Add(new TextBlock
            {
                Text = $"{d.Model} • {d.Capacity}{letters} — {health}",
                FontSize = 13, FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 0, 4),
            });
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

    /// <summary>Màu vòng ngoài theo mốc kết luận ổ: không rõ xám, ≥90% xanh,
    /// 70–89% cam, &lt;70% đỏ.</summary>
    static Color DriveBandColor(double? health) => health == null
        ? (Color)ColorConverter.ConvertFromString("#888080")
        : health >= 90 ? Colors.ForestGreen
        : health >= 70 ? (Color)ColorConverter.ConvertFromString("#DE870F")
        : (Color)ColorConverter.ConvertFromString("#D33D42");

    /// <summary>Màu vòng trong theo mốc chai pin: &lt;20% xanh, 20–&lt;40%
    /// cam, ≥40% đỏ; chưa có số thì xám.</summary>
    static Color BatteryBandColor(double? wear) => wear == null
        ? (Color)ColorConverter.ConvertFromString("#888080")
        : wear < 20 ? Colors.ForestGreen
        : wear < 40 ? (Color)ColorConverter.ConvertFromString("#DE870F")
        : (Color)ColorConverter.ConvertFromString("#D33D42");

    /// <summary>2 vòng donut lồng nhau: vòng ngoài (120) = sức khỏe ổ, vòng
    /// trong (84) = dung lượng pin còn lại; % hiện giữa vòng (của vòng ngoài
    /// nếu có, không thì của vòng trong; không có số nào thì "?"). Vòng nào
    /// không có dữ liệu thì không vẽ.</summary>
    Grid BuildNestedGauge(bool hasDrive, double? driveHealth, Color driveColor,
                          bool hasBattery, double? batteryRemaining, Color batteryColor)
    {
        const double outerSize = 120, innerSize = 84, stroke = 12;
        var host = new Grid { Width = outerSize, Height = outerSize };
        if (hasDrive) host.Children.Add(BuildRing(outerSize, stroke, driveHealth, driveColor));
        if (hasBattery)
        {
            var inner = BuildRing(innerSize, stroke, batteryRemaining, batteryColor);
            inner.HorizontalAlignment = HorizontalAlignment.Center;
            inner.VerticalAlignment = VerticalAlignment.Center;
            host.Children.Add(inner);
        }
        double? center = hasDrive ? driveHealth : batteryRemaining;
        host.Children.Add(new TextBlock
        {
            Text = center == null ? "?" : $"{center.Value:0.#}%",
            FontSize = 16, FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(hasDrive ? driveColor : batteryColor),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });
        return host;
    }

    /// <summary>Một vòng donut (track + cung đầy theo %): 100% → vẽ hẳn
    /// ellipse đầy (arc đầu=cuối vẽ không ra), 0% hoặc null → chỉ track.</summary>
    Grid BuildRing(double size, double stroke, double? pct, Color color)
    {
        double radius = size / 2 - stroke / 2;
        var ring = new Grid { Width = size, Height = size };
        var track = new System.Windows.Shapes.Ellipse
        {
            Width = size, Height = size, StrokeThickness = stroke,
            Fill = Brushes.Transparent,
        };
        track.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "BorderBrush");
        ring.Children.Add(track);
        double p = pct ?? 0;
        if (p >= 100)
        {
            // Vòng tròn đầy: arc có điểm đầu = điểm cuối thì vẽ không ra gì,
            // nên vẽ hẳn 1 ellipse đầy thay cho arc.
            ring.Children.Add(new System.Windows.Shapes.Ellipse
            {
                Width = size, Height = size, StrokeThickness = stroke,
                Stroke = BandBrush(color), Fill = Brushes.Transparent,
            });
        }
        else if (p > 0)
        {
            double angle = p / 100 * 360;
            double rad = angle * Math.PI / 180;
            var end = new Point(
                size / 2 + radius * Math.Sin(rad),
                size / 2 - radius * Math.Cos(rad));
            ring.Children.Add(new System.Windows.Shapes.Path
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
            });
        }
        return ring;
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
