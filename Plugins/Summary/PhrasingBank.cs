using System.Text.Json;

namespace YanJi.Plugin.Summary;

/// <summary>Ngân hàng câu kết luận, đọc từ Phrasings.json trong thư mục plugin
/// (plugins/summary/Phrasings.json), KHÔNG viết cố định trong C#. File hỏng hoặc
/// thiếu thì dùng câu dự phòng dưới đây, không crash app — câu dự phòng cũng là
/// câu thật của user (kết hợp A/D/E + câu tốt đầu tiên), không phải câu bịa.
/// Các pool pin / CPU-GPU / ngoại vi / kết hợp B-C đã có câu sẵn trong JSON,
/// nhưng Tổng kết hiện chưa tự chọn chúng vì chưa có tín hiệu (xem meta.signalNeeded
/// trong JSON): pin cần % chai, CPU/GPU cần nhiệt/throttling/artifact, ngoại vi
/// cần tick xác nhận tay, B/C cần cộng nhiều tín hiệu cùng lúc. Không đoán tín
/// hiệu từ dữ liệu không có — đoán sai còn tệ hơn không có câu chuyên biệt.</summary>
public class PhrasingBank
{
    public string[] Good { get; private set; } = [];
    public string[] CombinedA { get; private set; } = [];
    public string[] CombinedD { get; private set; } = [];
    public string[] CombinedE { get; private set; } = [];
    public string[] DriveWear { get; private set; } = [];
    public string[] DriveMediaError { get; private set; } = [];
    public string[] DriveBadSector { get; private set; } = [];
    public string[] DriveSmartUnknown { get; private set; } = [];
    public string[] Warning { get; private set; } = [];
    public string[] Incomplete { get; private set; } = [];
    public string[] NotBuy { get; private set; } = [];
    public string[] AnTamNotes { get; private set; } = [];

    // Các pool sẵn cho tín hiệu tương lai (pin / CPU-GPU / ngoại vi / B-C).
    // Tổng kết chưa dùng chúng vì chưa có tín hiệu, nhưng loader vẫn đọc để
    // kiểm tra JSON không hỏng và để bước nối tín hiệu sau chỉ việc gọi pool.
    public Dictionary<string, string[]> Battery { get; } = [];
    public Dictionary<string, string[]> CpuGpu { get; } = [];
    public Dictionary<string, string[]> Peripheral { get; } = [];
    public string[] CombinedB { get; private set; } = [];
    public string[] CombinedC { get; private set; } = [];

    public PhrasingBank()
    {
        try
        {
            string? dir = Path.GetDirectoryName(typeof(PhrasingBank).Assembly.Location);
            string[] candidates =
            [
                dir != null ? Path.Combine(dir, "Phrasings.json") : "",
                Path.Combine(AppContext.BaseDirectory, "plugins", "summary", "Phrasings.json"),
            ];
            string? path = candidates.FirstOrDefault(p => p.Length > 0 && File.Exists(p));
            if (path == null) throw new FileNotFoundException("Phrasings.json not found");
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;
            Good = Pool(root, "good");
            CombinedA = Pool(root, "combinedA");
            CombinedD = Pool(root, "combinedD");
            CombinedE = Pool(root, "combinedE");
            CombinedB = Pool(root, "combinedB");
            CombinedC = Pool(root, "combinedC");
            DriveWear = Pool(root, "driveWear");
            DriveMediaError = Pool(root, "driveMediaError");
            DriveBadSector = Pool(root, "driveBadSector");
            DriveSmartUnknown = Pool(root, "driveSmartUnknown");
            Warning = Pool(root, "warning");
            Incomplete = Pool(root, "incomplete");
            NotBuy = Pool(root, "notBuy");
            AnTamNotes = Pool(root, "anTamNotes");
            ReadGroup(root, "battery", Battery);
            ReadGroup(root, "cpuGpu", CpuGpu);
            ReadGroup(root, "peripheral", Peripheral);
            if (Good.Length == 0 || CombinedD.Length == 0 || CombinedE.Length == 0)
                throw new InvalidDataException("Phrasings.json missing a required pool");
        }
        catch
        {
            SetFallback();
        }
    }

    void SetFallback()
    {
        Good = ["Tin vui đây! Chiếc máy này đã vượt qua vòng kiểm tra khá êm đẹp. Nếu mức giá cũng dễ thương như tình trạng máy thì đây là một lựa chọn đáng cân nhắc!"];
        CombinedA = ["Chiếc máy này đã vượt qua vòng kiểm tra của An Tâm khá thuyết phục! Chưa phát hiện lỗi đáng kể ở các hạng mục được kiểm tra. Nếu giá bán hợp lý và nguồn gốc rõ ràng, bạn có thể cân nhắc lựa chọn này."];
        CombinedD = ["Giá có hấp dẫn đến đâu thì độ tin cậy vẫn là ưu tiên hàng đầu! Máy đã xuất hiện lỗi phần cứng quan trọng. An Tâm PC check khuyến nghị không mua trong tình trạng hiện tại."];
        CombinedE = ["Chiếc máy vẫn còn vài bí mật chưa được khám phá! Một số bài kiểm tra quan trọng chưa hoàn thành nên An Tâm PC check chưa thể đưa ra khuyến nghị đáng tin cậy. Hãy kiểm tra tiếp trước khi xuống tiền nhé."];
        DriveWear = ["Ổ cứng đã đi được một chặng đường khá dài! Chỉ số hao mòn cần được lưu ý, nhưng chưa phát hiện lỗi nghiêm trọng trong dữ liệu SMART hiện có. Hãy cân nhắc tuổi thọ còn lại và chi phí thay ổ khi thương lượng giá."];
        DriveMediaError = ["Cảnh báo đỏ từ ổ cứng! Dữ liệu SMART cho thấy đã xuất hiện lỗi media. Đây là rủi ro trực tiếp đến dữ liệu của bạn. An Tâm PC check không khuyến nghị mua nếu vấn đề chưa được xử lý."];
        DriveBadSector = ["Ổ cứng đang có dấu hiệu xuống sức thật sự! Các sector lỗi là điều không nên xem nhẹ, nhất là khi bạn cần một chiếc máy đáng tin cậy để lưu dữ liệu quan trọng."];
        DriveSmartUnknown = ["Ổ cứng vẫn còn là một ẩn số! Hệ thống chưa lấy được dữ liệu SMART nên chưa thể xác nhận sức khỏe thiết bị. Đừng xem việc không có cảnh báo là bằng chứng ổ cứng tốt nhé."];
        Warning = ["Máy dùng được, nhưng có chỗ cần kiểm tra thêm hoặc thương lượng giá trước khi mua."];
        Incomplete = ["Chưa đủ dữ liệu để kết luận mua hay không, test tiếp nhé."];
        NotBuy = ["Máy này không đạt chuẩn An Tâm, không mua."];
        AnTamNotes = ["Máy tốt là một chuyện, mua đúng giá mới là chuyện đáng vui!"];
    }

    static string[] Pool(JsonElement root, string key)
    {
        if (!root.TryGetProperty(key, out var arr) || arr.ValueKind != JsonValueKind.Array) return [];
        var list = new List<string>();
        foreach (var item in arr.EnumerateArray())
            if (item.ValueKind == JsonValueKind.String && item.GetString() is string s && s.Length > 0)
                list.Add(s);
        return [.. list];
    }

    static void ReadGroup(JsonElement root, string key, Dictionary<string, string[]> target)
    {
        if (!root.TryGetProperty(key, out var obj) || obj.ValueKind != JsonValueKind.Object) return;
        foreach (var prop in obj.EnumerateObject())
            target[prop.Name] = Pool(obj, prop.Name);
    }
}
