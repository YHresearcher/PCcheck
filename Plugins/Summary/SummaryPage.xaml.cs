using System.IO;
using System.Text;
using System.Text.Json;
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
    List<ModuleRow> _lastRows = [];
    string _lastOverallTitle = "";

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

    /// <summary>Lưu hồ sơ phiên này: 1 máy = 1 thư mục riêng, không ghi đè.
    /// Thư mục gốc mặc định là thư mục app đang chạy (bản portable = USB), tên
    /// <S/N>_<yyyyMMdd_HHmmss>; cùng S/N test lại trong cùng giây thì thêm _2, _3...
    /// Nếu thư mục app ghi không được (USB chỉ đọc, Program Files không quyền) thì
    /// chuyển sang %APPDATA%\YanJi\KetQua\ và báo rõ trong SaveStatusText, không
    /// im lặng lưu chỗ khác. File trong thư mục: BaoCao.txt (nhận xét + bằng chứng),
    /// KetQua.json (máy đọc được), KetQuaDoO.json (bản sao kết quả đo ổ mới nhất,
    /// nếu có — vì file gốc trong %APPDATA% sẽ bị máy sau ghi đè). Chỉ mục chung
    /// KetQua\DanhSach.csv thêm 1 dòng/phiên để Báo cáo &amp; Lịch sử mở lại được.
    /// Mỗi phiên chỉ gồm chữ + số nên &lt; 100 KB, USB 8 GB không phải giới hạn.</summary>
    void SaveSessionButton_Click(object sender, RoutedEventArgs e)
    {
        Refresh();
        string snRaw = SerialBox.Text.Trim();
        string sn = SanitizeFileName(snRaw);
        if (sn.Length == 0) sn = "KhongCoSN";
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        string phrasing = PhrasingText.Text;
        string overall = _lastOverallTitle.Length > 0 ? _lastOverallTitle : "CHƯA TEST XONG";

        string ketQuaDir;
        string fallbackNote = "";
        try
        {
            ketQuaDir = Path.Combine(AppContext.BaseDirectory, "KetQua");
            Directory.CreateDirectory(ketQuaDir);
        }
        catch
        {
            ketQuaDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YanJi", "KetQua");
            fallbackNote = " (thư mục app ghi không được, đã lưu vào %APPDATA%\\YanJi\\KetQua\\)";
        }

        string folderName = $"{sn}_{timestamp}";
        string folder = Path.Combine(ketQuaDir, folderName);
        for (int i = 2; Directory.Exists(folder); i++)
        {
            folderName = $"{sn}_{timestamp}_{i}";
            folder = Path.Combine(ketQuaDir, folderName);
        }

        try
        {
            Directory.CreateDirectory(folder);
            string diskJson = ReadDiskJson(out string diskLine);
            var txt = new StringBuilder();
            txt.AppendLine("BÁO CÁO KIỂM TRA MÁY — Vi Tính An Tâm");
            txt.AppendLine($"S/N: {(snRaw.Length > 0 ? snRaw : "Chưa nhập")}");
            txt.AppendLine($"Thời gian lưu: {savedAt}");
            txt.AppendLine($"Kết luận: {overall}" + (_markedLoai ? " (kỹ thuật viên đánh dấu loại)" : ""));
            txt.AppendLine($"Nhận xét: {phrasing}");
            txt.AppendLine();
            txt.AppendLine("Kết luận từng module:");
            foreach (var r in _lastRows)
                txt.AppendLine($"- {r.Name}: {r.Grade} — {r.Note}");
            txt.AppendLine();
            txt.AppendLine("Bằng chứng kỹ thuật:");
            txt.AppendLine(diskLine);
            txt.AppendLine("Lưu ý: số liệu SMART không đọc được thì hiện Không rõ, không được hiểu là ổ khỏe. " +
                "Bài stress ngắn đạt chỉ chứng minh máy chạy được chừng đó phút ở tải cao, không chứng minh ổn định lâu dài.");
            File.WriteAllText(Path.Combine(folder, "BaoCao.txt"), txt.ToString(), new UTF8Encoding(false));

            var json = new StringBuilder();
            json.AppendLine("{");
            json.AppendLine($"  \"sn\": {JsonSerializer.Serialize(snRaw)},");
            json.AppendLine($"  \"savedAt\": {JsonSerializer.Serialize(savedAt)},");
            json.AppendLine($"  \"overall\": {JsonSerializer.Serialize(overall)},");
            json.AppendLine($"  \"phrasing\": {JsonSerializer.Serialize(phrasing)},");
            json.AppendLine($"  \"markedLoai\": {(_markedLoai ? "true" : "false")},");
            json.AppendLine("  \"modules\": [");
            for (int i = 0; i < _lastRows.Count; i++)
            {
                var r = _lastRows[i];
                json.Append($"    {{\"name\": {JsonSerializer.Serialize(r.Name)}, \"grade\": {JsonSerializer.Serialize(r.Grade)}, \"note\": {JsonSerializer.Serialize(r.Note)}}}");
                json.AppendLine(i < _lastRows.Count - 1 ? "," : "");
            }
            json.AppendLine("  ],");
            json.AppendLine($"  \"disk\": {(diskJson.Length > 0 ? diskJson : "null")}");
            json.AppendLine("}");
            File.WriteAllText(Path.Combine(folder, "KetQua.json"), json.ToString(), new UTF8Encoding(false));

            string diskSrc = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YanJi", "disk_result", "KetQuaDoO.json");
            if (File.Exists(diskSrc))
                File.Copy(diskSrc, Path.Combine(folder, "KetQuaDoO.json"), overwrite: true);

            AppendIndex(ketQuaDir, savedAt, snRaw.Length > 0 ? snRaw : "KhongCoSN", overall, folderName);
            string freeNote = FreeSpaceNote(ketQuaDir);
            SaveStatusText.Text = $"Đã lưu hồ sơ vào {folder}{fallbackNote}.{freeNote} Lưu xong mới test máy tiếp theo, vì kết quả đo ổ của máy sau sẽ ghi đè file gốc.";
            SaveStatusText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
        }
        catch (Exception ex)
        {
            SaveStatusText.Text = $"Lưu hồ sơ thất bại: {ex.Message}. Thử kiểm tra USB còn ghi được không, rồi bấm Lưu lại.";
            SaveStatusText.SetResourceReference(TextBlock.ForegroundProperty, "ErrorBrush");
        }
    }

    static string SanitizeFileName(string s)
    {
        var sb = new StringBuilder();
        foreach (char c in s)
        {
            if (char.IsLetterOrDigit(c) || c is '-' or '_') sb.Append(c);
            else if (c is ' ') sb.Append('_');
        }
        string out_ = sb.ToString().Trim('_');
        return out_.Length > 60 ? out_[..60] : out_;
    }

    /// <summary>Đọc kết quả đo ổ mới nhất trong %APPDATA% (file này bị máy sau ghi đè
    /// nên phiên phải sao chép ngay). Trả về JSON gốc để ghi vào KetQua.json và
    /// 1 dòng tóm tắt cho BaoCao.txt; không có file thì trả về rỗng + dòng "Chưa đo".</summary>
    static string ReadDiskJson(out string line)
    {
        line = "Đo tốc độ ổ: chưa có kết quả (chưa đo, hoặc file gốc đã bị ghi đè bởi máy khác — lưu hồ sơ ngay sau khi đo xong).";
        try
        {
            string path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YanJi", "disk_result", "KetQuaDoO.json");
            if (!File.Exists(path)) return "";
            string json = File.ReadAllText(path).Trim();
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            double seqR = r.GetProperty("seq_read").GetDouble();
            double seqW = r.GetProperty("seq_write").GetDouble();
            double r4R = r.GetProperty("rnd4k_read").GetDouble();
            double r4W = r.GetProperty("rnd4k_write").GetDouble();
            line = $"Đo tốc độ ổ: đọc tuần tự {seqR:0.0} MB/s, ghi tuần tự {seqW:0.0} MB/s, " +
                $"đọc ngẫu nhiên 4K {r4R:0.0} MB/s, ghi ngẫu nhiên 4K {r4W:0.0} MB/s.";
            return json;
        }
        catch { return ""; }
    }

    static void AppendIndex(string ketQuaDir, string savedAt, string sn, string overall, string folderName)
    {
        try
        {
            string indexPath = Path.Combine(ketQuaDir, "DanhSach.csv");
            bool newFile = !File.Exists(indexPath);
            using var w = new StreamWriter(indexPath, append: true, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            if (newFile) w.WriteLine("Thời gian,S/N,Kết luận,Thư mục");
            string Cell(string s) => s.Replace(',', ';').Replace('\r', ' ').Replace('\n', ' ');
            w.WriteLine(string.Join(',', Cell(savedAt), Cell(sn), Cell(overall), Cell(folderName)));
        }
        catch { /* Chỉ mục lỗi không được làm mất hồ sơ phiên vừa lưu */ }
    }

    static string FreeSpaceNote(string dir)
    {
        try
        {
            string root = Path.GetPathRoot(Path.GetFullPath(dir)) ?? "";
            if (root.Length == 0) return "";
            var drive = new DriveInfo(root);
            double freeMB = drive.AvailableFreeSpace / 1048576.0;
            return freeMB < 100 ? $" Cảnh báo: ổ chứa hồ sơ còn {freeMB:0} MB trống." : "";
        }
        catch { return ""; }
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
        _lastRows = rows;

        if (grades.Any(g => g == ModuleVerdictLevel.Chan)) autoLevel = ModuleVerdictLevel.Chan;
        else if (grades.Any(g => g == ModuleVerdictLevel.CanhBao)) autoLevel = ModuleVerdictLevel.CanhBao;
        else if (modules.Count > 0 && grades.All(g => g == ModuleVerdictLevel.Dat)) autoLevel = ModuleVerdictLevel.Dat;
        else autoLevel = ModuleVerdictLevel.ChuaTest;

        string level = _markedLoai ? "Loai"
            : autoLevel == ModuleVerdictLevel.Chan ? "Sua"
            : autoLevel == ModuleVerdictLevel.CanhBao ? "CanhBao"
            : autoLevel == ModuleVerdictLevel.Dat ? "Dat"
            : "ChuaXong";

        _lastOverallTitle = OverallTitles[level];
        OverallText.Text = _lastOverallTitle
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
