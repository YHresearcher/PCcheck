using System.Windows;

namespace YanJi.PluginSdk;

/// <summary>Popup chuẩn của app: mọi MessageBox đều qua đây để chung 1 format —
/// tiêu đề luôn "An Tâm PC check", icon theo mức độ, nút theo ý nghĩa.
/// Owner (cửa sổ cha) truyền vào để popup hiện giữa cửa sổ app thay vì giữa màn hình.</summary>
public static class Dialogs
{
    const string Title = "An Tâm PC check";

    /// <summary>Thông báo thường (icon i).</summary>
    public static void Info(Window? owner, string message) =>
        MessageBox.Show(owner, message, Title, MessageBoxButton.OK, MessageBoxImage.Information);

    /// <summary>Cảnh báo (icon !) — việc chưa xong, cần người dùng kiểm tra thêm.</summary>
    public static void Warn(Window? owner, string message) =>
        MessageBox.Show(owner, message, Title, MessageBoxButton.OK, MessageBoxImage.Warning);

    /// <summary>Lỗi (icon X).</summary>
    public static void Error(Window? owner, string message) =>
        MessageBox.Show(owner, message, Title, MessageBoxButton.OK, MessageBoxImage.Error);

    /// <summary>Hỏi Có/Không (icon ?) — trả về true khi bấm Có.</summary>
    public static bool Ask(Window? owner, string message) =>
        MessageBox.Show(owner, message, Title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    /// <summary>Hỏi OK/Hủy (icon ?) — trả về true khi bấm OK.</summary>
    public static bool Confirm(Window? owner, string message) =>
        MessageBox.Show(owner, message, Title, MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK;
}
