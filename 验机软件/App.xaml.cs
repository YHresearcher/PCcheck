using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using YanJi.PluginSdk;
using YanJi.Views;

namespace YanJi;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // 插件待处理操作（上次会话排队的删除/覆盖导入）：必须在插件加载前执行，此时无文件锁
        Services.PluginPackageService.ProcessPendingOps();
        // 在窗口创建前加载主题字典（读取设置或跟随系统）——SplashWindow dùng luôn theme này
        Services.ThemeManager.ApplyInitial();
        // Từ điển chuỗi ngôn ngữ (VI/EN): nạp trước khi tạo MainWindow để XAML
        // dùng {DynamicResource Key} có chuỗi ngay từ đầu, kể cả SplashWindow.
        Services.LanguageManager.ApplyInitial();
        // Màn hình chờ logo/thương hiệu: hiện trước khi tạo MainWindow (lúc này
        // MainWindow sẽ quét + tải 7 module), đóng sau khi MainWindow tải xong
        // và đủ tối thiểu 1,5 giây để khỏi chớp tắt khi máy khởi động nhanh.
        var splash = new SplashWindow();
        var splashWatch = Stopwatch.StartNew();
        splash.Show();
        MainWindow main;
        try
        {
            main = new MainWindow();
        }
        catch
        {
            splash.Close();
            throw;
        }
        MainWindow = main;
        // SỬA LỖI TREO SPLASH (2026-11-03): Loaded có thể chạy ngay trong Show(),
        // nên phải gắn handler TRƯỚC Show() — gắn sau Show() sẽ bỏ lỡ Loaded,
        // splash Topmost ở lại mãi trên cùng, nhìn như app treo ở splash.
        // Hàm đóng dùng chung + cờ chống đóng 2 lần, và kiểm tra IsLoaded sau
        // Show() để bắt luôn trường hợp cửa sổ đã load sẵn trong Show().
        bool splashClosed = false;
        void CloseSplash()
        {
            if (splashClosed) return;
            splashClosed = true;
            long remaining = 1500 - splashWatch.ElapsedMilliseconds;
            if (remaining <= 0)
            {
                splash.Close();
                main.Activate();
                return;
            }
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(remaining) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                splash.Close();
                main.Activate();
            };
            timer.Start();
        }
        main.Loaded += (_, _) => CloseSplash();
        main.Show();
        if (main.IsLoaded) CloseSplash();
        DispatcherUnhandledException += (_, args) =>
        {
            // 完整异常（含内部异常与堆栈）写入日志，便于诊断；弹窗只显示外层消息会丢失根因
            string logPath = Path.Combine(Path.GetTempPath(), "YanJi-crash.log");
            try { File.WriteAllText(logPath, args.Exception.ToString()); }
            catch { /* 日志失败不致命 */ }
            Dialogs.Error(null, args.Exception.Message + $"\n\nNhật ký chi tiết: {logPath}");
            args.Handled = true;
        };
    }
}
