using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using YanJi.PluginSdk;

namespace YanJi.Plugin.Summary;

/// <summary>Trang Tổng kết = khu vực kết luận mua máy: xác định máy MUA ĐƯỢC HAY
/// KHÔNG, cho cả kỹ thuật viên và người mua dùng chung — người mua chỉ cần đọc
/// tiêu đề + câu thân thiện ở trên, kỹ thuật viên đọc chi tiết từng module ở dưới.
/// Mỗi kết luận tổng có nhiều cách nói thân thiện chọn ngẫu nhiên. Cách nói chỉ
/// đổi câu chữ, không đổi kết luận — bấm "Đổi cách nói" để xem câu khác, kết
/// luận giữ nguyên. Kết luận tổng tự động: có module Chặn = KHÔNG KHUYẾN NGHỊ
/// MUA; không Chặn nhưng có Cảnh báo = CẦN KIỂM TRA THÊM / THƯƠNG LƯỢNG; tất cả
/// Đạt = CÓ THỂ MUA; còn lại = CHƯA ĐỦ DỮ LIỆU. KHÔNG MUA không tự sinh ra (test
/// không biết chi phí sửa có đáng hay không) nên kỹ thuật viên tự đánh dấu bằng
/// nút bên dưới; bỏ đánh dấu thì về lại kết luận tự động.</summary>
public partial class SummaryPage : UserControl
{
    readonly IHostContext _host;
    readonly Random _random = new();
    readonly PhrasingBank _bank = new();
    bool _markedLoai;
    string _currentScenario = "";
    int _currentPhrasingIndex = -1;
    int _currentAnTamIndex = -1;
    List<ModuleRow> _lastRows = [];
    string _lastOverallTitle = "";
    string _lastAnTamNote = "";

    /// <summary>1 dòng kết luận từng module hiển thị trong danh sách.</summary>
    public record ModuleRow(string Name, string Grade, string Note);

    /// <summary>Kịch bản kết luận (pool câu lấy từ Phrasings.json qua PhrasingBank,
    /// không viết cố định trong C#). Thứ tự ưu tiên khi chọn kịch bản:
    /// lỗi nghiêm trọng → lỗi chức năng → cảnh báo linh kiện → thiếu dữ liệu → máy đạt.
    /// Thiếu dữ liệu quan trọng thì không được xếp vào nhóm tốt chỉ vì không phát hiện
    /// lỗi — nên bất kỳ module nào chưa test xong cũng về kịch bản "incomplete",
    /// không về "good". Kịch bản nào có tín hiệu thì dùng câu chuyên biệt của nó
    /// (ổ media / ổ sector / ổ hao mòn / ổ SMART chưa rõ); chưa có tín hiệu thì
    /// dùng câu chung của mức đó (warning / serious / incomplete / good), không đoán.
    /// Dòng "An Tâm nhắn bạn" chỉ đi kèm kịch bản good và warning, không đi kèm
    /// serious / incomplete / notBuy — để không làm nhẹ cảnh báo nghiêm trọng.</summary>
    static readonly Dictionary<string, string> OverallTitles = new()
    {
        ["Dat"] = "CÓ THỂ MUA — MUA ĐƯỢC",
        ["CanhBao"] = "CẦN KIỂM TRA THÊM / THƯƠNG LƯỢNG",
        ["Sua"] = "KHÔNG KHUYẾN NGHỊ MUA",
        ["Loai"] = "KHÔNG MUA MÁY NÀY",
        ["ChuaXong"] = "CHƯA ĐỦ DỮ LIỆU — CHƯA KẾT LUẬN ĐƯỢC",
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
        if (_currentScenario.Length == 0) Refresh();
        else
        {
            PhrasingText.Text = PickPhrasing(_currentScenario, _currentPhrasingIndex);
            RefreshAnTamNote(exclude: _currentAnTamIndex);
        }
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
        // Câu đã chọn được lưu cố định vào báo cáo: mở lại BaoCao.txt / KetQua.json
        // sẽ thấy đúng câu này, không random lại — vì báo cáo là file, không tự sinh lại.
        string anTamNote = _lastAnTamNote;
        string scenario = _currentScenario.Length > 0 ? _currentScenario : "incomplete";
        string overall = _lastOverallTitle.Length > 0 ? _lastOverallTitle : "CHƯA TEST XONG";

        string ketQuaDir;
        string fallbackNote = "";
        try
        {
            // Đường dẫn hồ sơ theo Cài đặt (UsbPath nếu có, không thì thư mục app)
            ketQuaDir = YanJi.PluginSdk.Services.AppSettings.GetKetQuaDir();
            Directory.CreateDirectory(ketQuaDir);
        }
        catch
        {
            ketQuaDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YanJi", "KetQua");
            fallbackNote = " (đường dẫn hồ sơ trong Cài đặt ghi không được, đã lưu vào %APPDATA%\\YanJi\\KetQua\\)";
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
            txt.AppendLine();
            txt.AppendLine("Phần 1 — Dành cho người mua (đọc là hiểu: máy mua được hay không):");
            txt.AppendLine($"Kết luận mua máy: {overall}" + (_markedLoai ? " (kỹ thuật viên đánh dấu không mua)" : ""));
            txt.AppendLine($"Nhận xét: {phrasing}");
            if (anTamNote.Length > 0)
                txt.AppendLine($"An Tâm nhắn bạn: {anTamNote}");
            txt.AppendLine();
            txt.AppendLine("Phần 2 — Dành cho kỹ thuật viên (bằng chứng kỹ thuật, vì sao ra kết luận trên):");
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
            json.AppendLine($"  \"scenario\": {JsonSerializer.Serialize(scenario)},");
            json.AppendLine($"  \"anTamNote\": {JsonSerializer.Serialize(anTamNote)},");
            json.AppendLine($"  \"phrasingLocked\": true,");
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

    string PickPhrasing(string scenario, int excludeIndex)
    {
        var pool = PoolForScenario(scenario);
        if (pool.Length == 0) return "";
        if (pool.Length == 1) { _currentPhrasingIndex = 0; return pool[0]; }
        int idx;
        do { idx = _random.Next(pool.Length); } while (idx == excludeIndex);
        _currentPhrasingIndex = idx;
        return pool[idx];
    }

    /// <summary>Pool câu cho 1 kịch bản. good = câu tốt (5 mẫu) + kết hợp A (1 câu);
    /// incomplete = kết hợp E + 5 câu chưa xong chung; serious = kết hợp D (1 câu,
    /// cố tình không trộn câu "sửa rồi test lại" để không làm nhẹ cảnh báo);
    /// warning / drive* / notBuy lấy đúng pool trong JSON.</summary>
    string[] PoolForScenario(string scenario) => scenario switch
    {
        "good" => [.. _bank.Good, .. _bank.CombinedA],
        "driveMediaError" => _bank.DriveMediaError,
        "driveBadSector" => _bank.DriveBadSector,
        "driveWear" => _bank.DriveWear,
        "driveSmartUnknown" => _bank.DriveSmartUnknown,
        "serious" => _bank.CombinedD,
        "warning" => _bank.Warning,
        "incomplete" => [.. _bank.CombinedE, .. _bank.Incomplete],
        "notBuy" => _bank.NotBuy,
        _ => _bank.Incomplete,
    };

    /// <summary>Dòng "An Tâm nhắn bạn": chỉ cho kịch bản good và warning. Khi không
    /// được hiện thì xóa text, ẩn TextBlock, và _lastAnTamNote = "" để báo cáo
    /// lưu cũng không có dòng này (không lưu 1 dòng nhắn nhủ vào báo cáo lỗi
    /// nghiêm trọng rồi làm nhẹ cảnh báo khi mở lại).</summary>
    void RefreshAnTamNote(int exclude = -1)
    {
        bool allowed = _currentScenario is "good" or "warning";
        if (!allowed || _bank.AnTamNotes.Length == 0)
        {
            _lastAnTamNote = "";
            AnTamNoteText.Text = "";
            AnTamNoteText.Visibility = Visibility.Collapsed;
            return;
        }
        var pool = _bank.AnTamNotes;
        int idx;
        if (pool.Length == 1) idx = 0;
        else do { idx = _random.Next(pool.Length); } while (idx == exclude);
        _currentAnTamIndex = idx;
        _lastAnTamNote = pool[idx];
        AnTamNoteText.Text = $"An Tâm nhắn bạn: {_lastAnTamNote}";
        AnTamNoteText.Visibility = Visibility.Visible;
    }

    void Refresh()
    {
        var rows = new List<ModuleRow>();
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

        // Chọn kịch bản theo thứ tự ưu tiên: lỗi nghiêm trọng → lỗi chức năng →
        // cảnh báo linh kiện → thiếu dữ liệu → máy đạt. Lỗi chức năng (loa rè, phím
        // liệt, điểm chết...) đã có câu trong JSON nhưng CHƯA được chọn ở đây, vì
        // các module đó hiện chỉ báo "cho qua / chưa cho qua", không báo "lỗi chức
        // năng" — khi nào module báo được lỗi chức năng thì thêm 1 nhánh ở đây,
        // trước nhánh cảnh báo, và không được đoán lỗi chức năng từ "chưa cho qua".
        // Thiếu dữ liệu quan trọng thì không được xếp tốt: bất kỳ module nào chưa
        // test xong cũng về "incomplete", không về "good", kể cả khi không có lỗi nào.
        string configNote = _host.Verdicts.TryGetValue("config", out var cv) ? cv.Note : "";
        string autoTitleKey;
        string autoScenario;
        if (grades.Any(g => g == ModuleVerdictLevel.Chan))
        {
            autoTitleKey = "Sua";
            autoScenario = configNote.Contains("lỗi toàn vẹn dữ liệu") ? "driveMediaError"
                : configNote.Contains("sector") ? "driveBadSector"
                : "serious";
        }
        else if (grades.Any(g => g == ModuleVerdictLevel.CanhBao))
        {
            autoTitleKey = "CanhBao";
            autoScenario = configNote.Contains("chưa rõ sức khỏe") || configNote.Contains("không đọc được lỗi nghiêm trọng")
                ? "driveSmartUnknown"
                : configNote.Contains("sức khỏe") ? "driveWear"
                : "warning";
        }
        else if (modules.Count > 0 && grades.All(g => g == ModuleVerdictLevel.Dat))
        {
            autoTitleKey = "Dat";
            autoScenario = "good";
        }
        else
        {
            autoTitleKey = "ChuaXong";
            autoScenario = "incomplete";
        }

        string titleKey = _markedLoai ? "Loai" : autoTitleKey;
        string scenario = _markedLoai ? "notBuy" : autoScenario;

        _lastOverallTitle = OverallTitles[titleKey];
        OverallText.Text = _lastOverallTitle
            + (_markedLoai ? $" (kỹ thuật viên đánh dấu; kết luận tự động là {OverallTitles[autoTitleKey]})" : "");
        if (titleKey == "Dat") OverallText.Foreground = Brushes.ForestGreen;
        else if (titleKey == "Sua" || titleKey == "Loai") OverallText.SetResourceReference(TextBlock.ForegroundProperty, "ErrorBrush");
        else if (titleKey == "CanhBao") OverallText.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
        else OverallText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");

        // Câu chỉ random lại khi kịch bản đổi, không random lại mỗi lần Refresh —
        // để câu đang hiện không tự nhảy sang câu khác khi 1 module báo cáo lại.
        if (scenario != _currentScenario)
        {
            _currentScenario = scenario;
            PhrasingText.Text = PickPhrasing(scenario, -1);
            RefreshAnTamNote();
        }
        else if (_currentPhrasingIndex < 0)
        {
            PhrasingText.Text = PickPhrasing(scenario, -1);
            RefreshAnTamNote();
        }
        MarkLoaiButton.Content = _markedLoai ? "Bỏ đánh dấu không mua" : "Đánh dấu không mua máy này";
    }

    static string GradeText(ModuleVerdictLevel level) => level switch
    {
        ModuleVerdictLevel.Dat => "Đạt",
        ModuleVerdictLevel.CanhBao => "Cảnh báo",
        ModuleVerdictLevel.Chan => "Chặn",
        _ => "Chưa test xong",
    };
}
