using System.Windows.Controls;
using YanJi.PluginSdk;

namespace YanJi.Plugin.Visual;

/// <summary>Module Ngoại quan (id visual, order 70, giữa Cổng &amp; Sạc và Đo tốc độ ổ):
/// bước kiểm tra bằng mắt duy nhất trong wizard — những gì phần mềm không đo được
/// (bản lề, trầy xước) thì kỹ thuật viên tick tay ở đây. Chỉ có 2 ô tick vì chỉ
/// 2 lỗi này đã có câu trong Phrasings.json; không bịa thêm ô cho lỗi chưa có câu.
/// Tick thì vẫn cho qua được (test đã xong, lỗi đã ghi nhận), nhưng verdict là
/// Cảnh báo + tín hiệu, Tổng kết ra câu tương ứng, không phải "không lỗi".</summary>
public class VisualPlugin : IYanJiPlugin
{
    public PluginInfo Info => new("visual", "Ngoại quan", "1.0.0", 70, false);

    public IReadOnlyList<PluginCredit> Credits => [];

    public UserControl CreatePage(IHostContext host) => new VisualPage(host);
}
