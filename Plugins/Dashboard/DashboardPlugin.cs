using System.Windows.Controls;
using YanJi.PluginSdk;

namespace YanJi.Plugin.Dashboard;

/// <summary>Trang Tổng quan (Terminal, order 0, đầu tiên): điểm vào của app, không
/// phải 1 bài test. Hiện tiến độ (đã xong x/y bài), kết luận mua máy hiện tại,
/// và các nút nhảy tới: Bắt đầu / Tiếp tục kiểm tra máy (vào wizard, 1 bài/lần),
/// Đối chiếu cấu hình (thông tin phần cứng), Sức khỏe thiết bị, Tổng kết
/// (kết luận mua), Mở thư mục hồ sơ. Trang này KHÔNG chọn câu nhận xét thân
/// thiện riêng — câu đó chỉ ở trang Tổng kết, để Dashboard và Tổng kết không
/// hiện 2 câu khác nhau cho cùng 1 kết luận.</summary>
public class DashboardPlugin : IYanJiPlugin
{
    public PluginInfo Info => new("dashboard", "Tổng quan", "1.0.0", 0, true);

    public IReadOnlyList<PluginCredit> Credits => [];

    public UserControl CreatePage(IHostContext host) => new DashboardPage(host);
}
