using System.Windows;

namespace YanJi.PluginSdk;

/// <summary>Popup chuẩn của app: mọi popup đều qua đây để chung 1 format —
/// tiêu đề luôn "An Tâm Check", icon theo mức độ, nút theo ý nghĩa.
/// Hiện bằng cửa sổ riêng của app (AnTamDialog), không dùng MessageBox
/// mặc định của Windows. Owner (cửa sổ cha) truyền vào để popup hiện
/// giữa cửa sổ app thay vì giữa màn hình.</summary>
public static class Dialogs
{
    /// <summary>Thông báo thường (icon i).</summary>
    public static void Info(Window? owner, string message) =>
        Show(owner, AnTamDialogKind.Info, message);

    /// <summary>Cảnh báo (icon !) — việc chưa xong, cần người dùng kiểm tra thêm.</summary>
    public static void Warn(Window? owner, string message) =>
        Show(owner, AnTamDialogKind.Warn, message);

    /// <summary>Lỗi (icon X).</summary>
    public static void Error(Window? owner, string message) =>
        Show(owner, AnTamDialogKind.Error, message);

    /// <summary>Hỏi Có/Không (icon ?) — trả về true khi bấm Có.</summary>
    public static bool Ask(Window? owner, string message) =>
        Show(owner, AnTamDialogKind.Ask, message) == true;

    /// <summary>Hỏi Đồng ý/Hủy (icon ?) — trả về true khi bấm Đồng ý.</summary>
    public static bool Confirm(Window? owner, string message) =>
        Show(owner, AnTamDialogKind.Confirm, message) == true;

    static bool? Show(Window? owner, AnTamDialogKind kind, string message)
    {
        var dlg = new AnTamDialog(kind, message) { Owner = owner };
        return dlg.ShowDialog();
    }
}
