using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using YanJi.PluginSdk;

namespace YanJi.Views;

/// <summary>Popup Giới thiệu: thông tin shop (tên + slogan + 2 chi nhánh +
/// website) + nút Ghé qua shop. Khung kiểu Mac giống AnTamDialog; font lấy
/// từ cửa sổ cha để khớp Be Vietnam Pro của host.</summary>
public partial class ShopInfoDialog : Window
{
    public ShopInfoDialog()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            FontFamily = Owner?.FontFamily
                ?? Application.Current?.MainWindow?.FontFamily
                ?? FontFamily;
            Title = Lang.Get("Nav.About");
        };
    }

    void VisitButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = "https://www.vitinhantam.com", UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Dialogs.Warn(this, string.Format(Lang.Get("Shell.WebsiteFailed"), ex.Message));
        }
    }

    void CloseButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
}
