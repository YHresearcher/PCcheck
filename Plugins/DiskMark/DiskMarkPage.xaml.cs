using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using YanJi.PluginSdk;
using YanJi.PluginSdk.Services;

namespace YanJi.Plugin.DiskMark;

/// <summary>「硬盘跑分」页：CrystalDiskMark 源码集成（CdmHost 子进程）对目标盘跑
/// SEQ1M Q8T1 顺序读/写 + RND4K Q1T1 随机读/写四项（原版 BibaAutoTest 内存+硬盘合并页
/// 裁掉 AIDA64 内存部分后的 CDM 单页；AIDA64 为商业软件且该部分后续改用开源方案，故不含）。
/// 成绩：JSON 落盘 %APPDATA%\YanJi\disk_result\硬盘基准.json + 追加 硬盘成绩记录.csv。
/// Đo xong hiện kết luận ổ cứng ngay trên trang (thông tin ổ + sức khỏe từ Đối chiếu
/// cấu hình qua IHostContext + tốc độ vừa đo → chỉ đánh giá ổ này tốt hay không,
/// KHÔNG phải kết luận mua máy, KHÔNG chặn nút cho qua, KHÔNG đổi kết luận tổng).
/// Ngưỡng tốc độ chỉ là tham khảo chung (không phải chuẩn shop): sức khỏe lỗi nặng
/// thì không tốt bất kể tốc độ nhanh; sức khỏe chưa rõ thì không kết luận ổ tốt.
/// 通过约定同其它模块：确认完成 → PassChanged(true) → 导航圆点变绿 + 自动跳下一模块。</summary>
public partial class DiskMarkPage : UserControl, IModulePage
{
    public event Action<bool>? PassChanged;

    readonly CdmService _cdm = new();
    readonly IHostContext? _host; // Để đọc thông tin ổ + sức khỏe từ Đối chiếu cấu hình (có thể null khi test riêng)
    CdmService.CdmResult? _result;
    CancellationTokenSource? _cts;   // 非 null = 正在跑（防重入）
    bool _passed;
    string _statusKey = "Disk.StatusNotMeasured"; // key trạng thái hiện tại, để đổi ngôn ngữ vẽ lại

    public DiskMarkPage(IHostContext? host = null)
    {
        _host = host;
        InitializeComponent();
        // 运行日志行（后台线程触发，封送 UI 追加 LogBox）
        _cdm.Logged += line => Dispatcher.BeginInvoke(() => Log(line));
        // 目标盘下拉：全部就绪的固定盘，默认系统盘
        string systemRoot = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
        foreach (var d in DriveInfo.GetDrives())
        {
            if (d.DriveType != DriveType.Fixed || !d.IsReady) continue;
            DriveBox.Items.Add(new ComboBoxItem { Content = d.Name.TrimEnd('\\'), Tag = d.Name });
        }
        for (int i = 0; i < DriveBox.Items.Count; i++)
            if (((ComboBoxItem)DriveBox.Items[i]).Tag is string tag &&
                tag.Equals(systemRoot, StringComparison.OrdinalIgnoreCase))
            {
                DriveBox.SelectedIndex = i;
                break;
            }
        if (DriveBox.SelectedIndex < 0 && DriveBox.Items.Count > 0) DriveBox.SelectedIndex = 0;
        Lang.Changed += RefreshLabels;
        if (!CdmService.HostAvailable)
            Log(string.Format(Lang.Get("Disk.Error.HostMissing"), CdmService.HostExe));
        // 页面常驻机制下切走即从视觉树移除：兜底停跑分
        Unloaded += (_, _) => Cleanup();
        // Trang resident: quay lại trang sau khi Đối chiếu cấu hình chạy xong thì
        // kết luận ổ cập nhật theo thông tin mới (nếu đã có kết quả đo).
        Loaded += (_, _) => RefreshDriveConclusion();
        UpdatePassHint();
    }

    /// <summary>静态兜底：程序关闭时杀跑分进程树（插件入口 IPluginShutdown 调）。</summary>
    public static void CleanupStatic() => CdmService.KillCurrent();

    void Cleanup()
    {
        try { _cts?.Cancel(); } catch { }
        CdmService.KillCurrent();
    }

    // ═══ 跑分流程 ═══

    async void RunButton_Click(object sender, RoutedEventArgs e)
    {
        if (_cts != null) return; // 防重入
        if (!CdmService.HostAvailable)
        {
            Log(string.Format(Lang.Get("Disk.Error.HostMissing"), CdmService.HostExe));
            return;
        }
        string drive = (DriveBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "C:\\";

        _result = null;
        ScoreSeqRead.Text = "—";
        ScoreSeqWrite.Text = "—";
        Score4KRead.Text = "—";
        Score4KWrite.Text = "—";
        DetailText.Text = "";
        TestProgress.Maximum = CdmService.TotalRuns;
        TestProgress.Value = 0;

        _cts = new CancellationTokenSource();
        RunButton.IsEnabled = false;
        CancelButton.IsEnabled = true;
        SetRunning();
        RefreshDriveConclusion();
        try
        {
            Log(string.Format(Lang.Get("Disk.Log.Start"), drive.TrimEnd('\\'), CdmService.TotalRuns));
            var progress = new Progress<CdmService.RunProgress>(p =>
                Dispatcher.BeginInvoke(() =>
                {
                    PhaseText.Text = p.Phase;
                    TestProgress.Value = p.RunsDone;
                }));
            var res = await _cdm.RunAsync(drive, progress, _cts.Token);
            TestProgress.Value = CdmService.TotalRuns;
            _result = res;
            ScoreSeqRead.Text = res.SeqRead.ToString("0.0");
            ScoreSeqWrite.Text = res.SeqWrite.ToString("0.0");
            Score4KRead.Text = res.Rnd4KRead.ToString("0.0");
            Score4KWrite.Text = res.Rnd4KWrite.ToString("0.0");
            DetailText.Text = string.Format(Lang.Get("Disk.Detail"),
                res.SeqRead.ToString("0.0"), res.SeqWrite.ToString("0.0"),
                res.Rnd4KRead.ToString("0.0"), res.Rnd4KWrite.ToString("0.0"), res.JsonPath);
            SetIdle("Disk.StatusDone");
            Log(string.Format(Lang.Get("Disk.Log.Done"),
                res.SeqRead.ToString("0.0"), res.SeqWrite.ToString("0.0"),
                res.Rnd4KRead.ToString("0.0"), res.Rnd4KWrite.ToString("0.0")));
        }
        catch (OperationCanceledException)
        {
            SetIdle("Disk.StatusCancelled");
            Log(Lang.Get("Disk.StatusCancelled"));
        }
        catch (Exception ex)
        {
            SetIdle("Disk.StatusError");
            Log(string.Format(Lang.Get("Disk.Log.Error"), ex.Message.Split('\n')[0]));
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            RunButton.IsEnabled = true;
            CancelButton.IsEnabled = false;
            PhaseText.Text = "";
            UpdatePassHint();
            RefreshDriveConclusion();
        }
    }

    void CancelButton_Click(object sender, RoutedEventArgs e) => _cts?.Cancel();

    void OpenResultDir_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(CdmService.ResultDir);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{CdmService.ResultDir}\"",
            UseShellExecute = true,
        });
    }

    // ═══ Kết luận ổ cứng (chỉ đánh giá ổ này, không phải kết luận mua máy) ═══

    /// <summary>Tìm thông tin ổ của ký tự ổ đang đo trong danh sách Đối chiếu cấu hình
    /// đã báo cáo: khớp đúng ký tự ổ trước; nếu chỉ có đúng 1 ổ mà không gắn được
    /// ký tự (DeviceNumber không đọc được) thì lấy ổ đó (máy 1 ổ); nhiều ổ mà không
    /// khớp ký tự thì trả null (không đoán) để sức khỏe hiện Chưa rõ.</summary>
    DriveSummary? FindDriveSummary(string drive)
    {
        var summaries = _host?.DriveSummaries;
        if (summaries == null || summaries.Count == 0) return null;
        string letter = drive.TrimEnd('\\');
        foreach (var s in summaries)
            foreach (var l in s.Letters)
                if (l.Equals(letter, StringComparison.OrdinalIgnoreCase))
                    return s;
        return summaries.Count == 1 ? summaries[0] : null;
    }

    /// <summary>Kết luận ổ cứng sau khi đo: thông tin ổ + sức khỏe (từ Đối chiếu cấu hình)
    /// + tốc độ vừa đo → 1 kết luận chỉ cho ổ này. Sức khỏe ưu tiên hơn tốc độ:
    /// chặn thì không tốt bất kể tốc độ; chưa rõ sức khỏe thì không kết luận tốt.</summary>
    void RefreshDriveConclusion()
    {
        if (_cts != null)
        {
            DriveInfoText.Text = Lang.Get("Disk.Conclusion.RunningLine");
            DriveHealthText.Text = Lang.Get("Disk.Conclusion.RunningLine");
            DriveSpeedText.Text = Lang.Get("Disk.Conclusion.RunningLine");
            DriveConclusionText.Text = Lang.Get("Disk.Conclusion.Running");
            DriveConclusionText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            return;
        }
        if (_result == null)
        {
            DriveInfoText.Text = Lang.Get("Disk.Conclusion.NoInfo");
            DriveHealthText.Text = Lang.Get("Disk.Conclusion.NoHealth");
            DriveSpeedText.Text = Lang.Get("Disk.StatusNotMeasured");
            DriveConclusionText.Text = Lang.Get("Disk.Conclusion.NotMeasured");
            DriveConclusionText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            return;
        }
        var res = _result;
        string drive = (DriveBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "C:\\";
        string letter = drive.TrimEnd('\\');
        var summary = FindDriveSummary(drive);

        if (summary != null)
        {
            string letters = summary.Letters.Count > 0 ? $" ({string.Join(" ", summary.Letters)})" : "";
            DriveInfoText.Text = string.Format(Lang.Get("Disk.Conclusion.DriveInfo"), letter, summary.Model, summary.Capacity, letters);
            // Kết luận ổ do Đối chiếu cấu hình sinh bằng tiếng Việt (canonical) —
            // dịch tại đây qua bảng dùng chung với trang đó (PluginSdk).
            string verdict = DriveVerdictText.TranslateVerdict(summary.VerdictText);
            DriveHealthText.Text = summary.HealthPercent is int hp
                ? string.Format(Lang.Get("Disk.Conclusion.HealthKnown"), hp, verdict)
                : string.Format(Lang.Get("Disk.Conclusion.HealthUnknown"), verdict);
        }
        else
        {
            DriveInfoText.Text = string.Format(Lang.Get("Disk.Conclusion.DriveInfoMissing"), letter);
            DriveHealthText.Text = Lang.Get("Disk.Conclusion.HealthMissing");
        }

        // Đánh giá tốc độ theo khoảng tham khảo chung (không phải chuẩn shop):
        // tuần tự quyết định copy file lớn, 4K quyết định cảm giác mở app hàng ngày.
        bool hddLike = res.SeqRead < 250 && res.Rnd4KRead < 5;
        string seqNote = res.SeqRead >= 1000 ? Lang.Get("Disk.Speed.SeqNvme")
            : res.SeqRead >= 250 ? Lang.Get("Disk.Speed.SeqSata")
            : res.SeqRead >= 100 ? Lang.Get("Disk.Speed.SeqSlow")
            : Lang.Get("Disk.Speed.SeqVeryLow");
        string fourKNote = hddLike ? Lang.Get("Disk.Speed.FourKHdd")
            : res.Rnd4KRead >= 40 ? Lang.Get("Disk.Speed.FourKFast")
            : res.Rnd4KRead >= 20 ? Lang.Get("Disk.Speed.FourKNormal")
            : res.Rnd4KRead >= 10 ? Lang.Get("Disk.Speed.FourKSlow")
            : Lang.Get("Disk.Speed.FourKVerySlow");
        string speedText = string.Format(Lang.Get("Disk.Speed.Line"),
            res.SeqRead.ToString("0.0"), seqNote, res.Rnd4KRead.ToString("0.0"), fourKNote);
        if (res.SeqWrite < res.SeqRead * 0.3)
            speedText += Lang.Get("Disk.Speed.WriteLow");
        DriveSpeedText.Text = speedText;

        if (summary != null && summary.BlockReasons.Count > 0)
        {
            DriveConclusionText.Text = Lang.Get("Disk.Conclusion.Bad");
            DriveConclusionText.SetResourceReference(TextBlock.ForegroundProperty, "ErrorBrush");
        }
        else if (summary != null && summary.WarningReasons.Count > 0)
        {
            DriveConclusionText.Text = Lang.Get("Disk.Conclusion.Conditional");
            DriveConclusionText.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
        }
        else if (summary != null && summary.HealthPercent != null)
        {
            if (hddLike)
            {
                DriveConclusionText.Text = Lang.Get("Disk.Conclusion.HddLike");
                DriveConclusionText.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
            }
            else if (res.Rnd4KRead < 10)
            {
                DriveConclusionText.Text = Lang.Get("Disk.Conclusion.SlowAbnormal");
                DriveConclusionText.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
            }
            else
            {
                DriveConclusionText.Text = Lang.Get("Disk.Conclusion.Good");
                DriveConclusionText.Foreground = Brushes.ForestGreen;
            }
        }
        else
        {
            DriveConclusionText.Text = Lang.Get("Disk.Conclusion.NoHealthData");
            DriveConclusionText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
        }
    }

    // ═══ 状态行 / 通过 ═══

    void SetRunning()
    {
        _passed = false;
        _statusKey = "Disk.StatusRunning";
        StatusText.Text = Lang.Get(_statusKey);
        StatusText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
    }

    void SetIdle(string statusKey)
    {
        if (_passed) return;
        _statusKey = statusKey;
        StatusText.Text = Lang.Get(_statusKey);
        StatusText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
    }

    /// <summary>Vẽ lại các nhãn do code-behind giữ khi đổi ngôn ngữ
    /// (theo mẫu Dashboard/Summary: Lang.Changed → re-render).</summary>
    void RefreshLabels()
    {
        StatusText.Text = Lang.Get(_statusKey);
        if (_passed) StatusText.Foreground = Brushes.ForestGreen;
        else StatusText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
        if (_result != null)
            DetailText.Text = string.Format(Lang.Get("Disk.Detail"),
                _result.SeqRead.ToString("0.0"), _result.SeqWrite.ToString("0.0"),
                _result.Rnd4KRead.ToString("0.0"), _result.Rnd4KWrite.ToString("0.0"), _result.JsonPath);
        UpdatePassHint();
        RefreshDriveConclusion();
    }

    void PassButton_Click(object sender, RoutedEventArgs e)
    {
        if (_cts != null) _cts.Cancel(); // 跑分中点通过：先取消（进程树 Kill 走 ct.Register）
        _passed = true;
        _statusKey = "Disk.StatusPassed";
        StatusText.Text = Lang.Get(_statusKey);
        StatusText.Foreground = Brushes.ForestGreen;
        PassChanged?.Invoke(true);
    }

    void UpdatePassHint()
    {
        PassHintText.Text = _cts != null ? Lang.Get("Disk.PassHint.Running")
            : _result == null ? Lang.Get("Disk.PassHint.Never")
            : Lang.Get("Disk.PassHint.Ready");
    }

    // ═══ 日志（LogBox 惯例：滚底 + 500 条截旧） ═══

    void Log(string message)
    {
        LogBox.AppendText($"{DateTime.Now:HH:mm:ss}  {message}\n");
        if (LogBox.LineCount > 500)
        {
            int cut = LogBox.Text.IndexOf('\n');
            if (cut > 0) LogBox.Text = LogBox.Text[(cut + 1)..];
        }
        LogBox.ScrollToEnd();
    }
}
