using System.Windows;

namespace YanJi.Views;

/// <summary>Màn hình chờ khởi động: chỉ hiển thị logo + thương hiệu, không có logic.
/// Việc hiện / đóng do App.xaml.cs điều khiển: hiện trước khi tạo MainWindow,
/// đóng sau khi MainWindow đã tải xong và đủ thời gian hiển thị tối thiểu.</summary>
public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
    }
}
