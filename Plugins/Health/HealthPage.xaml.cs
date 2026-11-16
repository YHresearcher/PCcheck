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
        Lang.Changed += Refresh;
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
            else if (_host.IsDone(m.Id)) { grade = ModuleVerdictLevel.Dat; note = Lang.Get("Health.DoneNote"); }
            else { grade = ModuleVerdictLevel.ChuaTest; note = Lang.Get("Health.UndoneNote"); }
            if (_host.Signals.TryGetValue(m.Id, out var sigs) && sigs.Count > 0)
                note += Lang.Get("Health.SignalsPrefix") + string.Join(", ", sigs.Select(SignalText));
            string gradeText = grade switch
            {
                ModuleVerdictLevel.Dat => Lang.Get("Health.Grade.Pass"),
                ModuleVerdictLevel.CanhBao => Lang.Get("Health.Grade.Warning"),
                ModuleVerdictLevel.Chan => Lang.Get("Health.Grade.Blocked"),
                _ => Lang.Get("Health.Grade.Untested"),
            };
            rows.Add(new(m.DisplayName, gradeText, note));
        }
        HealthList.ItemsSource = rows;
    }

    /// <summary>Mô tả cho 1 tín hiệu (chỉ để hiện ở trang Sức khỏe, câu
    /// kết luận chính thức vẫn lấy từ Phrasings.json ở trang Tổng kết).
    /// Mã tín hiệu lạ thì hiện nguyên mã (fallback), không đoán mô tả.</summary>
    static string SignalText(string signal) => signal switch
    {
        "batteryHealthy" => Lang.Get("Health.Signal.batteryHealthy"),
        "batteryWorn20_30" => Lang.Get("Health.Signal.batteryWorn20_30"),
        "batteryWornOver40" => Lang.Get("Health.Signal.batteryWornOver40"),
        "batteryNotCharging" => Lang.Get("Health.Signal.batteryNotCharging"),
        "cpuGood" => Lang.Get("Health.Signal.cpuGood"),
        "cpuOverheat" => Lang.Get("Health.Signal.cpuOverheat"),
        "cpuFail" => Lang.Get("Health.Signal.cpuFail"),
        "gpuArtifact" => Lang.Get("Health.Signal.gpuArtifact"),
        "keyboardDead" => Lang.Get("Health.Signal.keyboardDead"),
        "screenDeadPixel" => Lang.Get("Health.Signal.screenDeadPixel"),
        "speakerDistorted" => Lang.Get("Health.Signal.speakerDistorted"),
        "webcamFail" => Lang.Get("Health.Signal.webcamFail"),
        "hingeLoose" => Lang.Get("Health.Signal.hingeLoose"),
        "caseCracked" => Lang.Get("Health.Signal.caseCracked"),
        "screenScratches" => Lang.Get("Health.Signal.screenScratches"),
        "keyMissing" => Lang.Get("Health.Signal.keyMissing"),
        "portDamaged" => Lang.Get("Health.Signal.portDamaged"),
        "batterySwollen" => Lang.Get("Health.Signal.batterySwollen"),
        "openedSign" => Lang.Get("Health.Signal.openedSign"),
        "scratchesLight" => Lang.Get("Health.Signal.scratchesLight"),
        "driveMediaError" => Lang.Get("Health.Signal.driveMediaError"),
        "driveBadSector" => Lang.Get("Health.Signal.driveBadSector"),
        "driveSmartUnknown" => Lang.Get("Health.Signal.driveSmartUnknown"),
        "driveWear" => Lang.Get("Health.Signal.driveWear"),
        _ => signal,
    };
}
