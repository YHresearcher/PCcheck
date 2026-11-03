using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using YanJi.PluginSdk;

namespace YanJi.Plugin.AvConference;

/// <summary>「影音会议」页面：摄像头（WinRT 预览）/ 麦克风（NAudio 电平+录制回放）/ 扬声器（NAudio 播放试音曲）。
/// 进入页面初始化设备并开始预览/监听；离开页面（Unloaded，含关程序）释放全部设备——摄像头指示灯随之熄灭。
/// 通过约定同其它模块：确认无故障 → PassChanged(true) → 导航圆点变绿 + 自动跳下一模块。</summary>
public partial class AvConferencePage : UserControl, YanJi.PluginSdk.IModulePage
{
    const string MusicFileName = "不死のバイオレット - 幻月遠征隊.mp3";
    const int RecordLimitSec = 15;

    readonly CameraService _camera = new();
    readonly AudioService _audio = new();
    readonly IHostContext? _host;
    readonly string _recordFile = Path.Combine(Path.GetTempPath(), "yanji_mic_test.wav");
    readonly DispatcherTimer _levelTimer;
    readonly DispatcherTimer _recordLimitTimer;

    List<(string Id, string Name)> _camDevices = [], _micDevices = [], _spkDevices = [];
    WriteableBitmap? _wb;
    bool _initializing;
    bool _recording;

    /// <summary>核对完成状态变化（true = 已通过，false = 回到待测试）；MainWindow 据此同步导航圆点并自动跳转。</summary>
    public event Action<bool>? PassChanged;

    /// <summary>当前是否已确认通过。</summary>
    public bool Passed { get; private set; }

    public AvConferencePage(IHostContext? host = null)
    {
        _host = host;
        InitializeComponent();
        _camera.FrameReady += OnCameraFrame;
        _levelTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _levelTimer.Tick += (_, _) => MicLevel.Value = _audio.Peak * 100;
        _recordLimitTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(RecordLimitSec) };
        _recordLimitTimer.Tick += (_, _) => StopRecord();
        Loaded += (_, _) => _ = InitAsync();
        Unloaded += (_, _) => Cleanup();
    }

    // ═══ 初始化 / 资源释放 ═══

    /// <summary>枚举三类设备填充下拉框，并开启摄像头预览与麦克风监听。</summary>
    async Task InitAsync()
    {
        if (_initializing) return;
        _initializing = true;
        try
        {
            // 音频设备（同步枚举）
            _micDevices = AudioService.GetInputDevices();
            _spkDevices = AudioService.GetOutputDevices();
            FillBox(MicBox, _micDevices);
            FillBox(SpkBox, _spkDevices);
            if (_spkDevices.Count == 0)
                MusicStatus.Text = "Không phát hiện thiết bị phát âm thanh";
            else if (!File.Exists(MusicPath()))
                MusicStatus.Text = "Không tìm thấy file nhạc test (file này đi kèm với chương trình)";

            // 摄像头（异步枚举）
            try { _camDevices = await CameraService.GetDevicesAsync(); }
            catch { _camDevices = []; }
            FillBox(CameraBox, _camDevices);

            if (CurrentMicId != null) StartInput(CurrentMicId);
            _levelTimer.Start();
            if (CurrentCamId != null) await StartPreviewAsync(CurrentCamId);
            else CameraError.Text = "Không phát hiện camera";
        }
        finally { _initializing = false; }
    }

    static void FillBox(ComboBox box, List<(string Id, string Name)> devices)
    {
        box.Items.Clear();
        foreach (var d in devices) box.Items.Add(d.Name);
        if (devices.Count > 0) box.SelectedIndex = 0;
        box.IsEnabled = devices.Count > 0;
    }

    string? CurrentCamId => CameraBox.SelectedIndex >= 0 && CameraBox.SelectedIndex < _camDevices.Count
        ? _camDevices[CameraBox.SelectedIndex].Id : null;
    string? CurrentMicId => MicBox.SelectedIndex >= 0 && MicBox.SelectedIndex < _micDevices.Count
        ? _micDevices[MicBox.SelectedIndex].Id : null;
    string? CurrentSpkId => SpkBox.SelectedIndex >= 0 && SpkBox.SelectedIndex < _spkDevices.Count
        ? _spkDevices[SpkBox.SelectedIndex].Id : null;

    static string MusicPath() => Path.Combine(
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!, MusicFileName);

    /// <summary>离开页面/关闭程序：停预览（灭灯）、停捕获与回放、清录音临时文件。</summary>
    void Cleanup()
    {
        _levelTimer.Stop();
        _recordLimitTimer.Stop();
        _recording = false;
        RecordButton.Content = "Ghi âm";
        PlayMusicButton.Content = "Phát nhạc test";
        PlayRecButton.Content = "Phát lại";
        _audio.Dispose();
        _ = _camera.StopAsync();
        _wb = null;
        PreviewImage.Source = null;
        try { if (File.Exists(_recordFile)) File.Delete(_recordFile); }
        catch { /* 临时文件删不掉不致命 */ }
    }

    // ═══ 摄像头 ═══

    void CameraBox_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_initializing) _ = StartPreviewAsync(CurrentCamId);
    }

    async Task StartPreviewAsync(string? deviceId)
    {
        _wb = null;
        PreviewImage.Source = null;
        if (deviceId == null)
        {
            CameraError.Text = "Không phát hiện camera";
            CameraError.Visibility = Visibility.Visible;
            return;
        }
        CameraError.Visibility = Visibility.Collapsed;
        try { await _camera.StartAsync(deviceId); }
        catch (Exception ex)
        {
            CameraError.Text = "Camera không dùng được: " + ex.Message + "\n(Có thể đang bị chương trình khác chiếm hoặc chưa được cấp quyền)";
            CameraError.Visibility = Visibility.Visible;
        }
    }

    /// <summary>摄像头帧回调（后台线程）：封送到 UI 线程写 WriteableBitmap。</summary>
    void OnCameraFrame(int w, int h, byte[] px)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (_wb == null || _wb.PixelWidth != w || _wb.PixelHeight != h)
            {
                _wb = new WriteableBitmap(w, h, 96, 96, PixelFormats.Pbgra32, null);
                PreviewImage.Source = _wb;
                CameraError.Visibility = Visibility.Collapsed;
            }
            _wb.WritePixels(new Int32Rect(0, 0, w, h), px, w * 4, 0);
        });
    }

    // ═══ 麦克风 ═══

    void MicBox_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_initializing && CurrentMicId != null) StartInput(CurrentMicId);
    }

    void StartInput(string deviceId)
    {
        try
        {
            _audio.StartInput(deviceId);
            MicStatus.Text = "";
        }
        catch (Exception ex)
        {
            MicStatus.Text = "Mic không dùng được: " + ex.Message;
        }
    }

    void RecordButton_Click(object sender, RoutedEventArgs e)
    {
        if (_recording) { StopRecord(); return; }
        try
        {
            _audio.StartRecord(_recordFile);
            _recording = true;
            RecordButton.Content = "Dừng ghi âm";
            PlayRecButton.IsEnabled = false;
            MicStatus.Text = "Đang ghi âm…";
            _recordLimitTimer.Start();
        }
        catch (Exception ex)
        {
            MicStatus.Text = "Không ghi âm được: " + ex.Message;
        }
    }

    void StopRecord()
    {
        if (!_recording) return;
        _recordLimitTimer.Stop();
        _audio.StopRecord();
        _recording = false;
        RecordButton.Content = "Ghi âm";
        PlayRecButton.IsEnabled = true;
        MicStatus.Text = "Đã ghi xong, bấm “Phát lại” để nghe thử";
    }

    void PlayRecButton_Click(object sender, RoutedEventArgs e)
    {
        if (_audio.IsPlaying) { _audio.StopPlay(); return; }
        if (!File.Exists(_recordFile)) { MicStatus.Text = "Chưa có bản ghi âm"; return; }
        PlayRecButton.Content = "Dừng";
        try { _audio.Play(_recordFile, CurrentSpkId, OnPlaybackStopped); }
        catch (Exception ex)
        {
            PlayRecButton.Content = "Phát lại";
            MicStatus.Text = "Phát lại thất bại: " + ex.Message;
        }
    }

    // ═══ 扬声器 ═══

    void PlayMusicButton_Click(object sender, RoutedEventArgs e)
    {
        if (_audio.IsPlaying) { _audio.StopPlay(); return; }
        if (!File.Exists(MusicPath()))
        {
            MusicStatus.Text = "Không tìm thấy file nhạc test (file này đi kèm với chương trình)";
            return;
        }
        PlayMusicButton.Content = "Dừng";
        MusicStatus.Text = "Đang phát nhạc test…";
        try { _audio.Play(MusicPath(), CurrentSpkId, OnPlaybackStopped); }
        catch (Exception ex)
        {
            PlayMusicButton.Content = "Phát nhạc test";
            MusicStatus.Text = "Phát nhạc test thất bại: " + ex.Message;
        }
    }

    /// <summary>回放/播放结束（自然结束或手动停止）复位按钮文案（AudioService 回调在线程池线程）。</summary>
    void OnPlaybackStopped()
    {
        Dispatcher.BeginInvoke(() =>
        {
            PlayRecButton.Content = "Phát lại";
            PlayMusicButton.Content = "Phát nhạc test";
            if (MusicStatus.Text == "Đang phát nhạc test…") MusicStatus.Text = "";
        });
    }

    // ═══ 通过态 ═══

    void PassButton_Click(object sender, RoutedEventArgs e)
    {
        var signals = new List<string>();
        if (SpeakerCheck.IsChecked == true) signals.Add("speakerDistorted");
        if (WebcamCheck.IsChecked == true) signals.Add("webcamFail");
        _host?.ReportSignals("av", signals);
        _host?.ReportVerdict("av", signals.Count > 0
            ? new(ModuleVerdictLevel.CanhBao, string.Join("; ", signals.Select(s => s == "speakerDistorted" ? "Loa rè — kiểm tra chi phí sửa" : "Camera không hoạt động — xử lý trước khi mua")))
            : new(ModuleVerdictLevel.Dat, "Đã test camera / mic / loa, không lỗi"));
        SetPassed(true);
        PassButton.IsEnabled = false;
    }

    /// <summary>更新测试状态行（通过 = 绿色「✓ 已确认通过」）并通知导航圆点/自动跳转。</summary>
    void SetPassed(bool passed)
    {
        Passed = passed;
        if (passed)
        {
            if (SpeakerCheck.IsChecked == true || WebcamCheck.IsChecked == true)
            {
                StatusText.Text = "✓ Đã ghi nhận lỗi, cho qua (có lỗi)";
                StatusText.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
            }
            else
            {
                StatusText.Text = "✓ Đã xác nhận không lỗi, cho qua";
                StatusText.Foreground = Brushes.ForestGreen;
            }
        }
        else
        {
            StatusText.Text = "Chưa test";
            StatusText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
        }
        PassChanged?.Invoke(passed);
    }
}
