using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using YanJi.PluginSdk;

namespace YanJi.Plugin.Visual;

/// <summary>Trang Ngoại quan: 2 ô tick tay (bản lề lỏng, trầy nhẹ). Tick thì vẫn
/// cho qua được nhưng verdict là Cảnh báo + tín hiệu tương ứng, Tổng kết ra câu
/// bản lề / trầy nhẹ. Không tick thì verdict Đạt. Đặt lại thì xóa tín hiệu +
/// verdict về Chưa test, để tín hiệu cũ không dính sang lần kiểm tra sau.</summary>
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

    void PassButton_Click(object sender, RoutedEventArgs e)
    {
        var signals = new List<string>();
        if (HingeCheck.IsChecked == true) signals.Add("hingeLoose");
        if (ScratchesCheck.IsChecked == true) signals.Add("scratchesLight");
        _host?.ReportSignals("visual", signals);
        _host?.ReportVerdict("visual", signals.Count > 0
            ? new(ModuleVerdictLevel.CanhBao, string.Join("; ", signals.Select(s => s == "hingeLoose" ? "Bản lề lỏng — kiểm tra kỹ vì ảnh hưởng màn hình và vỏ" : "Trầy xước nhẹ — thương lượng giá")))
            : new(ModuleVerdictLevel.Dat, "Đã kiểm tra ngoại quan, không lỗi"));
        SetPassed(true);
        PassButton.IsEnabled = false;
    }

    void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        HingeCheck.IsChecked = false;
        ScratchesCheck.IsChecked = false;
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
            if (HingeCheck.IsChecked == true || ScratchesCheck.IsChecked == true)
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
