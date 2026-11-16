using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using YanJi.PluginSdk;

namespace YanJi.Plugin.StressTest;

/// <summary>「散热测试」页面：CPU 单烤（prime95）/ GPU 单烤（FurMark）/ 双烤。
/// 进程起停全部走 StressTestService；安全约定：页面 Unloaded（切走/关窗）自动停止烤机，
/// 程序退出还有 Job 对象兜底，绝不留下后台烤机进程。通过后状态行变绿并同步导航圆点。</summary>
public partial class StressTestPage : UserControl, YanJi.PluginSdk.IModulePage
{
    readonly IHostContext? _host; // Để báo tín hiệu CPU/GPU cho Tổng kết (có thể null khi test riêng)
    readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromMilliseconds(500) };
    double? _maxCpuTemp; // Nhiệt độ CPU tối đa trong lần nướng này (null = cảm biến không đọc được). Nhiệt GPU chỉ hiện trên biểu đồ, không tự kết luận quá nhiệt vì chưa có ngưỡng GPU — artifact GPU là tick tay.
    TimeSpan _lastRun = TimeSpan.Zero; // 最近一次烤了多久（未烤过为 0，状态行提示用）
    DateTime? _runStart;               // 本轮烤机起点（running→停止 的那一拍结算进 _lastRun）
    bool _autoStopAttempted;           // Đã thử tự dừng cho lần nướng này chưa (tránh gọi StopVerified mỗi 500ms nếu dừng không được)
    bool _autoStopped;                 // Lần nướng vừa rồi là tự dừng theo thiết lập ở trang này (để ghi chú ở trạng thái sau khi dừng)
    bool _startFailed;                 // Lần bấm Bắt đầu gần nhất khởi động thất bại (giữ thông báo lỗi tới lần bấm sau)
    Button? _failedInstallButton;      // Nút cài driver nào đang hiện "cài thất bại, bấm thử lại" (null = không có)
    string? _settingsMsgKey;           // Key thông báo lưu thiết lập gần nhất (null = chưa lưu lần nào)
    string? _settingsMsgArg;           // Tham số {0} của thông báo đó (vd lỗi lưu)
    bool _settingsMsgIsError;          // Thông báo đó có phải lỗi không (để tô màu lại khi đổi ngôn ngữ)

    /// <summary>核对完成状态变化（true = 已通过，false = 回到待核对）；MainWindow 据此同步导航圆点。</summary>
    public event Action<bool>? PassChanged;

    /// <summary>当前是否已核对通过。</summary>
    public bool Passed { get; private set; }

    public StressTestPage(IHostContext? host = null)
    {
        _host = host;
        InitializeComponent();

        // 线程数下拉：1 .. 逻辑核心数，默认拉满
        FillThreadsBox();
        Lang.Changed += RefreshLanguage;
        // Thời lượng nướng + tự dừng chỉnh ngay trên trang này (không còn trang Cài đặt):
        // điền giá trị đã lưu để kỹ thuật viên thấy và sửa.
        StressMinutesBox.Text = YanJi.PluginSdk.Services.AppSettings.StressMinutes.ToString();
        AutoStopCheck.IsChecked = YanJi.PluginSdk.Services.AppSettings.AutoStopStress;

        _tick.Tick += (_, _) => RefreshRunningState();
        StressTestService.StateChanged += OnServiceStateChanged;
        StressTestService.Logged += OnStressLogged;
        SensorMonitorService.Sampled += OnSensorSampled;
        SensorMonitorService.SensorsResolved += OnSensorsResolved;
        SensorMonitorService.GpuListReady += OnGpuListReady;
        SensorMonitorService.GpuChanging += OnGpuChanging;
        // 页面常驻机制下切走即 Unloaded：必须停烤机与传感器轮询（关窗也会触发，双保险）
        Unloaded += (_, _) =>
        {
            _tick.Stop();
            StressTestService.Stop();
            SensorMonitorService.Stop();
        };
        // 页面可见时才开传感器轮询（Loaded 在切回时会重发，Start 幂等）
        Loaded += (_, _) => SensorMonitorService.Start();
        SensorHintText.Text = Lang.Get("Stress.SensorInit");
        RefreshRunningState();
    }

    /// <summary>线程数下拉 + 提示：1 .. 逻辑核心数，默认拉满；换语言时重建以换文字，
    ///  giữ nguyên lựa chọn đang chọn.</summary>
    void FillThreadsBox()
    {
        int selected = ThreadsBox.SelectedIndex;
        var (phys, logical) = StressTestService.CoreCounts;
        ThreadsBox.Items.Clear();
        for (int i = 1; i <= logical; i++)
            ThreadsBox.Items.Add(new ComboBoxItem { Content = string.Format(Lang.Get("Stress.ThreadsCount"), i), Tag = i });
        ThreadsBox.SelectedIndex = selected >= 0 && selected < ThreadsBox.Items.Count ? selected : logical - 1;
        CoresHint.Text = string.Format(Lang.Get("Stress.CoresHint"), phys, logical);
    }

    /// <summary>换语言时重画全部代码设置的文字（XAML 的 DynamicResource 自行重画）。
    /// 在 UI 线程跑（Lang.Changed 由语言切换按钮在 UI 线程触发，与 Dashboard/Summary 相同）。</summary>
    void RefreshLanguage()
    {
        FillThreadsBox();
        if (_sensorMap == null) SensorHintText.Text = Lang.Get("Stress.SensorInit");
        else RefreshSensorHint();
        // 图表占位文案是调用时传入的已本地化字符串，换语言要重设一次
        if (_gpuTempDown) GpuTempChart.SetUnavailable(Lang.Get("Stress.NoSensor"));
        if (_gpuPowerDown) GpuPowerChart.SetUnavailable(Lang.Get("Stress.NoSensor"));
        // Nút cài driver đang báo lỗi thì vẽ lại chữ lỗi; còn lại XAML DynamicResource tự lo
        if (_failedInstallButton != null)
            _failedInstallButton.Content = Lang.Get("Stress.InstallFailedRetry");
        if (_settingsMsgKey != null)
        {
            StressSettingsStatusText.Text = _settingsMsgArg == null
                ? Lang.Get(_settingsMsgKey)
                : string.Format(Lang.Get(_settingsMsgKey), _settingsMsgArg);
            StressSettingsStatusText.SetResourceReference(TextBlock.ForegroundProperty,
                _settingsMsgIsError ? "ErrorBrush" : "TextSecondaryBrush");
        }
        RefreshRunningState();
        // Trạng thái kiểm tra (RefreshRunningState không chạm tới StatusText)
        if (Passed)
        {
            StatusText.Text = Lang.Get("Stress.Status.Passed");
            StatusText.Foreground = Brushes.ForestGreen;
            PassHintText.Text = "";
        }
        else
        {
            StatusText.Text = Lang.Get("Stress.Status.Waiting");
            StatusText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
        }
    }

    void SaveStressSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(StressMinutesBox.Text.Trim(), out int minutes) || minutes < 1 || minutes > 60)
        {
            _settingsMsgKey = "Stress.Settings.MinutesRange";
            _settingsMsgArg = null;
            _settingsMsgIsError = true;
            StressSettingsStatusText.Text = Lang.Get(_settingsMsgKey);
            StressSettingsStatusText.SetResourceReference(TextBlock.ForegroundProperty, "ErrorBrush");
            return;
        }
        bool autoStop = AutoStopCheck.IsChecked == true;
        if (YanJi.PluginSdk.Services.AppSettings.Save(minutes, autoStop, out string error))
        {
            _settingsMsgKey = "Stress.Settings.Saved";
            _settingsMsgArg = null;
            _settingsMsgIsError = false;
            StressSettingsStatusText.Text = Lang.Get(_settingsMsgKey);
            StressSettingsStatusText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
        }
        else
        {
            _settingsMsgKey = "Stress.Settings.SaveFailed";
            _settingsMsgArg = error;
            _settingsMsgIsError = true;
            StressSettingsStatusText.Text = string.Format(Lang.Get(_settingsMsgKey), error);
            StressSettingsStatusText.SetResourceReference(TextBlock.ForegroundProperty, "ErrorBrush");
        }
    }

    void StartButton_Click(object sender, RoutedEventArgs e)
    {
        _autoStopAttempted = false;
        _autoStopped = false;
        _startFailed = false;
        _maxCpuTemp = null;
        _host?.ReportSignals("stress", []);
        _host?.ReportVerdict("stress", new(YanJi.PluginSdk.ModuleVerdictLevel.ChuaTest, Lang.Get("Stress.Verdict.Restarting")));
        SetPassed(false);
        var mode = ParseTag<StressTestService.StressMode>(ModeBox);
        var fft = ParseTag<StressTestService.FftPreset>(FftBox);
        int threads = ParseTag<int>(ThreadsBox);
        int msaa = ParseTag<int>(MsaaBox);
        var (w, h) = ParseResolution();
        try
        {
            StressTestService.Start(mode,
                new StressTestService.Prime95Settings(threads, fft, P95ShowUiCheck.IsChecked == true),
                new StressTestService.FurMarkSettings(w, h, FullscreenCheck.IsChecked == true, msaa,
                    FurShowUiCheck.IsChecked == true));
        }
        catch (FileNotFoundException ex)
        {
            // Tool nướng máy thiếu: thông báo ngoại lệ của service vẫn giữ nguyên
            // văn bản chẩn đoán, trang này bọc lại bằng câu đã bản địa hóa + đường dẫn file.
            _startFailed = true;
            RunStateText.Text = string.Format(Lang.Get("Stress.StartFailedToolMissing"), ex.FileName);
        }
        catch (Exception ex)
        {
            _startFailed = true;
            RunStateText.Text = string.Format(Lang.Get("Stress.StartFailed"), ex.Message);
        }
        RefreshRunningState();
    }

    /// <summary>烤机事件日志（服务在后台线程触发，封送 UI 追加；上限 500 条截最旧，自动滚底）。</summary>
    void OnStressLogged(string line) =>
        Dispatcher.BeginInvoke(() =>
        {
            LogBox.AppendText(line + Environment.NewLine);
            if (LogBox.LineCount > 500)
            {
                int i = LogBox.Text.IndexOf('\n');
                if (i >= 0) LogBox.Text = LogBox.Text[(i + 1)..];
            }
            LogBox.ScrollToEnd();
        });

    void StopButton_Click(object sender, RoutedEventArgs e)
    {
        // Dừng có kiểm chứng: nếu tiến trình vẫn còn sống thì StopVerified đã ghi
        // cảnh báo vào LogBox và IsRunning vẫn true, giao diện không được báo dừng giả.
        // Xem StressTestService.StopVerified.
        StressTestService.StopVerified();
        RefreshRunningState();
    }

    void PassButton_Click(object sender, RoutedEventArgs e)
    {
        // 通过即停烤（随后自动跳转切页，Unloaded 也会再兜一次）
        // Nếu chưa dừng được thật thì KHÔNG cho qua: cho qua trong khi Prime95/FurMark
        // vẫn chạy sẽ khiến máy tiếp tục tải cao ở trang khác mà kỹ thuật viên tưởng đã xong.
        var survivors = StressTestService.StopVerified();
        if (survivors.Count > 0)
        {
            RunStateText.Text = string.Format(Lang.Get("Stress.Pass.NotStopped"), string.Join(", ", survivors));
            return;
        }
        // Lỗi do mắt người tick tay thì CHẶN cho qua (giống ổ bị chặn): báo verdict
        // Chặn + tín hiệu, Tổng kết ra câu lỗi nghiêm trọng, không phải "cho qua rồi
        // mà vẫn có lỗi". Throttling/quá nhiệt thì chỉ là cảnh báo, vẫn cho qua được.
        bool cpuFail = CpuFailCheck.IsChecked == true;
        bool gpuArtifact = GpuArtifactCheck.IsChecked == true;
        if (cpuFail || gpuArtifact)
        {
            var failSignals = new List<string>();
            if (cpuFail) failSignals.Add("cpuFail");
            if (gpuArtifact) failSignals.Add("gpuArtifact");
            _host?.ReportSignals("stress", failSignals);
            _host?.ReportVerdict("stress", new(ModuleVerdictLevel.Chan,
                cpuFail && gpuArtifact ? Lang.Get("Stress.Verdict.CpuFailGpuArtifact")
                : cpuFail ? Lang.Get("Stress.Verdict.CpuFail") : Lang.Get("Stress.Verdict.GpuArtifact")));
            RunStateText.Text = Lang.Get("Stress.Pass.BlockedNote");
            return;
        }
        // Quá nhiệt: ngưỡng 95°C cho CPU là mốc nháp từ checklist cũ (chưa phải tiêu
        // chuẩn chính thức của shop) — vượt thì cảnh báo, không chặn; chưa đọc được
        // nhiệt thì không kết luận quá nhiệt, chỉ kết luận "đã nướng xong không lỗi"
        // khi thực sự đã nướng (lastRun > 0 hoặc vừa dừng 1 lần nướng đang chạy).
        bool overheat = _maxCpuTemp is >= 95;
        bool runCompleted = _lastRun > TimeSpan.Zero || _runStart != null;
        var signals = new List<string>();
        if (overheat) signals.Add("cpuOverheat");
        else if (runCompleted) signals.Add("cpuGood");
        _host?.ReportSignals("stress", signals);
        _host?.ReportVerdict("stress", overheat
            ? new(ModuleVerdictLevel.CanhBao, string.Format(Lang.Get("Stress.Verdict.Overheat"), $"{_maxCpuTemp:0}"))
            : new(ModuleVerdictLevel.Dat, runCompleted ? Lang.Get("Stress.Verdict.DoneOk") : Lang.Get("Stress.Verdict.PassedNoRun")));
        SetPassed(true);
    }

    /// <summary>服务状态变化可能在线程池线程（ProcessExit 等），封送 UI 线程刷新。</summary>
    void OnServiceStateChanged() =>
        Dispatcher.BeginInvoke(() =>
        {
            if (StressTestService.IsRunning) _tick.Start();
            else _tick.Stop();
            RefreshRunningState();
        });

    // ═══ 数据监控（SensorMonitorService 事件均在后台线程，封送 UI） ═══

    bool _cpuTempDown, _cpuPowerDown, _gpuTempDown, _gpuPowerDown;
    SensorMonitorService.SensorMap? _sensorMap;

    void OnSensorSampled(SensorMonitorService.Reading r) =>
        Dispatcher.BeginInvoke(() =>
        {
            if (StressTestService.IsRunning)
            {
                if (r.CpuTemp is double ct) _maxCpuTemp = Math.Max(_maxCpuTemp ?? ct, ct);
            }
            UpdateChart(CpuTempChart, CpuTempOverlay, r.CpuTemp, ref _cpuTempDown);
            UpdateChart(CpuPowerChart, CpuPowerOverlay, r.CpuPower, ref _cpuPowerDown);
            UpdateChart(GpuTempChart, null, r.GpuTemp, ref _gpuTempDown);
            UpdateChart(GpuPowerChart, null, r.GpuPower, ref _gpuPowerDown);
        });

    /// <summary>单图更新：有值推点（收起叠层）；无值置占位——CPU 两路显示「文案 + 安装按钮」叠层，
    /// GPU 两路用图表自带占位文案（状态不变时不重复操作）。</summary>
    void UpdateChart(LineChartControl chart, UIElement? overlay, double? value, ref bool unavailable)
    {
        if (value is double v)
        {
            if (unavailable)
            {
                unavailable = false;
                if (overlay != null) overlay.Visibility = Visibility.Collapsed;
            }
            chart.PushSample(v);
        }
        else if (!unavailable)
        {
            unavailable = true;
            if (overlay != null)
            {
                chart.SetUnavailable(""); // 占位由叠层出（文案 + 按钮），图表只留网格
                overlay.Visibility = Visibility.Visible;
            }
            else
            {
                chart.SetUnavailable(Lang.Get("Stress.NoSensor"));
            }
        }
        RefreshSensorHint();
    }

    /// <summary>传感器口径四行排版：每路一行「指标 ← 硬件 / 传感器」，不可用行注明状态。</summary>
    void RefreshSensorHint()
    {
        var m = _sensorMap;
        if (m == null) return;
        SensorHintText.Text =
            string.Format(Lang.Get("Stress.Sensor.CpuTemp"), Line(m.CpuName, m.CpuTemp, _cpuTempDown, needsDriver: true)) + "\n" +
            string.Format(Lang.Get("Stress.Sensor.CpuPower"), Line(m.CpuName, m.CpuPower, _cpuPowerDown, needsDriver: true)) + "\n" +
            string.Format(Lang.Get("Stress.Sensor.GpuTemp"), Line(m.GpuName, m.GpuTemp, _gpuTempDown, needsDriver: false)) + "\n" +
            string.Format(Lang.Get("Stress.Sensor.GpuPower"), Line(m.GpuName, m.GpuPower, _gpuPowerDown, needsDriver: false));

        static string Line(string? hw, string? sensor, bool down, bool needsDriver)
        {
            if (sensor == null) return Lang.Get("Stress.Sensor.NotFound");
            string s = $"{hw} / {sensor}";
            if (down) s += Lang.Get(needsDriver ? "Stress.Sensor.BadReadingDriver" : "Stress.Sensor.UnavailableNow");
            return s;
        }
    }

    void OnSensorsResolved(SensorMonitorService.SensorMap map) =>
        Dispatcher.BeginInvoke(() =>
        {
            _sensorMap = map;
            RefreshSensorHint();
        });

    // ═══ GPU 选择（换绑立即清两图旧曲线；采样周期内生效） ═══

    bool _gpuBoxSyncing;

    void OnGpuListReady(IReadOnlyList<SensorMonitorService.GpuOption> options, int selected) =>
        Dispatcher.BeginInvoke(() =>
        {
            _gpuBoxSyncing = true;
            GpuBox.Items.Clear();
            foreach (var g in options)
                GpuBox.Items.Add(new ComboBoxItem { Content = g.Name, Tag = g.Index });
            GpuBox.SelectedIndex = selected;
            GpuBox.Visibility = options.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            _gpuBoxSyncing = false;
        });

    void GpuBox_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_gpuBoxSyncing) return;
        if ((GpuBox.SelectedItem as ComboBoxItem)?.Tag is int index)
            SensorMonitorService.SetGpu(index);
    }

    void OnGpuChanging() =>
        Dispatcher.BeginInvoke(() =>
        {
            // 立即清两图旧曲线与降级标志（新传感器下轮采样重评估）
            GpuTempChart.Clear();
            GpuPowerChart.Clear();
            _gpuTempDown = _gpuPowerDown = false;
        });

    /// <summary>「安装 PawnIO 驱动」：静默跑官方签名安装器（-install -silent），装完重初始化 LHM Computer
    /// （Stop+Start，无需重启程序），CPU 两路随即出真实读数；失败则提示。</summary>
    async void InstallPawnIo_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        btn.IsEnabled = false;
        _failedInstallButton = null;
        btn.Content = Lang.Get("Stress.Installing");
        bool ok = await Task.Run(SensorMonitorService.InstallDriver);
        if (ok)
        {
            SensorMonitorService.Restart(); // 重初始化：新 Computer 直接走驱动读数（无静态缓存依赖）
        }
        else
        {
            _failedInstallButton = btn;
            btn.Content = Lang.Get("Stress.InstallFailedRetry");
            btn.IsEnabled = true;
        }
    }

    void RefreshRunningState()
    {
        bool running = StressTestService.IsRunning;
        StartButton.IsEnabled = !running;
        StopButton.IsEnabled = running;

        if (running && StressTestService.StartedAt is DateTime t0)
        {
            _runStart = t0;
            var span = DateTime.Now - t0;
            int minutes = YanJi.PluginSdk.Services.AppSettings.StressMinutes;
            bool autoStop = YanJi.PluginSdk.Services.AppSettings.AutoStopStress;
            if (span >= TimeSpan.FromMinutes(minutes) && autoStop && !_autoStopAttempted)
            {
                // Tự dừng theo thiết lập ở trang này, dùng đúng đường dừng có kiểm chứng (không phải
                // đường dừng mới): nếu dừng không được, StopVerified đã ghi log và
                // IsRunning vẫn true, cờ _autoStopAttempted ngăn gọi lại mỗi 500ms.
                _autoStopAttempted = true;
                var survivors = StressTestService.StopVerified();
                if (survivors.Count == 0) _autoStopped = true;
                RefreshRunningState();
                return;
            }
            ElapsedText.Text = span >= TimeSpan.FromMinutes(minutes)
                ? string.Format(Lang.Get("Stress.Elapsed.Enough"), FormatSpan(span), minutes)
                : string.Format(Lang.Get("Stress.Elapsed.Running"), FormatSpan(span));
            RunStateText.Text = Lang.Get("Stress.RunState.Running");
        }
        else
        {
            // running→停止 的那一拍：结算本次烤机时长
            if (_runStart is DateTime rs)
            {
                _lastRun = DateTime.Now - rs;
                _runStart = null;
            }
            ElapsedText.Text = Lang.Get("Stress.NotStressed");
            if (_lastRun > TimeSpan.Zero)
                RunStateText.Text = _autoStopped
                    ? string.Format(Lang.Get("Stress.RunState.AutoStopped"), FormatSpan(_lastRun))
                    : string.Format(Lang.Get("Stress.RunState.LastRun"), FormatSpan(_lastRun));
            else if (!_startFailed)
                RunStateText.Text = Lang.Get("Stress.RunState.Never");
            // Không xóa _autoStopped ở đây: cờ này chỉ xóa khi bắt đầu lần nướng mới
            // (StartButton_Click), để ghi chú "tự dừng" không bị mất ngay sau lần Refresh đầu tiên.
        }

        // 通过按钮旁的提示：未在烤 / 已烤时长，由用户自行判断（不做强限制）
        if (!Passed)
            PassHintText.Text = running ? Lang.Get("Stress.PassHint.Running") :
                _lastRun > TimeSpan.Zero ? string.Format(Lang.Get("Stress.PassHint.LastRun"), FormatSpan(_lastRun)) : Lang.Get("Stress.PassHint.Never");
    }

    /// <summary>更新核对状态行（通过 = 绿色「✓ 已核对通过」）并通知导航圆点。</summary>
    void SetPassed(bool passed)
    {
        Passed = passed;
        if (passed)
        {
            StatusText.Text = Lang.Get("Stress.Status.Passed");
            StatusText.Foreground = Brushes.ForestGreen;
            PassHintText.Text = "";
        }
        else
        {
            StatusText.Text = Lang.Get("Stress.Status.Waiting");
            StatusText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
        }
        PassChanged?.Invoke(passed);
    }

    (int W, int H) ParseResolution()
    {
        string tag = (ResolutionBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "1920x1080";
        string[] parts = tag.Split('x');
        return (int.Parse(parts[0]), int.Parse(parts[1]));
    }

    static T ParseTag<T>(ComboBox box) where T : notnull
    {
        object tag = ((ComboBoxItem)box.SelectedItem).Tag;
        // Tag 在 XAML 里一律是字符串：枚举走 Enum.Parse，int 等走 Convert
        if (typeof(T).IsEnum) return (T)Enum.Parse(typeof(T), (string)tag);
        return (T)Convert.ChangeType(tag, typeof(T));
    }

    static string FormatSpan(TimeSpan s) =>
        s.TotalHours >= 1 ? $"{(int)s.TotalHours}:{s.Minutes:00}:{s.Seconds:00}" : $"{s.Minutes:00}:{s.Seconds:00}";
}
