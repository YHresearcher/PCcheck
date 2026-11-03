using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace YanJi.PluginSdk;

/// <summary>插件元数据（与插件目录里 plugin.json 一致）。Order 决定导航顺序（小的在前）；
/// Terminal = 终点页（无「确认通过」、不参与通过状态流、导航不配状态圆点）。</summary>
public record PluginInfo(string Id, string Name, string Version, int Order, bool Terminal);

/// <summary>Kết luận từng module, dùng cho trang Tổng kết: ChuaTest = chưa test xong,
/// Dat = đạt, CanhBao = đạt nhưng có điều kiện (ví dụ ổ cảnh báo, sạc Trung bình),
/// Chan = bị chặn, phải sửa rồi test lại (ví dụ ổ &lt;70% hoặc 0E &gt;0, sạc Kém).
/// Module nào chưa báo cáo thì Tổng kết tự suy từ IsDone: đã cho qua = Dat, chưa = ChuaTest.</summary>
public enum ModuleVerdictLevel { ChuaTest, Dat, CanhBao, Chan }

/// <summary>Kết luận 1 module: mức + ghi chú ngắn hiển thị ở trang Tổng kết
/// (ví dụ "Ổ sức khỏe 65% (&lt;70%)"). Ghi chú không chứa thông tin khách hàng.</summary>
public record ModuleVerdict(ModuleVerdictLevel Level, string Note);

/// <summary>宿主提供给插件的上下文（目前主要给「验机结束」类插件读全模块清单与完成状态）。</summary>
public interface IHostContext
{
    /// <summary>当前已加载的全部模块（含终点页），按导航顺序。</summary>
    IReadOnlyList<PluginInfo> Modules { get; }
    /// <summary>某模块当前是否已通过（终点页恒为 false）。</summary>
    bool IsDone(string moduleId);
    /// <summary>任一模块通过状态变化后触发（UI 线程）。</summary>
    event Action? DoneChanged;
    /// <summary>Kết luận từng module đã báo cáo (module chưa báo cáo thì không có trong này;
    /// trang Tổng kết tự suy từ IsDone). Chỉ host ghi, plugin chỉ đọc + báo cáo.</summary>
    IReadOnlyDictionary<string, ModuleVerdict> Verdicts { get; }
    /// <summary>Module báo cáo kết luận của mình (gọi trên UI thread; host kích VerdictChanged).</summary>
    void ReportVerdict(string moduleId, ModuleVerdict verdict);
    /// <summary>任一模块结结论变化后触发（UI 线程），trang Tổng kết đăng ký để làm mới.</summary>
    event Action? VerdictChanged;
    /// <summary>Chuyển tới 1 module theo id (Dashboard dùng để nhảy tới bài test /
    /// thông tin phần cứng / sức khỏe / tổng kết). Trả về false nếu không có
    /// module đó. Chỉ đổi SelectedIndex của host, không tự vào wizard.</summary>
    bool NavigateTo(string moduleId);
    /// <summary>Vào chế độ wizard: host khóa menu bên trái, hiện thanh wizard
    /// (Quay lại / Bài tiếp theo / Dừng tất cả / Thoát wizard), mỗi lần chỉ
    /// hiện 1 bài test. Thoát wizard thì mở lại menu, không mất trạng thái test.</summary>
    void StartWizard();
    /// <summary>Thoát chế độ wizard, mở lại menu bên trái.</summary>
    void ExitWizard();
    /// <summary>Wizard: quay lại bài test có thể cho qua gần nhất trước bài hiện tại.
    /// Trả về false nếu đã ở bài đầu tiên hoặc không ở trong wizard.</summary>
    bool WizardBack();
    /// <summary>Wizard: tới bài test có thể cho qua gần nhất sau bài hiện tại.
    /// Trả về false nếu đã ở bài cuối cùng hoặc không ở trong wizard.</summary>
    bool WizardNext();
}

/// <summary>插件用到的开源组件/素材致谢（名称/用途/协议），在「插件管理」窗口随插件展示——
/// 致谢跟着模块走，删掉模块其致谢也随之消失。</summary>
public record PluginCredit(string Name, string Usage, string License);

/// <summary>验机模块插件入口。插件程序集里必须有且仅有一个公共无参实现类（plugin.json 的 entryType 指定）。</summary>
public interface IYanJiPlugin
{
    /// <summary>插件信息；需与 plugin.json 一致（宿主以 plugin.json 为准校验）。</summary>
    PluginInfo Info { get; }
    /// <summary>本模块用到的开源组件/素材致谢（没有用到的第三方组件就保持空）。
    /// 默认空集合，第三方插件不实现也能编译。</summary>
    IReadOnlyList<PluginCredit> Credits => [];
    /// <summary>创建模块页面（宿主调用一次，实例常驻，切导航不丢状态）。</summary>
    UserControl CreatePage(IHostContext host);
}

/// <summary>「可通过」模块的页面实现此接口：确认通过 → PassChanged(true)，重新检测/重置 → PassChanged(false)。
/// 宿主据此同步导航圆点并自动跳转到下一个未完成模块。终点页不实现本接口。</summary>
public interface IModulePage
{
    event Action<bool>? PassChanged;
}

/// <summary>需要接收物理按键的页面实现此接口（如键盘测试）。宿主用 PreviewKeyDown/Up 隧道事件统一路由，
/// 仅当本页面为当前页时转发；返回 true 表示吞掉该按键（防焦点副作用）。</summary>
public interface IKeyHandlerPage
{
    bool HandleKey(Key key, bool down);
}

/// <summary>需要在程序关闭时做清理的插件实现此接口（如散热测试停烤机进程）。
/// 宿主在窗口 Closed 时逐个调用。</summary>
public interface IPluginShutdown
{
    void Shutdown();
}

/// <summary>Plugin có tiến trình tải cao (nướng máy, đo tốc độ ổ) thì implement thêm
/// interface này để nút "Dừng tất cả test nặng" ở MainWindow dừng được thật.
/// Trả về tên các tiến trình vẫn còn sống sau khi đã kill + quét lại; rỗng =
/// đã dừng thật. Không được trả về rỗng khi tiến trình thực ra còn sống —
/// đó chính là lỗi "chỉ dừng giao diện" mà bản này sửa.</summary>
public interface IStopHeavyTests
{
    IReadOnlyList<string> StopHeavyTests();
}
