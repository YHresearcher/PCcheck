using System.Windows.Controls;
using YanJi.PluginSdk;

namespace YanJi.Plugin.Health;

/// <summary>Trang Sức khỏe: đọc Verdicts + IsDone từ host, hiện mỗi module 1 dòng
/// với mức Đạt / Cảnh báo / Chặn / Chưa test. Không tự chấm thêm gì (pin, nhiệt,
/// tốc độ ổ chưa có ngưỡng shop chốt thì không bịa mức), không tự suy module chưa
/// test thành khỏe.</summary>
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
}
