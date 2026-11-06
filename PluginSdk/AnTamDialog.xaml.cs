using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace YanJi.PluginSdk;

public enum AnTamDialogKind { Info, Warn, Error, Ask, Confirm }

/// <summary>Popup riêng của app (không dùng MessageBox mặc định của Windows):
/// dải cam trên cùng như MainWindow, thẻ bo tròn + bóng mờ, icon tròn theo
/// mức độ, nút nổi kiểu app. Font lấy từ cửa sổ cha để khớp Be Vietnam Pro
/// của host (font nằm trong assembly của host, SDK không tự thấy được).</summary>
public partial class AnTamDialog : Window
{
    public AnTamDialog(AnTamDialogKind kind, string message)
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            FontFamily = Owner?.FontFamily
                ?? Application.Current?.MainWindow?.FontFamily
                ?? FontFamily;
        };
        MessageText.Text = message;

        switch (kind)
        {
            case AnTamDialogKind.Info:
                IconGlyph.Text = "i";
                IconBorder.SetResourceReference(Border.BackgroundProperty, "AccentBrush");
                PrimaryButton.Content = "Đã hiểu";
                PrimaryButton.IsCancel = true;
                break;
            case AnTamDialogKind.Warn:
                IconGlyph.Text = "!";
                // Cam cảnh báo, cùng mốc với pin chai 20–<40% (không có token riêng).
                IconBorder.Background = new SolidColorBrush(Color.FromRgb(0xDE, 0x87, 0x0F));
                PrimaryButton.Content = "Đã hiểu";
                PrimaryButton.IsCancel = true;
                break;
            case AnTamDialogKind.Error:
                IconGlyph.Text = "✕";
                IconBorder.SetResourceReference(Border.BackgroundProperty, "ErrorBrush");
                PrimaryButton.Content = "Đã hiểu";
                PrimaryButton.IsCancel = true;
                break;
            case AnTamDialogKind.Ask:
                IconGlyph.Text = "?";
                IconBorder.SetResourceReference(Border.BackgroundProperty, "AccentBrush");
                PrimaryButton.Content = "Có";
                SecondaryButton.Content = "Không";
                SecondaryButton.Visibility = Visibility.Visible;
                break;
            case AnTamDialogKind.Confirm:
                IconGlyph.Text = "?";
                IconBorder.SetResourceReference(Border.BackgroundProperty, "AccentBrush");
                PrimaryButton.Content = "Đồng ý";
                SecondaryButton.Content = "Hủy";
                SecondaryButton.Visibility = Visibility.Visible;
                break;
        }
    }

    void PrimaryButton_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    void SecondaryButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    void CloseButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
}
