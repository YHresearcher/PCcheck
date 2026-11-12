using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using YanJi.PluginSdk;

namespace YanJi.Views;

/// <summary>Màn hình Bắt đầu trước wizard: chọn Fast check / Deep check rồi
/// liệt kê các bài sẽ chạy (thiết kế hàng giống thẻ kết quả kiểm tra nhanh
/// trên Dashboard: icon + hàng pastel + trạng thái). Bấm Bắt đầu mới vào bài
/// chưa xong đầu tiên — màn hình này chưa khóa điều hướng.
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
        Loaded += (_, _) => { SyncModeCards(); Refresh(); };
    }

    /// <summary>Bấm Bắt đầu: host vào bài chưa xong đầu tiên (theo chế độ đã chọn).</summary>
    public event Action? StartRequested;

    /// <summary>Chế độ đã chọn: false = Fast check (mặc định), true = Deep check.</summary>
    public bool DeepSelected { get; private set; }

    public sealed record IntroRow(string Icon, string? IconPath, Brush RowBrush, Brush IconBackground, Brush IconBrush,
        string Name, string State, Brush StateBrush, string Mark, Brush MarkBrush);

    static Brush HexBrush(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }

    // Bảng màu + icon từng module, cùng bảng pastel với thẻ kết quả kiểm tra
    // nhanh trên Dashboard (icon thật trong bộ icon An Tâm Mac Check).
    const string IconAssets = "pack://application:,,,/Assets/";
    static readonly (string Id, string Icon, string? IconPath, string Row, string IconBg, string Fg)[] RowStyles =
    [
        ("config",   "⚙", IconAssets + "10_settings_orange.png",  "#E5F4FF", "#D2EAFF", "#1688ED"),
        ("disk",     "▥", IconAssets + "06_ssd_orange.png",      "#EEECFF", "#E1E0FF", "#536BFF"),
        ("keyboard", "⌨", IconAssets + "ban_phim_keyboard.png",  "#FFE7F2", "#FFD5EC", "#E83B9B"),
        ("screen",   "▣", IconAssets + "01_laptop_orange.png",   "#DFF8F0", "#C8F1E4", "#08BA87"),
        ("av",       "♫", IconAssets + "webcam_camera.png",      "#F1E5FF", "#E5D2FF", "#8645E8"),
        ("stress",   "◉", IconAssets + "03_cpu_chip_gray.png",   "#FFE9E3", "#FFDAD0", "#F26427"),
        ("ports",    "ϟ", IconAssets + "08_lightning_orange.png", "#FFF2D4", "#FFE8B0", "#E8A600"),
        ("visual",   "▦", IconAssets + "15_search_gray.png",     "#E6F8E7", "#D2F3D5", "#12AF37"),
    ];

    static (string Icon, string? IconPath, Brush RowBrush, Brush IconBackground, Brush IconBrush) StyleFor(string id)
    {
        foreach (var s in RowStyles)
            if (string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase))
                return (s.Icon, s.IconPath, HexBrush(s.Row), HexBrush(s.IconBg), HexBrush(s.Fg));
        return ("○", null, HexBrush("#F1EFEA"), HexBrush("#E4E0D8"), HexBrush("#888080"));
    }

    void FastCard_Click(object sender, RoutedEventArgs e)
    {
        DeepSelected = false;
        SyncModeCards();
        Refresh();
    }

    void DeepCard_Click(object sender, RoutedEventArgs e)
    {
        DeepSelected = true;
        SyncModeCards();
        Refresh();
    }

    /// <summary>Thẻ đang chọn: viền cam 2px + dấu ✓; thẻ còn lại viền thường.
    /// Dùng SetResourceReference để đổi theme vẫn theo kịp (không gán cứng brush).</summary>
    void SyncModeCards()
    {
        FastCheckMark.Visibility = DeepSelected ? Visibility.Collapsed : Visibility.Visible;
        DeepCheckMark.Visibility = DeepSelected ? Visibility.Visible : Visibility.Collapsed;
        if (DeepSelected)
        {
            FastCardBorder.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
            FastCardBorder.BorderThickness = new Thickness(1);
            DeepCardBorder.SetResourceReference(Border.BorderBrushProperty, "AccentBrush");
            DeepCardBorder.BorderThickness = new Thickness(2);
        }
        else
        {
            FastCardBorder.SetResourceReference(Border.BorderBrushProperty, "AccentBrush");
            FastCardBorder.BorderThickness = new Thickness(2);
            DeepCardBorder.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
            DeepCardBorder.BorderThickness = new Thickness(1);
        }
    }

    public void Refresh()
    {
        var rows = _host.Modules
            .Where(m => !m.Terminal && (DeepSelected || MainWindow.FastCheckModuleIds.Contains(m.Id)))
            .OrderBy(m => m.Order)
            .Select(m =>
            {
                bool done = _host.IsDone(m.Id);
                var style = StyleFor(m.Id);
                return new IntroRow(
                    style.Icon, style.IconPath, style.RowBrush, style.IconBackground, style.IconBrush,
                    m.DisplayName,
                    done ? Lang.Get("Wiz.IntroDone") : Lang.Get("Wiz.IntroPending"),
                    done ? Brushes.ForestGreen : HexBrush("#888080"),
                    done ? "✓" : "○",
                    done ? Brushes.ForestGreen : HexBrush("#888080"));
            }).ToList();
        ModuleList.ItemsSource = rows;
    }

    void StartButton_Click(object sender, RoutedEventArgs e) => StartRequested?.Invoke();
}
