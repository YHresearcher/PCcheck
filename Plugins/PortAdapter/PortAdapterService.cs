using System.Management;

namespace YanJi.Plugin.PortAdapter;

/// <summary>Quét cổng + sạc bằng WMI (Windows built-in, không dùng phần mềm thương mại).
/// Nguồn gốc: laptop-diagnostic-tool của Tanxe Studio (MIT), các hàm Get-ChargerInfo /
/// Test-AdapterHealth và phần phát hiện cổng trong FinalDiagnostic_v15.ps1, port sang C#.
///
/// Những chỗ KHÔNG copy nguyên bản, vì copy nguyên sẽ cho kết quả sai cho shop
/// (ghi rõ để lần sau khỏi "sửa lại cho giống gốc"):
/// 1. Gốc coi Win32_Battery.BatteryStatus = 2 là "đã cắm sạc". Trong Win32_Battery,
///    2 = Unknown (không rõ), không phải đang cắm sạc. Bản này dùng PowerOnline
///    của root\WMI BatteryStatus làm căn cứ chính, Win32_Battery chỉ làm dự phòng
///    với các mã chắc chắn: 3 = Sạc đầy, 6 = Đang sạc, 7 = Đang sạc và cao.
/// 2. Gốc tính mức pin từ BatteryStatus.RemainingCapacity / BatteryStatus.MaxCapacity,
///    nhưng lớp BatteryStatus không có thuộc tính MaxCapacity nên mức pin luôn ra 0.
///    Bản này lấy dung lượng sạc đầy từ lớp BatteryFullChargedCapacity (cùng nguồn
///    với powercfg /batteryreport mà module Đối chiếu cấu hình đã dùng).
/// 3. Gốc ước tính công suất sạc: nếu công suất trung bình vượt 330*0.85 thì rơi
///    về mặc định 65 W (sai). Bản này trả "330 W trở lên (ước tính)" trong trường hợp đó.
/// 4. Gốc ĐOÁN số cổng USB từ số root hub (1 hub = 2 cổng, 2 hub = 4 cổng...) và
///    ĐOÁN có HDMI từ đời CPU / tên máy (ThinkPad, Latitude...) rồi cuối cùng mặc
///    định luôn là có HDMI. Bản này KHÔNG đoán: chỉ báo những gì hệ thống thật sự
///    nhìn thấy (controller, hub, thiết bị USB-C/Thunderbolt, card mạng có dây),
///    còn cổng vật lý trên vỏ máy thì kỹ thuật viên kiểm tra bằng mắt và tick tay
///    ở trang này. Đoán cổng mà báo cho khách là bán hàng sai sự thật.
/// 5. Điện áp hiển thị là điện áp ở đầu pin (BatteryStatus.Voltage), KHÔNG phải
///    điện áp ra của củ sạc (thường 19-20 V, hệ thống không đo trực tiếp được).
///    Trang này ghi rõ điều đó, không để kỹ thuật viên nhầm.</summary>
public static class PortAdapterService
{
    const string Cimv2 = @"root\cimv2";
    const string WmiRoot = @"root\WMI";

    // ═══ Kết quả quét cổng ═══

    public record PortScanResult(
        int UsbControllerCount,
        List<string> UsbControllerNames,
        int RootHubCount,
        List<string> UsbCDeviceNames,
        bool UsbCPdSign,
        string? EthernetAdapterName,
        List<int> MonitorTechnologies);

    // ═══ Ảnh chụp nhanh sạc (1 lần đọc, không chờ 10 giây) ═══

    public record AdapterSnapshot(
        bool HasBattery,
        bool PowerOnline,
        bool Charging,
        double? VoltageVolts,
        double? ChargeWatts,
        double? BatteryPercent);

    // ═══ Kết quả test sạc 10 giây ═══

    public record AdapterTestResult(
        bool Tested,
        bool Connected,
        double VoltageMin,
        double VoltageMax,
        double VoltageAvg,
        string VoltageStability,
        double CurrentAvg,
        double PowerAvg,
        string EstimatedRating,
        double ChargingRateAvg,
        bool IsCharging,
        double BatteryStartPercent,
        double BatteryEndPercent,
        string Health,
        int HealthScore,
        List<string> Issues,
        List<string> Recommendations);

    // ═══ Quét cổng ═══

    public static PortScanResult ScanPorts()
    {
        var controllers = QueryStrings(Cimv2, "SELECT Name FROM Win32_USBController", "Name");
        var hubs = QueryStrings(Cimv2, "SELECT Description FROM Win32_USBHub", "Description");
        int rootHubs = hubs.Count(d => d.Contains("Root Hub", StringComparison.OrdinalIgnoreCase));

        // Thiết bị PnP: chỉ lấy theo TÊN (USB Type-C / Thunderbolt / USB4, Power Delivery).
        // Không dùng mẹo REV_03 của bản gốc (USB 3.x cổng A cũng có REV_03, đoán sai).
        var pnpNames = QueryStrings(Cimv2, "SELECT Name FROM Win32_PnPEntity", "Name");
        var usbC = pnpNames
            .Where(n => n.Contains("USB Type-C", StringComparison.OrdinalIgnoreCase)
                || n.Contains("USB-C", StringComparison.OrdinalIgnoreCase)
                || n.Contains("Thunderbolt", StringComparison.OrdinalIgnoreCase)
                || n.Contains("USB4", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();
        bool pdSign = pnpNames.Any(n =>
            n.Contains("Power Delivery", StringComparison.OrdinalIgnoreCase)
            || n.Contains("USB-C PD", StringComparison.OrdinalIgnoreCase)
            || n.Contains("Thunderbolt Charging", StringComparison.OrdinalIgnoreCase));

        // Card mạng có dây: tên chứa Ethernet/Gigabit/LAN/RJ45 và không phải WiFi/Virtual/Bluetooth.
        // Đây là card mạng, chưa chắc đã có cổng RJ45 trên vỏ — trang sẽ ghi rõ.
        string? ethernet = null;
        foreach (var mo in QueryObjects(Cimv2, "SELECT Name FROM Win32_NetworkAdapter"))
        {
            string name = GetStr(mo, "Name");
            if (name.Length == 0) continue;
            bool isEth = name.Contains("Ethernet", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Gigabit", StringComparison.OrdinalIgnoreCase)
                || name.Contains("LAN", StringComparison.OrdinalIgnoreCase)
                || name.Contains("RJ45", StringComparison.OrdinalIgnoreCase);
            bool excluded = name.Contains("Wireless", StringComparison.OrdinalIgnoreCase)
                || name.Contains("WiFi", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Virtual", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Bluetooth", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Miniport", StringComparison.OrdinalIgnoreCase);
            if (isEth && !excluded) { ethernet = name; break; }
        }

        // Kết nối màn hình HIỆN TẠI (không phải danh sách cổng): root\WMI WmiMonitorConnectionParams.
        // Nếu không cắm màn ngoài thì chỉ thấy màn hình nội bộ (LVDS/eDP), không suy ra cổng HDMI.
        var monitors = new List<int>();
        foreach (var mo in QueryObjects(WmiRoot, "SELECT VideoOutputTechnology FROM WmiMonitorConnectionParams"))
        {
            try
            {
                object? v = mo["VideoOutputTechnology"];
                if (v != null) monitors.Add(Convert.ToInt32(v));
            }
            catch { /* bỏ qua bản ghi lỗi */ }
        }

        return new PortScanResult(
            controllers.Count,
            controllers.Take(5).ToList(),
            rootHubs,
            usbC,
            pdSign,
            ethernet,
            monitors);
    }

    // ═══ Ảnh chụp nhanh sạc ═══

    public static AdapterSnapshot GetAdapterSnapshot()
    {
        var batteries = QueryObjects(Cimv2, "SELECT BatteryStatus FROM Win32_Battery");
        var status = QueryObjects(WmiRoot,
            "SELECT ChargeRate, Charging, Discharging, PowerOnline, RemainingCapacity, Voltage FROM BatteryStatus")
            .FirstOrDefault();
        var fulls = QueryObjects(WmiRoot, "SELECT FullChargedCapacity FROM BatteryFullChargedCapacity")
            .FirstOrDefault();

        bool hasBattery = batteries.Count > 0 || status != null;
        if (status == null)
            return new AdapterSnapshot(hasBattery, false, false, null, null, null);

        int chargeRateMw = GetInt(status, "ChargeRate");
        bool powerOnline = GetBool(status, "PowerOnline");
        if (!powerOnline && batteries.Count > 0)
        {
            // Dự phòng: Win32_Battery mã 3/6/7 mới chắc chắn là đang cắm sạc / sạc đầy.
            uint code = GetU32(batteries[0], "BatteryStatus");
            powerOnline = code is 3 or 6 or 7;
        }

        double? volts = null;
        uint rawVolts = GetU32(status, "Voltage");
        if (rawVolts > 0) volts = rawVolts / 1000.0;

        double? watts = chargeRateMw != 0 ? chargeRateMw / 1000.0 : null;

        double? percent = null;
        uint remaining = GetU32(status, "RemainingCapacity");
        uint full = fulls != null ? GetU32(fulls, "FullChargedCapacity") : 0;
        if (remaining > 0 && full > 0)
            percent = Math.Clamp(remaining * 100.0 / full, 0.0, 100.0);

        return new AdapterSnapshot(hasBattery, powerOnline, chargeRateMw > 0, volts, watts, percent);
    }

    // ═══ Test sạc 10 giây (giữ nguyên logic + ngưỡng của bản gốc) ═══

    public static async Task<AdapterTestResult> RunAdapterTestAsync(IProgress<string>? progress)
    {
        var first = await Task.Run(GetAdapterSnapshot);
        if (!first.HasBattery)
            return NotTested(false, "Không test được (không phát hiện pin — máy bàn hoặc máy không có pin)");

        if (!first.PowerOnline && !(first.ChargeWatts is > 0))
            return NotTested(false, "Chưa cắm sạc — cắm sạc rồi bấm \"Test sạc\" lại");

        var voltages = new List<double>();
        var powers = new List<double>();
        var currents = new List<double>();
        var chargeRates = new List<double>();
        bool isCharging = false;
        double startPercent = first.BatteryPercent ?? 0;

        // 10 mẫu, mỗi giây 1 mẫu (bản gốc: vòng lặp trong 10 giây, mẫu mỗi giây).
        for (int i = 0; i < 10; i++)
        {
            var snap = await Task.Run(GetAdapterSnapshot);
            if (snap.VoltageVolts is > 0) voltages.Add(snap.VoltageVolts.Value);
            if (snap.ChargeWatts != null)
            {
                powers.Add(snap.ChargeWatts.Value);
                if (snap.ChargeWatts.Value > 0)
                {
                    chargeRates.Add(snap.ChargeWatts.Value);
                    isCharging = true;
                    if (voltages.Count > 0 && voltages[^1] > 0)
                    {
                        double amps = snap.ChargeWatts.Value / voltages[^1];
                        if (amps > 0) currents.Add(amps);
                    }
                }
            }
            progress?.Report($"Mẫu {i + 1}/10: điện áp pin {(snap.VoltageVolts?.ToString("0.00") ?? "?")} V, " +
                $"công suất {(snap.ChargeWatts?.ToString("0.0") ?? "?")} W, " +
                $"pin {(snap.BatteryPercent?.ToString("0.#") ?? "?")} %");
            if (i < 9) await Task.Delay(1000);
        }

        var end = await Task.Run(GetAdapterSnapshot);
        double endPercent = end.BatteryPercent ?? startPercent;

        double vMin = voltages.Count > 0 ? voltages.Min() : 0;
        double vMax = voltages.Count > 0 ? voltages.Max() : 0;
        double vAvg = voltages.Count > 0 ? voltages.Average() : 0;
        double cAvg = currents.Count > 0 ? currents.Average() : 0;
        double pAvg = powers.Count > 0 ? powers.Average() : 0;
        double rAvg = chargeRates.Count > 0 ? chargeRates.Average() : 0;

        // Độ ổn định điện áp (ngưỡng bản gốc): <3% Xuất sắc, <5% Tốt, <10% Trung bình, còn lại Kém.
        string stability = "Không rõ";
        double variancePercent = 0;
        if (vAvg > 0)
        {
            variancePercent = (vMax - vMin) / vAvg * 100.0;
            stability = variancePercent < 3 ? "Xuất sắc"
                : variancePercent < 5 ? "Tốt"
                : variancePercent < 10 ? "Trung bình"
                : "Kém";
        }

        // Ước tính công suất định mức (bản gốc: mức phổ biến đầu tiên mà công suất
        // trung bình <= mức đó * 0.85; đã sửa trường hợp vượt quá mức lớn nhất).
        string rating = "Không rõ";
        if (pAvg > 0)
        {
            int[] common = [45, 65, 90, 120, 135, 150, 180, 230, 240, 330];
            rating = "330 W trở lên (ước tính)";
            foreach (int w in common)
            {
                if (pAvg <= w * 0.85) { rating = $"{w} W (ước tính)"; break; }
            }
        }

        // Điểm sức khỏe (ngưỡng bản gốc, giữ nguyên).
        int score = 100;
        var issues = new List<string>();
        var recommendations = new List<string>();
        if (!isCharging)
        {
            if (startPercent < 99)
            {
                score -= 50;
                issues.Add($"Đã cắm sạc nhưng pin không sạc (pin {startPercent:0.#} %)");
                recommendations.Add("Kiểm tra dây sạc và cổng sạc có lỏng, gãy không");
                recommendations.Add("Thử ổ điện khác");
            }
            else
            {
                recommendations.Add($"Pin đã đầy ({startPercent:0.#} %) nên không sạc nữa là bình thường");
            }
        }
        if (stability == "Kém")
        {
            score -= 30;
            issues.Add($"Điện áp không ổn định (chênh {variancePercent:0.#} %)");
            recommendations.Add("Sạc có thể sắp hỏng — cân nhắc thay sạc");
        }
        else if (stability == "Trung bình")
        {
            score -= 15;
            issues.Add($"Điện áp hơi dao động (chênh {variancePercent:0.#} %)");
            recommendations.Add("Theo dõi thêm — sạc hơi không ổn định");
        }
        if (rAvg > 0 && rAvg < 15)
        {
            score -= 20;
            issues.Add($"Tốc độ sạc thấp ({rAvg:0.#} W)");
            recommendations.Add("Sạc có thể yếu hơn chuẩn của máy hoặc đã xuống cấp");
        }
        string health = score >= 85 ? "Xuất sắc" : score >= 70 ? "Tốt" : score >= 50 ? "Trung bình" : "Kém";
        if (issues.Count == 0)
        {
            recommendations.Add("Sạc hoạt động bình thường");
            recommendations.Add("Giữ dây sạc gọn, tránh gập gãy để khỏi hỏng");
        }

        return new AdapterTestResult(
            true, true,
            vMin, vMax, vAvg, stability,
            cAvg, pAvg, rating, rAvg, isCharging,
            startPercent, endPercent,
            health, score, issues, recommendations);
    }

    static AdapterTestResult NotTested(bool connected, string reason) => new(
        false, connected,
        0, 0, 0, "Không rõ",
        0, 0, "Không rõ", 0, false,
        0, 0,
        reason, 0,
        [reason], ["Cắm sạc rồi bấm \"Test sạc\" lại"]);

    // ═══ WMI phụ trợ (theo mẫu của module Đối chiếu cấu hình) ═══

    static List<ManagementObject> QueryObjects(string scope, string wql)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(scope, wql);
            return searcher.Get().Cast<ManagementObject>().ToList();
        }
        catch { return []; }
    }

    static List<string> QueryStrings(string scope, string wql, string prop)
    {
        var list = new List<string>();
        foreach (var mo in QueryObjects(scope, wql))
        {
            string s = GetStr(mo, prop);
            if (s.Length > 0) list.Add(s);
        }
        return list;
    }

    static string GetStr(ManagementObject mo, string prop)
    {
        try { return mo[prop]?.ToString()?.Trim() ?? ""; } catch { return ""; }
    }

    static uint GetU32(ManagementObject mo, string prop)
    {
        try { return Convert.ToUInt32(mo[prop]); } catch { return 0; }
    }

    static int GetInt(ManagementObject mo, string prop)
    {
        try { return Convert.ToInt32(mo[prop]); } catch { return 0; }
    }

    static bool GetBool(ManagementObject mo, string prop)
    {
        try { return Convert.ToBoolean(mo[prop]); } catch { return false; }
    }
}
