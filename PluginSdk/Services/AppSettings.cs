using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace YanJi.PluginSdk.Services;

/// <summary>Cài đặt chung của app, đọc bởi nhiều plugin nên đặt ở PluginSdk (không
/// phải 1 plugin riêng, để Tổng kết / Tản nhiệt / Tổng quan cùng đọc 1 chỗ).
/// File: %APPDATA%\YanJi\settings.json (chỉ lưu thời lượng nướng + tự dừng, theo
/// máy test, không theo USB). File hỏng / thiếu thì về mặc định, không crash app.
/// Hồ sơ từng máy luôn lưu vào thư mục app \ KetQua (portable, tự tạo khi lưu),
/// không còn đường dẫn tùy chọn, không fallback về %APPDATA%.</summary>
public static class AppSettings
{
    const string FileName = "settings.json";

    static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YanJi", FileName);

    /// <summary>Thời lượng nướng khuyến nghị, phút (1–60, mặc định 3 — khớp đề xuất
    /// "stress nhanh 3 phút" trong thiết kế; chỉ là khuyến nghị, không tự dừng
    /// trừ khi AutoStopStress bật). Chỉnh ngay trên trang Tản nhiệt.</summary>
    public static int StressMinutes { get; private set; } = 3;

    /// <summary>Tự dừng nướng máy khi đủ thời lượng khuyến nghị (mặc định TẮT —
    /// kỹ thuật viên có thể muốn nướng lâu hơn cho máy nghi ngờ, nên không tự
    /// dừng nếu chưa bật). Chỉnh ngay trên trang Tản nhiệt.</summary>
    public static bool AutoStopStress { get; private set; }

    static AppSettings() => Load();

    sealed record SettingsDto(int? StressMinutes, bool? AutoStopStress);

    public static void Load()
    {
        StressMinutes = 3;
        AutoStopStress = false;
        try
        {
            if (!File.Exists(SettingsPath)) return;
            var dto = JsonSerializer.Deserialize<SettingsDto>(File.ReadAllText(SettingsPath));
            if (dto == null) return;
            if (dto.StressMinutes is int m) StressMinutes = Math.Clamp(m, 1, 60);
            AutoStopStress = dto.AutoStopStress == true;
        }
        catch
        {
            // File hỏng thì về mặc định, không crash app; lần lưu sau sẽ ghi đè file hỏng
        }
    }

    /// <summary>Lưu cài đặt, trả về false nếu ghi file không được (kèm lý do để trang Tản nhiệt báo).
    /// File này dùng chung với host Settings (theme): chỉ đổi 2 trường của mình,
    /// giữ nguyên DarkTheme trong file cũ, để lưu thời lượng nướng không xóa theme
    /// (và ngược lại); trường UsbPath cũ đã bỏ tính năng thì xóa luôn.</summary>
    public static bool Save(int stressMinutes, bool autoStopStress, out string error)
    {
        error = "";
        StressMinutes = Math.Clamp(stressMinutes, 1, 60);
        AutoStopStress = autoStopStress;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            JsonObject obj;
            try
            {
                obj = File.Exists(SettingsPath)
                    ? JsonNode.Parse(File.ReadAllText(SettingsPath)) as JsonObject ?? new JsonObject()
                    : new JsonObject();
            }
            catch { obj = new JsonObject(); }
            obj.Remove("UsbPath");
            obj["StressMinutes"] = StressMinutes;
            obj["AutoStopStress"] = AutoStopStress;
            File.WriteAllText(SettingsPath, obj.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static void Reset() => Save(3, false, out _);

    /// <summary>Thư mục chứa hồ sơ KetQua: luôn là thư mục app \ KetQua (bản
    /// portable = USB). Không tự tạo thư mục ở đây — nơi lưu hồ sơ sẽ tự tạo.</summary>
    public static string GetKetQuaDir() => Path.Combine(AppContext.BaseDirectory, "KetQua");
}
