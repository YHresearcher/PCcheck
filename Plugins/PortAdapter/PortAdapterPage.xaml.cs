using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace YanJi.Plugin.PortAdapter;

/// <summary>「Cổng kết nối &amp; Sạc」页面：进页自动 quét cổng + ảnh chụp nhanh sạc一次（后台线程，
/// 切页不重扫）；「Test sạc」跑 10 秒采样；人工目视勾选 + 确认通过。
/// 通过约定同其它模块：确认通过 → PassChanged(true) → 导航圆点变绿 + 自动跳下一模块；
/// 重新 quét / test sạc → 回到待测试。未 test sạc（máy có pin）或 sạc đánh giá Kém thì
/// không cho qua — sạc hỏng mà cho qua là bán máy kèm sạc hỏng cho khách。</summary>
public partial class PortAdapterPage : UserControl, YanJi.PluginSdk.IModulePage
{
    bool _running;
    bool _autoStarted; // 页面常驻机制下 Loaded 可能重入，切走再切回不自动重扫
    bool _noBattery;
    bool _adapterTested;
    string _adapterHealth = "";

    /// <summary>核对完成状态变化（true = 已通过，false = 重新测试回到待测试）；MainWindow 据此同步导航圆点。</summary>
    public event Action<bool>? PassChanged;

    /// <summary>当前是否已确认通过。</summary>
    public bool Passed { get; private set; }

    public PortAdapterPage()
    {
        InitializeComponent();
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
        RescanButton.IsEnabled = false;
        AdapterTestButton.IsEnabled = false;
        PassButton.IsEnabled = false;
        TestingHint.Text = "Đang quét cổng...";
        try
        {
            var ports = await Task.Run(PortAdapterService.ScanPorts);
            var snap = await Task.Run(PortAdapterService.GetAdapterSnapshot);
            _noBattery = !snap.HasBattery;
            UpdatePortsUI(ports);
            UpdateSnapshotUI(snap);
            AdapterVoltageText.Text = "Điện áp pin: chưa test 10 giây";
            AdapterPowerText.Text = "Công suất sạc: chưa test 10 giây";
            AdapterBatteryText.Text = "Mức pin: chưa test 10 giây";
            AdapterHealthText.Text = "Đánh giá sạc: chưa test 10 giây";
            AdapterIssuesText.Text = "";
            AppendLog($"Quét xong: {ports.UsbControllerCount} USB controller, {ports.RootHubCount} root hub, " +
                $"USB-C/Thunderbolt {ports.UsbCDeviceNames.Count} thiết bị, " +
                $"card mạng có dây: {(ports.EthernetAdapterName ?? "không phát hiện")}.");
            TestingHint.Text = _noBattery
                ? "Máy không có pin — bỏ qua phần sạc, chỉ test cổng."
                : "Quét xong. Cắm sạc rồi bấm \"Test sạc (10 giây)\".";
            PassButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            TestingHint.Text = "Quét bị lỗi: " + ex.Message;
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
        RescanButton.IsEnabled = false;
        AdapterTestButton.IsEnabled = false;
        PassButton.IsEnabled = false;
        TestingHint.Text = "Đang test sạc 10 giây, đừng rút sạc...";
        try
        {
            var progress = new Progress<string>(msg => AppendLog(msg));
            var r = await PortAdapterService.RunAdapterTestAsync(progress);
            _adapterTested = r.Tested;
            _adapterHealth = r.Health;
            if (!r.Tested)
            {
                AdapterHealthText.Text = $"Đánh giá sạc: {r.Health}";
                AdapterIssuesText.Text = string.Join(" ", r.Issues);
                AppendLog("Test sạc không chạy được: " + r.Health);
                TestingHint.Text = r.Health;
            }
            else
            {
                AdapterVoltageText.Text = $"Điện áp pin: trung bình {r.VoltageAvg:0.00} V " +
                    $"(thấp {r.VoltageMin:0.00} V, cao {r.VoltageMax:0.00} V, độ ổn định: {r.VoltageStability})";
                AdapterPowerText.Text = $"Công suất sạc: trung bình {r.PowerAvg:0.#} W, " +
                    $"dòng sạc trung bình {r.CurrentAvg:0.00} A, công suất định mức {r.EstimatedRating}";
                AdapterBatteryText.Text = $"Mức pin: đầu {r.BatteryStartPercent:0.#} %, cuối {r.BatteryEndPercent:0.#} %, " +
                    $"đang sạc: {(r.IsCharging ? "có" : "không")}, tốc độ sạc trung bình {r.ChargingRateAvg:0.#} W";
                AdapterHealthText.Text = $"Đánh giá sạc: {r.Health} ({r.HealthScore}/100)";
                AdapterIssuesText.Text = r.Issues.Count > 0 ? "Vấn đề: " + string.Join(" ", r.Issues) : "";
                AppendLog($"Test sạc xong: {r.Health} ({r.HealthScore}/100), " +
                    $"điện áp {r.VoltageAvg:0.00} V ({r.VoltageStability}), công suất {r.PowerAvg:0.#} W.");
                TestingHint.Text = r.Health == "Kém"
                    ? "Sạc đánh giá Kém — thay sạc rồi test lại, không cho qua."
                    : "Test sạc xong. Tick xong các cổng bằng mắt rồi mới cho qua.";
            }
            PassButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            TestingHint.Text = "Test sạc bị lỗi: " + ex.Message;
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
            StatusText.Text = "Chưa test sạc — bấm \"Test sạc (10 giây)\" trước, rồi mới cho qua";
            StatusText.SetResourceReference(TextBlock.ForegroundProperty, "ErrorBrush");
            AppendLog("Chưa cho qua: máy có pin nhưng chưa test sạc 10 giây.");
            return;
        }
        if (_adapterHealth == "Kém")
        {
            StatusText.Text = "Sạc đánh giá Kém — thay sạc rồi test lại, không cho qua";
            StatusText.SetResourceReference(TextBlock.ForegroundProperty, "ErrorBrush");
            AppendLog("Chưa cho qua: sạc đánh giá Kém.");
            return;
        }
        SetPassed(true);
        PassButton.IsEnabled = false;
        AppendLog($"Đã cho qua lúc {DateTime.Now:HH:mm:ss}. Tick tay: HDMI={ChkHdmi.IsChecked}, " +
            $"VGA={ChkVga.IsChecked}, RJ45={ChkRj45.IsChecked}, USB-A={ChkUsbA.IsChecked}, " +
            $"USB-C={ChkUsbC.IsChecked}, đầu sạc={ChkChargerPlug.IsChecked}.");
        TestingHint.Text = "Đã xác nhận cho qua; cần kiểm lại thì bấm \"Quét lại cổng\".";
    }

    void UpdatePortsUI(PortAdapterService.PortScanResult p)
    {
        UsbControllersText.Text = p.UsbControllerNames.Count > 0
            ? $"USB controller: {p.UsbControllerCount} cái ({string.Join("; ", p.UsbControllerNames)})"
            : $"USB controller: {p.UsbControllerCount} cái";
        RootHubsText.Text = $"USB root hub: {p.RootHubCount} cái " +
            "(chỉ là số hub, không suy ra số cổng vật lý trên vỏ máy)";
        UsbCText.Text = p.UsbCDeviceNames.Count > 0
            ? $"Thiết bị USB-C / Thunderbolt phát hiện qua tên: {string.Join("; ", p.UsbCDeviceNames)}"
            : "Thiết bị USB-C / Thunderbolt: không phát hiện qua tên thiết bị (kiểm tra bằng mắt ở dưới)";
        UsbPdText.Text = p.UsbCPdSign
            ? "Dấu hiệu USB-C PD: có thiết bị mang tên Power Delivery / sạc Thunderbolt"
            : "Dấu hiệu USB-C PD: không phát hiện (không có nghĩa là máy không hỗ trợ PD)";
        EthernetText.Text = p.EthernetAdapterName != null
            ? $"Card mạng có dây: {p.EthernetAdapterName} (có card chưa chắc có cổng RJ45 trên vỏ — kiểm tra bằng mắt)"
            : "Card mạng có dây: không phát hiện";
        MonitorText.Text = p.MonitorTechnologies.Count > 0
            ? $"Kết nối màn hình hiện tại (mã công nghệ, 5 = HDMI, 0 = VGA theo cách đọc của bản gốc): {string.Join(", ", p.MonitorTechnologies)} — đây là màn hình đang cắm, không phải danh sách cổng"
            : "Kết nối màn hình hiện tại: không đọc được";
    }

    void UpdateSnapshotUI(PortAdapterService.AdapterSnapshot s)
    {
        if (!s.HasBattery)
        {
            AdapterStatusText.Text = "Trạng thái sạc: không phát hiện pin (máy bàn hoặc máy không có pin)";
            AdapterInstantText.Text = "Ảnh chụp nhanh: không áp dụng cho máy không có pin";
            return;
        }
        AdapterStatusText.Text = s.PowerOnline
            ? "Trạng thái sạc: đã cắm sạc" + (s.Charging ? " và đang sạc" : "")
            : "Trạng thái sạc: chưa cắm sạc";
        string power = s.ChargeWatts == null ? "?"
            : s.ChargeWatts.Value > 0 ? $"đang sạc {s.ChargeWatts.Value:0.#} W"
            : s.ChargeWatts.Value < 0 ? $"đang xả {-s.ChargeWatts.Value:0.#} W"
            : "không sạc/xả";
        AdapterInstantText.Text = $"Ảnh chụp nhanh: điện áp pin {(s.VoltageVolts?.ToString("0.00") ?? "?")} V, " +
            $"{power}, mức pin {(s.BatteryPercent?.ToString("0.#") ?? "?")} %";
    }

    void AppendLog(string msg)
    {
        LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");
        LogBox.ScrollToEnd();
    }

    /// <summary>更新核对状态行（通过 = 绿色「✓ 已核对通过」）并通知导航圆点。</summary>
    void SetPassed(bool passed)
    {
        Passed = passed;
        if (passed)
        {
            StatusText.Text = "✓ Đã xác nhận không lỗi";
            StatusText.Foreground = Brushes.ForestGreen;
        }
        else
        {
            StatusText.Text = "Chờ kiểm tra";
            StatusText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
        }
        PassChanged?.Invoke(passed);
    }
}
