using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using YanJi.PluginSdk;

namespace YanJi.Plugin.Summary;

/// <summary>Trang Tổng kết: kết luận từng module + kết luận tổng, mỗi kết luận tổng
/// có nhiều cách nói thân thiện chọn ngẫu nhiên. Cách nói chỉ đổi câu chữ,
/// không đổi kết luận — bấm "Đổi cách nói" để xem câu khác, kết luận giữ nguyên.
/// Kết luận tổng tự động: có module Chặn = SỬA RỒI TEST LẠI; không Chặn nhưng có
/// Cảnh báo = BÁN CÓ ĐIỀU KIỆN; tất cả Đạt = ĐẠT; còn lại = CHƯA TEST XONG.
/// LOẠI không tự sinh ra (test không biết chi phí sửa có đáng hay không) nên
/// kỹ thuật viên tự đánh dấu bằng nút bên dưới; bỏ đánh dấu thì về lại kết luận tự động.</summary>
public partial class SummaryPage : UserControl
{
    readonly IHostContext _host;
    readonly Random _random = new();
    bool _markedLoai;
    string _currentLevel = "";
    int _currentPhrasingIndex = -1;

    /// <summary>1 dòng kết luận từng module hiển thị trong danh sách.</summary>
    public record ModuleRow(string Name, string Grade, string Note);

    /// <summary>Cách nói thân thiện cho mỗi kết luận tổng — mỗi mức 5 câu, app chọn
    /// ngẫu nhiên 1 câu, bấm "Đổi cách nói" thì đổi sang câu khác cùng mức.
    /// Giọng shop thật, câu ngắn, không dùng từ quảng cáo, không hứa gì thêm
    /// ngoài kết luận (ví dụ câu ĐẠT không tự thêm bảo hành, bảo hành đã có ở slogan chung).</summary>
    static readonly Dictionary<string, string[]> Phrasings = new()
    {
        ["Dat"] =
        [
            "Máy này ngon rồi, bán được ngay, khỏi lăn tăn gì thêm.",
            "Test xong hết, máy chạy ổn, giao khách được rồi đó.",
            "Mọi mục đều đạt, máy này yên tâm bán.",
            "Xong rồi, máy sạch, bán thôi.",
            "Máy này đạt chuẩn An Tâm, lên kệ được rồi.",
        ],
        ["CanhBao"] =
        [
            "Máy dùng được, nhưng có chỗ cần nói rõ với khách trước khi bán.",
            "Bán được, nhớ ghi rõ điều kiện vào đơn, đừng để khách bất ngờ.",
            "Máy này bán có điều kiện, xem lại phần cảnh báo bên dưới rồi hãy giao.",
            "Chưa phải máy hoàn hảo, nhưng bán được nếu khách đồng ý điều kiện.",
            "Có 1-2 chỗ chưa ngon hẳn, bán thì phải nói thật với khách.",
        ],
        ["Sua"] =
        [
            "Máy này chưa bán được, sửa chỗ bị chặn xong rồi test lại từ đầu nhé.",
            "Có mục bị chặn, thay linh kiện rồi chạy lại test, đừng giao vội.",
            "Sửa xong rồi test lại, máy này còn cứu được.",
            "Chặn ở vài chỗ, sửa rồi kiểm lại là bán được.",
            "Đừng bán máy này vội, sửa trước đã.",
        ],
        ["Loai"] =
        [
            "Máy này thôi, đừng cố bán, loại ra cho khỏe.",
            "Máy này không nên bán, giữ lại làm linh kiện hoặc loại hẳn.",
            "Loại máy này đi, bán ra chỉ thêm rắc rối.",
            "Máy này không đạt chuẩn An Tâm, loại.",
            "Thôi bỏ máy này, tìm máy khác ngon hơn cho khách.",
        ],
        ["ChuaXong"] =
        [
            "Chưa test xong đâu, còn mục chưa xong ở trên, làm nốt rồi hãy kết luận.",
            "Mới test được vài mục, chạy nốt các mục còn lại đi.",
            "Chưa đủ dữ liệu để kết luận, test tiếp nhé.",
            "Còn mục chưa xong, kết luận vội là sai đó.",
            "Test chưa xong, quay lại các mục còn thiếu rồi hãy xem tổng kết.",
        ],
    };

    static readonly Dictionary<string, string> OverallTitles = new()
    {
        ["Dat"] = "ĐẠT – BÁN ĐƯỢC NGAY",
        ["CanhBao"] = "BÁN CÓ ĐIỀU KIỆN",
        ["Sua"] = "SỬA RỒI TEST LẠI",
        ["Loai"] = "LOẠI – KHÔNG BÁN MÁY NÀY",
        ["ChuaXong"] = "CHƯA TEST XONG",
    };

    public SummaryPage(IHostContext host)
    {
        _host = host;
        InitializeComponent();
        _host.DoneChanged += Refresh;
        _host.VerdictChanged += Refresh;
        Loaded += (_, _) => Refresh();
    }

    void ChangePhrasingButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentLevel.Length == 0) Refresh();
        else PhrasingText.Text = PickPhrasing(_currentLevel, _currentPhrasingIndex);
    }

    void MarkLoaiButton_Click(object sender, RoutedEventArgs e)
    {
        _markedLoai = !_markedLoai;
        Refresh();
    }

    string PickPhrasing(string level, int excludeIndex)
    {
        var pool = Phrasings[level];
        if (pool.Length == 1) { _currentPhrasingIndex = 0; return pool[0]; }
        int idx;
        do { idx = _random.Next(pool.Length); } while (idx == excludeIndex);
        _currentPhrasingIndex = idx;
        return pool[idx];
    }

    void Refresh()
    {
        var rows = new List<ModuleRow>();
        ModuleVerdictLevel autoLevel;
        var modules = _host.Modules.Where(m => !m.Terminal).OrderBy(m => m.Order).ToList();
        var grades = new List<ModuleVerdictLevel>();
        foreach (var m in modules)
        {
            ModuleVerdictLevel grade;
            string note;
            if (_host.Verdicts.TryGetValue(m.Id, out var v))
            {
                grade = v.Level;
                note = v.Note;
            }
            else if (_host.IsDone(m.Id))
            {
                grade = ModuleVerdictLevel.Dat;
                note = "Đã cho qua";
            }
            else
            {
                grade = ModuleVerdictLevel.ChuaTest;
                note = "Chưa cho qua";
            }
            grades.Add(grade);
            rows.Add(new(m.Name, GradeText(grade), note));
        }
        ModuleList.ItemsSource = rows;

        if (grades.Any(g => g == ModuleVerdictLevel.Chan)) autoLevel = ModuleVerdictLevel.Chan;
        else if (grades.Any(g => g == ModuleVerdictLevel.CanhBao)) autoLevel = ModuleVerdictLevel.CanhBao;
        else if (modules.Count > 0 && grades.All(g => g == ModuleVerdictLevel.Dat)) autoLevel = ModuleVerdictLevel.Dat;
        else autoLevel = ModuleVerdictLevel.ChuaTest;

        string level = _markedLoai ? "Loai"
            : autoLevel == ModuleVerdictLevel.Chan ? "Sua"
            : autoLevel == ModuleVerdictLevel.CanhBao ? "CanhBao"
            : autoLevel == ModuleVerdictLevel.Dat ? "Dat"
            : "ChuaXong";

        OverallText.Text = OverallTitles[level]
            + (_markedLoai ? $" (kỹ thuật viên đánh dấu; kết luận tự động là {OverallTitles[LevelKey(autoLevel)]})" : "");
        if (level == "Dat") OverallText.Foreground = Brushes.ForestGreen;
        else if (level == "Sua" || level == "Loai") OverallText.SetResourceReference(TextBlock.ForegroundProperty, "ErrorBrush");
        else if (level == "CanhBao") OverallText.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
        else OverallText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");

        if (level != _currentLevel)
        {
            _currentLevel = level;
            PhrasingText.Text = PickPhrasing(level, -1);
        }
        else if (_currentPhrasingIndex < 0)
        {
            PhrasingText.Text = PickPhrasing(level, -1);
        }
        MarkLoaiButton.Content = _markedLoai ? "Bỏ đánh dấu loại" : "Đánh dấu loại máy này";
    }

    static string LevelKey(ModuleVerdictLevel level) => level switch
    {
        ModuleVerdictLevel.Dat => "Dat",
        ModuleVerdictLevel.CanhBao => "CanhBao",
        ModuleVerdictLevel.Chan => "Sua",
        _ => "ChuaXong",
    };

    static string GradeText(ModuleVerdictLevel level) => level switch
    {
        ModuleVerdictLevel.Dat => "Đạt",
        ModuleVerdictLevel.CanhBao => "Cảnh báo",
        ModuleVerdictLevel.Chan => "Chặn",
        _ => "Chưa test xong",
    };
}
