using System.Windows;
using YanJi.PluginSdk;

namespace YanJi.Services;

/// <summary>Chuyển ngữ VI/EN: đổi từ điển chuỗi trong Application.Resources
/// (giống cách ThemeManager đổi từ điển theme), lưu vào settings.json.
/// XAML dùng {DynamicResource Key} nên đổi tức thì; code-behind đọc qua
/// Lang.Get + đăng ký Lang.Changed để vẽ lại.</summary>
public static class LanguageManager
{
    static readonly Uri ViUri = new("Languages/Strings.vi.xaml", UriKind.Relative);
    static readonly Uri EnUri = new("Languages/Strings.en.xaml", UriKind.Relative);

    public static string Current { get; private set; } = "vi";

    public static event Action? LanguageChanged;

    public static void ApplyInitial() => Apply(Settings.Language ?? "vi", persist: false);

    public static void Set(string lang) => Apply(Normalize(lang), persist: true);

    static string Normalize(string lang) => lang == "en" ? "en" : "vi";

    static void Apply(string lang, bool persist)
    {
        Current = lang;
        Lang.CurrentLanguage = lang;
        var dicts = Application.Current.Resources.MergedDictionaries;
        for (int i = dicts.Count - 1; i >= 0; i--)
        {
            var s = dicts[i].Source?.OriginalString ?? "";
            if (s.EndsWith("Strings.vi.xaml") || s.EndsWith("Strings.en.xaml"))
                dicts.RemoveAt(i);
        }
        dicts.Add(new ResourceDictionary { Source = lang == "en" ? EnUri : ViUri });
        if (persist)
        {
            Settings.Language = lang;
            Settings.Save();
        }
        Lang.RaiseChanged();
        LanguageChanged?.Invoke();
    }
}
