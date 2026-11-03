using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using YanJi.PluginSdk.Services;

namespace YanJi.Plugin.Settings;

/// <summary>Trang Cài đặt: đọc AppSettings hiện tại vào ô nhập, lưu thì kiểm tra
/// đường dẫn hồ sơ tạo được thư mục rồi mới ghi file settings (không lưu đường
/// dẫn hỏng rồi để Tổng kết fallback im lặng). Số phút nhập sai (chữ, ngoài 1–60)
/// thì báo lỗi, không lưu, không tự sửa thành số khác mà không báo.</summary>
public partial class SettingsPage : UserControl
{
    public SettingsPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Refresh();
    }

    void Refresh()
    {
        AppSettings.Load();
        UsbPathBox.Text = AppSettings.UsbPath;
        StressMinutesBox.Text = AppSettings.StressMinutes.ToString();
        AutoStopCheck.IsChecked = AppSettings.AutoStopStress;
        EffectivePathText.Text = $"Đường dẫn hồ sơ đang dùng: {AppSettings.GetKetQuaDir()}";
        ToolsPathText.Text = $"Thư mục Tools: {Path.Combine(AppContext.BaseDirectory, "Tools")}";
        SaveStatusText.Text = "";
    }

    void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(StressMinutesBox.Text.Trim(), out int minutes) || minutes < 1 || minutes > 60)
        {
            SaveStatusText.Text = "Chưa lưu: thời lượng nướng phải là số nguyên từ 1 đến 60 phút.";
            return;
        }
        string usbPath = UsbPathBox.Text.Trim();
        if (usbPath.Length > 0)
        {
            try { Directory.CreateDirectory(usbPath); }
            catch (Exception ex)
            {
                SaveStatusText.Text = $"Chưa lưu: không tạo được thư mục hồ sơ ({usbPath}): {ex.Message}";
                return;
            }
        }
        if (AppSettings.Save(usbPath, minutes, AutoStopCheck.IsChecked == true, out string error))
        {
            SaveStatusText.Text = $"Đã lưu. Đường dẫn hồ sơ đang dùng: {AppSettings.GetKetQuaDir()}";
            EffectivePathText.Text = $"Đường dẫn hồ sơ đang dùng: {AppSettings.GetKetQuaDir()}";
        }
        else
        {
            SaveStatusText.Text = $"Chưa lưu được file cài đặt: {error}";
        }
    }

    void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        AppSettings.Reset();
        Refresh();
        SaveStatusText.Text = "Đã khôi phục mặc định: đường dẫn hồ sơ tự động (thư mục app), nướng khuyến nghị 3 phút, không tự dừng.";
    }

    void OpenToolsButton_Click(object sender, RoutedEventArgs e)
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "Tools");
        try
        {
            Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            SaveStatusText.Text = $"Không mở được thư mục Tools ({dir}): {ex.Message}";
        }
    }
}
