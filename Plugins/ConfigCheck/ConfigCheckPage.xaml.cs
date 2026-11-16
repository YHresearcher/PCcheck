using System.Text.RegularExpressions;
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

    // Trạng thái dòng PhaseText để vẽ lại đúng câu khi đổi ngôn ngữ (xem
    // RenderPhaseText): Idle = chưa thu thập lần nào, Collecting = đang thu
    // thập (giữ _lastStage/_lastStep/_lastTotal để vẽ lại dòng tiến độ),
    // Done = thu thập xong, Passed = đã bấm cho qua, BlockedPass = bấm cho
    // qua mà ổ bị chặn, Error = thu thập lỗi (giữ _lastError để vẽ lại).
    enum PhaseState { Idle, Collecting, Done, Passed, BlockedPass, Error }
    PhaseState _phase = PhaseState.Idle;
    string? _lastStage;
    int _lastStep;
    int _lastTotal;
    string _lastError = "";
    bool _statusBlocked; // Bấm cho qua mà ổ bị chặn: dòng trạng thái giữ câu chặn tới lần thu thập sau

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
        Lang.Changed += RefreshLanguage;
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
        _statusBlocked = false;
        _phase = PhaseState.Collecting;
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
                _lastStage = p.Stage;
                _lastStep = p.Step;
                _lastTotal = p.Total;
                PhaseText.Text = string.Format(Lang.Get("Config.Phase.Collecting"),
                    TranslateStage(p.Stage), p.Step + 1, p.Total);
            });
            var report = await Task.Run(() => HardwareInfoService.Collect(progress));
            _lastReport = report;
            (_batterySignals, _batteryNote, _batteryWear) = ComputeBatterySignals(report);
            SetBatteryGauge();
            // Báo cáo cả tín hiệu pin + tín hiệu ổ trong 1 lần (ReportSignals ghi
            // đè toàn bộ tín hiệu của module): trang Tổng kết chọn kịch bản ổ từ
            // tín hiệu này, không soi chuỗi ghi chú nữa.
            _host?.ReportSignals("config", [.. _batterySignals, .. ComputeDriveSignals(report)]);
            _host?.ReportBatteryWear(_batteryWear < 0 ? null : _batteryWear);
            ReportConfigVerdict();
            _host?.ReportDriveSummaries(report.Drives);
            SetTabGroups(report);
            SetIdentity(report);
            DetectProgress.Value = 100;
            _phase = PhaseState.Done;
            RenderPhaseText();
            PassButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            _phase = PhaseState.Error;
            RenderPhaseText();
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
            _statusBlocked = true;
            _phase = PhaseState.BlockedPass;
            RenderStatusText();
            RenderPhaseText();
            ReportConfigVerdict();
            return;
        }
        SetPassed(true);
        _statusBlocked = false;
        _phase = PhaseState.Passed;
        RenderPhaseText();
        ReportConfigVerdict();
        PassButton.IsEnabled = false;
    }

    /// <summary>Vẽ lại toàn bộ chữ hiện trên trang theo ngôn ngữ hiện tại, từ
    /// dữ liệu đã thu thập (_lastReport, canonical tiếng Việt — không đổi).
    /// Báo lại verdict cho host để dòng ghi chú ở trang Tổng kết cũng đổi
    /// ngôn ngữ theo (Tổng kết hiện nguyên văn note đã báo cáo).</summary>
    void RefreshLanguage()
    {
        if (_lastReport != null)
            (_, _batteryNote, _) = ComputeBatterySignals(_lastReport);
        SetTabGroups(_lastReport);
        SetIdentity(_lastReport);
        SetBatteryGauge();
        RenderStatusText();
        RenderPhaseText();
        ReportConfigVerdict();
    }

    /// <summary>Dòng trạng thái đối chiếu: chặn (bấm cho qua mà ổ bị chặn) /
    /// đã xác nhận / chờ đối chiếu. Chỉ vẽ chữ, không phát PassChanged —
    /// đổi ngôn ngữ không được làm host tưởng trạng thái cho qua vừa đổi.</summary>
    void RenderStatusText()
    {
        if (_statusBlocked)
        {
            StatusText.Text = Lang.Get("Config.Status.Blocked");
            StatusText.SetResourceReference(TextBlock.ForegroundProperty, "ErrorBrush");
        }
        else if (Passed)
        {
            StatusText.Text = Lang.Get("Config.Status.Passed");
            StatusText.Foreground = Brushes.ForestGreen;
        }
        else
        {
            StatusText.Text = Lang.Get("Config.Status.Waiting");
            StatusText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
        }
    }

    /// <summary>Dòng tiến độ / kết quả thu thập theo _phase. Thu thập xong thì
    /// vẽ lại đúng câu của lần thu thập gần nhất (kèm lý do ổ / SMBIOS đã
    /// dịch); đang thu thập thì vẽ lại từ stage đã lưu; lỗi thì vẽ lại từ
    /// _lastError. Chưa thu thập thì giữ câu mặc định trong XAML.</summary>
    void RenderPhaseText()
    {
        switch (_phase)
        {
            case PhaseState.Idle:
                PhaseText.Text = Lang.Get("Config.Phase.NotChecked");
                PhaseText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
                break;
            case PhaseState.Collecting:
                if (_lastStage != null)
                    PhaseText.Text = string.Format(Lang.Get("Config.Phase.Collecting"),
                        TranslateStage(_lastStage), _lastStep + 1, _lastTotal);
                break;
            case PhaseState.Done:
                if (_lastReport != null)
                {
                    PhaseText.Text = PhaseDoneText(_lastReport);
                    if (_lastReport.DriveBlockReasons.Count > 0)
                        PhaseText.SetResourceReference(TextBlock.ForegroundProperty, "ErrorBrush");
                    else
                        PhaseText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
                }
                break;
            case PhaseState.Passed:
                if (_lastReport != null)
                {
                    PhaseText.Text = PhasePassedText(_lastReport);
                    PhaseText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
                }
                break;
            case PhaseState.BlockedPass:
                if (_lastReport != null)
                {
                    PhaseText.Text = string.Format(Lang.Get("Config.Phase.Blocked"),
                        TranslateReasonsJoined(_lastReport.DriveBlockReasons));
                    PhaseText.SetResourceReference(TextBlock.ForegroundProperty, "ErrorBrush");
                }
                break;
            case PhaseState.Error:
                PhaseText.Text = string.Format(Lang.Get("Config.Phase.Error"), _lastError);
                PhaseText.SetResourceReference(TextBlock.ForegroundProperty, "ErrorBrush");
                break;
        }
    }

    /// <summary>Câu "thu thập xong" của 1 báo cáo: chặn thì cảnh báo + lý do
    /// ổ; không chặn thì mời đối chiếu + các lưu ý ổ (cảnh báo / chưa rõ) +
    /// lưu ý không khớp SMBIOS. Dùng chung cho lúc thu thập xong và khi vẽ
    /// lại sau đổi ngôn ngữ.</summary>
    static string PhaseDoneText(HardwareReport report)
    {
        string text;
        if (report.DriveBlockReasons.Count > 0)
        {
            text = string.Format(Lang.Get("Config.Phase.DoneBlocked"),
                $"{report.CollectedAt:HH:mm:ss}", TranslateReasonsJoined(report.DriveBlockReasons));
        }
        else
        {
            text = string.Format(Lang.Get("Config.Phase.Done"), $"{report.CollectedAt:HH:mm:ss}");
            if (report.HasDriveWarning)
                text += report.DriveWarningReasons.Count > 0
                    ? string.Format(Lang.Get("Config.Phase.DriveWarningReasons"),
                        TranslateReasonsJoined(report.DriveWarningReasons))
                    : Lang.Get("Config.Phase.DriveWarningGeneric");
            if (report.HasDriveUnknown)
                text += Lang.Get("Config.Phase.DriveUnknown");
        }
        if (report.SmbiosMismatch)
            text += string.Format(Lang.Get("Config.Phase.SmbiosMismatch"),
                TranslateReasonsJoined(report.SmbiosMismatchReasons));
        return text;
    }

    /// <summary>Câu sau khi bấm cho qua: xác nhận + các lưu ý ổ / SMBIOS
    /// (giống câu "thu thập xong" nhưng không lặp lại giờ thu thập).</summary>
    static string PhasePassedText(HardwareReport report)
    {
        string text = Lang.Get("Config.Phase.Passed");
        if (report.HasDriveWarning)
            text += report.DriveWarningReasons.Count > 0
                ? string.Format(Lang.Get("Config.Phase.DriveWarningReasons"),
                    TranslateReasonsJoined(report.DriveWarningReasons))
                : Lang.Get("Config.Phase.DriveWarningGeneric");
        if (report.HasDriveUnknown)
            text += Lang.Get("Config.Phase.DriveUnknown");
        if (report.SmbiosMismatch)
            text += string.Format(Lang.Get("Config.Phase.SmbiosMismatch"),
                TranslateReasonsJoined(report.SmbiosMismatchReasons));
        return text;
    }

    /// <summary>Chia nhóm thu thập thành tab kiểu CPU-Z. Ánh xạ theo tiêu đề nhóm
    /// (CPU / RAM / Ổ / Card màn hình), nhóm nào không khớp thì vào tab Khác —
    /// để nhóm mới sau này không bị lọc mất. Tab trống thì hiện trống, không lỗi.
    /// Lọc theo tiêu đề canonical (tiếng Việt) rồi mới dịch sang bản hiển thị
    /// (TranslateGroup) — tiêu đề dịch rồi không dùng để lọc được nữa.</summary>
    void SetTabGroups(HardwareReport? report)
    {
        var groups = report?.Groups ?? new List<InfoGroup>();
        CpuHost.ItemsSource = groups.Where(g => g.Title == "CPU").Select(TranslateGroup).ToList();
        MemoryHost.ItemsSource = groups.Where(g => g.Title == "RAM").Select(TranslateGroup).ToList();
        StorageHost.ItemsSource = groups.Where(g => g.Title == "Ổ").Select(TranslateGroup).ToList();
        GraphicsHost.ItemsSource = groups.Where(g => g.Title.StartsWith("Card màn hình")).Select(TranslateGroup).ToList();
        OtherHost.ItemsSource = groups.Where(g =>
            g.Title != "CPU" && g.Title != "RAM" && g.Title != "Ổ" && !g.Title.StartsWith("Card màn hình")).Select(TranslateGroup).ToList();
    }

    /// <summary>Thẻ chung Hãng / Model / S/N máy trên cùng trang (1 chỗ, khỏi phải
    /// chui vào tab Khác tìm trong nhóm Máy &amp; Mainboard). Giá trị lấy đúng từ
    /// nhóm Máy &amp; Mainboard của báo cáo (SMBIOS thô); SMBIOS không đọc được
    /// (nhóm chỉ có dòng "SMBIOS thô") thì cả 3 đều hiện "Không đọc được", không
    /// bịa số, không lấy WMI chắp vá. Chưa thu thập (report null) thì hiện
    /// "Chưa thu thập". Tra cứu theo tên canonical tiếng Việt (không đổi);
    /// chỉ chữ hiển thị ("Chưa thu thập" / "Không đọc được") mới dịch.</summary>
    void SetIdentity(HardwareReport? report)
    {
        string notCollected = Lang.Get("Config.NotCollected");
        string unreadable = Lang.Get("Config.Unreadable");
        string brand = notCollected, model = notCollected, serial = notCollected;
        var machine = report?.Groups.FirstOrDefault(g => g.Title == "Máy & Mainboard");
        if (machine != null)
        {
            string? Get(string name) => machine.Entries.FirstOrDefault(e => e.Name == name)?.Value;
            if (Get("SMBIOS thô") != null)
            {
                brand = model = serial = unreadable;
            }
            else
            {
                brand = Get("Hãng máy (SMBIOS)") ?? unreadable;
                model = Get("Model máy (SMBIOS)") ?? unreadable;
                serial = Get("S/N máy (SMBIOS)") ?? unreadable;
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
    /// (câu đó chỉ đúng khi biết chắc đây là laptop, mà app chưa biết dạng máy).
    /// Chạy trên UI thread (sau await Task.Run) nên Lang.Get được; tra cứu
    /// theo tên canonical "Độ chai" (không đổi), chỉ ghi chú hiển thị mới dịch.</summary>
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
        if (worstWear < 20) return (["batteryHealthy"],
            string.Format(Lang.Get("Config.Battery.Healthy"), $"{worstWear:0.#}"), worstWear);
        if (worstWear < 40) return (["batteryWorn20_30"],
            string.Format(Lang.Get("Config.Battery.Worn"), $"{worstWear:0.#}"), worstWear);
        return (["batteryWornOver40"],
            string.Format(Lang.Get("Config.Battery.Bad"), $"{worstWear:0.#}"), worstWear);
    }

    /// <summary>Tín hiệu ổ theo cấu trúc, để trang Tổng kết chọn kịch bản mà không
    /// cần soi chuỗi tiếng Việt trong ghi chú verdict (ghi chú sẽ dịch theo ngôn
    /// ngữ, soi chuỗi sẽ trượt ở chế độ EN). Ánh xạ đúng các điều kiện mà
    /// SummaryPage trước đây khớp bằng chuỗi trong ghi chú:
    /// driveMediaError = có ổ lỗi toàn vẹn dữ liệu; driveBadSector = có ổ sector
    /// đã thay thế / chờ thay thế / không sửa được; driveWear = có ổ sức khỏe
    /// 70–89%; driveSmartUnknown = có ổ cảnh báo "không đọc được lỗi nghiêm trọng",
    /// hoặc ghi chú verdict đang là dòng "Có ổ chưa rõ sức khỏe" (chưa rõ mà
    /// không có cảnh báo nào khác). "Chưa rõ" cũng tính cho combinedB giống hệt
    /// code cũ: ghi chú đó chứa chuỗi "sức khỏe" nên trước đây cũng về combinedB
    /// khi đủ CPU tốt + pin chai. Các cờ ổ do HardwareInfoService đặt trong
    /// DriveVerdict cùng lúc với lý do chặn/cảnh báo.</summary>
    static IReadOnlyList<string> ComputeDriveSignals(HardwareReport report)
    {
        var signals = new List<string>();
        if (report.HasDriveMediaError) signals.Add("driveMediaError");
        if (report.HasDriveBadSector) signals.Add("driveBadSector");
        if (report.HasDriveWear) signals.Add("driveWear");
        if (report.HasDriveSmartUnknownWarning || (report.HasDriveUnknown && !report.HasDriveWarning))
            signals.Add("driveSmartUnknown");
        return signals;
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
        BatteryNoteText.Text = string.Format(Lang.Get("Config.Battery.Remaining"), $"{remaining:0.#}", _batteryNote);
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
    /// ổ cảnh báo / chưa rõ thì là Cảnh báo, ổ chặn thì là Chặn, cho qua rồi mà sạch mới là Đạt.
    /// Ghi chú dịch theo ngôn ngữ hiện tại (lý do ổ / SMBIOS qua TranslateReason);
    /// đổi ngôn ngữ thì RefreshLanguage báo lại để Tổng kết hiện đúng ngôn ngữ.</summary>
    void ReportConfigVerdict()
    {
        if (_host == null) return;
        var r = _lastReport;
        ModuleVerdictLevel level;
        string note;
        if (r == null)
        {
            level = ModuleVerdictLevel.ChuaTest; note = Lang.Get("Config.Verdict.NotScanned");
        }
        else if (r.DriveBlockReasons.Count > 0)
        {
            level = ModuleVerdictLevel.Chan; note = TranslateReasonsJoined(r.DriveBlockReasons);
        }
        else if (r.HasDriveWarning)
        {
            level = ModuleVerdictLevel.CanhBao;
            note = r.DriveWarningReasons.Count > 0
                ? string.Format(Lang.Get("Config.Verdict.DriveWarningReasons"),
                    TranslateReasonsJoined(r.DriveWarningReasons))
                : Lang.Get("Config.Verdict.DriveWarningGeneric");
        }
        else if (r.HasDriveUnknown)
        {
            level = ModuleVerdictLevel.CanhBao; note = Lang.Get("Config.Verdict.DriveUnknown");
        }
        else if (_batterySignals.Contains("batteryWorn20_30") || _batterySignals.Contains("batteryWornOver40"))
        {
            level = ModuleVerdictLevel.CanhBao; note = _batteryNote;
        }
        else if (Passed)
        {
            level = ModuleVerdictLevel.Dat; note = Lang.Get("Config.Verdict.Passed");
        }
        else
        {
            level = ModuleVerdictLevel.ChuaTest; note = Lang.Get("Config.Verdict.Waiting");
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
                note = string.Format(Lang.Get("Config.Verdict.SmbiosMismatch"),
                    TranslateReasonsJoined(r.SmbiosMismatchReasons));
            }
            else if (level == ModuleVerdictLevel.CanhBao)
            {
                note += string.Format(Lang.Get("Config.Verdict.SmbiosMismatchSuffix"),
                    TranslateReasonsJoined(r.SmbiosMismatchReasons));
            }
        }
        _host.ReportVerdict("config", new(level, note));
    }

    /// <summary>更新核对状态行（通过 = 绿色「✓ 已核对通过」）并通知导航圆点。</summary>
    void SetPassed(bool passed)
    {
        Passed = passed;
        RenderStatusText();
        PassChanged?.Invoke(passed);
    }

    // ═══ Dịch hiển thị: canonical tiếng Việt (service) -> chuỗi ngôn ngữ hiện tại ═══
    // HardwareInfoService chạy trên background thread và KHÔNG được gọi Lang.Get
    // (WPF resources chỉ dùng được trên UI thread), nên mọi chuỗi canonical
    // trong HardwareReport giữ nguyên tiếng Việt. Chỉ lớp hiển thị này (chạy
    // trên UI thread) mới dịch, bằng bảng ánh xạ dưới đây, vẽ lại từ
    // _lastReport mỗi lần đổi ngôn ngữ. Chuỗi nào không khớp mẫu thì trả về
    // nguyên văn (không bịa, không rớt dữ liệu) — xem TranslateValue /
    // TranslateReasonFragment.

    /// <summary>Bản hiển thị của 1 nhóm thu thập: tiêu đề + từng dòng tên/giá
    /// trị đều qua bảng dịch; nhóm gốc (canonical) không đổi.</summary>
    static InfoGroup TranslateGroup(InfoGroup g)
    {
        var copy = new InfoGroup(TranslateTitle(g.Title));
        foreach (var e in g.Entries) copy.Add(TranslateName(e.Name), TranslateValue(e.Value));
        foreach (var d in g.Details) copy.AddDetail(TranslateName(d.Name), TranslateValue(d.Value));
        return copy;
    }

    /// <summary>Tiêu đề nhóm thu thập / stage tiến độ ("Máy &amp; Mainboard",
    /// "Card màn hình (GPU)", "Ổ", "Card mạng", "Màn hình", "Pin"); "CPU" /
    /// "RAM" giữ nguyên vì 2 thứ tiếng viết giống nhau.</summary>
    static string TranslateTitle(string title) => title switch
    {
        "Máy & Mainboard" => Lang.Get("Config.Group.Machine"),
        "Ổ" => Lang.Get("Config.Group.Storage"),
        "Card mạng" => Lang.Get("Config.Group.Network"),
        "Màn hình" => Lang.Get("Config.Group.Display"),
        "Pin" => Lang.Get("Config.Group.Battery"),
        _ when title.StartsWith("Card màn hình") => Lang.Get("Config.Group.Graphics"),
        _ => title,
    };

    /// <summary>Stage tiến độ thu thập chính là tên nhóm (xem HardwareInfoService
    /// .Collect) nên dùng chung bảng tiêu đề.</summary>
    static string TranslateStage(string stage) => TranslateTitle(stage);

    /// <summary>Tên dòng ("Model", "Sức khỏe #1", "Thanh RAM 2 (DIMM0)"...):
    /// tách hậu tố " #n" và "(vị trí)" rồi dịch tên gốc, gắn hậu tố lại.
    /// Tên không có trong bảng (vd "VRAM", dữ liệu thô) giữ nguyên.</summary>
    static string TranslateName(string name)
    {
        var ram = Regex.Match(name, @"^Thanh RAM (\d+)( \([^)]*\))?$");
        if (ram.Success)
            return string.Format(Lang.Get("Config.Entry.RamStick"), ram.Groups[1].Value) + ram.Groups[2].Value;
        string baseName = name, tag = "";
        var m = Regex.Match(name, @"^(.*) (#\d+)$");
        if (m.Success) { baseName = m.Groups[1].Value; tag = m.Groups[2].Value; }
        string? key = baseName switch
        {
            "Kết quả thu thập" => "Config.Entry.Result",
            "SMBIOS thô" => "Config.Entry.SmbiosRaw",
            "Ghi chú" => "Config.Entry.Note",
            "Hãng máy (SMBIOS)" => "Config.Entry.MachineBrand",
            "Model máy (SMBIOS)" => "Config.Entry.MachineModel",
            "S/N máy (SMBIOS)" => "Config.Entry.MachineSerial",
            "UUID máy (SMBIOS)" => "Config.Entry.MachineUuid",
            "Hãng mainboard (SMBIOS)" => "Config.Entry.BoardBrand",
            "Model mainboard (SMBIOS)" => "Config.Entry.BoardModel",
            "S/N mainboard (SMBIOS)" => "Config.Entry.BoardSerial",
            "BIOS (SMBIOS)" => "Config.Entry.Bios",
            "Khung máy (SMBIOS)" => "Config.Entry.Chassis",
            "CẢNH BÁO" => "Config.Entry.Warning",
            "Model máy (WMI)" => "Config.Entry.MachineModelWmi",
            "Model mainboard (WMI)" => "Config.Entry.BoardModelWmi",
            "Đối chiếu WMI" => "Config.Entry.WmiCheck",
            "CẢNH BÁO KHÔNG KHỚP" => "Config.Entry.MismatchWarning",
            "Đối chiếu SMBIOS ↔ WMI" => "Config.Entry.SmbiosWmiCheck",
            "Ghi chú chống fake" => "Config.Entry.AntiFakeNote",
            "Model" => "Config.Entry.Model",
            "Nhân / Luồng" => "Config.Entry.CoresThreads",
            "Phiên bản driver" => "Config.Entry.DriverVersion",
            "Tổng dung lượng" => "Config.Entry.TotalCapacity",
            "Tần số" => "Config.Entry.Frequency",
            "Khe đang dùng" => "Config.Entry.SlotsUsed",
            "Ổ" => "Config.Entry.Drive",
            "Sức khỏe" => "Config.Entry.Health",
            "Thời gian đã chạy" => "Config.Entry.PowerOnTime",
            "Lỗi toàn vẹn dữ liệu" => "Config.Entry.MediaErrors",
            "Lỗi nghiêm trọng" => "Config.Entry.SeriousErrors",
            "Kết luận ổ" => "Config.Entry.DriveVerdict",
            "Card mạng" => "Config.Entry.NetworkCard",
            "Độ phân giải" => "Config.Entry.Resolution",
            "Tần số quét" => "Config.Entry.RefreshRate",
            "Kích thước" => "Config.Entry.Size",
            "Tấm nền" => "Config.Entry.Panel",
            "Dải màu" => "Config.Entry.ColorGamut",
            "Pin" => "Config.Entry.Battery",
            "Dung lượng thiết kế" => "Config.Entry.DesignedCapacity",
            "Dung lượng sạc đầy" => "Config.Entry.FullChargeCapacity",
            "Độ chai" => "Config.Entry.Wear",
            "Số chu kỳ sạc" => "Config.Entry.CycleCount",
            _ => null,
        };
        return key == null ? name : Lang.Get(key) + tag;
    }

    /// <summary>Giá trị dòng: khớp chính xác trước, rồi khớp theo mẫu có trích
    /// số (kết luận ổ, lý do ổ, giờ chạy, độ chai...), cuối cùng trả nguyên
    /// văn nếu không khớp — dữ liệu số/model tên máy không có mẫu vẫn hiện
    /// đúng, chỉ không dịch được chữ tiếng Việt lạ chưa gặp.</summary>
    static string TranslateValue(string value)
    {
        switch (value)
        {
            case "Không đọc được": return Lang.Get("Config.Unreadable");
            case "Không rõ": return Lang.Get("Config.Value.Unknown");
            case "Không có": return Lang.Get("Config.Value.None");
            case "Không phát hiện": return Lang.Get("Config.Value.NotDetected");
            case "Không rõ dung lượng": return Lang.Get("Config.Value.UnknownCapacity");
            case "Không rõ model": return Lang.Get("Config.Value.UnknownModel");
            case "Không đọc được (hãng để trống)": return Lang.Get("Config.Value.UnreadableBlank");
            case "Không xác thực được model máy/mainboard từ firmware — đối chiếu tem máy tay trước khi mua":
                return Lang.Get("Config.Value.SmbiosUnverified");
            case "Máy ảo — thông tin SMBIOS do hypervisor đặt, không tin model này":
                return Lang.Get("Config.Value.VirtualMachine");
            case "Không đọc được WMI — không đối chiếu được": return Lang.Get("Config.Value.WmiUnreadable");
            case "Khớp hoặc không đối chiếu được (1 bên không đọc được / hãng để trống)":
                return Lang.Get("Config.Value.MatchOrUnknown");
            case "SMBIOS thô không bị sửa registry đổi được; nhưng nếu BIOS bị flash giả thì SMBIOS và WMI cùng nói dối giống nhau — khi đó tra S/N trên web hãng hoặc soi tem dưới đáy máy mới phân biệt được":
                return Lang.Get("Config.Value.AntiFake");
            case "Không phát hiện (máy bàn hoặc máy không có pin)": return Lang.Get("Config.Value.NoBattery");
            case "Không rõ — kiểm tra tay bằng CrystalDiskInfo, chưa đủ dữ liệu để kết luận (không tự cho là tốt)":
                return Lang.Get("Config.VerdictLine.Unknown");
            case "Desktop thấp (Low Profile)": return Lang.Get("Config.Value.ChassisLowProfile");
            case "Hand Held": return Lang.Get("Config.Value.ChassisHandHeld");
            case "Sub Notebook": return Lang.Get("Config.Value.ChassisSubNotebook");
        }
        Match m;
        if ((m = Regex.Match(value, @"^Thu thập thất bại: (.*)$")).Success)
            return string.Format(Lang.Get("Config.Value.CollectFailed"), m.Groups[1].Value);
        if ((m = Regex.Match(value, @"^Không đọc được — (.*)$")).Success)
            return string.Format(Lang.Get("Config.Value.UnreadableReason"), TranslateFailureReason(m.Groups[1].Value));
        if ((m = Regex.Match(value, @"^(\d+) nhân / (\d+) luồng$")).Success)
            return string.Format(Lang.Get("Config.Value.CoresThreads"), m.Groups[1].Value, m.Groups[2].Value);
        if ((m = Regex.Match(value, @"^Khoảng (.+) \(giá trị ước tính từ WMI\)$")).Success)
            return string.Format(Lang.Get("Config.Value.EstimatedWmi"), m.Groups[1].Value);
        if ((m = Regex.Match(value, @"^(.+) \(từ registry\)$")).Success)
            return string.Format(Lang.Get("Config.Value.FromRegistry"), m.Groups[1].Value);
        if ((m = Regex.Match(value, @"^(\d+) thanh$")).Success)
            return string.Format(Lang.Get("Config.Value.Sticks"), m.Groups[1].Value);
        if ((m = Regex.Match(value, @"^(\d+) giờ \(khoảng (\d+) ngày\)$")).Success)
            return string.Format(Lang.Get("Config.Value.HoursDays"), m.Groups[1].Value, m.Groups[2].Value);
        if ((m = Regex.Match(value, @"^(\d+) giờ$")).Success)
            return string.Format(Lang.Get("Config.Value.Hours"), m.Groups[1].Value);
        if ((m = Regex.Match(value, @"^(\d+) lần$")).Success)
            return string.Format(Lang.Get("Config.Value.Times"), m.Groups[1].Value);
        if ((m = Regex.Match(value, @"^([\d.,]+) % \(sức khỏe ([\d.,]+) %\)$")).Success)
            return string.Format(Lang.Get("Config.Value.WearHealth"), m.Groups[1].Value, m.Groups[2].Value);
        if ((m = Regex.Match(value, @"^≈ ([\d.,]+)% sRGB / ([\d.,]+)% DCI-P3 \(tỉ lệ thể tích EDID\)$")).Success)
            return string.Format(Lang.Get("Config.Value.Gamut"), m.Groups[1].Value, m.Groups[2].Value);
        if ((m = Regex.Match(value, @"^Mã (\d+)$")).Success)
            return string.Format(Lang.Get("Config.Value.ChassisCode"), m.Groups[1].Value);
        // Dòng "Kết luận ổ": dịch chung qua PluginSdk (Đo tốc độ ổ cũng hiển
        // thị chuỗi này) — xem DriveVerdictText.TranslateVerdict.
        string verdict = DriveVerdictText.TranslateVerdict(value);
        if (verdict != value) return verdict;
        // Dòng "CẢNH BÁO KHÔNG KHỚP": danh sách lý do không khớp nối "; "
        // rồi tới câu nhắc kiểm tra tem.
        if ((m = Regex.Match(value, @"^(.+) — kiểm tra tem máy tay trước khi mua$")).Success)
            return string.Format(Lang.Get("Config.Value.MismatchNote"),
                DriveVerdictText.TranslateJoinedFragments(m.Groups[1].Value));
        // Dòng "Lỗi nghiêm trọng": nhiều mảnh lý do nối "; " (không có đuôi
        // "(bình thường phải 0)" như lý do chặn) — dịch được hết mới dịch.
        if (value.Contains("; "))
        {
            string joined = DriveVerdictText.TranslateJoinedFragments(value);
            if (joined != value) return joined;
        }
        // Chi tiết từng thanh RAM: "16 GB　Định mức 4800 MT/s　Thực tế ..." —
        // chỉ thay 2 từ "Định mức"/"Thực tế", giữ nguyên số + tên hãng.
        if (value.Contains("Định mức ") || value.Contains("Thực tế "))
            return value
                .Replace("Định mức ", Lang.Get("Config.Value.Rated") + " ")
                .Replace("Thực tế ", Lang.Get("Config.Value.Actual") + " ");
        return value;
    }

    /// <summary>Lý do đọc SMBIOS thô thất bại (SmbiosService.LastFailureReason):
    /// các mẫu cố định của service; ngoại lệ runtime thì giữ nguyên message
    /// (thường đã là tiếng Anh của .NET).</summary>
    static string TranslateFailureReason(string reason)
    {
        Match m;
        if ((m = Regex.Match(reason, @"^parse bảng (\d+) byte không thấy Type 0/1/2/3$")).Success)
            return string.Format(Lang.Get("Config.Value.SmbiosParseFail"), m.Groups[1].Value);
        if ((m = Regex.Match(reason, @"^API firmware thất bại: truy vấn kích thước bảng trả về 0 \(mã lỗi (\d+)\)$")).Success)
            return string.Format(Lang.Get("Config.Value.SmbiosSizeFail"), m.Groups[1].Value);
        if ((m = Regex.Match(reason, @"^API firmware thất bại: đọc bảng trả về 0 \(mã lỗi (\d+)\)$")).Success)
            return string.Format(Lang.Get("Config.Value.SmbiosReadFail"), m.Groups[1].Value);
        if ((m = Regex.Match(reason, @"^API firmware thất bại: đọc bảng trả về (\d+) byte, vượt kích thước (\d+) byte$")).Success)
            return string.Format(Lang.Get("Config.Value.SmbiosOversize"), m.Groups[1].Value, m.Groups[2].Value);
        if (reason == "API firmware thất bại (không rõ chi tiết)")
            return Lang.Get("Config.Value.SmbiosApiFail");
        if (reason == "API firmware không hỗ trợ hoặc bị chặn")
            return Lang.Get("Config.Value.SmbiosApiUnsupported");
        if ((m = Regex.Match(reason, @"^ngoại lệ khi đọc SMBIOS: (.*)$")).Success)
            return string.Format(Lang.Get("Config.Value.SmbiosException"), m.Groups[1].Value);
        return reason;
    }

    /// <summary>Dịch 1 lý do ổ / SMBIOS: bỏ tiền tố "Ổ #n " (thành "Drive #n ")
    /// rồi dịch phần thân theo mẫu; không khớp mẫu thì giữ nguyên văn.</summary>
    static string TranslateReason(string reason)
    {
        string prefix = "";
        string body = reason;
        var m = Regex.Match(reason, @"^Ổ( #\d+)? ");
        if (m.Success)
        {
            prefix = Lang.Get("Config.Reason.Drive") + m.Groups[1].Value + " ";
            body = reason[m.Length..];
        }
        return prefix + (DriveVerdictText.TranslateReasonFragment(body) ?? body);
    }

    /// <summary>Nối nhiều lý do bằng "; " sau khi dịch từng cái (PhaseText,
    /// ghi chú verdict). Lý do nào không dịch được thì giữ nguyên văn.</summary>
    static string TranslateReasonsJoined(IReadOnlyList<string> reasons) =>
        string.Join("; ", reasons.Select(TranslateReason));
}
