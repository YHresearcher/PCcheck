using System.Windows.Controls;
using YanJi.PluginSdk;

namespace YanJi.Plugin.ConfigCheck;

/// <summary>「配置核对」插件入口：进页自动采集 CPU/显卡/内存/硬盘/网卡/屏幕，人工对照订单核对。</summary>
public class ConfigCheckPlugin : IYanJiPlugin
{
    public PluginInfo Info => new("config", "Đối chiếu cấu hình", "1.0.0", 10, false);

    public IReadOnlyList<PluginCredit> Credits =>
    [
        new("LibreHardwareMonitor", "Thư viện thu thập thông tin phần cứng (tham chiếu mã nguồn)", "MPL-2.0"),
        new("CrystalDiskInfo", "Tham khảo cách thu thập thông tin ổ (đọc trực tiếp SMART)", "MIT"),
    ];

    public UserControl CreatePage(IHostContext host) => new ConfigCheckPage(host);
}
