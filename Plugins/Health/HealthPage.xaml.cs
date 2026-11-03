using System.Windows.Controls;
using YanJi.PluginSdk;

namespace YanJi.Plugin.Health;

/// <summary>Trang Sức khỏe: đọc Verdicts + IsDone + Signals từ host, hiện mỗi module
/// 1 dòng với mức Đạt / Cảnh báo / Chặn / Chưa test, kèm mô tả tín hiệu chi tiết
/// (pin chai, CPU quá nhiệt, phím liệt...) nếu module đó đã báo tín hiệu. Không tự
/// chấm thêm gì, không tự suy module chưa test thành khỏe.</summary>
public partial class HealthPage : UserControl
{
    readonly IHostContext _host;

    public record HealthRow(string Name, string Grade, string Note);

    public HealthPage(IHostContext host)
    {
        _host = host;
        InitializeComponent();
        _host.DoneChanged += Refresh;
        _host.VerdictChanged += Refresh;
        _host.SignalChanged += Refresh;
        Loaded += (_, _) => Refresh();
    }

    void Refresh()
    {
        var rows = new List<HealthRow>();
        foreach (var m in _host.Modules.Where(m => !m.Terminal).OrderBy(m => m.Order))
        {
            ModuleVerdictLevel grade;
            string note;
            if (_host.Verdicts.TryGetValue(m.Id, out var v)) { grade = v.Level; note = v.Note; }
            else if (_host.IsDone(m.Id)) { grade = ModuleVerdictLevel.Dat; note = "Đã cho qua"; }
            else { grade = ModuleVerdictLevel.ChuaTest; note = "Chưa test — chưa đủ dữ liệu, không được hiểu là khỏe"; }
            if (_host.Signals.TryGetValue(m.Id, out var sigs) && sigs.Count > 0)
                note += " | Tín hiệu: " + string.Join(", ", sigs.Select(SignalText));
            string gradeText = grade switch
            {
                ModuleVerdictLevel.Dat => "Đạt",
                ModuleVerdictLevel.CanhBao => "Cảnh báo",
                ModuleVerdictLevel.Chan => "Chặn",
                _ => "Chưa test",
            };
            rows.Add(new(m.Name, gradeText, note));
        }
        HealthList.ItemsSource = rows;
    }

    /// <summary>Mô tả tiếng Việt cho 1 tín hiệu (chỉ để hiện ở trang Sức khỏe, câu
    /// kết luận chính thức vẫn lấy từ Phrasings.json ở trang Tổng kết).</summary>
    static string SignalText(string signal) => signal switch
    {
        "batteryHealthy" => "Pin còn khỏe",
        "batteryWorn20_30" => "Pin chai vừa (20–<40%)",
        "batteryWornOver40" => "Pin chai nặng (≥40%)",
        "batteryNotCharging" => "Sạc Kém / rắc rối nguồn điện",
        "cpuGood" => "CPU nướng xong không lỗi",
        "cpuOverheat" => "CPU quá nhiệt khi nướng (≥95°C)",
        "cpuFail" => "CPU gặp lỗi khi nướng",
        "gpuArtifact" => "GPU có artifact khi nướng",
        "keyboardDead" => "Có phím liệt",
        "screenDeadPixel" => "Màn hình có điểm chết",
        "speakerDistorted" => "Loa rè",
        "webcamFail" => "Camera không hoạt động",
        "hingeLoose" => "Bản lề lỏng",
        "scratchesLight" => "Trầy xước nhẹ",
        _ => signal,
    };
}
