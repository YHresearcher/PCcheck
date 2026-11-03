using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using YanJi.PluginSdk;

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
        if (!CdmService.HostAvailable)
            Log("Lỗi: không tìm thấy CdmHost (phải đi kèm plugin): " + CdmService.HostExe);
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
            Log("Lỗi: không tìm thấy CdmHost (phải đi kèm plugin): " + CdmService.HostExe);
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
            Log($"Bắt đầu đo tốc độ ổ (ổ {drive.TrimEnd('\\')}, SEQ1M Q8T1 + RND4K Q1T1, tổng {CdmService.TotalRuns} vòng)");
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
            DetailText.Text = $"Tuần tự {res.SeqRead:0.0} / {res.SeqWrite:0.0} MB/s, ngẫu nhiên 4K {res.Rnd4KRead:0.0} / {res.Rnd4KWrite:0.0} MB/s"
                + $" (đọc / ghi); kết quả đã lưu: {res.JsonPath}";
            SetIdle("Đo xong");
            Log($"Hoàn tất: tuần tự {res.SeqRead:0.0} / {res.SeqWrite:0.0}, ngẫu nhiên 4K {res.Rnd4KRead:0.0} / {res.Rnd4KWrite:0.0} MB/s (đọc/ghi)");
        }
        catch (OperationCanceledException)
        {
            SetIdle("Đã hủy");
            Log("Đã hủy");
        }
        catch (Exception ex)
        {
            SetIdle("Lỗi");
            Log("Lỗi: " + ex.Message.Split('\n')[0]);
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
            DriveInfoText.Text = "Đang đo...";
            DriveHealthText.Text = "Đang đo...";
            DriveSpeedText.Text = "Đang đo...";
            DriveConclusionText.Text = "Đang đo, đo xong sẽ hiện kết luận ổ ở đây.";
            DriveConclusionText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            return;
        }
        if (_result == null)
        {
            DriveInfoText.Text = "Chưa có thông tin ổ";
            DriveHealthText.Text = "Chưa có thông tin sức khỏe";
            DriveSpeedText.Text = "Chưa đo tốc độ";
            DriveConclusionText.Text = "Chưa đo — bấm Bắt đầu đo để đo rồi xem kết luận ổ ở đây.";
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
            DriveInfoText.Text = $"Ổ {letter}: {summary.Model} — {summary.Capacity}{letters}";
            DriveHealthText.Text = summary.HealthPercent is int hp
                ? $"Sức khỏe: {hp}% — {summary.VerdictText}"
                : $"Sức khỏe: Không rõ — {summary.VerdictText}";
        }
        else
        {
            DriveInfoText.Text = $"Ổ {letter}: chưa có thông tin model/dung lượng (chạy Đối chiếu cấu hình trước).";
            DriveHealthText.Text = "Sức khỏe: Chưa rõ (chưa có thông tin từ Đối chiếu cấu hình — chạy Đối chiếu cấu hình hoặc kiểm tra tay bằng CrystalDiskInfo trước khi kết luận).";
        }

        // Đánh giá tốc độ theo khoảng tham khảo chung (không phải chuẩn shop):
        // tuần tự quyết định copy file lớn, 4K quyết định cảm giác mở app hàng ngày.
        bool hddLike = res.SeqRead < 250 && res.Rnd4KRead < 5;
        string seqNote = res.SeqRead >= 1000 ? "nhanh, ngang NVMe"
            : res.SeqRead >= 250 ? "ở mức SSD SATA"
            : res.SeqRead >= 100 ? "thấp, ngang ổ cơ hoặc SSD chậm"
            : "rất thấp, bất thường";
        string fourKNote = hddLike ? "mức bình thường của ổ cơ (chậm hơn SSD nhiều)"
            : res.Rnd4KRead >= 40 ? "mở app/khởi động máy nhanh"
            : res.Rnd4KRead >= 20 ? "mở app/khởi động máy ở mức bình thường của SSD"
            : res.Rnd4KRead >= 10 ? "mở app/khởi động máy hơi chậm so với SSD thông thường"
            : "mở app/khởi động máy chậm bất thường cho SSD";
        string speedText = $"Tốc độ (tham khảo): đọc tuần tự {res.SeqRead:0.0} MB/s ({seqNote}), đọc 4K {res.Rnd4KRead:0.0} MB/s ({fourKNote}).";
        if (res.SeqWrite < res.SeqRead * 0.3)
            speedText += " Ghi tuần tự thấp hơn đọc nhiều bất thường, kiểm tra thêm (ổ gần đầy, hết cache, hoặc lỗi).";
        DriveSpeedText.Text = speedText;

        if (summary != null && summary.BlockReasons.Count > 0)
        {
            DriveConclusionText.Text = "Ổ KHÔNG TỐT — nên thay ổ rồi kiểm tra lại.";
            DriveConclusionText.SetResourceReference(TextBlock.ForegroundProperty, "ErrorBrush");
        }
        else if (summary != null && summary.WarningReasons.Count > 0)
        {
            DriveConclusionText.Text = "Ổ DÙNG ĐƯỢC CÓ ĐIỀU KIỆN — cân nhắc trừ giá / kiểm tra thêm.";
            DriveConclusionText.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
        }
        else if (summary != null && summary.HealthPercent != null)
        {
            if (hddLike)
            {
                DriveConclusionText.Text = "Ổ KHỎE, TỐC ĐỘ NGANG Ổ CƠ — dùng văn phòng cơ bản được nhưng chậm hơn SSD nhiều, cân nhắc nâng cấp SSD.";
                DriveConclusionText.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
            }
            else if (res.Rnd4KRead < 10)
            {
                DriveConclusionText.Text = "Ổ KHỎE NHƯNG TỐC ĐỘ THẤP BẤT THƯỜNG — kiểm tra thêm (cáp, khe cắm, nhiệt, ổ gần đầy) trước khi kết luận.";
                DriveConclusionText.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
            }
            else
            {
                DriveConclusionText.Text = "Ổ TỐT — dùng bình thường.";
                DriveConclusionText.Foreground = Brushes.ForestGreen;
            }
        }
        else
        {
            DriveConclusionText.Text = "CHƯA ĐỦ DỮ LIỆU SỨC KHỎE — tốc độ chỉ để tham khảo, chạy Đối chiếu cấu hình (hoặc kiểm tra tay bằng CrystalDiskInfo) trước khi kết luận ổ tốt hay không.";
            DriveConclusionText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
        }
    }

    // ═══ 状态行 / 通过 ═══

    void SetRunning()
    {
        _passed = false;
        StatusText.Text = "Đang đo…";
        StatusText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
    }

    void SetIdle(string text)
    {
        if (_passed) return;
        StatusText.Text = text;
        StatusText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
    }

    void PassButton_Click(object sender, RoutedEventArgs e)
    {
        if (_cts != null) _cts.Cancel(); // 跑分中点通过：先取消（进程树 Kill 走 ct.Register）
        _passed = true;
        StatusText.Text = "Đã đạt";
        StatusText.Foreground = Brushes.ForestGreen;
        PassChanged?.Invoke(true);
    }

    void UpdatePassHint()
    {
        PassHintText.Text = _cts != null ? "Đang đo"
            : _result == null ? "Chưa đo lần nào"
            : "Đã có kết quả + kết luận ổ ở trên, xem rồi bấm đạt";
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
