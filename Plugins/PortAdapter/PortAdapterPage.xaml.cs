using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using YanJi.PluginSdk;

namespace YanJi.Plugin.PortAdapter;

/// <summary>「Cổng kết nối &amp; Sạc」页面：进页自动 quét cổng + ảnh chụp nhanh sạc一次（后台线程，
/// 切页不重扫）；「Test sạc」跑 10 秒采样；人工目视勾选 + 确认通过。
/// 通过约定同其它模块：确认通过 → PassChanged(true) → 导航圆点变绿 + 自动跳下一模块；
/// 重新 quét / test sạc → 回到待测试。未 test sạc（máy có pin）或 sạc đánh giá Kém thì
/// không cho qua — sạc hỏng mà cho qua là mua máy kèm sạc hỏng。</summary>
public partial class PortAdapterPage : UserControl, YanJi.PluginSdk.IModulePage
{
    bool _running;
    bool _autoStarted; // 页面常驻机制下 Loaded 可能重入，切走再切回不自动重扫
    bool _noBattery;
    bool _adapterTested;
    string _adapterHealth = "";
    int _adapterScore;
    readonly IHostContext? _host;

    // Kết quả quét/test gần nhất, để vẽ lại nhãn khi đổi ngôn ngữ.
    PortAdapterService.PortScanResult? _lastPorts;
    PortAdapterService.AdapterSnapshot? _lastSnap;
    PortAdapterService.AdapterTestResult? _lastTest;

    // Gợi ý hiện tại (key + lỗi nếu có) và trạng thái lỗi của dòng trạng thái,
    // để vẽ lại khi đổi ngôn ngữ mà không làm mất thông báo đang hiện.
    string _hintKey = "";
    string? _hintError;
    string _statusKey = "";
    bool _statusTouched; // SetPassed từng chạy chưa (trước đó dòng trạng thái là "Chưa test" ở XAML)

    /// <summary>核对完成状态变化（true = 已通过，false = 重新测试回到待测试）；MainWindow 据此同步导航圆点。</summary>
    public event Action<bool>? PassChanged;

    /// <summary>当前是否已确认通过。</summary>
    public bool Passed { get; private set; }

    public PortAdapterPage(IHostContext? host = null)
    {
        _host = host;
        InitializeComponent();
        Lang.Changed += RefreshLabels;
        Loaded += (_, _) =>
        {
            if (_autoStarted) return;
            _autoStarted = true;
            _ = ScanAsync();
        };
    }

    void RescanButton_Click(object sender, RoutedEventArgs e) => _ = ScanAsync();

    async Task ScanAsync()
    {
        if (_running) return;
        _running = true;
        SetPassed(false);
        _adapterTested = false;
        _adapterHealth = "";
        _adapterScore = 0;
        _lastTest = null;
        ReportPortsVerdict();
        RescanButton.IsEnabled = false;
        AdapterTestButton.IsEnabled = false;
        PassButton.IsEnabled = false;
        SetHint("Ports.Hint.Scanning");
        try
        {
            var ports = await Task.Run(PortAdapterService.ScanPorts);
            var snap = await Task.Run(PortAdapterService.GetAdapterSnapshot);
            _noBattery = !snap.HasBattery;
            _lastPorts = ports;
            _lastSnap = snap;
            UpdatePortsUI(ports);
            UpdateSnapshotUI(snap);
            ResetTestTexts();
            AppendLog($"Quét xong: {ports.UsbControllerCount} USB controller, {ports.RootHubCount} root hub, " +
                $"USB-C/Thunderbolt {ports.UsbCDeviceNames.Count} thiết bị, " +
                $"card mạng có dây: {(ports.EthernetAdapterName ?? "không phát hiện")}.");
            SetHint(_noBattery ? "Ports.Hint.NoBattery" : "Ports.Hint.ScanDone");
            ReportPortsVerdict();
            PassButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            SetHint("Ports.Hint.ScanFailed", ex.Message);
        }
        finally
        {
            RescanButton.IsEnabled = true;
            AdapterTestButton.IsEnabled = true;
            _running = false;
        }
    }

    async void AdapterTestButton_Click(object sender, RoutedEventArgs e)
    {
        if (_running) return;
        _running = true;
        SetPassed(false);
        _adapterTested = false;
        _adapterHealth = "";
        _adapterScore = 0;
        ReportPortsVerdict();
        RescanButton.IsEnabled = false;
        AdapterTestButton.IsEnabled = false;
        PassButton.IsEnabled = false;
        SetHint("Ports.Hint.TestingCharger");
        try
        {
            var progress = new Progress<string>(msg => AppendLog(msg));
            var r = await PortAdapterService.RunAdapterTestAsync(progress);
            _adapterTested = r.Tested;
            _adapterHealth = r.Health;
            _adapterScore = r.Tested ? r.HealthScore : 0;
            _lastTest = r;
            ReportPortsVerdict();
            if (!r.Tested)
            {
                AppendLog("Test sạc không chạy được: " + r.Health);
            }
            else
            {
                AppendLog($"Test sạc xong: {r.Health} ({r.HealthScore}/100), " +
                    $"điện áp {r.VoltageAvg:0.00} V ({r.VoltageStability}), công suất {r.PowerAvg:0.#} W.");
            }
            RenderTestResult(r);
            PassButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            SetHint("Ports.Hint.TestFailed", ex.Message);
        }
        finally
        {
            RescanButton.IsEnabled = true;
            AdapterTestButton.IsEnabled = true;
            _running = false;
        }
    }

    void PassButton_Click(object sender, RoutedEventArgs e)
    {
        if (_running) return;
        if (!_noBattery && !_adapterTested)
        {
            _statusKey = "Ports.Status.NeedChargerTest";
            RenderStatus();
            AppendLog("Chưa cho qua: máy có pin nhưng chưa test sạc 10 giây.");
            ReportPortsVerdict();
            return;
        }
        if (_adapterHealth == "Kém")
        {
            _statusKey = "Ports.Status.PoorCharger";
            RenderStatus();
            AppendLog("Chưa cho qua: sạc đánh giá Kém.");
            ReportPortsVerdict();
            return;
        }
        SetPassed(true);
        ReportPortsVerdict();
        PassButton.IsEnabled = false;
        AppendLog($"Đã cho qua lúc {DateTime.Now:HH:mm:ss}. Tick tay: HDMI={ChkHdmi.IsChecked}, " +
            $"VGA={ChkVga.IsChecked}, RJ45={ChkRj45.IsChecked}, USB-A={ChkUsbA.IsChecked}, " +
            $"USB-C={ChkUsbC.IsChecked}, đầu sạc={ChkChargerPlug.IsChecked}.");
        SetHint("Ports.Hint.Passed");
    }

    void UpdatePortsUI(PortAdapterService.PortScanResult p)
    {
        UsbControllersText.Text = p.UsbControllerNames.Count > 0
            ? string.Format(Lang.Get("Ports.UsbControllersLine"), p.UsbControllerCount, string.Join("; ", p.UsbControllerNames))
            : string.Format(Lang.Get("Ports.UsbControllersLineNoNames"), p.UsbControllerCount);
        RootHubsText.Text = string.Format(Lang.Get("Ports.RootHubsLine"), p.RootHubCount);
        UsbCText.Text = p.UsbCDeviceNames.Count > 0
            ? string.Format(Lang.Get("Ports.UsbCLine"), string.Join("; ", p.UsbCDeviceNames))
            : Lang.Get("Ports.UsbCNone");
        UsbPdText.Text = p.UsbCPdSign
            ? Lang.Get("Ports.UsbPdYes")
            : Lang.Get("Ports.UsbPdNo");
        EthernetText.Text = p.EthernetAdapterName != null
            ? string.Format(Lang.Get("Ports.EthernetLine"), p.EthernetAdapterName)
            : Lang.Get("Ports.EthernetNone");
        MonitorText.Text = p.MonitorTechnologies.Count > 0
            ? string.Format(Lang.Get("Ports.MonitorLine"), string.Join(", ", p.MonitorTechnologies))
            : Lang.Get("Ports.MonitorNone");
    }

    void UpdateSnapshotUI(PortAdapterService.AdapterSnapshot s)
    {
        if (!s.HasBattery)
        {
            AdapterStatusText.Text = Lang.Get("Ports.AdapterStatus.NoBattery");
            AdapterInstantText.Text = Lang.Get("Ports.AdapterSnapshot.NoBattery");
            return;
        }
        AdapterStatusText.Text = s.PowerOnline
            ? Lang.Get(s.Charging ? "Ports.AdapterStatus.PluggedCharging" : "Ports.AdapterStatus.Plugged")
            : Lang.Get("Ports.AdapterStatus.Unplugged");
        string power = s.ChargeWatts == null ? "?"
            : s.ChargeWatts.Value > 0 ? string.Format(Lang.Get("Ports.Snapshot.Charging"), $"{s.ChargeWatts.Value:0.#}")
            : s.ChargeWatts.Value < 0 ? string.Format(Lang.Get("Ports.Snapshot.Discharging"), $"{-s.ChargeWatts.Value:0.#}")
            : Lang.Get("Ports.Snapshot.Idle");
        AdapterInstantText.Text = string.Format(Lang.Get("Ports.AdapterSnapshotLine"),
            s.VoltageVolts?.ToString("0.00") ?? "?", power, s.BatteryPercent?.ToString("0.#") ?? "?");
    }

    /// <summary>Vẽ lại 4 dòng kết quả test sạc + dòng vấn đề + gợi ý từ kết quả
    /// đã lưu (gọi sau khi test xong, hoặc khi đổi ngôn ngữ). updateHint=false
    /// thì không đụng tới gợi ý hiện tại (vd đã cho qua rồi mới đổi ngôn ngữ).</summary>
    void RenderTestResult(PortAdapterService.AdapterTestResult r, bool updateHint = true)
    {
        if (!r.Tested)
        {
            string reasonKey = NotTestedReasonKey(r.Health);
            AdapterHealthText.Text = string.Format(Lang.Get("Ports.HealthLine"), NotTestedReasonText(reasonKey));
            AdapterIssuesText.Text = NotTestedReasonText(reasonKey);
            if (updateHint) SetHint(reasonKey);
            return;
        }
        AdapterVoltageText.Text = string.Format(Lang.Get("Ports.VoltageLine"),
            $"{r.VoltageAvg:0.00}", $"{r.VoltageMin:0.00}", $"{r.VoltageMax:0.00}", HealthWord(r.VoltageStability));
        AdapterPowerText.Text = string.Format(Lang.Get("Ports.PowerLine"),
            $"{r.PowerAvg:0.#}", $"{r.CurrentAvg:0.00}", RatingDisplay(r.PowerAvg));
        AdapterBatteryText.Text = string.Format(Lang.Get("Ports.BatteryLine"),
            $"{r.BatteryStartPercent:0.#}", $"{r.BatteryEndPercent:0.#}",
            Lang.Get(r.IsCharging ? "Ports.Yes" : "Ports.No"), $"{r.ChargingRateAvg:0.#}");
        AdapterHealthText.Text = string.Format(Lang.Get("Ports.HealthLineScored"), HealthWord(r.Health), r.HealthScore);
        // Câu vấn đề do service sinh (kèm số đo) — dịch tại đây theo mẫu
        // (TranslateIssue); chỉ tiền tố "Vấn đề:" là chuỗi giao diện nên dịch.
        AdapterIssuesText.Text = r.Issues.Count > 0 ? string.Format(Lang.Get("Ports.IssuesPrefix"), string.Join(" ", r.Issues.Select(TranslateIssue))) : "";
        if (updateHint) SetHint(r.Health == "Kém" ? "Ports.Hint.PoorCharger" : "Ports.Hint.TestDone");
    }

    /// <summary>Đưa 4 dòng kết quả test sạc về trạng thái "chưa test 10 giây"
    /// (sau khi quét lại, hoặc khi đổi ngôn ngữ mà chưa có kết quả test).</summary>
    void ResetTestTexts()
    {
        AdapterVoltageText.Text = Lang.Get("Ports.VoltageNotTested");
        AdapterPowerText.Text = Lang.Get("Ports.PowerNotTested");
        AdapterBatteryText.Text = Lang.Get("Ports.BatteryNotTested");
        AdapterHealthText.Text = Lang.Get("Ports.HealthNotTested");
        AdapterIssuesText.Text = "";
    }

    void AppendLog(string msg)
    {
        LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");
        LogBox.ScrollToEnd();
    }

    /// <summary>Báo cáo kết luận module cho trang Tổng kết. Sạc Kém = Chặn, sạc Trung bình
    /// = Cảnh báo (cho qua được nhưng tối đa mua có điều kiện), sạc Tốt / Xuất sắc mà
    /// đã cho qua = Đạt, còn lại (chưa test, chưa cho qua) = Chưa test xong.
    /// Máy không pin thì không có phần sạc, cho qua cổng là Đạt.</summary>
    void ReportPortsVerdict()
    {
        if (_host == null) return;
        // Sạc Kém thì kèm tín hiệu batteryNotCharging để Tổng kết ra câu rắc rối
        // nguồn điện thay vì câu lỗi nghiêm trọng chung; các mức còn lại thì xóa
        // tín hiệu này (vd thay sạc mới rồi test lại mà đạt thì không còn lỗi sạc).
        _host.ReportSignals("ports", _adapterHealth == "Kém" ? ["batteryNotCharging"] : []);
        if (_adapterHealth == "Kém")
            _host.ReportVerdict("ports", new(ModuleVerdictLevel.Chan, string.Format(Lang.Get("Ports.Verdict.Poor"), _adapterScore)));
        else if (_adapterHealth == "Trung bình")
            _host.ReportVerdict("ports", new(ModuleVerdictLevel.CanhBao, string.Format(Lang.Get("Ports.Verdict.Fair"), _adapterScore)));
        else if (Passed)
            _host.ReportVerdict("ports", new(ModuleVerdictLevel.Dat, _noBattery ? Lang.Get("Ports.Verdict.NoBatteryPassed") : string.Format(Lang.Get("Ports.Verdict.PassedCharger"), HealthWord(_adapterHealth), _adapterScore)));
        else if (!_adapterTested && !_noBattery)
            _host.ReportVerdict("ports", new(ModuleVerdictLevel.ChuaTest, Lang.Get("Ports.Verdict.NotTested")));
        else if (_adapterTested)
            _host.ReportVerdict("ports", new(ModuleVerdictLevel.ChuaTest, string.Format(Lang.Get("Ports.Verdict.WaitingPass"), HealthWord(_adapterHealth), _adapterScore)));
        else
            _host.ReportVerdict("ports", new(ModuleVerdictLevel.ChuaTest, Lang.Get("Ports.Verdict.NoBatteryWaiting")));
    }

    /// <summary>更新核对状态行（通过 = 绿色「✓ 已核对通过」）并通知导航圆点。</summary>
    void SetPassed(bool passed)
    {
        Passed = passed;
        _statusTouched = true;
        if (!passed) _statusKey = "";
        RenderStatus();
        PassChanged?.Invoke(passed);
    }

    /// <summary>Vẽ lại dòng trạng thái từ Passed / _statusKey (không bắn
    /// PassChanged — dùng khi đổi ngôn ngữ hoặc khi PassButton báo lỗi).</summary>
    void RenderStatus()
    {
        if (Passed)
        {
            StatusText.Text = Lang.Get("Ports.Status.Passed");
            StatusText.Foreground = Brushes.ForestGreen;
        }
        else if (_statusKey.Length > 0)
        {
            StatusText.Text = _statusKey == "Ports.Status.NeedChargerTest"
                ? string.Format(Lang.Get(_statusKey), Lang.Get("Ports.ChargerTest"))
                : Lang.Get(_statusKey);
            StatusText.SetResourceReference(TextBlock.ForegroundProperty, "ErrorBrush");
        }
        else if (!_statusTouched)
        {
            StatusText.Text = Lang.Get("Ports.StatusNotTested");
            StatusText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
        }
        else
        {
            StatusText.Text = Lang.Get("Ports.Status.Waiting");
            StatusText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
        }
    }

    /// <summary>Vẽ lại toàn bộ nhãn hiện tại khi đổi ngôn ngữ (Lang.Changed).
    /// Nhãn tĩnh trong XAML tự đổi qua DynamicResource; ở đây chỉ vẽ lại các
    /// nhãn code-behind đang giữ, từ kết quả đã lưu — không chạy lại quét/test.</summary>
    void RefreshLabels()
    {
        RenderStatus();
        if (_lastPorts != null) UpdatePortsUI(_lastPorts);
        else
        {
            UsbControllersText.Text = Lang.Get("Ports.UsbControllersNotScanned");
            RootHubsText.Text = Lang.Get("Ports.RootHubsNotScanned");
            UsbCText.Text = Lang.Get("Ports.UsbCNotScanned");
            UsbPdText.Text = Lang.Get("Ports.UsbPdNotScanned");
            EthernetText.Text = Lang.Get("Ports.EthernetNotScanned");
            MonitorText.Text = Lang.Get("Ports.MonitorNotScanned");
        }
        if (_lastSnap != null) UpdateSnapshotUI(_lastSnap);
        else
        {
            AdapterStatusText.Text = Lang.Get("Ports.AdapterStatusNotScanned");
            AdapterInstantText.Text = Lang.Get("Ports.AdapterSnapshotNotScanned");
        }
        if (_lastTest != null) RenderTestResult(_lastTest, updateHint: false);
        else ResetTestTexts();
        RenderHint();
    }

    /// <summary>Câu gợi ý cho _hintKey (tham số động như tên nút được dịch lại
    /// lúc vẽ, để đổi ngôn ngữ không giữ tên nút cũ).</summary>
    string HintText(string key, string? error = null) => key switch
    {
        "Ports.Hint.ScanDone" => string.Format(Lang.Get(key), Lang.Get("Ports.ChargerTest")),
        "Ports.Hint.Passed" => string.Format(Lang.Get(key), Lang.Get("Ports.Rescan")),
        "Ports.NotTested.Unplugged" => string.Format(Lang.Get(key), Lang.Get("Ports.ChargerTest")),
        "Ports.Hint.ScanFailed" => string.Format(Lang.Get(key), error ?? ""),
        "Ports.Hint.TestFailed" => string.Format(Lang.Get(key), error ?? ""),
        _ => Lang.Get(key),
    };

    void SetHint(string key, string? error = null)
    {
        _hintKey = key;
        _hintError = error;
        TestingHint.Text = HintText(key, error);
    }

    void RenderHint()
    {
        TestingHint.Text = _hintKey.Length == 0 ? "" : HintText(_hintKey, _hintError);
    }

    /// <summary>Dịch từ sức khỏe/độ ổn định do service trả về ("Xuất sắc" /
    /// "Tốt" / "Trung bình" / "Kém" / "Không rõ") sang ngôn ngữ hiện tại, chỉ
    /// để hiển thị — _adapterHealth và mọi so sánh logic vẫn giữ nguyên văn
    /// tiếng Việt của service, không đổi ngưỡng hay mức kết luận.</summary>
    static string HealthWord(string vi) => vi switch
    {
        "Xuất sắc" => Lang.Get("Ports.Health.Excellent"),
        "Tốt" => Lang.Get("Ports.Health.Good"),
        "Trung bình" => Lang.Get("Ports.Health.Fair"),
        "Kém" => Lang.Get("Ports.Health.Poor"),
        "Không rõ" => Lang.Get("Ports.Health.Unknown"),
        _ => vi,
    };

    /// <summary>Dịch 1 câu vấn đề sạc do service sinh (kèm số đo): khớp theo
    /// mẫu rồi gắn lại số đo nguyên văn; không khớp mẫu thì giữ nguyên văn.
    /// Cùng kỹ thuật đối sánh nội dung như HealthWord — service giữ nguyên
    /// chuỗi canonical tiếng Việt, chỉ lớp hiển thị này dịch.</summary>
    static string TranslateIssue(string issue)
    {
        Match m;
        if ((m = Regex.Match(issue, @"^Đã cắm sạc nhưng pin không sạc \(pin ([\d.,]+) %\)$")).Success)
            return string.Format(Lang.Get("Ports.Issue.NotCharging"), m.Groups[1].Value);
        if ((m = Regex.Match(issue, @"^Điện áp không ổn định \(chênh ([\d.,]+) %\)$")).Success)
            return string.Format(Lang.Get("Ports.Issue.VoltageUnstable"), m.Groups[1].Value);
        if ((m = Regex.Match(issue, @"^Điện áp hơi dao động \(chênh ([\d.,]+) %\)$")).Success)
            return string.Format(Lang.Get("Ports.Issue.VoltageFluctuating"), m.Groups[1].Value);
        if ((m = Regex.Match(issue, @"^Tốc độ sạc thấp \(([\d.,]+) W\)$")).Success)
            return string.Format(Lang.Get("Ports.Issue.SlowCharging"), m.Groups[1].Value);
        return issue;
    }

    /// <summary>Phân biệt 2 lý do không test được của service theo nội dung câu
    /// (không có pin / chưa cắm sạc) — chỉ để chọn key dịch, không đổi logic.</summary>
    static string NotTestedReasonKey(string reason) =>
        reason.Contains("không phát hiện pin", StringComparison.Ordinal)
            ? "Ports.NotTested.NoBattery" : "Ports.NotTested.Unplugged";

    static string NotTestedReasonText(string key) => key == "Ports.NotTested.Unplugged"
        ? string.Format(Lang.Get(key), Lang.Get("Ports.ChargerTest"))
        : Lang.Get(key);

    /// <summary>Nhãn công suất định mức ước tính, tính lại từ công suất trung
    /// bình theo đúng bảng + hệ số 0.85 của service (45…330 W, vượt thì "330 W
    /// trở lên") — chỉ để hiển thị, vì chuỗi của service mang hậu tố tiếng Việt
    /// "(ước tính)" không tách được sạch ở tầng hiển thị.</summary>
    static string RatingDisplay(double pAvg)
    {
        if (pAvg <= 0) return Lang.Get("Ports.Health.Unknown");
        int[] common = [45, 65, 90, 120, 135, 150, 180, 230, 240, 330];
        foreach (int w in common)
            if (pAvg <= w * 0.85) return string.Format(Lang.Get("Ports.RatingEstimated"), w);
        return Lang.Get("Ports.RatingAbove330");
    }
}

// ─── Cố tình để lại tiếng Việt (không phải chuỗi hiển thị chưa dịch) ───
// 1. AppendLog: nhật ký append-only — mọi dòng đã ghi giữ nguyên văn như lúc
//    ghi (kể cả mẫu 10 giây do service đẩy qua Progress<string> từ thread nền,
//    nơi không gọi được Lang.Get an toàn). Dòng log mới vẫn là tiếng Việt.
// 2. PortAdapterService.cs: KHÔNG SỬA — từ sức khỏe/ổn định ("Kém", "Trung bình"...),
//    câu vấn đề/khuyến nghị kèm số đo, mẫu log 10 giây và lý do NotTested đều
//    là dữ liệu của service; đổi chúng sẽ chạm tới so sánh logic ở page này
//    (_adapterHealth == "Kém"). Câu vấn đề hiển thị ở AdapterIssuesText được
//    dịch tại page (TranslateIssue, khớp mẫu + gắn lại số đo), khuyến nghị
//    (Recommendations) chưa hiển thị ở đâu nên không dịch.
// 3. So sánh logic "Kém" / "Trung bình" và khóa khớp chuỗi trong HealthWord /
//    NotTestedReasonKey là khóa đối sánh nội bộ, không phải chuỗi hiển thị.
// 4. PortAdapterPlugin.cs + plugin.json: tên module "Cổng kết nối & Sạc" và
//    credit (metadata, coordinator quản lý — plugin.json đã có nameEn).
