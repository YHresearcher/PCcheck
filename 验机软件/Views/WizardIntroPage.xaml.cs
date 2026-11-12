using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using YanJi.PluginSdk;

namespace YanJi.Views;

/// <summary>Màn hình Bắt đầu trước wizard: liệt kê các bài test có thể cho qua
/// theo thứ tự + trạng thái đã xong/chưa, bấm Bắt đầu mới vào bài đầu tiên.
/// Trang của host (không phải plugin), 1 instance dùng chung ở MainWindow.</summary>
public partial class WizardIntroPage : UserControl
{
    readonly IHostContext _host;

    public WizardIntroPage(IHostContext host)
    {
        _host = host;
        InitializeComponent();
        _host.DoneChanged += Refresh;
        Lang.Changed += Refresh;
        Loaded += (_, _) => Refresh();
    }

    /// <summary>Bấm Bắt đầu: host vào bài chưa xong đầu tiên.</summary>
    public event Action? StartRequested;

    public sealed record IntroRow(string Mark, Brush MarkBrush, string Name, string State, Brush StateBrush);

    static Brush HexBrush(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }

    public void Refresh()
    {
        var rows = _host.Modules.Where(m => !m.Terminal).OrderBy(m => m.Order)
            .Select(m =>
            {
                bool done = _host.IsDone(m.Id);
                return new IntroRow(
                    done ? "✓" : "○",
                    done ? Brushes.ForestGreen : HexBrush("#888080"),
                    m.Name,
                    done ? Lang.Get("Wiz.IntroDone") : Lang.Get("Wiz.IntroPending"),
                    done ? Brushes.ForestGreen : HexBrush("#888080"));
            }).ToList();
        ModuleList.ItemsSource = rows;
    }

    void StartButton_Click(object sender, RoutedEventArgs e) => StartRequested?.Invoke();
}
