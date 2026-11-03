using System.Runtime.InteropServices;

namespace YanJi.PluginSdk.Services;

/// <summary>Đọc bảng SMBIOS thô từ firmware bằng GetSystemFirmwareTable('RSMB'),
/// KHÔNG qua WMI, KHÔNG qua registry — để sửa registry không đổi được thông tin
/// máy/mainboard hiện ở đây. Chỉ parse Type 0 (BIOS), 1 (máy), 2 (mainboard),
/// 3 (khung máy); các Type khác không cần cho nhóm Máy &amp; Mainboard nên không
/// parse (đặc biệt không parse Type 17 RAM: WMI PhysicalMemory vốn đã lấy từ
/// chính SMBIOS Type 17 nên đối chiếu 2 nguồn đó không bắt được fake ở tầng
/// firmware, chỉ thêm rủi ro sai offset).
/// Mỗi structure: [Type:1][Length:1][Handle:2][formatted area][chuỗi kết thúc
/// bằng 0, cả tập chuỗi kết thúc bằng 0 kép]. Chuỗi trong formatted area là
/// chỉ số 1-based vào tập chuỗi phía sau; 0 = không có chuỗi. Mọi lần đọc đều
/// kiểm tra giới hạn buffer — bảng firmware không được tin blindly.
/// Giá trị placeholder của hãng ("To be filled by O.E.M.", "Not Specified",
/// "Default string"...) được coi là "không có", không phải 1 model thật, và
/// không dùng để đối chiếu (tránh báo không khớp giả khi hãng để trống).</summary>
public static class SmbiosService
{
    public record SmbiosInfo(
        string? BiosVendor, string? BiosVersion, string? BiosDate,
        string? SystemManufacturer, string? SystemProduct, string? SystemVersion,
        string? SystemSerial, string? SystemUuid, string? SystemSku, string? SystemFamily,
        string? BoardManufacturer, string? BoardProduct, string? BoardVersion,
        string? BoardSerial, string? BoardAssetTag,
        string? ChassisManufacturer, byte? ChassisType, string? ChassisSerial,
        bool IsVirtualMachine);

    // Mã provider 'RSMB' theo cách đóng gói multichar của MSVC (ký tự đầu ở
    // byte cao nhất: 'R'<<24 | 'S'<<16 | 'M'<<8 | 'B'). Để chắc chắn dù cách
    // hiểu packing nào đúng, thử cả 2 giá trị, cái nào trả về dữ liệu thì dùng.
    const uint RsmbMsvc = 0x52534D42;
    const uint RsmbLittleEndian = 0x424D5352;

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern uint GetSystemFirmwareTable(uint firmwareTableProviderSignature, uint firmwareTableID, IntPtr pFirmwareTableBuffer, uint bufferSize);

    static readonly string[] Placeholders =
    [
        "", "to be filled by o.e.m.", "to be filled by oem", "to be filled",
        "not specified", "not applicable", "default string", "none", "unknown",
        "o.e.m.", "tbd", "n/a", "serial number to be filled",
    ];

    static readonly string[] VmMarkers =
    [
        "vmware", "qemu", "innotek", "virtualbox", "bochs", "xen", "kvm",
        "parallels", "virtual machine", "hyper-v",
    ];

    /// <summary>Đọc SMBIOS thô; thất bại (API không hỗ trợ, không đọc được) thì
    /// trả về null — nhóm Máy &amp; Mainboard khi đó hiện "không đọc được", không bịa.</summary>
    public static SmbiosInfo? Read()
    {
        try
        {
            foreach (uint signature in new[] { RsmbMsvc, RsmbLittleEndian })
            {
                uint size = GetSystemFirmwareTable(signature, 0, IntPtr.Zero, 0);
                if (size == 0) continue;
                var buffer = Marshal.AllocHGlobal((int)size);
                try
                {
                    uint read = GetSystemFirmwareTable(signature, 0, buffer, size);
                    if (read == 0 || read > size) continue;
                    var bytes = new byte[read];
                    Marshal.Copy(buffer, bytes, 0, (int)read);
                    return Parse(bytes);
                }
                finally { Marshal.FreeHGlobal(buffer); }
            }
        }
        catch { /* API lỗi → null, nhóm hiển thị không đọc được */ }
        return null;
    }

    static SmbiosInfo? Parse(byte[] table)
    {
        string? biosVendor = null, biosVersion = null, biosDate = null;
        string? sysMfg = null, sysProd = null, sysVer = null, sysSerial = null, sysUuid = null, sysSku = null, sysFamily = null;
        string? boardMfg = null, boardProd = null, boardVer = null, boardSerial = null, boardAsset = null;
        string? chassisMfg = null, chassisSerial = null;
        byte? chassisType = null;
        bool foundAny = false;

        int pos = 0;
        while (pos + 4 <= table.Length)
        {
            byte type = table[pos];
            byte length = table[pos + 1];
            if (length < 4) break; // Bảng hỏng thì dừng, không đọc tiếp lung tung
            int end = pos + length;
            if (end > table.Length) break;
            // Tập chuỗi: bắt đầu sau formatted area, kết thúc bằng 0 kép
            int strStart = end;
            int strEnd = strStart;
            while (strEnd + 1 < table.Length && !(table[strEnd] == 0 && table[strEnd + 1] == 0))
                strEnd++;
            // strEnd trỏ tới 0 đầu của cặp 0 kép (hoặc cuối buffer nếu bảng cắt ngang)
            var strings = ParseStrings(table, strStart, strEnd);

            string? Str(int offset) =>
                offset < length ? Clean(strings, table[pos + offset]) : null;

            switch (type)
            {
                case 0:
                    biosVendor = Str(4); biosVersion = Str(5); biosDate = Str(8);
                    foundAny = true;
                    break;
                case 1:
                    sysMfg = Str(4); sysProd = Str(5); sysVer = Str(6); sysSerial = Str(7);
                    sysUuid = ReadUuid(table, pos, length);
                    sysSku = Str(25); sysFamily = Str(26);
                    foundAny = true;
                    break;
                case 2:
                    boardMfg = Str(4); boardProd = Str(5); boardVer = Str(6);
                    boardSerial = Str(7); boardAsset = Str(8);
                    foundAny = true;
                    break;
                case 3:
                    chassisMfg = Str(4);
                    if (5 < length) chassisType = (byte)(table[pos + 5] & 0x7F);
                    chassisSerial = Str(7);
                    foundAny = true;
                    break;
                case 127:
                    // End-of-table: dừng ngay, không đọc tiếp
                    pos = table.Length;
                    continue;
            }
            // Bước tới structure tiếp theo: sau 0 kép (2 byte), hoặc cuối buffer
            pos = Math.Min(strEnd + 2, table.Length);
            if (strEnd + 1 >= table.Length) break;
        }
        if (!foundAny) return null;

        bool isVm = VmMarkers.Any(m =>
            (sysMfg ?? "").Contains(m, StringComparison.OrdinalIgnoreCase) ||
            (sysProd ?? "").Contains(m, StringComparison.OrdinalIgnoreCase) ||
            (boardMfg ?? "").Contains(m, StringComparison.OrdinalIgnoreCase) ||
            (boardProd ?? "").Contains(m, StringComparison.OrdinalIgnoreCase));
        return new SmbiosInfo(biosVendor, biosVersion, biosDate,
            sysMfg, sysProd, sysVer, sysSerial, sysUuid, sysSku, sysFamily,
            boardMfg, boardProd, boardVer, boardSerial, boardAsset,
            chassisMfg, chassisType, chassisSerial, isVm);
    }

    static List<string> ParseStrings(byte[] table, int start, int end)
    {
        var list = new List<string>();
        int i = start;
        while (i < end)
        {
            int j = i;
            while (j < end && table[j] != 0) j++;
            list.Add(System.Text.Encoding.UTF8.GetString(table, i, j - i));
            i = j + 1;
        }
        return list;
    }

    /// <summary>Chuỗi SMBIOS: chỉ số 0 = không có; chuỗi placeholder = không có.
    /// Trả về chuỗi đã Trim, hoặc null.</summary>
    static string? Clean(List<string> strings, byte index)
    {
        if (index == 0 || index > strings.Count) return null;
        string s = strings[index - 1].Trim();
        if (s.Length == 0) return null;
        string lowered = s.ToLowerInvariant();
        if (Placeholders.Contains(lowered)) return null;
        return s;
    }

    /// <summary>UUID Type 1 (16 byte ở offset 8): toàn 0 hoặc toàn FF = chưa đặt.
    /// Hiển thị bằng Guid (byte đầu theo little-endian như quy ước x86); UUID chỉ
    /// để tham khảo, KHÔNG dùng để đối chiếu vì cách hiển thị UUID giữa các tool
    /// có thể lệch byte order mà vẫn là cùng 1 máy.</summary>
    static string? ReadUuid(byte[] table, int pos, int length)
    {
        if (length < 24 || pos + 24 > table.Length) return null;
        var bytes = new byte[16];
        Array.Copy(table, pos + 8, bytes, 0, 16);
        if (bytes.All(b => b == 0) || bytes.All(b => b == 0xFF)) return null;
        return new Guid(bytes).ToString();
    }

    /// <summary>Tên khung máy theo mã Type 3 (chỉ các mã thường gặp; mã lạ thì
    /// trả về mã số, không bịa tên).</summary>
    public static string ChassisTypeName(byte? type) => type switch
    {
        0x03 => "Desktop",
        0x04 => "Desktop thấp (Low Profile)",
        0x05 => "Pizza Box",
        0x06 => "Mini Tower",
        0x07 => "Tower",
        0x08 => "Portable",
        0x09 => "Laptop",
        0x0A => "Notebook",
        0x0B => "Hand Held",
        0x0D => "All in One",
        0x0E => "Sub Notebook",
        0x10 => "Lunch Box",
        0x11 => "Main Server Chassis",
        0x17 => "Rack Mount",
        null => "Không đọc được",
        _ => $"Mã {type}",
    };
}
