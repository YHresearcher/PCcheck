using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using YanJi.PluginSdk;
using YanJi.PluginSdk.Services;

namespace YanJi.Plugin.ConfigCheck;

/// <summary>「配置核对」页面：进入页面即后台自动采集硬件信息（LibreHardwareMonitor + WMI + EDID）→
/// 分组卡片展示 → 人工逐项核对后点「确认无误，通过」。通过后状态行变绿并通知 MainWindow 把导航圆点变绿；
/// 「重新检测」会清空通过态重新采集。</summary>
public partial class ConfigCheckPage : UserControl, YanJi.PluginSdk.IModulePage
{
    bool _running;
    bool _autoStarted; // 页面常驻机制下 Loaded 可能重入（切走再切回），自动检测只触发一次
    HardwareReport? _lastReport; // Báo cáo lần thu thập gần nhất, để nút cho qua kiểm tra ổ bị chặn
    string _batteryNote = ""; // Ghi chú pin từ lần thu thập gần nhất (vd "Pin chai 45%")
    double _batteryWear = -1; // % chai lớn nhất (vd 45), -1 = không đọc được / không có pin
    IReadOnlyList<string> _batterySignals = []; // Tín hiệu pin hiện tại (xem ComputeBatterySignals)
    readonly IHostContext? _host; // Để báo cáo kết luận module cho trang Tổng kết (có thể null khi test riêng)

    /// <summary>核对完成状态变化（true = 已通过，false = 重新检测回到待核对）；MainWindow 据此同步导航圆点。</summary>
    public event Action<bool>? PassChanged;

    /// <summary>当前是否已核对通过。</summary>
    public bool Passed { get; private set; }

    /// <summary>Thu thập nền ngay khi mở app (không đợi vào trang Đối chiếu cấu hình):
    /// cùng luồng StartDetect() của Loaded, cờ _autoStarted chống chạy trùng.
    /// Chỉ thu thập + báo cáo, không tự cho qua — kỹ thuật viên vẫn đối chiếu tay.</summary>
    public void StartBackgroundCollect()
    {
        if (_autoStarted) return;
        _autoStarted = true;
        StartDetect();
    }

    public ConfigCheckPage(IHostContext? host = null)
    {
        _host = host;
        InitializeComponent();
        // Vào trang mới thu thập (thủ công bấm "Kiểm tra lại" cũng gọi StartDetect);
        // thu thập nền lúc mở app đi qua StartBackgroundCollect ở plugin entry.
        Loaded += (_, _) => StartBackgroundCollect();
    }

    void RunButton_Click(object sender, RoutedEventArgs e) => StartDetect();

    async void StartDetect()
    {
        if (_running) return;
        _running = true;
        SetPassed(false);
        RunButton.IsEnabled = false;
        PassButton.IsEnabled = false;
        DetectProgress.Value = 0;
        SetTabGroups(null);
        SetIdentity(null);
        _batteryWear = -1;
        _batteryNote = "";
        SetBatteryGauge();
        PhaseText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
        try
        {
            // WMI/LHM/EDID 采集放后台线程，进度经 IProgress 回报（不卡 UI）
            var progress = new Progress<HardwareInfoService.CollectProgress>(p =>
            {
                DetectProgress.Value = 100.0 * p.Step / p.Total;
                PhaseText.Text = $"Đang thu thập: {p.Stage} ({p.Step + 1}/{p.Total})";
            });
            var report = await Task.Run(() => HardwareInfoService.Collect(progress));
            _lastReport = report;
            (_batterySignals, _batteryNote, _batteryWear) = ComputeBatterySignals(report);
            SetBatteryGauge();
            _host?.ReportSignals("config", _batterySignals);
            ReportConfigVerdict();
            _host?.ReportDriveSummaries(report.Drives);
            SetTabGroups(report);
            SetIdentity(report);
            DetectProgress.Value = 100;
            if (report.DriveBlockReasons.Count > 0)
            {
                PhaseText.Text = $"Thu thập xong ({report.CollectedAt:HH:mm:ss}) — CẢNH BÁO: có ổ bị CHẶN, " +
                    $"xem dòng Kết luận ổ, thay ổ rồi bấm Kiểm tra lại. {string.Join("; ", report.DriveBlockReasons)}.";
                PhaseText.SetResourceReference(TextBlock.ForegroundProperty, "ErrorBrush");
            }
            else
            {
                PhaseText.Text = $"Thu thập xong ({report.CollectedAt:HH:mm:ss}), vui lòng đối chiếu từng mục bên dưới";
                if (report.HasDriveWarning)
                    PhaseText.Text += report.DriveWarningReasons.Count > 0
                        ? $" Lưu ý: có ổ cảnh báo — {string.Join("; ", report.DriveWarningReasons)} — tối đa mua có điều kiện."
                        : " Lưu ý: có ổ cảnh báo (<90%) — tối đa mua có điều kiện.";
                if (report.HasDriveUnknown)
                    PhaseText.Text += " Lưu ý: có ổ chưa rõ sức khỏe — kiểm tra tay bằng CrystalDiskInfo trước khi mua.";
            }
            if (report.SmbiosMismatch)
                PhaseText.Text += $" Lưu ý: thông tin máy không khớp SMBIOS↔WMI — {string.Join("; ", report.SmbiosMismatchReasons)} — kiểm tra tem máy tay.";
            PassButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            PhaseText.Text = "Thu thập bị lỗi: " + ex.Message;
        }
        finally
        {
            RunButton.IsEnabled = true;
            _running = false;
        }
    }

    void PassButton_Click(object sender, RoutedEventArgs e)
    {
        // Ổ bị chặn (lỗi nghiêm trọng >0 hoặc sức khỏe <70%) thì không cho qua — thay ổ rồi
        // kiểm tra lại, giống quy ước chặn của module Cổng kết nối & Sạc với sạc Kém.
        // Ổ cảnh báo / chưa rõ thì vẫn cho qua, nhưng PhaseText ghi rõ điều kiện.
        if (_lastReport?.DriveBlockReasons.Count > 0)
        {
            StatusText.Text = "Ổ bị chặn — thay ổ rồi kiểm tra lại, không cho qua";
            StatusText.SetResourceReference(TextBlock.ForegroundProperty, "ErrorBrush");
            PhaseText.Text = "Ổ bị chặn: " + string.Join("; ", _lastReport.DriveBlockReasons) + ".";
            PhaseText.SetResourceReference(TextBlock.ForegroundProperty, "ErrorBrush");
            ReportConfigVerdict();
            return;
        }
        SetPassed(true);
        ReportConfigVerdict();
        PassButton.IsEnabled = false;
        PhaseText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
        PhaseText.Text = "Đã xác nhận cho qua; nếu thay linh kiện hoặc cần kiểm lại, bấm \"Kiểm tra lại\"";
        if (_lastReport?.HasDriveWarning == true)
            PhaseText.Text += _lastReport.DriveWarningReasons.Count > 0
                ? $" Lưu ý: có ổ cảnh báo — {string.Join("; ", _lastReport.DriveWarningReasons)} — tối đa mua có điều kiện."
                : " Lưu ý: có ổ cảnh báo (<90%) — tối đa mua có điều kiện.";
        if (_lastReport?.HasDriveUnknown == true)
            PhaseText.Text += " Lưu ý: có ổ chưa rõ sức khỏe — kiểm tra tay bằng CrystalDiskInfo trước khi mua.";
        if (_lastReport?.SmbiosMismatch == true)
            PhaseText.Text += $" Lưu ý: thông tin máy không khớp SMBIOS↔WMI — {string.Join("; ", _lastReport.SmbiosMismatchReasons)} — kiểm tra tem máy tay.";
    }


    /// <summary>Chia nhóm thu thập thành tab kiểu CPU-Z. Ánh xạ theo tiêu đề nhóm
    /// (CPU / RAM / Ổ / Card màn hình), nhóm nào không khớp thì vào tab Khác —
    /// để nhóm mới sau này không bị lọc mất. Tab trống thì hiện trống, không lỗi.</summary>
    void SetTabGroups(HardwareReport? report)
    {
        var groups = report?.Groups ?? new List<InfoGroup>();
        CpuHost.ItemsSource = groups.Where(g => g.Title == "CPU").ToList();
        MemoryHost.ItemsSource = groups.Where(g => g.Title == "RAM").ToList();
        StorageHost.ItemsSource = groups.Where(g => g.Title == "Ổ").ToList();
        GraphicsHost.ItemsSource = groups.Where(g => g.Title.StartsWith("Card màn hình")).ToList();
        OtherHost.ItemsSource = groups.Where(g =>
            g.Title != "CPU" && g.Title != "RAM" && g.Title != "Ổ" && !g.Title.StartsWith("Card màn hình")).ToList();
    }

    /// <summary>Thẻ chung Hãng / Model / S/N máy trên cùng trang (1 chỗ, khỏi phải
    /// chui vào tab Khác tìm trong nhóm Máy &amp; Mainboard). Giá trị lấy đúng từ
    /// nhóm Máy &amp; Mainboard của báo cáo (SMBIOS thô); SMBIOS không đọc được
    /// (nhóm chỉ có dòng "SMBIOS thô") thì cả 3 đều hiện "Không đọc được", không
    /// bịa số, không lấy WMI chắp vá. Chưa thu thập (report null) thì hiện
    /// "Chưa thu thập".</summary>
    void SetIdentity(HardwareReport? report)
    {
        string brand = "Chưa thu thập", model = "Chưa thu thập", serial = "Chưa thu thập";
        var machine = report?.Groups.FirstOrDefault(g => g.Title == "Máy & Mainboard");
        if (machine != null)
        {
            string? Get(string name) => machine.Entries.FirstOrDefault(e => e.Name == name)?.Value;
            if (Get("SMBIOS thô") != null)
            {
                brand = model = serial = "Không đọc được";
            }
            else
            {
                brand = Get("Hãng máy (SMBIOS)") ?? "Không đọc được";
                model = Get("Model máy (SMBIOS)") ?? "Không đọc được";
                serial = Get("S/N máy (SMBIOS)") ?? "Không đọc được";
            }
        }
        BrandText.Text = brand;
        ModelText.Text = model;
        SerialText.Text = serial;
    }

    /// <summary>Tính tín hiệu pin từ nhóm Pin trong báo cáo: lấy % chai lớn nhất
    /// qua các viên pin (mỗi viên 1 dòng "Độ chai X %"). Mốc theo câu mẫu của user
    /// (20–30% và trên 40% là ví dụ, không phải tiêu chuẩn cứng cho mọi loại pin):
    /// chai &lt;20% = pin khỏe, 20–&lt;40% = chai vừa (dùng câu 20–30%), ≥40% = chai
    /// nặng. Pin không đọc được % hoặc máy không có pin (desktop) thì KHÔNG báo
    /// tín hiệu — không đoán, và không dùng câu "không đọc được pin" cho desktop
    /// (câu đó chỉ đúng khi biết chắc đây là laptop, mà app chưa biết dạng máy).</summary>
    static (IReadOnlyList<string>, string, double) ComputeBatterySignals(HardwareReport report)
    {
        var pinGroup = report.Groups.FirstOrDefault(g => g.Title == "Pin");
        if (pinGroup == null) return ([], "", -1);
        double worstWear = -1;
        foreach (var e in pinGroup.Entries.Where(e => e.Name.StartsWith("Độ chai")))
        {
            // Giá trị dạng "25,5 % (sức khỏe 74,5 %)" hoặc "25.5 % ..." tùy culture
            string num = e.Value.Split(' ')[0].Replace(',', '.');
            if (double.TryParse(num, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double wear))
                worstWear = Math.Max(worstWear, wear);
        }
        if (worstWear < 0) return ([], "", -1);
        if (worstWear < 20) return (["batteryHealthy"], $"Pin chai {worstWear:0.#}% — còn khỏe", worstWear);
        if (worstWear < 40) return (["batteryWorn20_30"], $"Pin chai {worstWear:0.#}% — tính thời lượng thực tế và chi phí thay pin", worstWear);
        return (["batteryWornOver40"], $"Pin chai {worstWear:0.#}% — tính chi phí thay pin trước khi mua", worstWear);
    }

    /// <summary>Hình viên pin thể hiện độ chai: mức đầy = dung lượng còn lại
    /// (100 - % chai), màu theo mốc chai (<20% xanh, 20–<40% cam, ≥40% đỏ).
    /// Chỉ hiện khi đọc được % chai; máy không pin / không đọc được thì ẩn.</summary>
    void SetBatteryGauge()
    {
        if (_batteryWear < 0)
        {
            BatteryCard.Visibility = Visibility.Collapsed;
            return;
        }
        double remaining = 100 - _batteryWear;
        BatteryFillColumn.Width = new GridLength(remaining, GridUnitType.Star);
        BatteryEmptyColumn.Width = new GridLength(_batteryWear, GridUnitType.Star);
        Color baseColor = _batteryWear < 20 ? Colors.ForestGreen
            : _batteryWear < 40 ? (Color)ColorConverter.ConvertFromString("#DE870F")
            : (Color)ColorConverter.ConvertFromString("#D33D42");
        BatteryFill.Fill = BandBrush(baseColor);
        BatteryNoteText.Text = $"Còn {remaining:0.#}% dung lượng • {_batteryNote}";
        BatteryCard.Visibility = Visibility.Visible;
    }

    /// <summary>Gradient dọc cho fill viên pin (sáng trên -> màu gốc -> đậm dưới)
    /// để pin nhìn nổi 3D thay vì mảng màu phẳng (cùng vệt bóng + bóng mờ ở XAML).</summary>
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

    /// <summary>Báo cáo kết luận module cho trang Tổng kết. Quét xong mà chưa bấm cho qua
    /// thì chưa phải Đạt, kể cả khi ổ sạch (vì kỹ thuật viên chưa đối chiếu với đơn hàng);
    /// ổ cảnh báo / chưa rõ thì là Cảnh báo, ổ chặn thì là Chặn, cho qua rồi mà sạch mới là Đạt.</summary>
    void ReportConfigVerdict()
    {
        if (_host == null) return;
        var r = _lastReport;
        ModuleVerdictLevel level;
        string note;
        if (r == null)
        {
            level = ModuleVerdictLevel.ChuaTest; note = "Chưa quét";
        }
        else if (r.DriveBlockReasons.Count > 0)
        {
            level = ModuleVerdictLevel.Chan; note = string.Join("; ", r.DriveBlockReasons);
        }
        else if (r.HasDriveWarning)
        {
            level = ModuleVerdictLevel.CanhBao;
            note = r.DriveWarningReasons.Count > 0
                ? $"Có ổ cảnh báo — {string.Join("; ", r.DriveWarningReasons)} — tối đa mua có điều kiện"
                : "Có ổ cảnh báo (<90%) — tối đa mua có điều kiện";
        }
        else if (r.HasDriveUnknown)
        {
            level = ModuleVerdictLevel.CanhBao; note = "Có ổ chưa rõ sức khỏe — kiểm tra tay bằng CrystalDiskInfo";
        }
        else if (_batterySignals.Contains("batteryWorn20_30") || _batterySignals.Contains("batteryWornOver40"))
        {
            level = ModuleVerdictLevel.CanhBao; note = _batteryNote;
        }
        else if (Passed)
        {
            level = ModuleVerdictLevel.Dat; note = "Đã đối chiếu đúng với đơn hàng";
        }
        else
        {
            level = ModuleVerdictLevel.ChuaTest; note = "Đã quét, chờ đối chiếu với đơn hàng";
        }
        // Không khớp SMBIOS↔WMI: không chặn như ổ, nhưng không cho qua sạch —
        // nếu đã cho qua mà không còn cảnh báo nào khác thì thành Cảnh báo, nếu
        // đã có cảnh báo khác thì ghi thêm vào ghi chú. Chưa cho qua (ChuaTest)
        // hoặc ổ bị chặn (Chan) thì giữ nguyên mức, vì không khớp đã hiện trong
        // nhóm Máy & Mainboard và dòng PhaseText, không cần đổi mức.
        if (r?.SmbiosMismatch == true)
        {
            if (level == ModuleVerdictLevel.Dat)
            {
                level = ModuleVerdictLevel.CanhBao;
                note = $"Thông tin máy không khớp giữa SMBIOS và WMI — kiểm tra tem máy tay ({string.Join("; ", r.SmbiosMismatchReasons)})";
            }
            else if (level == ModuleVerdictLevel.CanhBao)
            {
                note += $" | Không khớp SMBIOS↔WMI: {string.Join("; ", r.SmbiosMismatchReasons)}";
            }
        }
        _host.ReportVerdict("config", new(level, note));
    }

    /// <summary>更新核对状态行（通过 = 绿色「✓ 已核对通过」）并通知导航圆点。</summary>
    void SetPassed(bool passed)
    {
        Passed = passed;
        if (passed)
        {
            StatusText.Text = "✓ Đã xác nhận đúng";
            StatusText.Foreground = Brushes.ForestGreen;
        }
        else
        {
            StatusText.Text = "Chờ đối chiếu";
            StatusText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
        }
        PassChanged?.Invoke(passed);
    }
}
