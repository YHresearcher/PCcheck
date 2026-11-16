using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using YanJi.PluginSdk;

namespace YanJi.Plugin.Visual;

/// <summary>Trang Ngoại quan: kiểm tra bằng mắt 8 hạng mục quan trọng nhất (vỏ,
/// bản lề, màn hình, phím, cổng, pin phồng, dấu mở máy, trầy nhẹ). Tick thì vẫn cho qua được
/// nhưng verdict là Cảnh báo + tín hiệu tương ứng, Tổng kết ra câu chuyên biệt
/// nếu có (bản lề, trầy nhẹ) hoặc câu cảnh báo chung nếu không (vỏ nứt, phím
/// mất, cổng móp... — các lỗi này chưa có câu riêng trong Phrasings.json nên
/// không bịa câu, dùng câu cảnh báo chung là đủ). Ngoại lệ duy nhất: pin phồng
/// thì CHẶN cho qua (giống ổ bị chặn và sạc Kém) vì nguy hiểm, thay pin rồi mới
/// kiểm tra lại. Đặt lại thì xóa tín hiệu + verdict về Chưa test, để tín hiệu cũ
/// không dính sang lần kiểm tra sau.</summary>
public partial class VisualPage : UserControl, IModulePage
{
    readonly IHostContext? _host;
    bool _blocked; // pin phồng chặn cho qua — trạng thái này không nằm trong Passed

    public event Action<bool>? PassChanged;

    public bool Passed { get; private set; }

    public VisualPage(IHostContext? host = null)
    {
        _host = host;
        InitializeComponent();
        Lang.Changed += RefreshStatusText;
    }

    /// <summary>Danh sách hạng mục: ô tick, mã tín hiệu, ghi chú hiển thị trong
    /// verdict. Chỉ bản lề lỏng và trầy nhẹ có câu riêng trong Phrasings.json;
    /// các mã còn lại Tổng kết sẽ về câu cảnh báo chung qua verdict Cảnh báo.</summary>
    (CheckBox Box, string Signal, string Note)[] Items() =>
    [
        (CaseCrackedCheck, "caseCracked", Lang.Get("Visual.Item.CaseCracked")),
        (HingeCheck, "hingeLoose", Lang.Get("Visual.Note.Hinge")),
        (ScreenScratchesCheck, "screenScratches", Lang.Get("Visual.Item.ScreenScratches")),
        (KeyMissingCheck, "keyMissing", Lang.Get("Visual.Item.KeyMissing")),
        (TrackpadFailCheck, "trackpadFail", Lang.Get("Visual.Note.TrackpadFail")),
        (PortDamagedCheck, "portDamaged", Lang.Get("Visual.Item.PortDamaged")),
        (BatterySwollenCheck, "batterySwollen", Lang.Get("Visual.Note.BatterySwollen")),
        (OpenedSignCheck, "openedSign", Lang.Get("Visual.Note.OpenedSign")),
        (ScratchesCheck, "scratchesLight", Lang.Get("Visual.Note.Scratches")),
    ];

    void PassButton_Click(object sender, RoutedEventArgs e)
    {
        var ticked = Items().Where(i => i.Box.IsChecked == true).ToList();
        var signals = ticked.Select(i => i.Signal).ToList();
        if (TrackpadVerifiedCheck.IsChecked == true || TrackpadFailCheck.IsChecked == true)
            signals.Add("trackpadTested");
        _host?.ReportSignals("visual", signals);
        // Pin phồng thì chặn cho qua (nguy hiểm, không bán nguyên trạng), giống
        // ổ bị chặn và sạc Kém: báo verdict Chặn + tín hiệu, không SetPassed.
        if (BatterySwollenCheck.IsChecked == true)
        {
            _host?.ReportVerdict("visual", new(ModuleVerdictLevel.Chan,
                Lang.Get("Visual.Blocked.Battery") +
                (ticked.Count > 1 ? "; " + string.Join("; ", ticked.Where(i => i.Signal != "batterySwollen").Select(i => i.Note)) : "")));
            _blocked = true;
            RefreshStatusText();
            return;
        }
        _host?.ReportVerdict("visual", ticked.Count > 0
            ? new(ModuleVerdictLevel.CanhBao, string.Join("; ", ticked.Select(i => i.Note)))
            : new(ModuleVerdictLevel.Dat, Lang.Get("Visual.Verdict.Pass")));
        SetPassed(true);
        PassButton.IsEnabled = false;
    }

    void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in Items()) item.Box.IsChecked = false;
        TrackpadVerifiedCheck.IsChecked = false;
        _host?.ReportSignals("visual", []);
        _host?.ReportVerdict("visual", new(ModuleVerdictLevel.ChuaTest, Lang.Get("Visual.Status.NotTested")));
        SetPassed(false);
        PassButton.IsEnabled = true;
    }

    void SetPassed(bool passed)
    {
        Passed = passed;
        _blocked = false;
        RefreshStatusText();
        PassChanged?.Invoke(passed);
    }

    /// <summary>Vẽ lại dòng trạng thái theo ngôn ngữ hiện tại (chỉ dòng này
    /// là chữ đặt bằng code, còn lại XAML tự đổi qua DynamicResource).
    /// Đổi ngôn ngữ không phát lại PassChanged — trạng thái cho qua không đổi,
    /// chỉ chữ hiển thị đổi theo.</summary>
    void RefreshStatusText()
    {
        if (_blocked)
        {
            StatusText.Text = Lang.Get("Visual.Blocked.Battery");
            StatusText.SetResourceReference(TextBlock.ForegroundProperty, "ErrorBrush");
        }
        else if (Passed)
        {
            if (Items().Any(i => i.Box.IsChecked == true))
            {
                StatusText.Text = Lang.Get("Visual.Status.PassedWithFaults");
                StatusText.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
            }
            else
            {
                StatusText.Text = Lang.Get("Visual.Status.PassedClean");
                StatusText.Foreground = Brushes.ForestGreen;
            }
        }
        else
        {
            StatusText.Text = Lang.Get("Visual.Status.NotTested");
            StatusText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
        }
    }
}
