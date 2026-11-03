using System.Windows.Controls;
using YanJi.PluginSdk;

namespace YanJi.Plugin.StressTest;

/// <summary>「散热测试」插件入口：CPU 单烤 prime95 / GPU 单烤 FurMark / 双烤 + 四路传感器监控。
/// 实现 IPluginShutdown：程序关闭时兜底停掉烤机进程。</summary>
public class StressTestPlugin : IYanJiPlugin, IPluginShutdown, IStopHeavyTests
{
    public PluginInfo Info => new("stress", "Test tản nhiệt", "1.0.0", 50, false);

    public IReadOnlyList<PluginCredit> Credits =>
    [
        new("Prime95（GIMPS, George Woltman）", "Nướng CPU (biên dịch từ mã nguồn công khai, xem prime95-build/)", "GIMPS freeware (kèm license.txt)"),
        new("FurMark (bản Python, StanislavPetrovV)", "Bản mẫu dựng hình cho nướng GPU (viết lại bằng C# / OpenTK, shader và texture kèm giấy phép riêng)", "MIT"),
        new("OpenTK 4.x", "Cửa sổ OpenGL (GLFW) và binding cho nướng GPU", "MIT"),
        new("StbImageSharp", "Giải mã texture cho nướng GPU (bản chuyển stb_image sang C#)", "Phạm vi công cộng"),
        new("LibreHardwareMonitor", "Theo dõi cảm biến (thu thập nhiệt độ/công suất)", "MPL-2.0"),
        new("PawnIO（namazso）", "Driver nhân Ring0 cho cảm biến CPU/mainboard (trình cài đặt chính thức có chữ ký kèm trong Tools/pawnio/, chỉ cài khi người dùng bấm; driver cài trên từng máy test, không đi theo USB)", "GPL-2.0 (kèm ngoại lệ giao tiếp IOCTL)"),
    ];

    public UserControl CreatePage(IHostContext host) => new StressTestPage();
    public void Shutdown() => StressTestService.Stop();
    public IReadOnlyList<string> StopHeavyTests() => StressTestService.StopVerified();
}
