using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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

    /// <summary>核对完成状态变化（true = 已通过，false = 重新检测回到待核对）；MainWindow 据此同步导航圆点。</summary>
    public event Action<bool>? PassChanged;

    /// <summary>当前是否已核对通过。</summary>
    public bool Passed { get; private set; }

    public ConfigCheckPage()
    {
        InitializeComponent();
        // 进入页面即自动检测一次（手动再次采集走「重新检测」按钮）
        Loaded += (_, _) =>
        {
            if (_autoStarted) return;
            _autoStarted = true;
            StartDetect();
        };
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
        ResultsHost.ItemsSource = null;
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
            ResultsHost.ItemsSource = report.Groups;
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
                    PhaseText.Text += " Lưu ý: có ổ cảnh báo (<90%) — tối đa bán có điều kiện.";
                if (report.HasDriveUnknown)
                    PhaseText.Text += " Lưu ý: có ổ chưa rõ sức khỏe — kiểm tra tay bằng CrystalDiskInfo trước khi bán.";
            }
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
        // Ổ bị chặn (sức khỏe <70% hoặc lỗi 0E >0) thì không cho qua — thay ổ rồi
        // kiểm tra lại, giống quy ước chặn của module Cổng kết nối & Sạc với sạc Kém.
        // Ổ cảnh báo / chưa rõ thì vẫn cho qua, nhưng PhaseText ghi rõ điều kiện.
        if (_lastReport?.DriveBlockReasons.Count > 0)
        {
            StatusText.Text = "Ổ bị chặn — thay ổ rồi kiểm tra lại, không cho qua";
            StatusText.SetResourceReference(TextBlock.ForegroundProperty, "ErrorBrush");
            PhaseText.Text = "Ổ bị chặn: " + string.Join("; ", _lastReport.DriveBlockReasons) + ".";
            PhaseText.SetResourceReference(TextBlock.ForegroundProperty, "ErrorBrush");
            return;
        }
        SetPassed(true);
        PassButton.IsEnabled = false;
        PhaseText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
        PhaseText.Text = "Đã xác nhận cho qua; nếu thay linh kiện hoặc cần kiểm lại, bấm \"Kiểm tra lại\"";
        if (_lastReport?.HasDriveWarning == true)
            PhaseText.Text += " Lưu ý: có ổ cảnh báo (<90%) — tối đa bán có điều kiện.";
        if (_lastReport?.HasDriveUnknown == true)
            PhaseText.Text += " Lưu ý: có ổ chưa rõ sức khỏe — kiểm tra tay bằng CrystalDiskInfo trước khi bán.";
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
