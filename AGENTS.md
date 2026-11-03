# GHI CHÚ BẢN DỊCH TIẾNG VIỆT (An Tâm PC test v0.0.4-VN) — đọc trước
# Đây là bản fork dịch giao diện sang tiếng Việt từ BibaYanji gốc, giữ nguyên bản quyền MIT của Bibapcs.
# Thay đổi so với bản gốc: (1) chữ hiện ra cho người dùng đã sang tiếng Việt, tên app thành "An Tâm PC test",
# nhà phát hành thành "Vi Tính An Tâm"; (2) trình cài đặt ra file AnTamPCtestSetup-v0.0.4-VN.exe, cài vào
# {autopf}\AnTamPCtest, nhưng tên file exe chính là AnTamPCtest.exe; (3) tên file kết quả đo ổ đổi thành
# KetQuaDoO.json và LichSuDoO.csv trong %APPDATA%\YanJi\disk_result; (4) KHÔNG tích hợp phần mềm thương mại
# đóng nguồn nào (ví dụ HWiNFO) vào code này, theo quy tắc gốc; (5) dòng ghi công bài nhạc test ở trang
# Camera - Mic - Loa giữ nguyên văn bản gốc, không được đổi, theo quy tắc gốc; (6) giao diện thương hiệu An Tâm:
# logo #ED740C ở 验机软件/Assets/logo-antam.png + app.ico, theme Light/Dark dùng cam An Tâm (sáng #D95E00,
# tối #ED740C), dải cam top bar + logo top bar + footer vitinhantam.com, token theme giữ nguyên 18 khóa hai bên;
# (7) module mới "Cổng kết nối & Sạc" (Plugins/PortAdapter, id=ports, order=60): port từ Tanxe Studio
# laptop-diagnostic-tool (MIT, ghi công trong Credits của plugin + README.md), test sạc 10 giây + quét cổng WMI
# + tick tay cổng vật lý; đã sửa 4 lỗi/đoán của bản gốc (BatteryStatus 2, MaxCapacity, ước tính 65 W,
# đoán cổng USB/HDMI) — chi tiết ở đầu PortAdapterService.cs, không được "sửa lại cho giống gốc";
# (8) kết luận ổ tự động (module Đối chiếu cấu hình, HardwareInfoService.DriveVerdict):
# ≥90% ĐẠT, 70-89% CẢNH BÁO (cho qua được, ghi điều kiện), <70% hoặc NVMe 0E >0 CHẶN
# (ConfigCheckPage chặn nút cho qua khi HardwareReport.DriveBlockReasons không rỗng),
# SMART không đọc được = Không rõ (không tự cho là tốt, không chặn cứng);
# (9) splash screen (Views/SplashWindow, 480x400, logo 120px + "An Tâm PC test" + Vi Tính An Tâm +
# slogan bảo hành 6 tháng + địa chỉ/website + thanh tiến trình): App.xaml bỏ tự tạo MainWindow,
# App.OnStartup hiện splash trước, tạo MainWindow (tải 7 module), đóng splash sau khi MainWindow
# Loaded và đủ tối thiểu 1,5 giây, rồi Activate MainWindow; màu splash theo token theme, không màu trực tiếp;
# (10) phát hành Releases (workflow .github/workflows/build-windows.yml): sau bước upload-artifact,
# nén publish thành AnTamPCtest-portable.zip rồi dùng gh CLI cập nhật Release "latest" duy nhất
# (upload --clobber, chưa có thì create), link tải ổn định releases/latest/download/..., không cần đăng nhập;
# workflow cần permissions contents: write, nếu để read thì bước Release lỗi 403; Release chỉ cập nhật
# khi build thành công, bản lỗi không ghi đè bản tốt.
# (11) kết luận từng module + kết luận tổng (Plugins/Summary, id=summary, order=90, Terminal):
# PluginSdk thêm ModuleVerdictLevel (ChuaTest/Dat/CanhBao/Chan) + ModuleVerdict + IHostContext.Verdicts/
# ReportVerdict/VerdictChanged, MainWindow.HostContext triển khai; ConfigCheck báo cáo theo kết luận ổ
# (Chặn/Cảnh báo/Chưa test/Đạt sau khi cho qua), PortAdapter báo cáo theo kết luận sạc (Kém=Chặn,
# Trung bình=Cảnh báo, còn lại theo đã cho qua/chưa); module khác chưa báo cáo thì Tổng kết suy từ
# IsDone (cho qua=Đạt, chưa=Chưa test). Kết luận tổng tự động: có Chặn=SỬA, không Chặn mà có Cảnh báo=
# BÁN CÓ ĐIỀU KIỆN, tất cả Đạt=ĐẠT, còn lại=CHƯA TEST XONG; LOẠI chỉ do kỹ thuật viên đánh dấu tay,
# không tự sinh. Mỗi kết luận tổng có 5 cách nói thân thiện random, đổi bằng nút "Đổi cách nói",
# kết luận không đổi khi đổi câu; chưa chấm điểm pin/nhiệt/tốc độ ổ vì chưa chốt ngưỡng;
# (12) SỬA LỖI SPLASH TREO + ĐỔI TÊN EXE (2026-11-03): Loaded phải gắn TRƯỚC Show() (Loaded có thể
# chạy ngay trong Show(), gắn sau sẽ bỏ lỡ, splash Topmost treo mãi) + kiểm tra IsLoaded sau Show()
# + cờ chống đóng 2 lần; tên exe chính đổi từ 验机软件.exe sang AnTamPCtest.exe qua
# <AssemblyName> trong host csproj (không code nào tham chiếu tên assembly host, RestartApp dùng
# Environment.ProcessPath nên an toàn), shortcut trình cài đặt + tài liệu đổi theo; thư mục mã nguồn
# vẫn giữ tên 验机软件, chỉ đổi tên file exe đầu ra.
# (13) KẾT LUẬN Ổ THEO MỨC ĐỘ NGHIÊM TRỌNG (2026-11-03): LhmDisk thêm CriticalWarning
# (NVMe 0xE0), AvailableSpare/Threshold (0xE2/0xE3), SATA Reallocated 0x05 / Pending 0xC5 /
# Uncorrectable 0xC6 / Reported 0xBB / CRC 0xC7 (RawValue, null = đọc không được, không phải 0);
# DriveVerdict xét lỗi nghiêm trọng >0 = CHẶN trước, rồi mới sức khỏe <70% = CHẶN, rồi cảnh báo
# (70-89%, CRC, hoặc không đọc được lỗi nghiêm trọng nào dù sức khỏe cao thì không được ĐẠT),
# rồi mới ĐẠT; HardwareReport thêm DriveWarningReasons để Tổng kết hiển thị đúng lý do.
# (14) STOP ALL CÓ KIỂM CHỨNG (2026-11-03): PluginSdk thêm IStopHeavyTests (trả về tên tiến
# trình còn sống, rỗng = dừng thật); StressTestService.StopVerified kill 2 lần + quét mồ côi
# theo đường dẫn Tools, KHÔNG xóa trường khi chưa chết thật (lỗi cũ: xóa trường trước nên giao
# diện báo dừng giả trong khi Prime95/FurMark còn chạy); MainWindow thêm nút "Dừng tất cả test
# nặng", trang Tản nhiệt không cho qua khi chưa dừng được; DiskMark StopVerified quét CdmHost/diskspd.
# (15) HỒ SƠ PHIÊN TRÊN USB (2026-11-03): trang Tổng kết thêm ô S/N + nút "Lưu hồ sơ phiên này",
# thư mục <app>\KetQua\<S/N>_<yyyyMMdd_HHmmss>\ (timestamp nên không ghi đè, trùng giây thêm _2),
# gồm BaoCao.txt + KetQua.json + bản sao KetQuaDoO.json, chỉ mục KetQua\DanhSach.csv 1 dòng/phiên;
# S/N trống vẫn lưu (KhongCoSN); thư mục app ghi không được thì fallback %APPDATA%\YanJi\KetQua\
# và báo rõ. Thiết kế mới (wizard 1 bài/lần, Hardware Info kiểu CPU-Z, kết luận mua 2 phần) đã
# chốt thành file thiết kế ở ~/workspace/your_files/ThietKeMoi_TestMayAnTam_Wizard_Dashboard.md,
# CHƯA sửa MainWindow theo thiết kế đó — nhưng kết luận mua đã chốt ngày 2026-11-03 (xem (16)), chỉ còn chờ làm Dashboard/wizard.
# Phần còn lại của file này vẫn là
# tài liệu gốc bằng tiếng Trung, chưa dịch, dùng để tham khảo kỹ thuật.

# AGENTS.md — 笔吧验机

## 项目是什么

Windows 桌面验机工具（WPF，net10.0-windows10.0.19041.0，.NET SDK 10.0.400）。
**主程序 + 插件架构**：主程序只是外壳（顶栏 / 左侧导航 / 主题 / 插件加载与管理），
每个验机功能模块都是一个独立插件（class library），放在 exe 同级 `plugins/<id>/` 目录下，
用户可在安装向导勾选模块、也可在程序内「插件管理」里导入 zip 插件包（0.0.2 起支持多选批量导入）或删除插件（重启生效）。

硬性约束：开源软件，**禁止集成闭源商业软件**（如 HWiNFO）。硬件采集只用
LibreHardwareMonitor 源码（MPL-2.0）+ Windows 自带接口（WMI / 注册表 / Win32 API）。
详细工程背景与硬件采集的坑见 `项目交接文档.md`（先通读再动采集相关代码）。

## 目录结构

```
【视频】验机软件/
├── 验机软件.slnx              # 解决方案
├── 验机软件/                   # ★ 宿主外壳（WinExe，AssemblyName=验机软件，RootNamespace=YanJi）
│   ├── App.xaml(.cs)          # 启动：加载主题 + 全局异常落盘 %TEMP%\YanJi-crash.log
│   ├── MainWindow.xaml(.cs)   # 外壳：顶栏（插件管理按钮+主题开关）+ 动态导航 + 内容区
│   ├── Services/
│   │   ├── PluginLoader.cs         # 插件扫描/加载 + 依赖解析兜底钩子
│   │   ├── PluginPackageService.cs # zip 插件包导入/删除
│   │   ├── ThemeManager.cs / TitleBarTheme.cs
│   ├── Views/PluginManagerWindow   # 插件管理窗口（列表/删除/导入 zip）
│   ├── Themes/                # Light.xaml / Dark.xaml（令牌）+ Controls.xaml（隐式样式）
│   └── Tools/                 # 烤机外部 exe 负载（prime95/furmark/pawnio，散热插件用）
├── PluginSdk/                 # ★ 插件契约（YanJi.PluginSdk.dll）：接口 + 共享服务
│   ├── Contracts.cs           # IYanJiPlugin / IModulePage / IKeyHandlerPage / IPluginShutdown / IHostContext
│   └── Services/              # DisplayInfoService + EdidParser（多个插件共用才放这里）
├── Plugins/                   # ★ 七个内置模块插件（每个 = 一个 class lib 工程 + plugin.json）
│   ├── ConfigCheck/           # 配置核对（LHM + WMI 采集，含电池容量/损耗/循环次数） id=config    order=10
│   ├── KeyboardTest/          # 键盘测试（104 键）             id=keyboard  order=20
│   ├── ScreenDeadPixel/       # 屏幕坏点（全屏纯色）           id=screen    order=30
│   ├── AvConference/          # 影音会议（摄像头/麦/扬声器）   id=av        order=40
│   ├── StressTest/            # 散热测试（烤机+传感器监控）    id=stress    order=50
│   ├── PortAdapter/           # Cổng kết nối & Sạc（WMI quét cổng + test sạc 10 秒 + tick tay） id=ports order=60
│   └── DiskMark/              # 硬盘跑分（CDM 源码集成 CdmHost） id=disk      order=80
├── FurMark/                   # GPU 烤机工具工程（构建后拷到 验机软件/Tools/furmark/）
├── prime95-build/             # prime95 叠加构建工作区（不进 slnx）
├── 集成开源项目代码/           # 开源组件源码（LibreHardwareMonitorLib 被插件项目引用）
├── installer/setup.iss        # Inno Setup 安装向导（可选组件 = 逐模块勾选）
└── publish/                   # 发布产物（self-contained 便携包，plugins/ 在根下）
```

## 构建 / 运行 / 发布

```bash
dotnet build 验机软件.slnx          # 构建全部（宿主 BuildPlugins target 会先建插件再拷到输出）
dotnet run --project 验机软件       # 运行（exe 是 requireAdministrator，开发期弹 UAC 属正常）

# 注意：全新克隆首次构建后再执行一次（或先单独 dotnet build FurMark）——
# Tools/furmark 的 None glob 在评估期求值而 FurMark 负载是构建期生成的，第二次构建才进输出。

# 发布（两条命令顺序不可换；publish/ 为自包含便携包，目标机无需装 .NET）
dotnet publish FurMark/FurMark.csproj -p:PublishProfile=FolderProfile
dotnet publish 验机软件/验机软件.csproj -p:PublishProfile=FolderProfile

# 安装向导（Inno Setup 6，产物 installer/笔吧验机Setup-v0.0.4.exe）
"%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" //Q "D:\【视频】验机软件\installer\setup.iss"
```

宿主构建链路：`BuildPlugins`（BeforeBuild，MSBuild 任务建 7 个插件工程）→ 插件构建 →
`CopyPluginsToOutput`（AfterTargets=Build，插件输出拷到 `$(OutDir)plugins/<id>/`）；
publish 时另有 `CopyPluginsToPublish`（AfterTargets=Publish）同步到 `publish\plugins\`。
**新增内置插件时，这三个 target 与 slnx 及 installer/setup.iss 的 [Components]/[Files] 都要加对应条目。**

## 插件机制

### 运行时布局与加载

```
AnTamPCtest.exe
├── YanJi.PluginSdk.dll        # 契约（插件与宿主共享同一份类型）
└── plugins/
    └── <id>/
        ├── plugin.json        # 清单（必需）
        ├── <入口>.dll         # 插件程序集
        └── *.dll / 其它资源    # 插件依赖（NAudio 等）与附带文件（如试音曲 mp3）
```

- 宿主启动时 `PluginLoader` 扫 `plugins/*/plugin.json`，校验后用
  `AssemblyLoadContext.Default.LoadFromAssemblyPath` 加载入口程序集并实例化 `entryType`。
- 插件引用 `YanJi.PluginSdk` 必须 `Private=false`（不拷进插件目录，运行时与宿主共享同一类型，
  否则接口类型不识别）。插件工程需设 `CopyLocalLockFileAssemblies=true`（类库默认不拷 NuGet 依赖到输出）。
- 插件的依赖 dll 放插件目录；宿主挂了 `AssemblyLoadContext.Default.Resolving` 兜底钩子，
  默认解析失败时到各插件目录找同名 dll（先加载者胜出，同名同版本天然去重），
  **并会下钻 `runtimes/win-x64/lib/**` 找 RID 专属托管资产**（如 LHM 的传递依赖
  Mono.Posix.NETStandard——缺它 LHM 的 OpCode.Open 在 JIT 时抛异常，采集功能全灭）。
- **增删插件 = 增删 `plugins/<id>/` 目录**。生效时机：**新插件导入后宿主帅即加载（免重启）**；
  删除/覆盖安装正在运行的插件时其 dll 被默认加载上下文锁定（运行时不卸载程序集），
  写入 `%APPDATA%\YanJi\plugin-ops.json` 待处理队列，提示后**自动重启**——下次启动在插件
  加载前统一执行（此时无文件锁）。「插件管理」窗口（顶栏按钮）封装了这套流程。
- ⚠️ 插件与宿主同进程、同**管理员权限**（宿主 manifest 是 requireAdministrator）——只安装可信来源插件。

### plugin.json 字段

```json
{
  "id": "screen",              // 目录名/唯一标识：小写字母数字 - _ ，≤32 字符
  "name": "屏幕坏点",           // 导航显示名
  "version": "1.0.0",
  "order": 30,                 // 导航排序，小的在前；内置插件按 10/20/.../90 留间隔便于插入
  "terminal": false,           // true = 终点页：无「确认通过」、不配状态圆点、不参与通过状态流
  "entryAssembly": "YanJi.Plugin.ScreenDeadPixel.dll",
  "entryType": "YanJi.Plugin.ScreenDeadPixel.ScreenDeadPixelPlugin"  // 全限定名，公共无参类，实现 IYanJiPlugin
}
```

### 契约（PluginSdk/Contracts.cs）

```csharp
public interface IYanJiPlugin                       // 入口，必需
{
    PluginInfo Info { get; }                        // 与 plugin.json 一致
    IReadOnlyList<PluginCredit> Credits => [];      // 本模块的致谢（开源组件/专有软件/素材版权；展示在插件管理窗口）
    UserControl CreatePage(IHostContext host);      // 宿主调用一次，页面实例常驻（切导航不丢状态）
}

public record PluginCredit(string Name, string Usage, string License);  // 致谢条目：名称/用途/协议

public interface IModulePage                        // 页面实现：「可通过」模块
{
    event Action<bool>? PassChanged;                // 确认通过=true，重新检测/重置=false
}

public interface IKeyHandlerPage                    // 页面实现：需要物理按键（键盘测试）
{
    bool HandleKey(Key key, bool down);             // 返回 true = 吞掉该按键
}

public interface IPluginShutdown                    // 插件实现：关程序时要清理（散热测试停烤机）
{
    void Shutdown();
}

public interface IHostContext                       // 宿主注入 CreatePage
{
    IReadOnlyList<PluginInfo> Modules { get; }      // 已加载全部模块（需要全模块清单的插件用，如汇总类终点页）
    bool IsDone(string moduleId);
    event Action? DoneChanged;                      // 任一模块通过状态变化（UI 线程）
}
```

宿主行为约定：
- 页面订阅 `IModulePage.PassChanged` 后统一处理：圆点变绿/还原 + 通过时自动跳下一个未完成模块；
  全部非终点模块完成 → 跳终点页 + 导航底部「🎉 全部验机项目已完成」。
- `PassChanged` 回退（false）要能还原圆点并撤掉全部完成提示。
- 物理按键由宿主 PreviewKeyDown/Up 隧道统一路由给当前页的 `IKeyHandlerPage`（已过滤 IsRepeat；
  Space/Enter 等已 Handled 的也能收到）。

### 开发一个新插件（完整步骤）

1. 建工程：仿照 `Plugins/ScreenDeadPixel/` 建 class lib（`Microsoft.NET.Sdk`，
   `TargetFramework=net10.0-windows10.0.19041.0`、`UseWPF=true`、`Nullable/ImplicitUsings=enable`、
   `CopyLocalLockFileAssemblies=true`），`ProjectReference` 引 `..\..\PluginSdk\YanJi.PluginSdk.csproj`
   并加 `Private="false"`。
2. 写页面：一个 `UserControl`（XAML + code-behind，无 MVVM 框架）。需要「确认通过」就实现
   `IModulePage`；需要按键实现 `IKeyHandlerPage`；关程序要清理实现 `IPluginShutdown`（在入口类上）。
3. 写入口类：公共无参类实现 `IYanJiPlugin`，`CreatePage` 返回页面实例；**模块用到的第三方组件/素材
   必须填 `Credits`**（名称/用途/协议或许可，展示在插件管理窗口——致谢随插件走，插件删了致谢也消失；
   专有软件也可列，注明许可即可，不要叫「开源致谢」）。
4. 写 `plugin.json`（`None Update` + `CopyToOutputDirectory=PreserveNewest` 随输出拷贝）。
5. 本地调试：把插件输出目录（含 plugin.json 与依赖）整体拷到宿主输出
   `验机软件/bin/<cfg>/net10.0-windows10.0.19041.0/plugins/<id>/`，启动宿主即可；
   要内置随包就把工程加进 slnx + 宿主 csproj 的 BuildPlugins/CopyPlugins 两个 target +
   installer/setup.iss 的 [Components]/[Files]。
6. 打包分发：把插件目录（plugin.json 在 zip 根或一层包裹目录下）压成 zip，
   用户经「插件管理 → 导入插件包 (.zip)…」导入（0.0.2 起文件对话框可多选批量导入，
   覆盖确认合并为一次，结果统一汇总），重启生效。示例：
   `powershell Compress-Archive -Path "plugins\myplugin\*" -DestinationPath myplugin.zip`
   （plugin.json 直接在 zip 根；或 `Compress-Archive -Path "plugins\myplugin" ...` 形成一层包裹目录，两种都支持）。

### UI 约定（与全局风格一致，验收会看）

- 主题：只用语义画刷令牌（`WindowBgBrush`/`SurfaceBgBrush`/`SubtleBgBrush`/`TextBrush`/
  `TextSecondaryBrush`/`BorderBrush`/`AccentBrush`/`ErrorBrush` 等，键集见 Themes/Light.xaml，
  深/浅两个字典键集必须一致），**全部 DynamicResource 引用，不写字面颜色**；
  控件样式复用宿主 Controls.xaml 的隐式样式（Button/ComboBox/CheckBox/TextBox 等已扁平化）。
- 「测试通过」语义色用 `ForestGreen`（导航圆点/状态行/清单 ✓）。
- 状态行/圆点等资源还原用 `SetResourceReference` 恢复动态绑定，直接赋画刷会断主题切换。
- 日志区惯例：只读 TextBox（Consolas 11）外套 `CornerRadius=6` 的 Border（LogBox 风格）；
  详情折叠区用 Controls.xaml 的 `DetailsExpander` 样式。
- 页面实例常驻：`Loaded` 会随切回重发（自动初始化要防重入），`Unloaded` 用来释放设备/停后台线程
  （摄像头灯、传感器轮询、烤机进程都靠它兜底）。

### 已知坑（插件化相关）

- **System.Management 双份实现**：NuGet 的 lib/ 下是 74KB PNSE 桩，真身在 runtimes/win（314KB）。
  插件目录里拷到的是桩，运行时靠**宿主 deps.json/TPA** 解析到真身——所以宿主 csproj 保留着
  System.Management 的 PackageReference（宿主代码并不直接用 WMI），**勿删**。
- **LibreHardwareMonitorLib 必须按 x64 构建**：插件引用它时 ProjectReference 必须带
  `SetPlatform="Platform=x64"`（CsWin32 部分 API 只在指定架构生成，AnyCPU 会编译失败）。
- 发布时插件工程不能继承宿主的 RuntimeIdentifier/SelfContained——宿主 BuildPlugins 的
  MSBuild 任务已 `RemoveProperties="RuntimeIdentifier;SelfContained"`，仿建即可。
- 安装向导的「可选组件」与 `plugins/<id>/` 目录一一对应；Tools/ 随散热组件安装。
  0.0.2 起附加任务新增「安装 PawnIO 内核驱动」（默认勾选，安装末尾
  `Tools\pawnio\PawnIO_setup.exe -install -silent` 静默完成、用户零点击——只传 `-install` 会弹版权确认窗；
  未装散热组件或系统已装 PawnIO 时 Check 自动跳过，后者避免其「请先卸载」阻塞弹窗）。

## 修改守则

- 最小改动；新代码风格对齐周边（注释密度、命名、UserControl+code-behind、无第三方 UI 库）。
- 改采集逻辑前先读 `项目交接文档.md` 第三节的坑（EDID/SMART/色域/LHM 降级矩阵都有实测结论）。
- 试音曲署名（AvConference 页面）是用户给定原文，不得改动。
- 临时探针工程（.ui-probe 等）用完即删；LHM 上游源码不要批量转码/改动。
- 改了本文件描述的机制（目录、契约、构建链路、安装组件）时同步更新本文件。

# (16) KẾT LUẬN MUA MÁY, CHO CẢ KỸ THUẬT VIÊN VÀ NGƯỜI MUA (2026-11-03, user chốt):
# Kết luận tổng đổi hẳn sang mua, không song song bán: CÓ THỂ MUA / CẦN KIỂM TRA
# THÊM-THƯƠNG LƯỢNG / KHÔNG KHUYẾN NGHỊ MUA / CHƯA ĐỦ DỮ LIỆU / KHÔNG MUA (đánh dấu tay).
# Trang Tổng kết chia 2 tầng: trên cho người mua (tiêu đề + câu thân thiện là đủ hiểu),
# dưới cho kỹ thuật viên (chi tiết từng module); BaoCao.txt cũng chia Phần 1 người mua /
# Phần 2 kỹ thuật viên. Các chữ bán ở module ổ/sạc đổi sang mua (mua được nguyên trạng,
# tối đa mua có điều kiện, kiểm tra tay trước khi mua).

# (17) DASHBOARD + WIZARD 1 BÀI/LẦN + SỨC KHỎE (2026-11-03, chưa test Windows):
# PluginSdk.IHostContext thêm NavigateTo / StartWizard / ExitWizard / WizardBack /
# WizardNext (chỉ MainWindow.HostContext implement). MainWindow thêm thanh WizardBar
# (hàng mới giữa top bar và main, Grid.Row main thành 3): vào wizard thì khóa
# NavList, mỗi lần chỉ 1 bài test có thể cho qua, nút Quay lại / Bài tiếp theo /
# Dừng tất cả / Thoát wizard luôn hiện (không bị kẹt trong wizard); cho qua 1 bài
# vẫn tự nhảy bài chưa xong tiếp theo nhờ AdvanceToNextModule cũ + OnNavSelect
# cập nhật nút wizard. Plugin mới: Dashboard (id dashboard, order 0, Terminal,
# đầu tiên) — tiến độ x/y + kết luận mua hiện tại (cùng logic Tổng kết, KHÔNG
# chọn câu random riêng để khỏi 2 câu khác nhau cho cùng 1 kết luận) + nút Bắt đầu
# / Tiếp tục phiên dang dở (vào wizard rồi nhảy bài chưa xong đầu tiên) + nhảy tới
# config / health / summary + Mở thư mục hồ sơ; Health (id health, order 85,
# Terminal, giữa Disk và Summary) — chỉ đọc Verdicts + IsDone, mức Đạt / Cảnh báo /
# Chặn / Chưa test, chưa test ≠ khỏe, stress ngắn ≠ ổn định lâu dài. Wiring mới:
# host csproj BuildPlugins + CopyPluginsToOutput + CopyPluginsToPublish, .slnx,
# installer/setup.iss components/files. CHƯA LÀM: tab CPU-Z cho Thông tin phần cứng
# (ConfigCheck vẫn là trang nhóm, chưa tách tab CPU/Memory/Storage/Graphics) và
# Cài đặt (đường dẫn USB, thời lượng test — cần settings service chung trong
# PluginSdk để Summary/Stress cùng đọc, làm riêng 1 commit, không nhét vào bản này).

# (18) THÔNG TIN PHẦN CỨNG KIỂU CPU-Z + CÀI ĐẶT (2026-11-03, chưa test Windows):
# ConfigCheckPage thành tab CPU / Memory / Storage / Graphics / Khác, 1 template
# GroupCardTemplate dùng chung cho mọi tab (trong UserControl.Resources); ánh xạ
# theo tiêu đề nhóm (CPU / RAM / Ổ / Card màn hình...), nhóm không khớp thì vào
# tab Khác, không bị lọc mất. Trang vẫn là module cho qua (đối chiếu với đơn
# hàng), chỉ đổi cách trình bày — kết luận vẫn ở Sức khỏe / Tổng kết, không ở đây.
# PluginSdk/Services/AppSettings.cs: settings.json ở %APPDATA%\YanJi (theo app,
# không theo USB), UsbPath trống = tự động (thư mục app \ KetQua), StressMinutes
# 1-60 mặc định 3, AutoStopStress mặc định TẮT; file hỏng thì về mặc định, không
# crash. Summary + Dashboard dùng AppSettings.GetKetQuaDir() (fallback %APPDATA%
# nếu ghi không được, báo rõ). StressPage: hiện thời lượng khuyến nghị, đủ phút
# thì báo có thể dừng, bật AutoStop thì tự dừng bằng StopVerified đã kiểm chứng
# (cờ _autoStopAttempted ngăn gọi lại mỗi 500ms nếu dừng không được, cờ _autoStopped
# giữ ghi chú cho tới lần nướng mới). Plugin Settings (id settings, order 95,
# Terminal, cuối menu): ô đường dẫn hồ sơ (lưu thì thử tạo thư mục trước, không
# lưu đường hỏng), ô số phút (sai thì báo, không lưu), checkbox tự dừng, Tools
# chỉ xem + nút mở thư mục, theme chỉ ghi chú (đổi bằng công tắc top bar, vì
# ThemeManager ở host). Wiring: host csproj Build/Copy/Publish, .slnx, installer.
# Dashboard thêm nút Mở Cài đặt.

# (19) CÂU KẾT LUẬN TỪ FILE JSON + KỊCH BẢN ƯU TIÊN + "AN TÂM NHẮN BẠN" (2026-11-03, chưa test Windows):
# Plugins/Summary/Phrasings.json là nguồn câu duy nhất (đọc lúc chạy qua
# PhrasingBank.cs, csproj copy file vào plugins/summary/); C# không còn viết cố
# định câu kết luận. File hỏng/thiếu thì PhrasingBank dùng câu dự phòng (cũng là
# câu thật của user: kết hợp A/D/E + câu tốt đầu tiên), không crash.
# Thứ tự ưu tiên khi chọn kịch bản: lỗi nghiêm trọng → lỗi chức năng → cảnh báo
# linh kiện → thiếu dữ liệu → máy đạt. Thiếu dữ liệu quan trọng thì không về
# nhóm tốt: bất kỳ module nào chưa test xong cũng về "incomplete".
# Kịch bản tự chọn được hiện tại (từ tín hiệu đã có): good (5 câu tốt + kết hợp A),
# driveMediaError (note chứa "lỗi toàn vẹn dữ liệu"), driveBadSector (note chứa
# "sector"), driveWear (cảnh báo sức khỏe), driveSmartUnknown (chưa rõ SMART),
# serious (kết hợp D, cố tình không trộn câu "sửa rồi test lại" để không làm nhẹ
# cảnh báo), warning (5 câu cảnh báo chung, vd sạc Trung bình / CRC), incomplete
# (kết hợp E + 5 câu chưa xong), notBuy (5 câu loại, đánh dấu tay).
# Câu pin / CPU-GPU / ngoại vi / kết hợp B-C đã có sẵn trong JSON nhưng CHƯA tự
# chọn vì chưa có tín hiệu (pin cần % chai, CPU/GPU cần nhiệt/throttling/artifact,
# ngoại vi cần tick xác nhận tay, B/C cần cộng nhiều tín hiệu) — không đoán từ
# dữ liệu không có. Câu chỉ random lại khi kịch bản đổi, không random lại mỗi
# Refresh. "Đổi cách nói" đổi cả câu kết luận và dòng An Tâm (cùng kịch bản,
# kết luận không đổi). Dòng "An Tâm nhắn bạn" chỉ hiện khi good hoặc warning,
# không hiện khi serious / incomplete / notBuy; lưu vào BaoCao.txt + KetQua.json
# cùng câu kết luận + scenario + phrasingLocked=true — mở lại file đã lưu sẽ thấy
# đúng câu đã lưu, không random lại.

# (20) TÍN HIỆU CHI TIẾT → CÂU CHUYÊN BIỆT (2026-11-03, chưa test Windows):
# PluginSdk IHostContext thêm Signals (mỗi module 1 danh sách tín hiệu) + ReportSignals
# (ghi đè lần báo trước của module đó) + SignalChanged; MainWindow.HostContext là
# nơi duy nhất implement. Quy tắc: tín hiệu nào ảnh hưởng tiêu đề thì phải kèm
# verdict cùng mức; tín hiệu tốt (pin khỏe, CPU tốt) chỉ thêm câu vào pool tốt,
# không đổi tiêu đề. Tổng kết chọn kịch bản theo thứ tự ưu tiên: nghiêm trọng
# (ổ media / ổ sector / CPU lỗi / GPU artifact / sạc Kém / Chặn chung) → chức năng
# (phím liệt → điểm chết → loa rè → webcam → bản lề, theo thứ tự module) → cảnh báo
# (kết hợp B → pin chai nặng → pin chai vừa → CPU quá nhiệt → ổ SMART chưa rõ →
# ổ hao mòn → trầy nhẹ → cảnh báo chung) → thiếu dữ liệu → máy đạt (pool tốt =
# câu tốt + kết hợp A + câu CPU tốt nếu đã nướng xong không lỗi + câu pin khỏe
# nếu pin chai <20%). An Tâm nhắn bạn đi kèm đạt / cảnh báo / chức năng, không
# kèm nghiêm trọng / thiếu dữ liệu / không mua.
# Nguồn tín hiệu: pin từ % chai lớn nhất của nhóm Pin (Đối chiếu cấu hình, <20%
# khỏe, 20–<40% chai vừa dùng câu 20–30%, ≥40% chai nặng; mốc là ví dụ của user,
# không phải tiêu chuẩn cứng; pin không đọc được % hoặc máy không có pin thì
# KHÔNG báo tín hiệu — không dùng câu "không đọc được pin" cho desktop vì app
# chưa biết dạng máy); CPU quá nhiệt từ nhiệt tối đa khi nướng (≥95°C, mốc nháp
# từ checklist cũ, chưa phải tiêu chuẩn chính thức; nhiệt GPU chỉ hiện biểu đồ,
# không tự kết luận vì chưa có ngưỡng GPU); CPU lỗi / GPU artifact là tick tay
# ở Tản nhiệt và CHẶN cho qua (giống ổ bị chặn); phím liệt / điểm chết / loa rè
# / webcam / bản lề / trầy nhẹ là tick tay ở từng bài test, vẫn cho qua được
# nhưng verdict Cảnh báo + tín hiệu; module Ngoại quan mới (id visual, order 70,
# giữa Cổng & Sạc và Đo tốc độ ổ) chỉ có 2 ô bản lề / trầy nhẹ vì chỉ 2 lỗi này
# đã có câu trong JSON; sạc Kém báo batteryNotCharging; kết hợp B chỉ khi CPU tốt
# + ổ hao mòn đã đo được + pin chai cùng lúc (không tính "ổ chưa rõ" thành "ổ cũ").
# Chưa tự chọn: pin không đọc được, kết hợp C (chưa có định nghĩa "cấu hình mạnh").
# Khi 1 module test lại / đặt lại thì xóa tín hiệu cũ + verdict về Chưa test
# (Tản nhiệt bắt đầu nướng, Bàn phím đặt lại, Ngoại quan đặt lại), để tín hiệu cũ
# không dính sang lần test sau. Trang Sức khỏe hiện tín hiệu kèm mô tả tiếng Việt.
# Wiring module mới: host csproj Build/Copy/Publish, .slnx, installer (như mọi plugin).

# (21) MÁY & MAINBOARD TỪ SMBIOS THÔ + ĐỐI CHIẾU WMI (2026-11-03, chưa test Windows):
# Plugins/ConfigCheck/SmbiosService.cs đọc bảng SMBIOS thô bằng GetSystemFirmwareTable
# ('RSMB', thử cả 2 cách đóng gói 0x52534D42 và 0x424D5352, cái nào trả dữ liệu thì
# dùng), KHÔNG qua WMI, KHÔNG qua registry. Chỉ parse Type 0 (BIOS), 1 (máy), 2
# (mainboard), 3 (khung máy); KHÔNG parse Type 17 RAM (WMI PhysicalMemory vốn đã
# lấy từ chính Type 17 nên đối chiếu 2 nguồn đó không bắt được fake tầng firmware,
# chỉ thêm rủi ro sai offset). Placeholder hãng ("To be filled by O.E.M.", "Not
# Specified", "Default string"...) = không có, không dùng để đối chiếu.
# Nhóm "Máy & Mainboard" là nhóm đầu tiên trong Đối chiếu cấu hình, rơi vào tab
# Khác (cùng Card mạng / Màn hình / Pin) vì SetTabGroups chỉ tách CPU/RAM/Ổ/GPU.
# Đối chiếu (HardwareInfoService.CollectMachine): model khớp tuyệt đối sau chuẩn
# hóa = khớp; 1 bên chứa bên kia (WMI thêm hậu tố "Notebook PC") = gần khớp, chỉ
# ghi chú, không tính không khớp; còn lại = không khớp. Serial phải khớp tuyệt
# đối (chứa nhau cũng tính không khớp). Serial Type 1 và Type 2 khác nhau là BÌNH
# THƯỜNG (serial máy vs serial board) — không đối chiếu 2 serial này với nhau.
# Không khớp ghi vào HardwareReport.SmbiosMismatchReasons; verdict: không chặn như
# ổ, nhưng đã cho qua mà không còn cảnh báo nào khác thì thành Cảnh báo, đã có
# cảnh báo khác thì ghi thêm vào ghi chú; chưa cho qua hoặc ổ chặn thì giữ mức
# (không khớp đã hiện trong nhóm + PhaseText). Máy ảo (VMware/QEMU/VirtualBox...)
# thì nhóm báo rõ là máy ảo, không tin model. UUID chỉ để tham khảo, KHÔNG dùng
# để đối chiếu (cách hiển thị UUID giữa các tool có thể lệch byte order).
# VRAM từ registry (qwMemorySize) giờ ghi rõ "(từ registry)" vì có thể bị sửa để
# fake VRAM; LHM (NVAPI/ADL) mới là số đo trực tiếp. EDID màn hình CHƯA đổi nguồn:
# block EDID đầy đủ (để tính kích thước/tấm nền/dải màu) không có API Windows đơn
# giản nào trả về ngoài cache registry (đọc trực tiếp phải qua DDC/I2C từ màn hình),
# nên giữ cache registry và ghi rõ đây là nguồn chưa xác thực được trong nhóm —
# không đổi sang WmiMonitorID (nó chỉ có hãng/mã/serial/tên, không có block EDID).
# Giới hạn vẫn đúng: BIOS bị flash giả thì SMBIOS và WMI cùng nói dối giống nhau,
# đối chiếu trong máy không bắt được — phải tra S/N trên web hãng hoặc soi tem
# dưới đáy máy.

# (22) BỎ NÚT ĐỔI CÁCH NÓI + S/N TỰ ĐIỀN + FOOTER v1.1.0 + NGOẠI QUAN 15 HẠNG MỤC (2026-11-03, chưa test Windows):
# Nút "Đổi cách nói" đã bỏ: câu kết luận chỉ random 1 lần khi kịch bản đổi, rồi
# cố định cho tới khi kịch bản đổi tiếp hoặc lưu báo cáo; code PickPhrasing vẫn
# giữ _currentPhrasingIndex để biết "chưa chọn lần nào", chỉ không còn nút gọi
# lại nó. README + SummaryPlugin doc đã sửa theo, AGENTS (19)/(20) còn nhắc nút
# cũ là lịch sử, không phải mô tả hiện tại.
# S/N tự điền: SmbiosService chuyển từ Plugins/ConfigCheck sang
# PluginSdk/Services (namespace YanJi.PluginSdk.Services) để cả Đối chiếu cấu hình
# và Tổng kết cùng dùng; HardwareInfoService đã có using namespace đó nên không
# đổi code gọi. Trang Tổng kết Loaded 1 lần: ô S/N còn trống thì điền serial máy
# từ SMBIOS thô; firmware không có serial thật thì để trống cho nhập tay; người
# dùng sửa/xóa sau đó thì không điền lại (tem khác serial firmware là trường hợp
# cảnh báo không khớp, không được tự ghi đè).
# Thương hiệu: tên hiển thị thống nhất "An Tâm PC Test" (chữ T hoa) ở Window
# Title, top bar, Splash; website vitinhantam.com đưa lên top bar (subtitle, cùng
# slogan bảo hành 6 tháng); footer menu trái thành "An Tâm PC Test v1.1.0".
# Installer AppName/ AppVersion / OutputBaseFilename theo v1.1.0; tên exe
# AnTamPCtest.exe và zip portable AnTamPCtest-portable.zip KHÔNG đổi (tên file
# không dấu, không khoảng trắng, tránh vỡ shortcut/link cũ).
# Ngoại quan mở rộng từ 2 ô lên 15 hạng mục theo nhóm (vỏ nứt/móp; bản lề lỏng/
# kêu-kẹt, viền nứt, màn trầy; phím bong, touchpad trầy; cổng móp, dây sạc sứt;
# pin phồng, thiếu ốc/tem rách/dấu mở máy, ố nước/rỉ sét; trầy nhẹ/nặng), trang
# cuộn được (ScrollViewer) vì nhiều ô. Chỉ bản lề lỏng và trầy nhẹ có câu riêng
# trong Phrasings.json; các lỗi còn lại dùng câu cảnh báo chung qua verdict Cảnh
# báo, không bịa câu riêng. Pin phồng là ngoại lệ duy nhất chặn cho qua (Chan,
# giống ổ bị chặn và sạc Kém) vì nguy hiểm; Tổng kết khi đó về câu nghiêm trọng
# chung (combinedD) vì chưa có câu riêng cho pin phồng. Trang Sức khỏe đã thêm
# mô tả tiếng Việt cho 13 mã tín hiệu ngoại quan mới.
