using System.Windows;

namespace YanJi.PluginSdk;

/// <summary>Đọc chuỗi ngôn ngữ hiện tại (từ điển Strings.vi/en của host,
/// đổi bằng nút chuyển ngữ trên thanh công cụ). Trả về chính key nếu
/// thiếu — để lỗi thiếu chuỗi hiện rõ thay vì im lặng thành chuỗi rỗng.</summary>
public static class Lang
{
    public static event Action? Changed;

    /// <summary>Ngôn ngữ giao diện hiện tại ("vi"/"en"), do host LanguageManager
    /// đặt mỗi lần đổi — plugin dùng để chọn pool câu theo ngôn ngữ (vd PhrasingBank).</summary>
    public static string CurrentLanguage { get; set; } = "vi";

    public static string Get(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;

    public static void RaiseChanged() => Changed?.Invoke();
}
