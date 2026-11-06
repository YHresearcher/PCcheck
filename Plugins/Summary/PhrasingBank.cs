using System.IO;
using System.Text.Json;
using YanJi.PluginSdk;

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
    public Dictionary<string, string[]> AnTamGroups { get; } = [];
    // Bản dịch EN của 21 câu + 5 nhóm (anTamNotesEn / anTamGroupsEn trong JSON):
    // lục bát 6-8 + vần không dịch được sang tiếng Anh, bản EN giữ y nghĩa +
    // dạng 2 dòng + giọng shop ấm. Nhóm EN song song tuyệt đối với nhóm VI
    // (cùng thứ tự, cùng độ dài) để cùng chỉ số = cùng câu khi đổi ngôn ngữ.
    public string[] AnTamNotesEn { get; private set; } = [];
    public Dictionary<string, string[]> AnTamGroupsEn { get; } = [];

    /// <summary>Pool "An Tâm nhắn bạn" theo ngôn ngữ đang chọn: bản EN nếu có
    /// đủ, không thì bản VI (không crash, không trống).</summary>
    public string[] AnTamNotePool() =>
        Lang.CurrentLanguage == "en" && AnTamNotesEn.Length > 0 ? AnTamNotesEn : AnTamNotes;

    /// <summary>Nhóm câu theo kịch bản, theo ngôn ngữ đang chọn; EN thiếu nhóm
    /// thì dùng nhóm VI (cùng thứ tự, cùng độ dài).</summary>
    public string[]? AnTamGroup(string key)
    {
        if (Lang.CurrentLanguage == "en" && AnTamGroupsEn.TryGetValue(key, out var en) && en.Length > 0) return en;
        return AnTamGroups.TryGetValue(key, out var vi) ? vi : null;
    }

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
            ReadGroup(root, "anTamGroups", AnTamGroups);
            AnTamNotesEn = Pool(root, "anTamNotesEn");
            ReadGroup(root, "anTamGroupsEn", AnTamGroupsEn);
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
        AnTamNotes = ["Máy ngon mua được thật vui,\nGiá mà vừa túi, niềm vui thêm đầy."];
        AnTamNotesEn = ["A good machine is joy indeed,\nwhen the price fits just what you need."];
        AnTamGroupsEn["good"] = ["A good machine is joy indeed,\nwhen the price fits just what you need.", "A lasting machine is worth the pay,\npay a little more, keep trouble away.", "Know a good machine through and through,\nunderstand it first, then pay — respect what's due.", "Even on a small budget, choose well,\nclear origins and lasting strength will tell.", "A good machine fears no inspection,\nthe more you look, the more affection."];
        AnTamGroupsEn["fair"] = ["Don't cheer too soon for prices low,\nclear origins make joy still grow.", "Keep the report for days ahead,\ncompare with ease, no aching head.", "A good machine fits the job just right,\ntoo much power wastes away its might.", "Office work wants a machine that's serene,\nstudy wants a strong battery, light and lean.", "Joy is a machine that runs well from day one,\nendless repairs leave joy with none."];
        AnTamGroupsEn["negotiate"] = ["Don't cheer that bargain, hold your glee,\ncount the repair cost, then you'll see.", "Worn battery, weak drive — keep in mind,\nsubtract the cost, and a fair price you'll find.", "Don't judge by the sticker price alone,\nweigh its condition — then the worth is shown.", "Bargaining is never wrong, it's true,\nspeak your mind — no one will blame you.", "This machine isn't bad, you'll see,\na slightly lower price, then we'll agree."];
        AnTamGroupsEn["manyFaults"] = ["Look it over before you pay,\na faulty machine will rue the day.", "Don't pay for pretty, shining skin,\na glossy shell hides trouble within.", "Check it thoroughly today,\nso no bitter regret comes your way.", "Ask every warranty detail clear,\nwhen trouble comes, you'll have less to fear.", "Pretty as a flower on the outside,\nafter the check, it's sad on the inside."];
        AnTamGroupsEn["notBuy"] = ["Through how many hands has this machine been?\nAsk where it's from, or you'll worry again.", "Charger, battery, drive — beware,\neach small fix sends money into the air.", "A good machine needs its source made clear,\ndon't trust the shine — brass can pose as gold, I fear.", "If the seller rushes you to buy,\ntake your time to check before you pay.", "Don't hand over money for a pretty face,\nor tomorrow you'll hold the machine — and the trouble in its place."];
        AnTamGroups["good"] = ["Máy ngon mua được thật vui,\nGiá mà vừa túi, niềm vui thêm đầy.", "Máy bền mới đáng đồng tiền,\nGiá cao đôi chút, đỡ phiền mai sau.", "Máy ngon phải hiểu tận tường,\nRõ rồi mới trả, mới thương đồng tiền.", "Ít tiền vẫn chọn máy ngon,\nMiễn là nguồn gốc, máy còn bền lâu.", "Máy ngon chẳng ngại đem soi,\nCàng xem càng thích, càng coi càng mừng."];
        AnTamGroups["fair"] = ["Giá rẻ chớ vội mừng vui,\nNguồn gốc cho rõ, mới vui lâu dài.", "Bản kiểm nhớ giữ về sau,\nCó khi đối chiếu, khỏi đau cái đầu.", "Máy ngon hợp việc là hay,\nCấu hình quá mạnh, có ngày phí công.", "Văn phòng cần máy chạy êm,\nHọc hành pin khỏe, nhẹ thêm đôi phần.", "Mua về chạy tốt mới vui,\nChứ còn sửa mãi, niềm vui còn gì."];
        AnTamGroups["negotiate"] = ["Máy rẻ chớ vội hân hoan,\nTính luôn tiền sửa cho an trong lòng.", "Pin chai ổ yếu đừng quên,\nTrừ hao chi phí, nói lên giá mình.", "Chớ so mỗi giá niêm yết,\nXét thêm tình trạng mới biết đáng mua.", "Mua hàng mặc cả chẳng sai,\nChưa ưng cứ nói, chẳng ai trách mình.", "Máy này chưa hẳn là tồi,\nGiá mà bớt chút, thì ngồi tính sau."];
        AnTamGroups["manyFaults"] = ["Xem cho kỹ trước khi mua,\nBiết đâu máy lỗi, tránh thua một đời.", "Máy xinh chớ vội xuống tiền,\nVỏ ngoài bóng bẩy, lắm phiền bên trong.", "Kiểm cho thật kỹ hôm nay,\nMai sau khỏi tiếc, đắng cay một mình.", "Bảo hành hỏi rõ từng điều,\nCó khi trục trặc, đỡ nhiều âu lo.", "Nhìn ngoài bóng bẩy như hoa,\nKiểm xong mới biết, hóa ra hơi buồn."];
        AnTamGroups["notBuy"] = ["Máy qua tay mấy người rồi,\nHỏi cho rõ gốc, kẻo ngồi âu lo.", "Sạc pin ổ cứng coi chừng,\nThay từng món nhỏ, tiền từng ấy bay.", "Máy ngon phải rõ nguồn hàng,\nChớ tin vỏ bóng, ngỡ vàng hóa thau.", "Người bán có giục mua nhanh,\nCứ xem cho kỹ, mới đành xuống tay.", "Thấy xinh chớ vội trao tiền,\nKẻo mai ôm máy, muộn phiền ôm theo."];
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
