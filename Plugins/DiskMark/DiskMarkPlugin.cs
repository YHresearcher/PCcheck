using System.Windows.Controls;
using YanJi.PluginSdk;

namespace YanJi.Plugin.DiskMark;

/// <summary>「硬盘跑分」插件入口：CrystalDiskMark 源码集成（CdmHost 子进程）对目标盘跑
/// SEQ1M Q8T1 顺序读/写 + RND4K Q1T1 随机读/写四项。
/// 实现 IPluginShutdown：程序关闭时兜底杀掉跑分进程树（连带 diskspd）。</summary>
public class DiskMarkPlugin : IYanJiPlugin, IPluginShutdown, IStopHeavyTests
{
    public PluginInfo Info => new("disk", "Đo tốc độ ổ", "1.0.1", 80, false);

    public IReadOnlyList<PluginCredit> Credits =>
    [
        new("CrystalDiskMark 9.0.3 (hiyohiyo / Crystal Dew World)", "Engine đo tốc độ ổ (biên dịch nguyên bản từ mã nguồn thành CdmHost chạy console, đi kèm plugin)", "MIT"),
        new("diskspd 2.0.20a (Microsoft, hiyohiyo fork tùy chỉnh)", "Tiến trình con đo tốc độ (do engine CDM gọi, đi kèm plugin)", "MIT"),
    ];

    public UserControl CreatePage(IHostContext host) => new DiskMarkPage(host);
    public void Shutdown() => DiskMarkPage.CleanupStatic();
    public IReadOnlyList<string> StopHeavyTests() => CdmService.StopVerified();
}
