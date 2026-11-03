using LibreHardwareMonitor.Hardware;

namespace YanJi.Plugin.ConfigCheck;

/// <summary>LibreHardwareMonitor（MPL-2.0 开源库，本项目以源码项目引用）采集封装。
/// 只取「型号/容量类」信息：无管理员权限、无 PawnIO 驱动时 LHM 会自动降级（能拿多少拿多少），
/// 拿不到的字段由 HardwareInfoService 退回 WMI / 注册表补充。采集较耗时，须在后台线程调用。</summary>
public static class LhmService
{
    /// <summary>显卡：型号 + 显存（MB，NVAPI/ADL 直读，无则为 null）。</summary>
    public record LhmGpu(string Name, double? VramMB);

    /// <summary>硬盘：型号 + 容量（字节）+ 健康度（%，SMART，null=未知）+ 通电时间（小时，null=未知）
    /// + 物理盘号（StorageDeviceNumber，与 Win32_DiskDrive.Index 对应，用于关联盘符，null=未知）
    /// + 0E 介质与数据完整性错误计数（NVMe SMART，null=未知/非 NVMe）
    /// + 严重错误计数（null=该项读不到/不适用，绝不能当成 0）：
    /// NVMe: CriticalWarning (0xE0 Raw), AvailableSpare (0xE2 Current), AvailableSpareThreshold (0xE3 Current)；
    /// SATA: Reallocated (0x05 Raw), Pending (0xC5 Raw), Uncorrectable (0xC6 Raw),
    /// ReportedUncorrectable (0xBB Raw), CrcErrors (0xC7 Raw)。
    /// 这些字段是「按严重程度结结论」的基础：健康度只是磨损%，错误计数才是盘体故障信号，
    /// 95% 健康度 + 错误计数 &gt;0 仍必须判严重，不能因为健康度高就报 ĐẠT。</summary>
    public record LhmDisk(string Name, ulong? SizeBytes, int? HealthPercent, long? PowerOnHours, uint? DeviceNumber, long? MediaErrors,
        long? CriticalWarning = null, int? AvailableSpare = null, int? AvailableSpareThreshold = null,
        long? Reallocated = null, long? Pending = null, long? Uncorrectable = null,
        long? ReportedUncorrectable = null, long? CrcErrors = null);

    /// <summary>内存条：SPD 名称（厂商 + 料号）+ 容量（GB）；仅 PawnIO 驱动可用时才有。</summary>
    public record LhmDimm(string Name, double? CapacityGB);

    /// <summary>电池：名称 + 设计容量（mWh）+ 完全充电容量（mWh）；IOCTL 读不到对应项时为 null。</summary>
    public record LhmBattery(string Name, double? DesignedMWh, double? FullChargedMWh);

    /// <summary>LHM 一次采集的快照；各列表为空表示 LHM 没拿到（退回 WMI）。</summary>
    public class Snapshot
    {
        public List<string> Cpus { get; } = [];
        public List<LhmGpu> Gpus { get; } = [];
        public List<LhmDisk> Disks { get; } = [];
        public List<LhmDimm> Dimms { get; } = [];
        public List<LhmBattery> Batteries { get; } = [];
        /// <summary>物理内存总量（GB，GlobalMemoryStatusEx 口径）。</summary>
        public double? MemoryTotalGB { get; set; }
        /// <summary>整体失败原因（Computer.Open 抛异常时记录，供诊断展示）。</summary>
        public string? Error { get; set; }
    }

    /// <summary>采集整机快照。任何阶段异常都只影响对应类别，不向外抛。</summary>
    public static Snapshot Collect()
    {
        var snap = new Snapshot();
        var computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsStorageEnabled = true,
            IsBatteryEnabled = true,
        };
        try
        {
            computer.Open();
            foreach (IHardware hw in computer.Hardware)
            {
                try { CollectHardware(hw, snap); }
                catch { /* 单设备失败跳过 */ }
            }
        }
        catch (Exception ex)
        {
            snap.Error = ex.Message;
        }
        finally
        {
            try { computer.Close(); } catch { /* 关闭失败不影响结果 */ }
        }
        return snap;
    }

    static void CollectHardware(IHardware hw, Snapshot snap)
    {
        // 多数「总量/显存」传感器要 Update 一次才有值
        try { hw.Update(); } catch { /* 更新失败仍用构造期数据 */ }

        switch (hw.HardwareType)
        {
            case HardwareType.Cpu:
                if (!string.IsNullOrWhiteSpace(hw.Name))
                    snap.Cpus.Add(hw.Name.Trim());
                break;

            case HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel:
                snap.Gpus.Add(new LhmGpu(hw.Name.Trim(), FindSensorMB(hw, "GPU Memory Total")));
                break;

            case HardwareType.Memory:
                CollectMemory(hw, snap);
                break;

            case HardwareType.Storage:
                CollectStorage(hw, snap);
                break;

            case HardwareType.Battery:
                // 容量传感器构造期即有值（来自 BATTERY_INFORMATION），单位 mWh
                snap.Batteries.Add(new LhmBattery(hw.Name.Trim(),
                    FindSensorMWh(hw, "Designed Capacity"), FindSensorMWh(hw, "Fully-Charged Capacity")));
                break;
        }

        // 内嵌子硬件（如主板下的 SuperIO）也一并扫，防御性处理
        foreach (IHardware sub in hw.SubHardware)
        {
            try { CollectHardware(sub, snap); }
            catch { /* 单设备失败跳过 */ }
        }
    }

    static void CollectMemory(IHardware hw, Snapshot snap)
    {
        // LHM 内存硬件有三类：/ram = TotalMemory（总量）、/vram = VirtualMemory（忽略）、
        // /memory/dimm/N = DIMM（SPD 直读，需 PawnIO 驱动）——只把真正的 DIMM 计入
        string id = hw.Identifier.ToString();
        if (id.Equals("/ram", StringComparison.OrdinalIgnoreCase))
        {
            // Total Memory：总量 = 已用 + 可用（SensorType.Data，单位 GB）
            double? used = FindSensorGB(hw, "Memory Used");
            double? avail = FindSensorGB(hw, "Memory Available");
            if (used is > 0 && avail is >= 0)
                snap.MemoryTotalGB = used.Value + avail.Value;
        }
        else if (id.Contains("/dimm/", StringComparison.OrdinalIgnoreCase))
        {
            // DIMM #n：名称已含「厂商 - 料号」
            snap.Dimms.Add(new LhmDimm(hw.Name.Trim(), FindSensorGB(hw, "Capacity")));
        }
    }

    static void CollectStorage(IHardware hw, Snapshot snap)
    {
        // StorageDevice 是 LHM 公开类型，直接读 DiskInfoToolkit 的属性（型号/容量）与 SMART（健康度/通电时间）
        if (hw is LibreHardwareMonitor.Hardware.Storage.StorageDevice dev)
        {
            var s = dev.Storage;
            snap.Disks.Add(new LhmDisk(
                (s.ProductName ?? hw.Name).Trim(),
                s.DiskSizeBytes,
                ComputeHealth(s),
                ComputePowerOnHours(s),
                s.StorageDeviceNumber,
                ComputeMediaErrors(s),
                ComputeNvmeCriticalWarning(s),
                ComputeNvmeAvailableSpare(s),
                ComputeNvmeAvailableSpareThreshold(s),
                ComputeSataError(s, 0x05),
                ComputeSataError(s, 0xC5),
                ComputeSataError(s, 0xC6),
                ComputeSataError(s, 0xBB),
                ComputeSataError(s, 0xC7)));
        }
        else
        {
            snap.Disks.Add(new LhmDisk(hw.Name.Trim(), null, null, null, null, null));
        }
    }

    /// <summary>健康度（%，参照 CrystalDiskInfo 口径）：NVMe = 100 − SMART「Percentage Used」（属性 0xE4）
    /// 的 <b>RawValue</b>（NVMe 规范原值）——注意 DiskInfoToolkit 把 NVMe 条目的 CurrentValue 填成已折算的
    /// 「剩余寿命%」（= 100 − RawValue），若对它再取 100 − CurrentValue 会双重取反，新盘显示成 0%；
    /// SATA = 常见寿命属性（0xE7 SSD Life Left / 0xAD Wear Leveling Count，取可用者）；
    /// SMART 读不到（如无权限）返回 null → 显示「未知」，不强行用 WMI 反推。</summary>
    static int? ComputeHealth(DiskInfoToolkit.StorageDevice s)
    {
        try
        {
            if (s.TransportKind == DiskInfoToolkit.StorageTransportKind.Nvme)
            {
                var used = s.SmartAttributes.FirstOrDefault(a => a.ID == 0xE4);
                if (used != null) return (int)Math.Clamp(100 - (long)used.RawValue, 0, 100);
            }
            foreach (byte id in new byte[] { 0xE7, 0xAD })
            {
                var attr = s.SmartAttributes.FirstOrDefault(a => a.ID == id && a.CurrentValue > 0);
                if (attr != null) return attr.CurrentValue;
            }
        }
        catch { /* SMART 读取失败 → 未知 */ }
        return null;
    }

    /// <summary>通电时间（小时）：DiskInfoToolkit 解析好的 PowerOnHours（NVMe 同名）优先，
    /// 退回 SATA SMART 属性 0x09 的 RawValue；读不到返回 null。</summary>
    static long? ComputePowerOnHours(DiskInfoToolkit.StorageDevice s)
    {
        try
        {
            if (s.PowerOnHours.HasValue) return (long)s.PowerOnHours.Value;
            var attr = s.SmartAttributes.FirstOrDefault(a => a.ID == 0x09 && a.RawValue > 0);
            if (attr != null) return (long)attr.RawValue;
        }
        catch { /* SMART 读取失败 → 未知 */ }
        return null;
    }

    /// <summary>0E 介质与数据完整性错误计数（CrystalDiskInfo 的 NVMe 列表第 0E 项 = NVMe SMART log
    /// 偏移 160 的 Media and Data Integrity Errors；验机关注项，正常应为 0，非 0 多为盘体/主控异常前兆）。
    /// 注意 DiskInfoToolkit 对 NVMe 属性用 0xE0+ 序号编号，此项实为 <b>0xED</b>（0x0E 是 CDI 显示序号，
    /// 直接按 0x0E 匹配永远落空）。仅 NVMe 盘有此语义（SATA 的 0x0E 含义不同，不显示）；读不到返回 null。</summary>
    static long? ComputeMediaErrors(DiskInfoToolkit.StorageDevice s)
    {
        try
        {
            if (s.TransportKind != DiskInfoToolkit.StorageTransportKind.Nvme) return null;
            var attr = s.SmartAttributes.FirstOrDefault(a => a.ID == 0xED);
            if (attr != null) return (long)attr.RawValue;
        }
        catch { /* SMART 读取失败 → 未知 */ }
        return null;
    }

    /// <summary>NVMe Critical Warning（SMART log 偏移 0，DiskInfoToolkit 编号 0xE0）：
    /// 0 = 正常，&gt;0 = 有严重警告位（备用空间不足 / 过热 / 可靠性下降 / 只读 / 备份失败），
    /// 任何位都不该出现在待售机器上。仅 NVMe 有此语义；读不到返回 null。</summary>
    static long? ComputeNvmeCriticalWarning(DiskInfoToolkit.StorageDevice s)
    {
        try
        {
            if (s.TransportKind != DiskInfoToolkit.StorageTransportKind.Nvme) return null;
            var attr = s.SmartAttributes.FirstOrDefault(a => a.ID == 0xE0);
            if (attr != null) return (long)attr.RawValue;
        }
        catch { /* SMART 读取失败 → 未知 */ }
        return null;
    }

    /// <summary>NVMe Available Spare（0xE2，CurrentValue = 剩余备用空间 %）与
    /// Available Spare Threshold（0xE3，CurrentValue = 阈值 %）。
    /// 剩余 &lt; 阈值 = 备用空间耗尽，盘按规范已属故障，即使健康度（Percentage Used）
    /// 看起来还高。仅 NVMe；读不到返回 null。</summary>
    static int? ComputeNvmeAvailableSpare(DiskInfoToolkit.StorageDevice s)
    {
        try
        {
            if (s.TransportKind != DiskInfoToolkit.StorageTransportKind.Nvme) return null;
            var attr = s.SmartAttributes.FirstOrDefault(a => a.ID == 0xE2);
            if (attr != null) return attr.CurrentValue;
        }
        catch { /* SMART 读取失败 → 未知 */ }
        return null;
    }

    static int? ComputeNvmeAvailableSpareThreshold(DiskInfoToolkit.StorageDevice s)
    {
        try
        {
            if (s.TransportKind != DiskInfoToolkit.StorageTransportKind.Nvme) return null;
            var attr = s.SmartAttributes.FirstOrDefault(a => a.ID == 0xE3);
            if (attr != null) return attr.CurrentValue;
        }
        catch { /* SMART 读取失败 → 未知 */ }
        return null;
    }

    /// <summary>SATA 严重错误计数（RawValue 就是次数，正常必须为 0）：
    /// 0x05 已重映射扇区、0xC5 待重映射扇区、0xC6 无法纠正扇区、0xBB 已报告无法纠正错误、
    /// 0xC7 UltraDMA CRC 错误。NVMe 不用这套编号，返回 null（不把 NVMe 的同号属性
    /// 误读成 SATA 错误）。读不到返回 null，绝不能当成 0。</summary>
    static long? ComputeSataError(DiskInfoToolkit.StorageDevice s, byte id)
    {
        try
        {
            if (s.TransportKind == DiskInfoToolkit.StorageTransportKind.Nvme) return null;
            var attr = s.SmartAttributes.FirstOrDefault(a => a.ID == id);
            if (attr != null) return (long)attr.RawValue;
        }
        catch { /* SMART 读取失败 → 未知 */ }
        return null;
    }

    /// <summary>SmallData 传感器值（MB）。</summary>
    static double? FindSensorMB(IHardware hw, string name) =>
        hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.SmallData && s.Name == name)
            ?.Value is float v && v > 0 ? v : null;

    /// <summary>Data 传感器值（GB）。</summary>
    static double? FindSensorGB(IHardware hw, string name) =>
        hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Data && s.Name == name)
            ?.Value is float v && v >= 0 ? v : null;

    /// <summary>Energy 传感器值（mWh）。</summary>
    static double? FindSensorMWh(IHardware hw, string name) =>
        hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Energy && s.Name == name)
            ?.Value is float v && v > 0 ? v : null;
}
