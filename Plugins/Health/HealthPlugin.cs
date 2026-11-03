using System.Windows.Controls;
using YanJi.PluginSdk;

namespace YanJi.Plugin.Health;

/// <summary>Trang Sức khỏe thiết bị (Terminal, order 85, giữa Đo tốc độ ổ và Tổng kết):
/// chỉ đọc, không phải bài test mới. Hiện đánh giá theo mức độ nghiêm trọng từ
/// kết luận từng module đã báo cáo (ổ, sạc) + trạng thái cho qua / chưa cho qua
/// của các module còn lại, lấy từ IHostContext. Khác với Thông tin phần cứng
/// (số liệu thô kiểu CPU-Z): đây là đánh giá, có màu xanh / vàng / đỏ / xám.
/// Module chưa có dữ liệu thì hiện Chưa test, không được hiểu là khỏe.</summary>
public class HealthPlugin : IYanJiPlugin
{
    public PluginInfo Info => new("health", "Sức khỏe thiết bị", "1.0.0", 85, true);

    public IReadOnlyList<PluginCredit> Credits => [];

    public UserControl CreatePage(IHostContext host) => new HealthPage(host);
}
