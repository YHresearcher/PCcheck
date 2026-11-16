using System.Text.RegularExpressions;

namespace YanJi.PluginSdk.Services;

/// <summary>Dịch chuỗi kết luận ổ / lý do ổ từ dạng canonical tiếng Việt sang
/// ngôn ngữ hiện tại. Sống ở PluginSdk vì 2 nơi cùng hiển thị kết luận ổ:
/// Đối chiếu cấu hình (trang tab "Kết luận ổ", PhaseText, ghi chú verdict)
/// và Đo tốc độ ổ (dòng sức khỏe) — plugin không được tham chiếu lẫn nhau
/// nên bảng dịch không thể nằm ở 1 plugin. Chuỗi canonical do
/// HardwareInfoService sinh (luồng nền) nên chỉ dịch ở lớp hiển thị (UI
/// thread) qua Lang.Get, giống như bảng dịch của trang Đối chiếu cấu hình.
/// Chuỗi nào không khớp mẫu thì trả nguyên văn (không bịa, không rớt dữ liệu).</summary>
public static class DriveVerdictText
{
    /// <summary>Dịch 1 dòng kết luận ổ đầy đủ ("CHẶN — ...", "CẢNH BÁO — ...",
    /// "ĐẠT — ...", "Không rõ — ..."). Mẫu CHẶN theo sức khỏe phải khớp TRƯỚC
    /// mẫu CHẶN chung (không thì mẫu chung nuốt luôn phần "sức khỏe ... (&lt;70%)"
    /// vào danh sách lý do). Không khớp mẫu nào thì trả nguyên văn.</summary>
    public static string TranslateVerdict(string verdict)
    {
        if (verdict == "Không rõ — kiểm tra tay bằng CrystalDiskInfo, chưa đủ dữ liệu để kết luận (không tự cho là tốt)")
            return Lang.Get("Config.VerdictLine.Unknown");
        Match m;
        if ((m = Regex.Match(verdict, @"^CHẶN — sức khỏe ([\d.,]+)% \(<70%\), thay ổ rồi kiểm tra lại$")).Success)
            return string.Format(Lang.Get("Config.VerdictLine.BlockedHealth"), m.Groups[1].Value);
        if ((m = Regex.Match(verdict, @"^CHẶN — (.+), thay ổ rồi kiểm tra lại$")).Success)
            return string.Format(Lang.Get("Config.VerdictLine.BlockedSerious"),
                TranslateJoinedFragments(m.Groups[1].Value));
        if ((m = Regex.Match(verdict, @"^CẢNH BÁO — (.+), tối đa mua có điều kiện$")).Success)
            return string.Format(Lang.Get("Config.VerdictLine.Warning"),
                TranslateJoinedFragments(m.Groups[1].Value));
        if ((m = Regex.Match(verdict, @"^ĐẠT — sức khỏe ([\d.,]+)% \(≥90%\), không có lỗi nghiêm trọng, mua được nguyên trạng$")).Success)
            return string.Format(Lang.Get("Config.VerdictLine.Pass"), m.Groups[1].Value);
        if ((m = Regex.Match(verdict, @"^ĐẠT — sức khỏe ([\d.,]+)% \(≥90%\), mua được nguyên trạng$")).Success)
            return string.Format(Lang.Get("Config.VerdictLine.PassWmi"), m.Groups[1].Value);
        return verdict;
    }

    /// <summary>Dịch 1 mảnh lý do (không có tiền tố "Ổ"): các mẫu chặn/cảnh
    /// báo của DriveVerdict (cả dạng dài "(bình thường phải 0)" lẫn dạng
    /// ngắn của SeriousErrorsText) + lý do không khớp SMBIOS. Trả null nếu
    /// không khớp mẫu nào — caller giữ nguyên văn.</summary>
    public static string? TranslateReasonFragment(string fragment)
    {
        Match m;
        if ((m = Regex.Match(fragment, @"^cảnh báo nghiêm trọng NVMe (\d+) \(bình thường phải 0\)$")).Success)
            return string.Format(Lang.Get("Config.Reason.NvmeWarning"), m.Groups[1].Value);
        if ((m = Regex.Match(fragment, @"^lỗi toàn vẹn dữ liệu (\d+) lần \(bình thường phải 0\)$")).Success)
            return string.Format(Lang.Get("Config.Reason.MediaErrors"), m.Groups[1].Value);
        if ((m = Regex.Match(fragment, @"^dự phòng NVMe còn (\d+)% dưới ngưỡng (\d+)%$")).Success)
            return string.Format(Lang.Get("Config.Reason.SpareBelow"), m.Groups[1].Value, m.Groups[2].Value);
        if ((m = Regex.Match(fragment, @"^sector đã thay thế (\d+) \(bình thường phải 0\)$")).Success)
            return string.Format(Lang.Get("Config.Reason.Reallocated"), m.Groups[1].Value);
        if ((m = Regex.Match(fragment, @"^sector chờ thay thế (\d+) \(bình thường phải 0\)$")).Success)
            return string.Format(Lang.Get("Config.Reason.Pending"), m.Groups[1].Value);
        if ((m = Regex.Match(fragment, @"^sector không sửa được (\d+) \(bình thường phải 0\)$")).Success)
            return string.Format(Lang.Get("Config.Reason.Uncorrectable"), m.Groups[1].Value);
        if ((m = Regex.Match(fragment, @"^lỗi không sửa được đã báo (\d+) \(bình thường phải 0\)$")).Success)
            return string.Format(Lang.Get("Config.Reason.ReportedUncorrectable"), m.Groups[1].Value);
        if ((m = Regex.Match(fragment, @"^sức khỏe ([\d.,]+)% \(<70%\)$")).Success)
            return string.Format(Lang.Get("Config.Reason.HealthLow"), m.Groups[1].Value);
        if ((m = Regex.Match(fragment, @"^sức khỏe ([\d.,]+)% \(<90%\)$")).Success)
            return string.Format(Lang.Get("Config.Reason.HealthWarn"), m.Groups[1].Value);
        if ((m = Regex.Match(fragment, @"^lỗi CRC (\d+) lần \(kiểm tra cáp/cổng rồi test lại\)$")).Success)
            return string.Format(Lang.Get("Config.Reason.Crc"), m.Groups[1].Value);
        if (fragment == "không đọc được lỗi nghiêm trọng (chưa biết có lỗi hay không, không được coi là khỏe)")
            return Lang.Get("Config.Reason.SmartUnknown");
        if ((m = Regex.Match(fragment, @"^cảnh báo nghiêm trọng NVMe (\d+)$")).Success)
            return string.Format(Lang.Get("Config.Reason.NvmeWarningShort"), m.Groups[1].Value);
        if ((m = Regex.Match(fragment, @"^lỗi toàn vẹn dữ liệu (\d+) lần$")).Success)
            return string.Format(Lang.Get("Config.Reason.MediaErrorsShort"), m.Groups[1].Value);
        if ((m = Regex.Match(fragment, @"^dự phòng còn (\d+)% dưới ngưỡng (\d+)%$")).Success)
            return string.Format(Lang.Get("Config.Reason.SpareBelowShort"), m.Groups[1].Value, m.Groups[2].Value);
        if ((m = Regex.Match(fragment, @"^sector đã thay thế (\d+)$")).Success)
            return string.Format(Lang.Get("Config.Reason.ReallocatedShort"), m.Groups[1].Value);
        if ((m = Regex.Match(fragment, @"^sector chờ thay thế (\d+)$")).Success)
            return string.Format(Lang.Get("Config.Reason.PendingShort"), m.Groups[1].Value);
        if ((m = Regex.Match(fragment, @"^sector không sửa được (\d+)$")).Success)
            return string.Format(Lang.Get("Config.Reason.UncorrectableShort"), m.Groups[1].Value);
        if ((m = Regex.Match(fragment, @"^lỗi không sửa được đã báo (\d+)$")).Success)
            return string.Format(Lang.Get("Config.Reason.ReportedShort"), m.Groups[1].Value);
        if ((m = Regex.Match(fragment, @"^(Model máy|Model mainboard|S/N máy|S/N mainboard): SMBIOS '(.*)' ≠ WMI '(.*)'$")).Success)
        {
            string labelKey = m.Groups[1].Value switch
            {
                "Model máy" => "Config.Reason.LabelMachineModel",
                "Model mainboard" => "Config.Reason.LabelBoardModel",
                "S/N máy" => "Config.Reason.LabelMachineSerial",
                _ => "Config.Reason.LabelBoardSerial",
            };
            return string.Format(Lang.Get("Config.Reason.Mismatch"),
                Lang.Get(labelKey), m.Groups[2].Value, m.Groups[3].Value);
        }
        return null;
    }

    /// <summary>Dịch chuỗi lý do nối "; " theo từng mảnh; mảnh nào không dịch
    /// được thì trả nguyên cả chuỗi (không dịch nửa vời).</summary>
    public static string TranslateJoinedFragments(string joined)
    {
        var parts = joined.Split("; ");
        var translated = new List<string>();
        foreach (var p in parts)
        {
            string? t = TranslateReasonFragment(p);
            if (t == null) return joined;
            translated.Add(t);
        }
        return string.Join("; ", translated);
    }
}
