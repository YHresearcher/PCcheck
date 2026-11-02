using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
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
        main.Show();
        // Loaded chỉ chạy khi UI thread rảnh lại sau OnStartup, nên gắn handler
        // sau Show() vẫn bắt được; đừng chuyển thành tạo MainWindow sau khi đóng splash.
        main.Loaded += (_, _) =>
        {
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
        };
        DispatcherUnhandledException += (_, args) =>
        {
            // 完整异常（含内部异常与堆栈）写入日志，便于诊断；弹窗只显示外层消息会丢失根因
            string logPath = Path.Combine(Path.GetTempPath(), "YanJi-crash.log");
            try { File.WriteAllText(logPath, args.Exception.ToString()); }
            catch { /* 日志失败不致命 */ }
            MessageBox.Show(args.Exception.Message + $"\n\nNhật ký chi tiết: {logPath}", "Lỗi chưa được xử lý",
                MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
    }
}
