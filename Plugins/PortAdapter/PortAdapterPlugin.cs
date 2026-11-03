using System.Windows.Controls;
using YanJi.PluginSdk;

namespace YanJi.Plugin.PortAdapter;

/// <summary>「Cổng kết nối &amp; Sạc」插件入口：端口自动探测（WMI）+ 充电器 10 秒监测 + 人工目视勾选。
/// 充电监测逻辑与电压稳定性阈值移植自 Tanxe Studio 的 laptop-diagnostic-tool（MIT），
/// 详见 PortAdapterService 里的移植说明与 README.md 的致谢。</summary>
public class PortAdapterPlugin : IYanJiPlugin
{
    public PluginInfo Info => new("ports", "Cổng kết nối & Sạc", "1.0.0", 60, false);

    public IReadOnlyList<PluginCredit> Credits =>
    [
        new("laptop-diagnostic-tool (Tanxe Studio)",
            "Logic kiểm tra sạc (giám sát 10 giây, ngưỡng độ ổn định điện áp) và phát hiện cổng, port từ PowerShell sang C#",
            "MIT"),
    ];

    public UserControl CreatePage(IHostContext host) => new PortAdapterPage();
}
