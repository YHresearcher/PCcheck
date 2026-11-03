namespace YanJi.Plugin.ConfigCheck;

/// <summary>一条「项目 = 值」展示行。</summary>
public record InfoEntry(string Name, string Value);

/// <summary>一个信息分组（如 处理器 / 显卡 / 内存 / 硬盘 / 屏幕）。</summary>
public class InfoGroup(string title)
{
    public string Title { get; } = title;
    public List<InfoEntry> Entries { get; } = [];
    /// <summary>「详细信息」折叠区条目（Expander，默认收起；为空则页面不显示该区域）。</summary>
    public List<InfoEntry> Details { get; } = [];
    public void Add(string name, string value) => Entries.Add(new InfoEntry(name, value));
    public void AddDetail(string name, string value) => Details.Add(new InfoEntry(name, value));
}

/// <summary>整机硬件采集结果（分组列表 + 采集时间）。</summary>
public class HardwareReport
{
    public DateTime CollectedAt { get; } = DateTime.Now;
    public List<InfoGroup> Groups { get; } = [];
    /// <summary>Kết luận ổ tự động: ổ nào bị CHẶN (sức khỏe &lt;70% hoặc lỗi toàn vẹn
    /// dữ liệu NVMe 0E &gt;0) thì liệt kê lý do ở đây; rỗng = không có ổ bị chặn.
    /// Trang Đối chiếu cấu hình chặn nút cho qua khi danh sách này không rỗng.</summary>
    public List<string> DriveBlockReasons { get; } = [];
    /// <summary>Có ổ ở mức CẢNH BÁO (sức khỏe 70-89%): cho qua được nhưng tối đa mua có điều kiện.</summary>
    public bool HasDriveWarning { get; set; }
    /// <summary>Lý do cảnh báo từng ổ (sức khỏe 70-89%, lỗi CRC, hoặc không đọc được
    /// lỗi nghiêm trọng dù sức khỏe còn cao — trường hợp cuối tuyệt đối không được
    /// báo là ĐẠT chỉ vì sức khỏe cao). Trang Tổng kết hiển thị danh sách này thay
    /// cho câu chung chung "sức khỏe &lt;90%" khi có.</summary>
    public List<string> DriveWarningReasons { get; } = [];
    /// <summary>Thông tin từng ổ vật lý (model, dung lượng, ký tự ổ, sức khỏe,
    /// kết luận ổ, lý do chặn/cảnh báo riêng từng ổ) để Đo tốc độ ổ làm kết luận
    /// ổ — do CollectDisks điền cùng lúc với nhóm hiển thị, không parse lại chuỗi
    /// hiển thị. Rỗng = chưa thu thập ổ nào (LHM + WMI đều không có).</summary>
    public List<YanJi.PluginSdk.DriveSummary> Drives { get; } = [];
    /// <summary>Có ổ chưa rõ sức khỏe (SMART đọc không được): cho qua được nhưng phải
    /// kiểm tra tay bằng CrystalDiskInfo trước khi mua, không tự cho là tốt.</summary>
    public bool HasDriveUnknown { get; set; }
    /// <summary>Thông tin máy/mainboard từ SMBIOS thô không khớp với WMI (model hoặc
    /// serial lệch nhau ở cả 2 nguồn đều đọc được): cho qua được nhưng verdict
    /// thành Cảnh báo, kỹ thuật viên phải kiểm tra tem máy tay. Rỗng = khớp hoặc
    /// không đối chiếu được (1 bên không đọc được / hãng để trống).</summary>
    public List<string> SmbiosMismatchReasons { get; } = [];
    public bool SmbiosMismatch => SmbiosMismatchReasons.Count > 0;
}
