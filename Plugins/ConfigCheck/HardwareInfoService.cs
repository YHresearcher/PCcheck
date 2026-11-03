using Microsoft.Win32;
using System.Management;
using System.Text;
using YanJi.PluginSdk.Services;

namespace YanJi.Plugin.ConfigCheck;

/// <summary>硬件配置采集：优先 LibreHardwareMonitor（开源，源码项目引用），
/// 它拿不到的字段退回 WMI（System.Management，Windows 自带）/ 注册表 / EDID 补充。
/// 采集较耗时，调用方应放后台线程（Task.Run）；每个分组独立容错——单项失败显示「获取失败」，不影响其它项。</summary>
public static class HardwareInfoService
{
    const string Cimv2 = @"root\cimv2";

    /// <summary>采集进度（Step 从 0 起，Total 为总阶段数）。</summary>
    public record CollectProgress(int Step, int Total, string Stage);

    /// <summary>采集整机硬件信息（CPU / 显卡 / 内存 / 硬盘 / 屏幕 / 电池）。</summary>
    public static HardwareReport Collect(IProgress<CollectProgress>? progress = null)
    {
        var report = new HardwareReport();
        const int total = 7;

        // LibreHardwareMonitor 整体只 Open 一次，快照分给各分组用
        progress?.Report(new(0, total, "CPU"));
        var lhm = LhmService.Collect();

        report.Groups.Add(SafeGroup("CPU", g => CollectCpu(g, lhm)));
        progress?.Report(new(1, total, "Card màn hình (GPU)"));
        report.Groups.Add(SafeGroup("Card màn hình (GPU)", g => CollectGpu(g, lhm)));
        progress?.Report(new(2, total, "RAM"));
        report.Groups.Add(SafeGroup("RAM", g => CollectMemory(g, lhm)));
        progress?.Report(new(3, total, "Ổ"));
        report.Groups.Add(SafeGroup("Ổ", g => CollectDisks(g, lhm, report)));
        progress?.Report(new(4, total, "Card mạng"));
        report.Groups.Add(SafeGroup("Card mạng", CollectNics));
        progress?.Report(new(5, total, "Màn hình"));
        report.Groups.Add(SafeGroup("Màn hình", CollectDisplays));
        progress?.Report(new(6, total, "Pin"));
        report.Groups.Add(SafeGroup("Pin", g => CollectBattery(g, lhm)));
        return report;
    }

    /// <summary>分组容错壳：组内任何异常 → 该组只显示「获取失败」，不波及其它组。</summary>
    static InfoGroup SafeGroup(string title, Action<InfoGroup> fill)
    {
        var g = new InfoGroup(title);
        try { fill(g); }
        catch (Exception ex) { g.Add("Kết quả thu thập", "Thu thập thất bại: " + ex.Message); }
        if (g.Entries.Count == 0) g.Add("Kết quả thu thập", "Không phát hiện");
        return g;
    }

    /// <summary>多子项分隔：第 2 个及以后的子项前插一个空行。</summary>
    static void Separator(InfoGroup g, int index)
    {
        if (index > 0) g.Add("", "");
    }

    // ═══ CPU ═══

    static void CollectCpu(InfoGroup g, LhmService.Snapshot lhm)
    {
        var cpus = WmiQuery(Cimv2,
            "SELECT Name, NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor");
        for (int i = 0; i < cpus.Count; i++)
        {
            var mo = cpus[i];
            Separator(g, i);
            string tag = cpus.Count > 1 ? $" #{i + 1}" : "";
            // 型号：LHM（CPUID 直读）优先，拿不到退回 WMI
            string name = i < lhm.Cpus.Count && lhm.Cpus[i].Length > 0
                ? lhm.Cpus[i]
                : GetStr(mo, "Name");
            g.Add("Model" + tag, name);
            g.Add("Nhân / Luồng" + tag,
                $"{GetU32(mo, "NumberOfCores")} nhân / {GetU32(mo, "NumberOfLogicalProcessors")} luồng");
        }
        // WMI 也没查到但 LHM 有：保底列出 LHM 的型号
        if (cpus.Count == 0)
            for (int i = 0; i < lhm.Cpus.Count; i++)
            {
                Separator(g, i);
                g.Add(lhm.Cpus.Count > 1 ? $"Model #{i + 1}" : "Model", lhm.Cpus[i]);
            }
    }

    // ═══ GPU ═══

    static void CollectGpu(InfoGroup g, LhmService.Snapshot lhm)
    {
        var gpus = WmiQuery(Cimv2,
            "SELECT Name, AdapterRAM, PNPDeviceID, DriverVersion FROM Win32_VideoController");
        // 显存精度优先级：LHM「GPU Memory Total」（NVAPI/ADL 直读）> 注册表 qwMemorySize > WMI AdapterRAM（uint32，>4GB 溢出，仅兜底）
        var regVram = ReadRegistryVram();
        for (int i = 0; i < gpus.Count; i++)
        {
            var mo = gpus[i];
            Separator(g, i);
            string name = GetStr(mo, "Name");
            string tag = gpus.Count > 1 ? $" #{i + 1}" : "";
            g.Add("Model" + tag, name);

            var lhmGpu = FindByName(lhm.Gpus.Select(x => (x.Name, x)), name)?.Obj;
            if (lhmGpu?.VramMB is > 0)
            {
                g.Add("VRAM" + tag, FormatSize((long)(lhmGpu.VramMB.Value * 1024 * 1024)));
            }
            else
            {
                long? vram = MatchRegistryVram(regVram, name, GetStr(mo, "PNPDeviceID"));
                if (vram is > 0)
                    g.Add("VRAM" + tag, FormatSize(vram.Value));
                else
                {
                    ulong wmi = GetU64(mo, "AdapterRAM");
                    g.Add("VRAM" + tag, wmi > 0 ? $"Khoảng {FormatSize((long)wmi)} (giá trị ước tính từ WMI)" : "Không đọc được");
                }
            }

            string drv = GetStr(mo, "DriverVersion");
            if (drv.Length > 0) g.Add("Phiên bản driver" + tag, drv);
        }
        // WMI 没查到但 LHM 有：保底列出
        if (gpus.Count == 0)
            for (int i = 0; i < lhm.Gpus.Count; i++)
            {
                Separator(g, i);
                var lg = lhm.Gpus[i];
                string tag = lhm.Gpus.Count > 1 ? $" #{i + 1}" : "";
                g.Add("Model" + tag, lg.Name);
                if (lg.VramMB is > 0)
                    g.Add("VRAM" + tag, FormatSize((long)(lg.VramMB.Value * 1024 * 1024)));
            }
    }

    /// <summary>显卡类注册表项里的准确显存（HardwareInformation.qwMemorySize，REG_QWORD）。</summary>
    record VramEntry(string MatchingDeviceId, string DriverDesc, long Bytes);

    static List<VramEntry> ReadRegistryVram()
    {
        var list = new List<VramEntry>();
        try
        {
            using var klass = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}");
            if (klass == null) return list;
            foreach (var sub in klass.GetSubKeyNames())
            {
                try
                {
                    using var key = klass.OpenSubKey(sub);
                    if (key?.GetValue("HardwareInformation.qwMemorySize") is not long bytes || bytes <= 0)
                        continue;
                    list.Add(new VramEntry(
                        ((key.GetValue("MatchingDeviceId") as string) ?? "").ToLowerInvariant(),
                        (key.GetValue("DriverDesc") as string) ?? "", bytes));
                }
                catch { /* 单个子键失败跳过 */ }
            }
        }
        catch { /* 无权限/键不存在 → 退回 WMI 估算 */ }
        return list;
    }

    static long? MatchRegistryVram(List<VramEntry> entries, string name, string pnpId)
    {
        // 先按 PNPDeviceID 包含 MatchingDeviceId 匹配（如 pci\ven_10de&dev_2520）
        string pnp = pnpId.ToLowerInvariant();
        foreach (var e in entries)
            if (e.MatchingDeviceId.Length > 0 && pnp.Contains(e.MatchingDeviceId))
                return e.Bytes;
        // 再按驱动描述 == WMI 名称
        foreach (var e in entries)
            if (e.DriverDesc.Length > 0 && e.DriverDesc.Equals(name, StringComparison.OrdinalIgnoreCase))
                return e.Bytes;
        return null;
    }

    // ═══ 内存 ═══

    static void CollectMemory(InfoGroup g, LhmService.Snapshot lhm)
    {
        var sticks = WmiQuery(Cimv2,
            "SELECT Capacity, Speed, ConfiguredClockSpeed, Manufacturer, PartNumber, DeviceLocator FROM Win32_PhysicalMemory");

        // 总容量：LHM（GlobalMemoryStatusEx）优先，退回 WMI 求和
        if (lhm.MemoryTotalGB is > 0)
            g.Add("Tổng dung lượng", FormatSize((long)(lhm.MemoryTotalGB.Value * 1073741824.0)));
        else
        {
            ulong total = 0;
            foreach (var mo in sticks) total += GetU64(mo, "Capacity");
            if (total > 0) g.Add("Tổng dung lượng", FormatSize((long)total));
        }

        // 实际运行频率（ConfiguredClockSpeed，BIOS/主板当前设定值，不是物料标称 Speed）；
        // 多根不一致时按实际值列出（如「4800 / 8000 MT/s」）
        var actual = sticks.Select(mo => GetU32(mo, "ConfiguredClockSpeed"))
            .Where(v => v > 0).Distinct().ToList();
        g.Add("Tần số", actual.Count > 0
            ? string.Join(" / ", actual.Select(v => $"{v} MT/s"))
            : "Không đọc được");

        // ── 详细信息（折叠区，默认收起）：插槽占用 + 每根内存条 ──
        int slots = QueryTotalSlots();
        int stickCount = lhm.Dimms.Count > 0 ? lhm.Dimms.Count : sticks.Count;
        if (stickCount > 0)
            g.AddDetail("Khe đang dùng", slots > 0 ? $"{stickCount} / {slots}" : $"{stickCount} thanh");

        if (lhm.Dimms.Count > 0)
        {
            // LHM SPD 直读（需 PawnIO 驱动）：名称含「厂商 - 料号」
            for (int i = 0; i < lhm.Dimms.Count; i++)
            {
                var d = lhm.Dimms[i];
                string cap = d.CapacityGB is > 0 ? FormatSize((long)(d.CapacityGB.Value * 1073741824.0)) + "　" : "";
                g.AddDetail($"Thanh RAM {i + 1}", cap + d.Name);
            }
        }
        else
        {
            // 退回 WMI：容量 / 标称频率（SPD）/ 实际频率（当前运行）/ 厂商 / 料号 / 插槽
            for (int i = 0; i < sticks.Count; i++)
            {
                var mo = sticks[i];
                var sb = new StringBuilder(FormatSize((long)GetU64(mo, "Capacity")));
                uint speed = GetU32(mo, "Speed");
                uint cfg = GetU32(mo, "ConfiguredClockSpeed");
                if (speed > 0) sb.Append($"　Định mức {speed} MT/s");
                if (cfg > 0) sb.Append($"　Thực tế {cfg} MT/s");
                string vendorPart = $"{GetStr(mo, "Manufacturer")} {GetStr(mo, "PartNumber")}".Trim();
                if (vendorPart.Length > 0) sb.Append($"　{vendorPart}");
                string loc = GetStr(mo, "DeviceLocator");
                g.AddDetail(loc.Length > 0 ? $"Thanh RAM {i + 1} ({loc})" : $"Thanh RAM {i + 1}", sb.ToString());
            }
        }
    }

    /// <summary>内存插槽总数（Win32_PhysicalMemoryArray.MemoryDevices 求和）；失败返回 0。</summary>
    static int QueryTotalSlots()
    {
        try
        {
            int sum = 0;
            foreach (var mo in WmiQuery(Cimv2, "SELECT MemoryDevices FROM Win32_PhysicalMemoryArray"))
                sum += (int)GetU32(mo, "MemoryDevices");
            return sum;
        }
        catch { return 0; }
    }

    // ═══ 硬盘 ═══

    static void CollectDisks(InfoGroup g, LhmService.Snapshot lhm, HardwareReport report)
    {
        // 每块盘四行：容量　(盘符)　型号（一行；盘符可多个，无盘符不显示括号）+ 健康度 + 通电时间
        // + 0E 介质与数据完整性错误（SMART，拿不到显示「未知」）；盘变多行后恢复盘间空行（与 GPU/屏幕一致）
        // + 一行「Kết luận ổ」tự động（见 DriveVerdict：≥90% ĐẠT，70-89% CẢNH BÁO，
        // &lt;70% 或 0E &gt;0 CHẶN，SMART không đọc được thì Không rõ — không tự cho là tốt）。
        if (lhm.Disks.Count > 0)
        {
            // 盘符按物理盘号关联：LHM StorageDeviceNumber = Windows 物理盘号 = Win32_DiskDrive.Index；
            // 对应不上（DeviceNumber 缺失或 WMI 无记录）就不显示括号，不报错
            var letters = QueryDiskLetters();
            for (int i = 0; i < lhm.Disks.Count; i++)
            {
                Separator(g, i);
                var d = lhm.Disks[i];
                string tag = lhm.Disks.Count > 1 ? $" #{i + 1}" : "";
                string cap = d.SizeBytes is > 0 ? FormatSize((long)d.SizeBytes.Value) : "Không rõ dung lượng";
                g.Add("Ổ" + tag, cap + FormatLetters(
                    d.DeviceNumber is uint n ? letters.GetValueOrDefault((int)n) : null)
                    + $"　{(d.Name.Length > 0 ? d.Name : "Không rõ model")}");
                g.Add("Sức khỏe" + tag, d.HealthPercent is int hp ? $"{hp} %" : "Không rõ");
                g.Add("Thời gian đã chạy" + tag, FormatHours(d.PowerOnHours));
                g.Add("Lỗi toàn vẹn dữ liệu" + tag, d.MediaErrors is long me ? $"{me} lần" : "Không rõ");
                g.Add("Lỗi nghiêm trọng" + tag, SeriousErrorsText(d));
                g.Add("Kết luận ổ" + tag, DriveVerdict(d, report, tag));
            }
            return;
        }

        // WMI 兜底通道没有 SMART：容量/型号照列，健康度/通电时间/0E「未知」（不强行反推）；
        // 本通道本来就有 Win32_DiskDrive.Index，直接关联盘符
        var disks = WmiQuery(Cimv2, "SELECT Model, Size, Index FROM Win32_DiskDrive");
        var wmiLetters = QueryDiskLetters();
        for (int i = 0; i < disks.Count; i++)
        {
            Separator(g, i);
            var mo = disks[i];
            string tag = disks.Count > 1 ? $" #{i + 1}" : "";
            ulong size = GetU64(mo, "Size");
            string cap = size > 0 ? FormatSize((long)size) : "Không rõ dung lượng";
            string model = GetStr(mo, "Model");
            g.Add("Ổ" + tag, cap + FormatLetters(wmiLetters.GetValueOrDefault((int)GetU32(mo, "Index")))
                + $"　{(model.Length > 0 ? model : "Không rõ model")}");
            g.Add("Sức khỏe" + tag, "Không rõ");
            g.Add("Thời gian đã chạy" + tag, "Không rõ");
            g.Add("Lỗi toàn vẹn dữ liệu" + tag, "Không rõ");
            g.Add("Kết luận ổ" + tag, DriveVerdict(null, null, report, tag));
        }
    }

    /// <summary>Dòng "Lỗi nghiêm trọng" cho 1 ổ: liệt kê mọi lỗi nghiêm trọng đã đọc
    /// được (&gt;0), "Không có" khi các lỗi đã đọc đều = 0, "Không rõ" khi không đọc
    /// được lỗi nghiêm trọng nào (không được hiểu là không có lỗi).</summary>
    static string SeriousErrorsText(LhmService.LhmDisk d)
    {
        var found = new List<string>();
        if (d.CriticalWarning is > 0) found.Add($"cảnh báo nghiêm trọng NVMe {d.CriticalWarning}");
        if (d.MediaErrors is > 0) found.Add($"lỗi toàn vẹn dữ liệu {d.MediaErrors} lần");
        if (d.AvailableSpare is int sp && d.AvailableSpareThreshold is int th && sp < th)
            found.Add($"dự phòng còn {sp}% dưới ngưỡng {th}%");
        if (d.Reallocated is > 0) found.Add($"sector đã thay thế {d.Reallocated}");
        if (d.Pending is > 0) found.Add($"sector chờ thay thế {d.Pending}");
        if (d.Uncorrectable is > 0) found.Add($"sector không sửa được {d.Uncorrectable}");
        if (d.ReportedUncorrectable is > 0) found.Add($"lỗi không sửa được đã báo {d.ReportedUncorrectable}");
        if (found.Count > 0) return string.Join("; ", found);
        bool anyKnown = d.CriticalWarning != null || d.MediaErrors != null
            || d.AvailableSpare != null || d.Reallocated != null || d.Pending != null
            || d.Uncorrectable != null || d.ReportedUncorrectable != null;
        return anyKnown ? "Không có" : "Không rõ";
    }

    /// <summary>Kết luận tự động cho 1 ổ, THEO MỨC ĐỘ NGHIÊM TRỌNG TRƯỚC, ngưỡng sức
    /// khỏe chỉ xét sau (sửa theo yêu cầu 2026-11-03):
    /// (1) Lỗi nghiêm trọng &gt;0 = CHẶN, bất kể sức khỏe cao bao nhiêu — ổ 95%
    /// nhưng có lỗi media / cảnh báo NVMe / sector đã thay thế-chờ-không sửa được
    /// vẫn CHẶN, không được về ĐẠT chỉ vì sức khỏe cao. Dự phòng NVMe dưới ngưỡng
    /// cũng CHẶN. (2) Sức khỏe &lt;70% = CHẶN. (3) Cảnh báo: sức khỏe 70–89%,
    /// hoặc lỗi CRC &gt;0 (thường do cáp/cổng, không chặn cứng ổ), hoặc không đọc
    /// được lỗi nghiêm trọng nào dù sức khỏe còn cao (không được báo ĐẠT khi chưa
    /// biết có lỗi hay không). (4) Sức khỏe ≥90% và đã đọc được ít nhất 1 lỗi
    /// nghiêm trọng = 0, không có lỗi nào &gt;0 = ĐẠT. (5) Còn lại = Không rõ.
    /// Lỗi CRC chỉ là cảnh báo vì nó thường do cáp, thay cáp test lại có thể hết;
    /// các lỗi sector/media/cảnh báo NVMe thì không hết bằng cách thay cáp nên chặn.</summary>
    static string DriveVerdict(LhmService.LhmDisk d, HardwareReport report, string tag)
    {
        int? health = d.HealthPercent;
        var blocks = new List<string>();
        if (d.CriticalWarning is > 0) blocks.Add($"cảnh báo nghiêm trọng NVMe {d.CriticalWarning} (bình thường phải 0)");
        if (d.MediaErrors is > 0) blocks.Add($"lỗi toàn vẹn dữ liệu {d.MediaErrors} lần (bình thường phải 0)");
        if (d.AvailableSpare is int sp && d.AvailableSpareThreshold is int th && sp < th)
            blocks.Add($"dự phòng NVMe còn {sp}% dưới ngưỡng {th}%");
        if (d.Reallocated is > 0) blocks.Add($"sector đã thay thế {d.Reallocated} (bình thường phải 0)");
        if (d.Pending is > 0) blocks.Add($"sector chờ thay thế {d.Pending} (bình thường phải 0)");
        if (d.Uncorrectable is > 0) blocks.Add($"sector không sửa được {d.Uncorrectable} (bình thường phải 0)");
        if (d.ReportedUncorrectable is > 0) blocks.Add($"lỗi không sửa được đã báo {d.ReportedUncorrectable} (bình thường phải 0)");
        if (blocks.Count > 0)
        {
            foreach (var b in blocks) report.DriveBlockReasons.Add($"Ổ{tag} {b}");
            return $"CHẶN — {string.Join("; ", blocks)}, thay ổ rồi kiểm tra lại";
        }
        if (health is < 70)
        {
            report.DriveBlockReasons.Add($"Ổ{tag} sức khỏe {health}% (<70%)");
            return $"CHẶN — sức khỏe {health}% (<70%), thay ổ rồi kiểm tra lại";
        }
        var warnings = new List<string>();
        if (health is < 90) warnings.Add($"sức khỏe {health}% (<90%)");
        if (d.CrcErrors is > 0) warnings.Add($"lỗi CRC {d.CrcErrors} lần (kiểm tra cáp/cổng rồi test lại)");
        bool anySeriousKnown = d.CriticalWarning != null || d.MediaErrors != null
            || d.AvailableSpare != null || d.Reallocated != null || d.Pending != null
            || d.Uncorrectable != null || d.ReportedUncorrectable != null;
        if (health != null && !anySeriousKnown)
            warnings.Add("không đọc được lỗi nghiêm trọng (chưa biết có lỗi hay không, không được coi là khỏe)");
        if (warnings.Count > 0)
        {
            report.HasDriveWarning = true;
            foreach (var w in warnings) report.DriveWarningReasons.Add($"Ổ{tag} {w}");
            return $"CẢNH BÁO — {string.Join("; ", warnings)}, tối đa bán có điều kiện";
        }
        if (health != null)
            return $"ĐẠT — sức khỏe {health}% (≥90%), không có lỗi nghiêm trọng, bán nguyên trạng";
        report.HasDriveUnknown = true;
        return "Không rõ — kiểm tra tay bằng CrystalDiskInfo, chưa đủ dữ liệu để kết luận (không tự cho là tốt)";
    }

    /// <summary>Bản cũ cho kênh WMI (không có SMART): chỉ có sức khỏe/media = null
    /// nên luôn về Không rõ, giữ lại để kênh WMI không bị chặn oan.</summary>
    static string DriveVerdict(int? health, long? mediaErrors, HardwareReport report, string tag)
    {
        if (mediaErrors is > 0)
        {
            report.DriveBlockReasons.Add($"Ổ{tag} lỗi toàn vẹn dữ liệu {mediaErrors} lần (bình thường phải 0)");
            return $"CHẶN — lỗi toàn vẹn dữ liệu {mediaErrors} lần, thay ổ rồi kiểm tra lại";
        }
        if (health is < 70)
        {
            report.DriveBlockReasons.Add($"Ổ{tag} sức khỏe {health}% (<70%)");
            return $"CHẶN — sức khỏe {health}% (<70%), thay ổ rồi kiểm tra lại";
        }
        if (health is < 90)
        {
            report.HasDriveWarning = true;
            return $"CẢNH BÁO — sức khỏe {health}% (<90%), tối đa bán có điều kiện";
        }
        if (health != null)
            return $"ĐẠT — sức khỏe {health}% (≥90%), bán nguyên trạng";
        report.HasDriveUnknown = true;
        return "Không rõ — kiểm tra tay bằng CrystalDiskInfo, chưa đủ dữ liệu để kết luận (không tự cho là tốt)";
    }

    /// <summary>盘符列表 → 「　(C: D:)」片段；null 或空列表返回空串（不显示括号）。</summary>
    static string FormatLetters(List<string>? letters) =>
        letters is { Count: > 0 } ? $"　({string.Join(" ", letters)})" : "";

    /// <summary>物理盘 Index → 盘符列表（如 0 → [C:];同一盘多盘符都收）。关联链：
    /// Win32_DiskDrive → Win32_DiskDriveToDiskPartition → Win32_LogicalDiskToPartition → Win32_LogicalDisk；
    /// 任何一步失败返回已有结果（最坏空表，调用方不显示括号）。</summary>
    static Dictionary<int, List<string>> QueryDiskLetters()
    {
        var map = new Dictionary<int, List<string>>();
        try
        {
            // 物理盘 DeviceID（\\.\PHYSICALDRIVE0）→ Index
            var diskIdToIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var mo in WmiQuery(Cimv2, "SELECT DeviceID, Index FROM Win32_DiskDrive"))
                diskIdToIndex[GetStr(mo, "DeviceID")] = (int)GetU32(mo, "Index");

            // 分区 DeviceID（Disk #0, Partition #1）→ 物理盘 Index
            var partToDisk = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var mo in WmiQuery(Cimv2, "SELECT Antecedent, Dependent FROM Win32_DiskDriveToDiskPartition"))
            {
                string partId = ExtractPathDeviceId(GetStr(mo, "Dependent"));
                if (partId.Length > 0 &&
                    diskIdToIndex.TryGetValue(ExtractPathDeviceId(GetStr(mo, "Antecedent")), out int idx))
                    partToDisk[partId] = idx;
            }

            // 分区 → 逻辑盘符（C:）
            foreach (var mo in WmiQuery(Cimv2, "SELECT Antecedent, Dependent FROM Win32_LogicalDiskToPartition"))
            {
                string letter = ExtractPathDeviceId(GetStr(mo, "Dependent"));
                if (letter.Length > 0 &&
                    partToDisk.TryGetValue(ExtractPathDeviceId(GetStr(mo, "Antecedent")), out int idx))
                {
                    if (!map.TryGetValue(idx, out var list)) map[idx] = list = [];
                    list.Add(letter);
                }
            }
            foreach (var list in map.Values) list.Sort(StringComparer.OrdinalIgnoreCase);
        }
        catch { /* 关联查询失败 → 无盘符可显示 */ }
        return map;
    }

    /// <summary>从 WMI 对象路径（形如 \\机器\root\cimv2:Win32_DiskDrive.DeviceID="\\\\.\\PHYSICALDRIVE0"）
    /// 取出 DeviceID 原值（去 \\ 转义）；取不到返回空串。</summary>
    static string ExtractPathDeviceId(string objectPath)
    {
        const string marker = "DeviceID=\"";
        int i = objectPath.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (i < 0) return "";
        i += marker.Length;
        int j = objectPath.IndexOf('"', i);
        return j < 0 ? "" : objectPath[i..j].Replace("\\\\", "\\");
    }

    /// <summary>通电时间：「N 小时」，≥24 小时附约天数。</summary>
    static string FormatHours(long? hours) =>
        hours is not > 0 ? "Không rõ" : hours >= 24 ? $"{hours} giờ (khoảng {hours / 24} ngày)" : $"{hours} giờ";

    // ═══ 网卡 ═══

    static void CollectNics(InfoGroup g)
    {
        // 全部物理网卡（含未连接），口径与设备管理器一致；名称不做清洗，原样显示
        var nics = WmiQuery(Cimv2,
            "SELECT Name FROM Win32_NetworkAdapter WHERE PhysicalAdapter = True");
        for (int i = 0; i < nics.Count; i++)
        {
            string tag = nics.Count > 1 ? $" #{i + 1}" : "";
            string name = GetStr(nics[i], "Name");
            g.Add("Card mạng" + tag, name.Length > 0 ? name : "Không rõ model");
        }
    }

    // ═══ 屏幕 ═══

    static void CollectDisplays(InfoGroup g)
    {
        var displays = DisplayInfoService.GetDisplays();
        for (int i = 0; i < displays.Count; i++)
        {
            Separator(g, i);
            var d = displays[i];
            string tag = displays.Count > 1 ? $" #{i + 1}" : "";
            g.Add("Độ phân giải" + tag, d.Width > 0 ? $"{d.Width} × {d.Height}" : "Không đọc được");
            g.Add("Tần số quét" + tag, d.Frequency > 0 ? $"{d.Frequency} Hz" : "Không đọc được");

            var e = d.Edid;
            g.Add("Kích thước" + tag, e is { DiagonalInches: > 0 }
                ? $"{e.DiagonalInches:0.0} inch ({e.WidthCm} × {e.HeightCm} cm)"
                : "Không đọc được");

            string mfgProd = e != null ? $"{e.Manufacturer} {e.ProductCode}".Trim() : "";
            g.Add("Tấm nền" + tag, e != null && e.PanelName.Length > 0
                ? (mfgProd.Length > 0 ? $"{e.PanelName}（{mfgProd}）" : e.PanelName)
                : "Không đọc được");

            // 注意：由 EDID 色度坐标算的是色域「容积比」而非覆盖率（顶点可超出标准色域，比值可 >100%）
            g.Add("Dải màu" + tag, e is { SrgbVolume: > 0 }
                ? $"≈ {e.SrgbVolume:0}% sRGB / {e.P3Volume:0}% DCI-P3 (tỉ lệ thể tích EDID)"
                : "Không đọc được");
        }
    }

    // ═══ 电池 ═══

    /// <summary>root\WMI：电池类驱动注册的 WMI 类所在命名空间（BatteryStaticData 等）。</summary>
    const string WmiRoot = @"root\WMI";

    static void CollectBattery(InfoGroup g, LhmService.Snapshot lhm)
    {
        // 容量：LHM（电池类驱动 IOCTL 封装）优先，退回 root\WMI 电池 WMI 类
        // （BatteryStaticData / BatteryFullChargedCapacity，powercfg /batteryreport 同源）；
        // 循环次数 LHM 未导出（BATTERY_INFORMATION.CycleCount 被其丢弃），只有
        // root\WMI BatteryCycleCount 一条通道，读不到显示「无法读取」，不反推。
        var wmiBatteries = WmiQuery(Cimv2, "SELECT Name FROM Win32_Battery");
        var statics = QueryRootWmiBattery("BatteryStaticData", "DesignedCapacity");
        var fulls = QueryRootWmiBattery("BatteryFullChargedCapacity", "FullChargedCapacity");
        var cycles = QueryRootWmiBattery("BatteryCycleCount", "CycleCount");

        int count = Math.Max(wmiBatteries.Count,
            Math.Max(lhm.Batteries.Count, Math.Max(statics.Count, fulls.Count)));
        if (count == 0)
        {
            g.Add("Pin", "Không phát hiện (máy bàn hoặc máy không có pin)");
            return;
        }

        for (int i = 0; i < count; i++)
        {
            Separator(g, i);
            string tag = count > 1 ? $" #{i + 1}" : "";
            // 双通道实例顺序都与电池序号一致，按索引对应
            double? designed = i < lhm.Batteries.Count ? lhm.Batteries[i].DesignedMWh : null;
            designed ??= i < statics.Count ? statics[i] : null;
            double? full = i < lhm.Batteries.Count ? lhm.Batteries[i].FullChargedMWh : null;
            full ??= i < fulls.Count ? fulls[i] : null;

            g.Add("Dung lượng thiết kế" + tag, designed is > 0 ? $"{designed:N0} mWh" : "Không đọc được");
            g.Add("Dung lượng sạc đầy" + tag, full is > 0 ? $"{full:N0} mWh" : "Không đọc được");

            if (designed is > 0 && full is > 0)
            {
                double health = Math.Clamp(full.Value / designed.Value * 100.0, 0.0, 100.0);
                g.Add("Độ chai" + tag, $"{100.0 - health:0.#} % (sức khỏe {health:0.#} %)");
            }
            else
                g.Add("Độ chai" + tag, "Không đọc được");

            uint cycle = i < cycles.Count ? cycles[i] : 0;
            g.Add("Số chu kỳ sạc" + tag, cycle is > 0 and < 0xFFFFFFFF ? $"{cycle} lần" : "Không đọc được");
        }
    }

    /// <summary>root\WMI 电池类单属性查询（实例顺序与电池序号一致）；类未注册/查询失败返回空表。</summary>
    static List<uint> QueryRootWmiBattery(string className, string prop)
    {
        var values = new List<uint>();
        try
        {
            foreach (var mo in WmiQuery(WmiRoot, $"SELECT {prop} FROM {className}"))
                values.Add(GetU32(mo, prop));
        }
        catch { /* 电池 WMI 类未注册（无电池/驱动不支持） */ }
        return values;
    }

    // ═══ 通用辅助 ═══

    /// <summary>在 (名称, 对象) 序列里按名称匹配（相等或互相包含，忽略大小写）。</summary>
    static (string Name, T Obj)? FindByName<T>(IEnumerable<(string Name, T Obj)> items, string name)
    {
        foreach (var it in items)
            if (it.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                return it;
        foreach (var it in items)
            if (name.Contains(it.Name, StringComparison.OrdinalIgnoreCase) ||
                it.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
                return it;
        return null;
    }

    static List<ManagementObject> WmiQuery(string scope, string wql)
    {
        using var searcher = new ManagementObjectSearcher(scope, wql);
        return searcher.Get().Cast<ManagementObject>().ToList();
    }

    static string GetStr(ManagementObject mo, string prop)
    {
        try { return mo[prop]?.ToString()?.Trim() ?? ""; } catch { return ""; }
    }

    static uint GetU32(ManagementObject mo, string prop)
    {
        try { return Convert.ToUInt32(mo[prop]); } catch { return 0; }
    }

    static ulong GetU64(ManagementObject mo, string prop)
    {
        try { return Convert.ToUInt64(mo[prop]); } catch { return 0; }
    }

    /// <summary>字节数 → GB/TB（1024 进制，与 Windows 口径一致）。</summary>
    static string FormatSize(long bytes)
    {
        double gb = bytes / 1073741824.0;
        return gb >= 1024 ? $"{gb / 1024:0.0#} TB" : $"{gb:0.#} GB";
    }
}
