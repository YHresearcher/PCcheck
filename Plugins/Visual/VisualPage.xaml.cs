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

    public event Action<bool>? PassChanged;

    public bool Passed { get; private set; }

    public VisualPage(IHostContext? host = null)
    {
        _host = host;
        InitializeComponent();
    }

    /// <summary>Danh sách hạng mục: ô tick, mã tín hiệu, ghi chú hiển thị trong
    /// verdict. Chỉ bản lề lỏng và trầy nhẹ có câu riêng trong Phrasings.json;
    /// các mã còn lại Tổng kết sẽ về câu cảnh báo chung qua verdict Cảnh báo.</summary>
    (CheckBox Box, string Signal, string Note)[] Items() =>
    [
        (CaseCrackedCheck, "caseCracked", "Vỏ nứt / móp / vỡ góc"),
        (HingeCheck, "hingeLoose", "Bản lề lỏng — kiểm tra kỹ vì ảnh hưởng màn hình và vỏ"),
        (ScreenScratchesCheck, "screenScratches", "Màn hình trầy / viền nứt"),
        (KeyMissingCheck, "keyMissing", "Phím bong / mất phím"),
        (TrackpadFailCheck, "trackpadFail", "Trackpad không hoạt động hoặc thao tác lỗi"),
        (PortDamagedCheck, "portDamaged", "Cổng móp / gãy chân"),
        (BatterySwollenCheck, "batterySwollen", "Pin phồng"),
        (OpenedSignCheck, "openedSign", "Thiếu ốc / tem rách / dấu mở máy — kiểm tra kỹ lịch sử sửa chữa"),
        (ScratchesCheck, "scratchesLight", "Trầy xước nhẹ — thương lượng giá"),
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
                "Pin phồng — không cho qua, thay pin rồi kiểm tra lại" +
                (ticked.Count > 1 ? "; " + string.Join("; ", ticked.Where(i => i.Signal != "batterySwollen").Select(i => i.Note)) : "")));
            StatusText.Text = "Pin phồng — không cho qua, thay pin rồi kiểm tra lại";
            StatusText.SetResourceReference(TextBlock.ForegroundProperty, "ErrorBrush");
            return;
        }
        _host?.ReportVerdict("visual", ticked.Count > 0
            ? new(ModuleVerdictLevel.CanhBao, string.Join("; ", ticked.Select(i => i.Note)))
            : new(ModuleVerdictLevel.Dat, "Đã kiểm tra ngoại quan, không lỗi"));
        SetPassed(true);
        PassButton.IsEnabled = false;
    }

    void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in Items()) item.Box.IsChecked = false;
        TrackpadVerifiedCheck.IsChecked = false;
        _host?.ReportSignals("visual", []);
        _host?.ReportVerdict("visual", new(ModuleVerdictLevel.ChuaTest, "Chưa test"));
        SetPassed(false);
        PassButton.IsEnabled = true;
    }

    void SetPassed(bool passed)
    {
        Passed = passed;
        if (passed)
        {
            if (Items().Any(i => i.Box.IsChecked == true))
            {
                StatusText.Text = "✓ Đã ghi nhận lỗi, cho qua (có lỗi ngoại quan)";
                StatusText.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
            }
            else
            {
                StatusText.Text = "✓ Đã xác nhận không lỗi";
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
