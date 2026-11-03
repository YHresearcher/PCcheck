using System.Windows.Controls;
using YanJi.PluginSdk;

namespace YanJi.Plugin.Settings;

/// <summary>Trang Cài đặt (Terminal, order 95, cuối menu): đường dẫn hồ sơ (USB),
/// thời lượng nướng khuyến nghị + tự dừng, đường dẫn Tools (chỉ xem), giao diện
/// sáng/tối (ghi chú: đổi bằng công tắc ở top bar, vì ThemeManager ở host, plugin
/// không đổi theme được). Cài đặt lưu ở %APPDATA%\YanJi\settings.json, theo app,
/// không theo USB — hồ sơ từng máy mới theo đường dẫn USB trong cài đặt này.</summary>
public class SettingsPlugin : IYanJiPlugin
{
    public PluginInfo Info => new("settings", "Cài đặt", "1.0.0", 95, true);

    public IReadOnlyList<PluginCredit> Credits => [];

    public UserControl CreatePage(IHostContext host) => new SettingsPage();
}
