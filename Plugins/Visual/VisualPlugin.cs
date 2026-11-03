using System.Windows.Controls;
using YanJi.PluginSdk;

namespace YanJi.Plugin.Visual;

/// <summary>Module Ngoại quan (id visual, order 70, giữa Cổng &amp; Sạc và Đo tốc độ ổ):
/// bước kiểm tra bằng mắt duy nhất trong wizard — những gì phần mềm không đo được
/// thì kỹ thuật viên tick tay ở đây (15 hạng mục theo nhóm: vỏ, bản lề/màn hình,
/// phím/touchpad, cổng/sạc, pin/dấu vết, trầy xước). Chỉ bản lề lỏng và trầy nhẹ
/// có câu riêng trong Phrasings.json; các lỗi còn lại dùng câu cảnh báo chung,
/// không bịa câu riêng. Tick thì vẫn cho qua được (test đã xong, lỗi đã ghi nhận),
/// nhưng verdict là Cảnh báo + tín hiệu, không phải "không lỗi" — trừ pin phồng
/// thì chặn cho qua vì nguy hiểm.</summary>
public class VisualPlugin : IYanJiPlugin
{
    public PluginInfo Info => new("visual", "Ngoại quan", "1.0.0", 70, false);

    public IReadOnlyList<PluginCredit> Credits => [];

    public UserControl CreatePage(IHostContext host) => new VisualPage(host);
}
