# Test Máy An Tâm — bản dịch tiếng Việt từ BibaYanji

Đây là bản dịch tiếng Việt từ BibaYanji của Bibapcs, tool test máy trước khi bán.
Mã gốc theo giấy phép MIT, bản quyền gốc của Bibapcs vẫn giữ nguyên, file LICENSE
không đổi. Các thành phần kèm theo giữ giấy phép riêng của chúng: ví dụ
LibreHardwareMonitor (MPL-2.0), Prime95 (GIMPS freeware), PawnIO (GPL-2.0 có
ngoại lệ), CrystalDiskMark và diskspd (MIT). Khi copy hay phát lại thư mục này,
giữ nguyên các file LICENSE kèm theo, đừng xóa.

## Phạm vi bản dịch này — đọc trước để khỏi hiểu lầm

Bản này đã dịch toàn bộ chữ hiện ra cho người dùng sang tiếng Việt, gồm:

- Khung chính: tiêu đề "Test Máy An Tâm", dòng thông tin Vi Tính An Tâm ở
  top bar, quản lý module, nút và thông báo.
- 6 module: **Đối chiếu cấu hình**, **Test bàn phím**, **Test điểm chết màn
  hình**, **Camera - Mic - Loa**, **Test tản nhiệt**, **Đo tốc độ ổ**.
- Trình cài đặt: tên app, nhà phát hành, thư mục cài đặt, mô tả các component
  và nhiệm vụ thêm, hộp thoại gỡ cài đặt đã sang tiếng Việt.

Còn sót lại, cố tình giữ nguyên hoặc chưa dịch được, xem kỹ trước khi dùng:

- Nút chuẩn của trình cài đặt Inno Setup (như Tiếp theo, Quay lại, Hủy,
  Duyệt) vẫn tiếng Trung, vì trình cài đặt đang dùng file ngôn ngữ
  `ChineseSimplified` có sẵn của Inno Setup, chưa có file ngôn ngữ tiếng Việt
  đi kèm. Muốn hết hẳn thì cần thêm file `.isl` tiếng Việt cho Inno Setup.
- Dòng ghi công bài nhạc test ở trang Camera - Mic - Loa giữ nguyên văn bản
  gốc, theo quy tắc của repo gốc là không được đổi dòng ghi công đó.
- Tên file exe chính vẫn giữ nguyên tên gốc, và tên file nhạc test cũng giữ
  nguyên, vì đổi tên sẽ làm app không tìm thấy file.
- Comment trong code vẫn tiếng Trung, chỉ người đọc code mới thấy, người
  dùng cuối không thấy.
- Tên file kết quả đo ổ đã đổi sang tiếng Việt: `KetQuaDoO.json` và
  `LichSuDoO.csv` trong `%APPDATA%\YanJi\disk_result`.

## Vi Tính An Tâm

- Địa chỉ: 52 Nguyễn Cửu Vân, phường Long An, tỉnh Tây Ninh
- Website: vitinhantam.com
- Email: hello@vitinhantam.com
- Máy bán ra đã kiểm tra toàn bộ trước khi giao, bảo hành 6 tháng nguyên máy.
- An Tâm bán máy đã qua sử dụng đã test kỹ, **không phải tiệm sửa máy**.
  An Tâm **không có showroom bán lẻ**, không nhận máy ngoài vào sửa.

## Giao diện thương hiệu An Tâm

- Màu cam logo: **#ED740C** (đo trực tiếp từ file logo gốc, 2000×2000 PNG
  nền trong suốt). Logo nằm ở `验机软件/Assets/logo-antam.png`, nhúng vào
  exe, hiện ở top bar bên trái tên app.
- Top bar có dải cam 3px trên cùng, cuối thanh điều hướng trái có dòng
  "Vi Tính An Tâm • vitinhantam.com".
- Icon app và icon trình cài đặt là logo An Tâm, tạo từ chính file logo
  trên (`验机软件/app.ico`, đủ 16/32/48/64/128/256px, nền trong suốt).
  Icon cũ vẫn còn trong lịch sử git nếu cần khôi phục.
- Hai theme đều dùng cam, chỉ đổi sắc độ cho đủ tương phản, không đổi
  tên hay số lượng token trong `Themes/Light.xaml` và `Themes/Dark.xaml`:
  - Sáng (dùng ban ngày): nền trắng, nền phụ #FFF6EC, cam #D95E00,
    hover #C25200, mục đang chọn #FFE7CC.
  - Tối (giao diện thương hiệu chính, nền gần đen như logo trình bày):
    nền #111111, nền phụ #262626, cam đúng #ED740C, hover #F5861F,
    mục đang chọn #3D2A1A.
- Vì toàn bộ control đều dùng DynamicResource trỏ vào token theme nên
  đổi 2 file theme là toàn bộ app (khung chính + 6 module + cửa sổ quản
  lý module) đổi màu theo, không cần sửa từng trang. Màu đỏ lỗi giữ
  nguyên #C42B1C / #FF6B5E, không dùng cam cho lỗi để khỏi nhầm.
- Font chính đổi thứ tự thành "Segoe UI, Microsoft YaHei UI" cho chữ
  tiếng Việt có dấu hiển thị đúng; font Consolas ở các ô log giữ nguyên.

## Build trên máy Windows có .NET SDK 10 — và nếu đang xài Mac thì sao

Nếu đang xài Mac, đọc file `Huong_dan_build_tren_Mac.md` trong gói này
trước: cách khuyên dùng là GitHub Actions (file
`.github/workflows/build-windows.yml` đã có sẵn, push lên GitHub rồi bấm
Run workflow là tải được bản portable về), ngoài ra còn có cách máy ảo
Windows trên Mac và cách điều khiển máy Windows từ xa. Không build trực
tiếp được trên Mac, và Mac chip M cũng không có Boot Camp.

## Build trên máy Windows có .NET SDK 10

Chạy từ thư mục gốc của repo này, đúng thứ tự sau, không đổi thứ tự:

```bat
dotnet publish FurMark/FurMark.csproj -p:PublishProfile=FolderProfile
dotnet publish 验机软件/验机软件.csproj -p:PublishProfile=FolderProfile
```

Thư mục `publish` thu được là gói tự chứa, máy test không cần cài .NET.

Muốn đóng gói trình cài đặt thì chạy tiếp bằng Inno Setup 6:

```bat
"%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" installer\setup.iss
```

File cài đặt thu được là `TestMayAnTamSetup-v0.0.4-VN.exe` trong thư mục
`installer`, cài mặc định vào `{autopf}\TestMayAnTam`.

## Chạy portable từ USB

1. Copy cả thư mục `publish` sang USB, giữ nguyên cấu trúc bên trong.
2. Cắm USB vào máy cần test, bấm đúp file exe chính trong thư mục gốc
   (tên file exe vẫn giữ nguyên tên gốc, chưa đổi tên — bấm đúp file exe
   duy nhất nằm ở gốc thư mục publish là chạy).
3. Bấm Yes khi Windows hỏi quyền Admin, rồi test như bình thường.
4. Test xong, copy kết quả ra USB trước khi xóa máy để bán (xem giới hạn
   số 4 bên dưới).

## 4 giới hạn đã biết, không phải lỗi

1. **Vẫn cần quyền Admin.** App cần quyền Admin để đọc SMART của ổ và một
   số cảm biến.
2. **Máy test cần Windows 10 bản 2004 trở lên hoặc Windows 11, bản 64-bit.**
3. **Driver PawnIO không đi theo USB.** Driver này là driver nhân, để đọc
   nhiệt và công suất CPU lúc nướng máy. Nó phải cài riêng trên từng máy
   test, không cài thì app vẫn chạy, chỉ mất 2 đường nhiệt và công suất CPU
   trong lúc nướng.
4. **Kết quả nằm trong `%APPDATA%\YanJi` trên máy test, không nằm trên USB.**
   Điểm ổ nằm trong `%APPDATA%\YanJi\disk_result`. Test xong phải copy
   thư mục đó ra USB, rồi xóa trên máy test trước khi giao máy cho khách,
   không thì xóa máy là mất kết quả.
