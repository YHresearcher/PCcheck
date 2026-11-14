using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using YanJi.PluginSdk;
using YanJi.PluginSdk.Services;

namespace YanJi.Plugin.Summary;

/// <summary>Trang Tổng kết = khu vực kết luận mua máy: xác định máy MUA ĐƯỢC HAY
/// KHÔNG, cho cả kỹ thuật viên và người mua dùng chung — người mua chỉ cần đọc
/// tiêu đề + câu thân thiện ở trên, kỹ thuật viên đọc chi tiết từng module ở dưới.
/// Mỗi kịch bản có nhiều cách nói thân thiện, nhưng chỉ chọn ngẫu nhiên 1 lần
/// khi kịch bản đổi, rồi cố định cho tới khi kịch bản đổi tiếp hoặc lưu báo cáo
/// (không còn nút đổi câu tay — câu đã chọn là câu của phiên kiểm tra đó).
/// Kết luận tổng tự động: có module Chặn = KHÔNG KHUYẾN NGHỊ
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
    bool _serialAutoFilled; // S/N đã tự điền THÀNH CÔNG 1 lần từ SMBIOS thì không điền lại (tôn trọng chữ người dùng sửa/xóa sau đó); đọc thất bại thì lần sau mở lại trang sẽ thử lại
    string _currentScenario = "";
    int _currentPhrasingIndex = -1;
    int _currentAnTamIndex = -1;
    List<ModuleRow> _lastRows = [];
    string _lastOverallTitle = "";
    string _lastAnTamNote = "";
    string _lastAnTamLang = Lang.CurrentLanguage; // ngôn ngữ lúc câu An Tâm đang hiện được chọn
    string _lastSentenceLang = Lang.CurrentLanguage; // ngôn ngữ lúc câu kết luận đang hiện được chọn

    /// <summary>1 dòng kết luận từng module hiển thị trong danh sách.</summary>
    public record ModuleRow(string Name, string Grade, Brush GradeBrush, string Note);

    /// <summary>Màu mức kết luận trong danh sách chi tiết kiểu Mac: Đạt xanh,
    /// Cảnh báo cam, Chặn đỏ, chưa test xám.</summary>
    static Brush GradeBrushFor(ModuleVerdictLevel level) => level switch
    {
        ModuleVerdictLevel.Dat => Brushes.ForestGreen,
        ModuleVerdictLevel.CanhBao => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DE870F")),
        ModuleVerdictLevel.Chan => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D33D42")),
        _ => Brushes.Gray,
    };

    /// <summary>Kịch bản kết luận (pool câu lấy từ Phrasings.json qua PhrasingBank,
    /// không viết cố định trong C#). Thứ tự ưu tiên khi chọn kịch bản:
    /// lỗi nghiêm trọng → lỗi chức năng → cảnh báo linh kiện → thiếu dữ liệu → máy đạt.
    /// Thiếu dữ liệu quan trọng thì không được xếp vào nhóm tốt chỉ vì không phát hiện
    /// lỗi — good chỉ khi tất cả module đều Đạt và không còn tín hiệu cảnh báo nào.
    /// Kịch bản nào có tín hiệu thì dùng câu chuyên biệt của nó (ổ media / sector /
    /// hao mòn / SMART chưa rõ, pin chai, CPU quá nhiệt / lỗi, GPU artifact, phím
    /// liệt, điểm chết, loa rè, webcam, bản lề, trầy nhẹ, sạc Kém, kết hợp B);
    /// chưa có tín hiệu thì dùng câu chung của mức đó (warning / serious /
    /// incomplete / good), không đoán. Tín hiệu tốt (pin khỏe, CPU tốt) chỉ thêm
    /// câu vào pool tốt, không đổi tiêu đề. Dòng "An Tâm nhắn bạn" chia theo 5 nhóm tình trạng (máy tốt / khá / cần
    /// thương lượng / nhiều lỗi / không nên mua), chọn ngẫu nhiên 1 câu trong nhóm
    /// đúng tình trạng; chỉ ẩn khi chưa đủ dữ liệu. Các câu nghiêm trọng / không mua
    /// là câu cảnh báo / thất vọng nên không làm nhẹ cảnh báo.
    /// Tiêu đề kết luận lấy từ từ điển ngôn ngữ (Sum.Title.Dat/CanhBao/Sua/Loai/ChuaXong),
    /// không viết cố định — đổi ngữ thì tiêu đề đổi theo.</summary>
    static string OverallTitle(string key) => Lang.Get("Sum.Title." + key);

    public SummaryPage(IHostContext host)
    {
        _host = host;
        InitializeComponent();
        _host.DoneChanged += Refresh;
        _host.VerdictChanged += Refresh;
        _host.SignalChanged += Refresh;
        Lang.Changed += Refresh;
        Loaded += (_, _) => { Refresh(); FillSerialFromSmbios(); };
    }

    /// <summary>Tự điền ô S/N từ serial máy trong SMBIOS thô (cùng nguồn với nhóm
    /// Máy &amp; Mainboard, không qua WMI/registry) — kỹ thuật viên khỏi gõ tay,
    /// và tên thư mục hồ sơ sẽ đúng serial thật của máy. Chỉ điền khi ô còn trống
    /// và đọc được serial thật; đọc không được thì lần sau mở lại trang sẽ thử lại
    /// (trước đây đánh dấu đã điền ngay cả khi đọc thất bại nên không bao giờ thử
    /// lại); firmware không có serial thật (placeholder đã bị lọc thành null) thì
    /// để trống cho nhập tay; người dùng sửa/xóa sau khi điền thành công thì không
    /// điền lại, vì tem dán khác serial firmware chính là trường hợp cảnh báo không
    /// khớp cần kiểm tra tay, không được tự ghi đè.</summary>
    void FillSerialFromSmbios()
    {
        if (_serialAutoFilled || SerialBox.Text.Trim().Length > 0) return;
        string? serial = SmbiosService.Read()?.SystemSerial;
        if (serial == null) return; // Đọc không được thì lần sau mở lại trang sẽ thử lại, không khóa luôn như trước
        SerialBox.Text = serial;
        _serialAutoFilled = true;
    }

    void MarkLoaiButton_Click(object sender, RoutedEventArgs e)
    {
        _markedLoai = !_markedLoai;
        Refresh();
    }

    // Nút "Tiếp tục kiểm tra" ở thẻ tiến độ kiểu Mac: vào lại wizard, host
    // nhảy tới bài chưa xong đầu tiên theo chế độ đã chọn.
    void ContinueButton_Click(object sender, RoutedEventArgs e) => _host.BeginWizardCheck();


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
        string overall = _lastOverallTitle.Length > 0 ? _lastOverallTitle : Lang.Get("Sum.Title.ChuaXong");

        // Hồ sơ luôn lưu vào thư mục app \ KetQua (portable, tự tạo khi lưu)
        string ketQuaDir = YanJi.PluginSdk.Services.AppSettings.GetKetQuaDir();
        try
        {
            Directory.CreateDirectory(ketQuaDir);
        }
        catch (Exception ex)
        {
            SaveStatusText.Text = string.Format(Lang.Get("Sum.Save.FolderFailed"), ketQuaDir, ex.Message);
            SaveStatusText.SetResourceReference(TextBlock.ForegroundProperty, "ErrorBrush");
            return;
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
                txt.AppendLine($"An Tâm nhắn bạn:\n{anTamNote}");
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
            SaveStatusText.Text = string.Format(Lang.Get("Sum.Save.Done"), folder, freeNote);
            SaveStatusText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
        }
        catch (Exception ex)
        {
            SaveStatusText.Text = string.Format(Lang.Get("Sum.Save.Failed"), ex.Message);
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
            return freeMB < 100 ? string.Format(Lang.Get("Sum.FreeSpaceWarn"), freeMB) : "";
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

    /// <summary>Đổi ngôn ngữ: giữ nguyên câu kết luận đang hiện (cùng chỉ số
    /// trong pool song song VI/EN), chỉ đổi sang bản dịch — như cách câu An
    /// Tâm giữ chỉ số khi đổi ngôn ngữ.</summary>
    string PickPhrasingSameIndex(string scenario)
    {
        var pool = PoolForScenario(scenario);
        if (pool.Length == 0) return "";
        int idx = Math.Clamp(_currentPhrasingIndex, 0, pool.Length - 1);
        _currentPhrasingIndex = idx;
        return pool[idx];
    }

    HashSet<string> AllSignals()
    {
        var set = new HashSet<string>();
        foreach (var list in _host.Signals.Values)
            foreach (var s in list) set.Add(s);
        return set;
    }

    string[] GoodPool()
    {
        var pool = new List<string>();
        pool.AddRange(_bank.Sentences("good"));
        pool.AddRange(_bank.Sentences("combinedA"));
        var signals = AllSignals();
        if (signals.Contains("cpuGood") && _bank.SignalSentences("cpuGpu", "cpuGood") is { } cpuGood)
            pool.AddRange(cpuGood);
        if (signals.Contains("batteryHealthy") && _bank.SignalSentences("battery", "healthy") is { } batGood)
            pool.AddRange(batGood);
        return [.. pool];
    }

    /// <summary>Pool câu cho 1 kịch bản. good = câu tốt (5 mẫu) + kết hợp A (1 câu);
    /// incomplete = kết hợp E + 5 câu chưa xong chung; serious = kết hợp D (1 câu,
    /// cố tình không trộn câu "sửa rồi test lại" để không làm nhẹ cảnh báo);
    /// warning / drive* / notBuy lấy đúng pool trong JSON.</summary>
    string[] PoolForScenario(string scenario) => scenario switch
    {
        // Pool tốt: câu tốt chung + kết hợp A + câu chuyên biệt nếu có tín hiệu tốt
        // (CPU đã nướng xong không lỗi, pin còn khỏe). Tín hiệu tốt không đổi tiêu
        // đề, chỉ thêm câu vào pool để lần chọn ngẫu nhiên khi kịch bản đổi có
        // thể ra câu chuyên biệt (không còn nút đổi câu tay).
        "good" => GoodPool(),
        "driveMediaError" => _bank.Sentences("driveMediaError"),
        "driveBadSector" => _bank.Sentences("driveBadSector"),
        "driveWear" => _bank.Sentences("driveWear"),
        "driveSmartUnknown" => _bank.Sentences("driveSmartUnknown"),
        "cpuFail" => _bank.SignalSentences("cpuGpu", "cpuFail") ?? _bank.Sentences("combinedD"),
        "gpuArtifact" => _bank.SignalSentences("cpuGpu", "gpuArtifact") ?? _bank.Sentences("combinedD"),
        "batteryNotCharging" => _bank.SignalSentences("battery", "notCharging") ?? _bank.Sentences("combinedD"),
        "keyboardDead" => _bank.SignalSentences("peripheral", "keyboard") ?? _bank.Sentences("warning"),
        "screenDeadPixel" => _bank.SignalSentences("peripheral", "screen") ?? _bank.Sentences("warning"),
        "speakerDistorted" => _bank.SignalSentences("peripheral", "speaker") ?? _bank.Sentences("warning"),
        "webcamFail" => _bank.SignalSentences("peripheral", "webcam") ?? _bank.Sentences("warning"),
        "hingeLoose" => _bank.SignalSentences("peripheral", "hinge") ?? _bank.Sentences("warning"),
        "combinedB" => _bank.Sentences("combinedB"),
        "batteryWornOver40" => _bank.SignalSentences("battery", "wornOver40") ?? _bank.Sentences("warning"),
        "batteryWorn20_30" => _bank.SignalSentences("battery", "worn20_30") ?? _bank.Sentences("warning"),
        "cpuOverheat" => _bank.SignalSentences("cpuGpu", "cpuOverheat") ?? _bank.Sentences("warning"),
        "scratchesLight" => _bank.SignalSentences("peripheral", "scratches") ?? _bank.Sentences("warning"),
        "serious" => _bank.Sentences("combinedD"),
        "warning" => _bank.Sentences("warning"),
        "incomplete" => [.. _bank.Sentences("combinedE"), .. _bank.Sentences("incomplete")],
        "notBuy" => _bank.Sentences("notBuy"),
        _ => _bank.Sentences("incomplete"),
    };

    /// <summary>Dòng "An Tâm nhắn bạn" chia theo nhóm tình trạng (user chốt 2026-11-03):
    /// kho thơ chia 5 nhóm — máy tốt / máy khá / cần thương lượng / nhiều lỗi /
    /// không nên mua — hệ thống chọn ngẫu nhiên 1 câu trong nhóm đúng tình trạng,
    /// thay vì hiện lời khuyên chung chung. Ánh xạ: good → tốt; lỗi chức năng →
    /// khá (máy vẫn dùng được, chỉ lỗi nhỏ); warning / cảnh báo linh kiện → cần
    /// thương lượng; lỗi nghiêm trọng → nhiều lỗi; kỹ thuật viên đánh dấu không
    /// mua → không nên mua. Nghiêm trọng + không mua ĐƯỢC hiện vì các câu này là
    /// câu cảnh báo/thất vọng, không phải câu vui làm nhẹ cảnh báo. Chỉ ẩn khi
    /// chưa đủ dữ liệu. Câu chỉ random lại khi kịch bản đổi (qua Refresh), không
    /// random lại mỗi lần Refresh. File thiếu nhóm thì dự phòng bằng random pool
    /// chung (luật cũ), không thì ẩn — không tự bịa câu.</summary>
    string? AnTamGroupKey() => _currentScenario switch
    {
        "good" => "good",
        "warning" or "combinedB" or "batteryWornOver40" or "batteryWorn20_30" or "cpuOverheat" or "scratchesLight" or "driveWear" or "driveSmartUnknown" => "negotiate",
        "keyboardDead" or "screenDeadPixel" or "speakerDistorted" or "webcamFail" or "hingeLoose" => "fair",
        "driveMediaError" or "driveBadSector" or "cpuFail" or "gpuArtifact" or "batteryNotCharging" or "serious" => "manyFaults",
        "notBuy" => "notBuy",
        _ => null,
    };

    void ShowAnTamNote()
    {
        // Tiêu đề đã là header của thẻ (Sum.AnTamTitle) nên dòng này chỉ còn
        // câu thơ, không lặp lại nhãn.
        AnTamNoteText.Text = _lastAnTamNote;
        AnTamCard.Visibility = Visibility.Visible;
    }

    /// <summary>Đổi ngôn ngữ: giữ nguyên câu đang hiện (cùng chỉ số trong nhóm),
    /// chỉ đổi sang bản dịch — để khi bấm EN/VI, câu đang đọc hiện đúng bản dịch
    /// của nó chứ không nhảy sang câu khác. Nhóm EN song song tuyệt đối với nhóm
    /// VI nên cùng chỉ số = cùng câu.</summary>
    void RefreshAnTamNoteLanguage()
    {
        if (_lastAnTamNote.Length == 0 || _currentAnTamIndex < 0) return;
        string? groupKey = AnTamGroupKey();
        string[]? pool = groupKey != null ? _bank.AnTamGroup(groupKey) : _bank.AnTamNotePool();
        if (pool == null || _currentAnTamIndex >= pool.Length) return;
        _lastAnTamNote = pool[_currentAnTamIndex];
        ShowAnTamNote();
    }

    void RefreshAnTamNote(int exclude = -1)
    {
        string? groupKey = AnTamGroupKey();
        if (groupKey == null)
        {
            // Chưa đủ dữ liệu (hoặc kịch bản lạ) → ẩn cả thẻ, báo cáo lưu cũng không có dòng này.
            _lastAnTamNote = "";
            AnTamNoteText.Text = "";
            AnTamCard.Visibility = Visibility.Collapsed;
            return;
        }
        if (_bank.AnTamGroup(groupKey) is { Length: > 0 } group)
        {
            int idx;
            if (group.Length == 1) idx = 0;
            else do { idx = _random.Next(group.Length); } while (idx == exclude);
            _currentAnTamIndex = idx;
            _lastAnTamNote = group[idx];
            ShowAnTamNote();
            return;
        }
        // File thiếu nhóm → dự phòng luật cũ: random pool chung, không thì ẩn.
        var pool = _bank.AnTamNotePool();
        if (pool.Length == 0)
        {
            _lastAnTamNote = "";
            AnTamNoteText.Text = "";
            AnTamCard.Visibility = Visibility.Collapsed;
            return;
        }
        int idx2;
        if (pool.Length == 1) idx2 = 0;
        else do { idx2 = _random.Next(pool.Length); } while (idx2 == exclude);
        _currentAnTamIndex = idx2;
        _lastAnTamNote = pool[idx2];
        ShowAnTamNote();
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
                note = Lang.Get("Sum.DoneNote");
            }
            else
            {
                grade = ModuleVerdictLevel.ChuaTest;
                note = Lang.Get("Sum.UndoneNote");
            }
            grades.Add(grade);
            rows.Add(new(m.DisplayName, GradeText(grade), GradeBrushFor(grade), note));
        }
        ModuleList.ItemsSource = rows;
        _lastRows = rows;

        // Chọn kịch bản theo thứ tự ưu tiên (đúng thứ tự user chốt):
        // 1. Lỗi nghiêm trọng → 2. Lỗi chức năng → 3. Cảnh báo linh kiện →
        // 4. Thiếu dữ liệu → 5. Máy đạt. Trong mỗi nhóm, câu chuyên biệt (tín hiệu
        // cụ thể) thắng câu chung: vd ổ media thắng câu nghiêm trọng chung, pin
        // chai nặng thắng câu cảnh báo chung. Thiếu dữ liệu chỉ về "incomplete"
        // khi không có lỗi/cảnh báo nào trước nó — nhưng không bao giờ về "good":
        // good chỉ khi tất cả module đều Đạt và không còn tín hiệu cảnh báo nào.
        // Tín hiệu tốt (cpuGood, batteryHealthy) không đổi tiêu đề, chỉ thêm câu
        // vào pool tốt (xem GoodPool).
        var signals = AllSignals();
        string configNote = _host.Verdicts.TryGetValue("config", out var cv) ? cv.Note : "";
        string autoTitleKey;
        string autoScenario;
        if (configNote.Contains("lỗi toàn vẹn dữ liệu")) { autoTitleKey = "Sua"; autoScenario = "driveMediaError"; }
        else if (configNote.Contains("sector")) { autoTitleKey = "Sua"; autoScenario = "driveBadSector"; }
        else if (signals.Contains("cpuFail")) { autoTitleKey = "Sua"; autoScenario = "cpuFail"; }
        else if (signals.Contains("gpuArtifact")) { autoTitleKey = "Sua"; autoScenario = "gpuArtifact"; }
        else if (signals.Contains("batteryNotCharging")) { autoTitleKey = "Sua"; autoScenario = "batteryNotCharging"; }
        else if (grades.Any(g => g == ModuleVerdictLevel.Chan)) { autoTitleKey = "Sua"; autoScenario = "serious"; }
        else if (signals.Contains("keyboardDead")) { autoTitleKey = "CanhBao"; autoScenario = "keyboardDead"; }
        else if (signals.Contains("screenDeadPixel")) { autoTitleKey = "CanhBao"; autoScenario = "screenDeadPixel"; }
        else if (signals.Contains("speakerDistorted")) { autoTitleKey = "CanhBao"; autoScenario = "speakerDistorted"; }
        else if (signals.Contains("webcamFail")) { autoTitleKey = "CanhBao"; autoScenario = "webcamFail"; }
        else if (signals.Contains("hingeLoose")) { autoTitleKey = "CanhBao"; autoScenario = "hingeLoose"; }
        // Kết hợp B (CPU tốt + ổ cũ + pin chai cùng lúc): câu tổng hợp hay hơn
        // câu đơn lẻ, nên kiểm tra trước các câu cảnh báo đơn. Ổ cũ ở đây là
        // driveWear (sức khỏe <90%) hoặc driveSmartUnknown không tính — chỉ
        // tính hao mòn đã đo được, không tính "chưa rõ" thành "cũ".
        else if (signals.Contains("cpuGood") && configNote.Contains("sức khỏe")
            && (signals.Contains("batteryWornOver40") || signals.Contains("batteryWorn20_30"))) { autoTitleKey = "CanhBao"; autoScenario = "combinedB"; }
        else if (signals.Contains("batteryWornOver40")) { autoTitleKey = "CanhBao"; autoScenario = "batteryWornOver40"; }
        else if (signals.Contains("batteryWorn20_30")) { autoTitleKey = "CanhBao"; autoScenario = "batteryWorn20_30"; }
        else if (signals.Contains("cpuOverheat")) { autoTitleKey = "CanhBao"; autoScenario = "cpuOverheat"; }
        else if (configNote.Contains("chưa rõ sức khỏe") || configNote.Contains("không đọc được lỗi nghiêm trọng")) { autoTitleKey = "CanhBao"; autoScenario = "driveSmartUnknown"; }
        else if (configNote.Contains("sức khỏe")) { autoTitleKey = "CanhBao"; autoScenario = "driveWear"; }
        else if (signals.Contains("scratchesLight")) { autoTitleKey = "CanhBao"; autoScenario = "scratchesLight"; }
        else if (grades.Any(g => g == ModuleVerdictLevel.CanhBao)) { autoTitleKey = "CanhBao"; autoScenario = "warning"; }
        else if (modules.Count == 0 || grades.Any(g => g == ModuleVerdictLevel.ChuaTest)) { autoTitleKey = "ChuaXong"; autoScenario = "incomplete"; }
        else { autoTitleKey = "Dat"; autoScenario = "good"; }

        string titleKey = _markedLoai ? "Loai" : autoTitleKey;
        string scenario = _markedLoai ? "notBuy" : autoScenario;

        _lastOverallTitle = OverallTitle(titleKey);
        OverallText.Text = _lastOverallTitle
            + (_markedLoai ? string.Format(Lang.Get("Sum.MarkedSuffix"), OverallTitle(autoTitleKey)) : "");
        if (titleKey == "Dat") OverallText.Foreground = Brushes.ForestGreen;
        else if (titleKey == "Sua" || titleKey == "Loai") OverallText.SetResourceReference(TextBlock.ForegroundProperty, "ErrorBrush");
        else if (titleKey == "CanhBao") OverallText.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
        else OverallText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");

        // Mascot kết luận đổi theo kết quả: máy tốt → vui, máy có vấn đề
        // (chặn / cảnh báo / đánh dấu không mua) → lo lắng; chưa đủ dữ liệu
        // thì giữ mascot suy nghĩ (mascot-new) như trước.
        HealthMascot.Source = new BitmapImage(new Uri(titleKey switch
        {
            "Dat" => "pack://application:,,,/Assets/mascot-happy.png",
            "Sua" or "CanhBao" or "Loai" => "pack://application:,,,/Assets/mascot-worried.png",
            _ => "pack://application:,,,/Assets/mascot-new.png",
        }, UriKind.Absolute));

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
        // Đổi ngôn ngữ: giữ nguyên câu kết luận đang hiện (cùng chỉ số), chỉ
        // đổi sang bản dịch — đặt sau khối kịch bản để khi kịch bản cũng đổi
        // thì câu mới đã chọn đúng ngôn ngữ rồi, gọi lại cũng chỉ vẽ lại cùng câu.
        if (_lastSentenceLang != Lang.CurrentLanguage)
        {
            _lastSentenceLang = Lang.CurrentLanguage;
            if (_currentPhrasingIndex >= 0)
                PhrasingText.Text = PickPhrasingSameIndex(scenario);
        }
        // Đổi ngôn ngữ: giữ nguyên câu An Tâm đang hiện (cùng chỉ số), chỉ đổi
        // sang bản dịch — đặt sau khối kịch bản để khi kịch bản cũng đổi thì câu
        // mới đã chọn đúng ngôn ngữ rồi, gọi lại cũng chỉ vẽ lại cùng câu.
        if (_lastAnTamLang != Lang.CurrentLanguage)
        {
            _lastAnTamLang = Lang.CurrentLanguage;
            RefreshAnTamNoteLanguage();
        }
        MarkLoaiButton.Content = _markedLoai ? Lang.Get("Sum.UnmarkLoai") : Lang.Get("Sum.MarkLoai");

        // Hộp "Lý do chính" trong thẻ Sức khỏe & Rủi ro (dời từ Dashboard):
        // lý do chặn đầu tiên, không có thì cảnh báo đầu tiên — cùng logic
        // Dashboard cũ (đếm done/total theo grades), nhưng câu chữ sửa cho
        // đúng ngữ cảnh trang Báo cáo (không còn "mở Báo cáo", "ở trang Tổng kết").
        string reason = "";
        foreach (var r in rows)
        {
            if (r.Grade == "Chặn") { reason = r.Note; break; }
        }
        if (reason.Length == 0)
            foreach (var r in rows)
            {
                if (r.Grade == "Cảnh báo") { reason = r.Note; break; }
            }
        int doneCount = grades.Count(g => g != ModuleVerdictLevel.ChuaTest);
        int totalCount = grades.Count;
        HealthReasonText.Text = _markedLoai
            ? Lang.Get("Sum.Reason.Marked")
            : titleKey switch
            {
                "Sua" => string.Format(Lang.Get("Sum.Reason.Blocked"), reason) + (doneCount < totalCount ? string.Format(Lang.Get("Sum.Reason.Remaining"), totalCount - doneCount) : ""),
                "CanhBao" => string.Format(Lang.Get("Sum.Reason.Warning"), reason) + (doneCount < totalCount ? string.Format(Lang.Get("Sum.Reason.Remaining"), totalCount - doneCount) : ""),
                "Dat" => Lang.Get("Sum.Reason.Good"),
                _ => Lang.Get("Sum.Reason.Incomplete"),
            };

        // Thẻ tiến độ kiểu Mac: x/y hạng mục + thanh tiến độ + trạng thái;
        // còn bài chưa xong thì hiện nút vào lại wizard.
        ProgressCountText.Text = string.Format(Lang.Get("Sum.ProgressCount"), doneCount, totalCount);
        SessionProgressBar.Maximum = totalCount;
        SessionProgressBar.Value = doneCount;
        bool allDone = totalCount > 0 && doneCount == totalCount;
        ProgressStatusText.Text = allDone ? Lang.Get("Sum.ProgressDone") : Lang.Get("Sum.ProgressLeft");
        if (allDone) ProgressStatusText.Foreground = Brushes.ForestGreen;
        else ProgressStatusText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
        ContinueButton.Visibility = allDone ? Visibility.Collapsed : Visibility.Visible;
        // Thẻ Pin & Lưu trữ kiểu Mac: % pin còn lại + số ổ nhận diện.
        double? wear = _host.BatteryWearPercent;
        PinLineText.Text = wear == null
            ? Lang.Get("Sum.PinUnknown")
            : string.Format(Lang.Get("Sum.PinLine"), $"{100 - wear.Value:0.#}");
        DrivesLineText.Text = string.Format(Lang.Get("Sum.DrivesLine"), _host.DriveSummaries.Count);
    }

    static string GradeText(ModuleVerdictLevel level) => level switch
    {
        ModuleVerdictLevel.Dat => Lang.Get("Sum.Grade.Dat"),
        ModuleVerdictLevel.CanhBao => Lang.Get("Sum.Grade.CanhBao"),
        ModuleVerdictLevel.Chan => Lang.Get("Sum.Grade.Chan"),
        _ => Lang.Get("Sum.Grade.Untested"),
    };
}
