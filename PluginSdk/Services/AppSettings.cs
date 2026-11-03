using System.IO;
using System.Text.Json;

namespace YanJi.PluginSdk.Services;

/// <summary>Cài đặt chung của app, đọc bởi nhiều plugin nên đặt ở PluginSdk (không
/// phải 1 plugin riêng, để Tổng kết / Tản nhiệt / Tổng quan cùng đọc 1 chỗ).
/// File: %APPDATA%\YanJi\settings.json (theo app, không theo USB — vì cài đặt
/// là của bản app trên USB đó, còn hồ sơ từng máy mới theo đường dẫn USB).
/// File hỏng / thiếu thì về mặc định, không crash app. Đường dẫn hồ sơ trống =
/// tự động (thư mục app \ KetQua, ghi không được thì fallback %APPDATA%).</summary>
public static class AppSettings
{
    const string FileName = "settings.json";

    static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YanJi", FileName);

    /// <summary>Đường dẫn gốc chứa hồ sơ KetQua do người dùng tự chọn (trống = tự động).</summary>
    public static string UsbPath { get; private set; } = "";

    /// <summary>Thời lượng nướng khuyến nghị, phút (1–60, mặc định 3 — khớp đề xuất
    /// "stress nhanh 3 phút" trong thiết kế; chỉ là khuyến nghị, không tự dừng
    /// trừ khi AutoStopStress bật).</summary>
    public static int StressMinutes { get; private set; } = 3;

    /// <summary>Tự dừng nướng máy khi đủ thời lượng khuyến nghị (mặc định TẮT —
    /// kỹ thuật viên có thể muốn nướng lâu hơn cho máy nghi ngờ, nên không tự
    /// dừng nếu chưa bật).</summary>
    public static bool AutoStopStress { get; private set; }

    static AppSettings() => Load();

    sealed record SettingsDto(string? UsbPath, int? StressMinutes, bool? AutoStopStress);

    public static void Load()
    {
        UsbPath = "";
        StressMinutes = 3;
        AutoStopStress = false;
        try
        {
            if (!File.Exists(SettingsPath)) return;
            var dto = JsonSerializer.Deserialize<SettingsDto>(File.ReadAllText(SettingsPath));
            if (dto == null) return;
            UsbPath = dto.UsbPath?.Trim() ?? "";
            if (dto.StressMinutes is int m) StressMinutes = Math.Clamp(m, 1, 60);
            AutoStopStress = dto.AutoStopStress == true;
        }
        catch
        {
            // File hỏng thì về mặc định, không crash app; lần lưu sau sẽ ghi đè file hỏng
        }
    }

    /// <summary>Lưu cài đặt, trả về false nếu ghi file không được (kèm lý do để trang Cài đặt báo).</summary>
    public static bool Save(string usbPath, int stressMinutes, bool autoStopStress, out string error)
    {
        error = "";
        UsbPath = usbPath?.Trim() ?? "";
        StressMinutes = Math.Clamp(stressMinutes, 1, 60);
        AutoStopStress = autoStopStress;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var json = JsonSerializer.Serialize(new SettingsDto(UsbPath, StressMinutes, AutoStopStress),
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static void Reset() => Save("", 3, false, out _);

    /// <summary>Thư mục gốc chứa hồ sơ KetQua theo cài đặt: UsbPath nếu có, còn không
    /// là thư mục app (bản portable = USB). Không tự tạo thư mục ở đây — nơi lưu
    /// hồ sơ sẽ tự tạo và fallback nếu ghi không được, để báo rõ cho người dùng.</summary>
    public static string GetKetQuaDir() => UsbPath.Length > 0
        ? UsbPath
        : Path.Combine(AppContext.BaseDirectory, "KetQua");
}
