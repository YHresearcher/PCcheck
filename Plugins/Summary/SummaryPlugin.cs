using System.Windows.Controls;
using YanJi.PluginSdk;

namespace YanJi.Plugin.Summary;

/// <summary>Trang Tổng kết (Terminal, order 90, cuối cùng): đọc kết luận từng module
/// từ IHostContext, tính kết luận tổng, hiện mỗi kết luận tổng bằng 1 cách nói
/// thân thiện chọn ngẫu nhiên 1 lần khi kịch bản đổi, rồi cố định (không còn nút
/// đổi câu tay — câu đã chọn là câu của phiên kiểm tra đó, và là câu lưu vào báo cáo).
/// Không dùng thư viện bên ngoài, không tự chấm điểm pin / nhiệt độ / tốc độ ổ
/// (chưa có ngưỡng shop chốt cho các mục đó) — các module chưa có kết luận riêng
/// thì chỉ tính theo đã cho qua / chưa cho qua.</summary>
public class SummaryPlugin : IYanJiPlugin
{
    public PluginInfo Info => new("summary", "Tổng kết", "1.0.0", 90, true);

    public IReadOnlyList<PluginCredit> Credits => [];

    public UserControl CreatePage(IHostContext host) => new SummaryPage(host);
}
