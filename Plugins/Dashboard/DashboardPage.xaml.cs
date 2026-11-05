using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Collections.Generic;
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

    void ShopButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://vitinhantam.com") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Chưa mở được website: {ex.Message}", "Tham quan shop",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    void StopButton_Click(object sender, RoutedEventArgs e)
    {
        var survivors = _host.StopAllHeavyTests();
        if (survivors.Count == 0)
            MessageBox.Show("Đã dừng các bài kiểm tra tải nặng. Bạn vẫn có thể tiếp tục kiểm tra thủ công nha!",
                "An Tâm PC check", MessageBoxButton.OK, MessageBoxImage.Information);
        else
            MessageBox.Show($"Còn tiến trình chưa dừng: {string.Join(", ", survivors)}. Hãy kiểm tra Task Manager nhé.",
                "Cần kiểm tra thêm", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    public sealed record TestLine(string Icon, string Name, string Status, Brush StatusBrush);

    static string IconFor(string id) => id.ToLowerInvariant() switch
    {
        "config" => "▣", "keyboard" => "⌨", "screen" => "▤",
        "av" => "♫", "stress" => "◉", "port" => "⌁", "visual" => "◈",
        "disk" => "▥", _ => "○"
    };

    void HardwareButton_Click(object sender, RoutedEventArgs e) => _host.NavigateTo("config");
    void HealthButton_Click(object sender, RoutedEventArgs e) => _host.NavigateTo("health");
    void SummaryButton_Click(object sender, RoutedEventArgs e) => _host.NavigateTo("summary");

    void ReportsButton_Click(object sender, RoutedEventArgs e)
    {
        string dir = YanJi.PluginSdk.Services.AppSettings.GetKetQuaDir();
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
            ? $"{done}/{total} mục đã xong"
            : "Chưa có mục kiểm tra";
        ProgressBar.Value = total > 0 ? 100.0 * done / total : 0;
        var tests = new List<TestLine>();
        foreach (var m in passable)
        {
            var grade = _host.Verdicts.TryGetValue(m.Id, out var v) ? v.Level
                : _host.IsDone(m.Id) ? ModuleVerdictLevel.Dat : ModuleVerdictLevel.ChuaTest;
            string label = grade switch
            {
                ModuleVerdictLevel.Dat => "Đạt",
                ModuleVerdictLevel.CanhBao => "Cảnh báo",
                ModuleVerdictLevel.Chan => "Có lỗi",
                _ => "Chưa thử"
            };
            Brush color = grade switch
            {
                ModuleVerdictLevel.Dat => Brushes.ForestGreen,
                ModuleVerdictLevel.Chan => Application.Current?.TryFindResource("ErrorBrush") as Brush ?? Brushes.Red,
                ModuleVerdictLevel.CanhBao => Application.Current?.TryFindResource("AccentBrush") as Brush ?? Brushes.Orange,
                _ => Application.Current?.TryFindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray
            };
            tests.Add(new TestLine(IconFor(m.Id), m.Name, label, color));
        }
        QuickTestList.ItemsSource = tests;
        StorageText.Text = _host.DriveSummaries.Count > 0
            ? $"Ổ đĩa: {_host.DriveSummaries.Count} ổ được ghi nhận • Xem thông tin chi tiết"
            : "SSD / HDD: Chưa có dữ liệu kiểm tra";

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
        StartCaption.Text = done == 0 ? "BẮT ĐẦU"
            : done < total ? "TIẾP TỤC"
            : "XEM TỔNG KẾT";
    }
}
