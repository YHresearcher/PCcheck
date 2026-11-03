using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using YanJi.PluginSdk;

namespace YanJi.Plugin.Dashboard;

/// <summary>Trang Tổng quan: tiến độ + kết luận mua hiện tại + nút nhảy.
/// Kết luận mua tính đúng logic trang Tổng kết (Chặn = Không khuyến nghị mua,
/// Cảnh báo = Cần kiểm tra thêm, tất cả Đạt = Có thể mua, còn lại = Chưa đủ
/// dữ liệu), nhưng KHÔNG chọn câu nhận xét random ở đây — câu đó chỉ ở Tổng kết.
/// Nút Bắt đầu / Tiếp tục: vào wizard rồi nhảy tới bài chưa xong đầu tiên
/// (chưa xong bài nào thì là bài đầu tiên); đó chính là "tiếp tục phiên dang dở"
/// ở mức phiên test trong app (phiên đã lưu ra USB thì xem lại ở thư mục hồ sơ).</summary>
public partial class DashboardPage : UserControl
{
    readonly IHostContext _host;

    public DashboardPage(IHostContext host)
    {
        _host = host;
        InitializeComponent();
        _host.DoneChanged += Refresh;
        _host.VerdictChanged += Refresh;
        Loaded += (_, _) => Refresh();
    }

    void StartButton_Click(object sender, RoutedEventArgs e)
    {
        var passable = _host.Modules.Where(m => !m.Terminal).OrderBy(m => m.Order).ToList();
        // Đã xong hết thì nút này chỉ là lối tắt tới Tổng kết, không vào wizard nữa.
        // Dùng 1 handler duy nhất quyết định theo trạng thái lúc bấm, không đổi handler
        // theo Refresh (đổi handler theo Refresh sẽ gắn trùng handler khi Refresh chạy nhiều lần).
        if (passable.Count > 0 && passable.All(m => _host.IsDone(m.Id)))
        {
            _host.NavigateTo("summary");
            return;
        }
        string target = passable.FirstOrDefault(m => !_host.IsDone(m.Id))?.Id
            ?? passable.FirstOrDefault()?.Id ?? "summary";
        _host.StartWizard();
        if (!_host.NavigateTo(target))
        {
            // Không nhảy được (module bị xóa lúc chạy) thì thoát wizard để khỏi kẹt
            _host.ExitWizard();
        }
    }

    void HardwareButton_Click(object sender, RoutedEventArgs e) => _host.NavigateTo("config");
    void HealthButton_Click(object sender, RoutedEventArgs e) => _host.NavigateTo("health");
    void SummaryButton_Click(object sender, RoutedEventArgs e) => _host.NavigateTo("summary");

    void SettingsButton_Click(object sender, RoutedEventArgs e) => _host.NavigateTo("settings");

    void ReportsButton_Click(object sender, RoutedEventArgs e)
    {
        string dir = YanJi.PluginSdk.Services.AppSettings.GetKetQuaDir();
        if (!Directory.Exists(dir))
            dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YanJi", "KetQua");
        try
        {
            Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không mở được thư mục hồ sơ ({dir}): {ex.Message}",
                "Mở thư mục hồ sơ", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    void Refresh()
    {
        var passable = _host.Modules.Where(m => !m.Terminal).OrderBy(m => m.Order).ToList();
        int done = passable.Count(m => _host.IsDone(m.Id));
        int total = passable.Count;
        ProgressText.Text = total > 0
            ? $"Tiến độ kiểm tra: đã xong {done}/{total} bài test."
            : "Chưa có bài test nào được cài.";

        ModuleVerdictLevel autoLevel;
        var grades = new List<ModuleVerdictLevel>();
        string reason = "";
        foreach (var m in passable)
        {
            ModuleVerdictLevel grade;
            string note;
            if (_host.Verdicts.TryGetValue(m.Id, out var v)) { grade = v.Level; note = v.Note; }
            else if (_host.IsDone(m.Id)) { grade = ModuleVerdictLevel.Dat; note = "Đã cho qua"; }
            else { grade = ModuleVerdictLevel.ChuaTest; note = "Chưa cho qua"; }
            grades.Add(grade);
            if (grade == ModuleVerdictLevel.Chan && reason.Length == 0) reason = note;
            else if (grade == ModuleVerdictLevel.CanhBao && reason.Length == 0) reason = note;
        }
        if (grades.Any(g => g == ModuleVerdictLevel.Chan)) autoLevel = ModuleVerdictLevel.Chan;
        else if (grades.Any(g => g == ModuleVerdictLevel.CanhBao)) autoLevel = ModuleVerdictLevel.CanhBao;
        else if (total > 0 && grades.All(g => g == ModuleVerdictLevel.Dat)) autoLevel = ModuleVerdictLevel.Dat;
        else autoLevel = ModuleVerdictLevel.ChuaTest;

        (OverallText.Text, OverallText.Foreground) = autoLevel switch
        {
            ModuleVerdictLevel.Chan => ("KHÔNG KHUYẾN NGHỊ MUA", Application.Current?.TryFindResource("ErrorBrush") as Brush ?? Brushes.Red),
            ModuleVerdictLevel.CanhBao => ("CẦN KIỂM TRA THÊM / THƯƠNG LƯỢNG", Application.Current?.TryFindResource("AccentBrush") as Brush ?? Brushes.Orange),
            ModuleVerdictLevel.Dat => ("CÓ THỂ MUA — MUA ĐƯỢC", Brushes.ForestGreen),
            _ => ("CHƯA ĐỦ DỮ LIỆU — CHƯA KẾT LUẬN ĐƯỢC", Application.Current?.TryFindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray),
        };
        ReasonText.Text = autoLevel switch
        {
            ModuleVerdictLevel.Chan => $"Lý do chặn đầu tiên: {reason}",
            ModuleVerdictLevel.CanhBao => $"Cảnh báo đầu tiên: {reason}",
            ModuleVerdictLevel.Dat => "Tất cả bài test đã qua, chưa phát hiện vấn đề đáng kể. Câu nhận xét thân thiện đầy đủ ở trang Tổng kết.",
            _ => "Còn bài test chưa xong, hoặc phiên bị dừng giữa chừng — chưa đủ dữ liệu để kết luận mua hay không.",
        };
        StartButton.Content = done == 0 ? "Bắt đầu kiểm tra máy"
            : done < total ? $"Tiếp tục phiên dang dở (còn {total - done} bài)"
            : "Xem kết luận mua máy";
    }
}
