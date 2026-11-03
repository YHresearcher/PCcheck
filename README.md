# An Tâm PC test

Tool test máy tính cũ trước khi bán, của cửa hàng **Vi Tính An Tâm**.
Mở app lên là có splash logo An Tâm, test xong từng module thì bấm cho qua,
màn hình chính hiện dấu xanh từng mục đã xong.

Bản này dịch sang tiếng Việt và tùy biến từ
[BibaYanji](https://github.com/Bibapcs/BibaYanji) v0.0.4 của Bibapcs.
Mã gốc theo giấy phép MIT, bản quyền gốc vẫn của Bibapcs, file LICENSE
không đổi. Bản gốc tiếng Trung vẫn giữ nguyên ở file
`README-BibaYanji-goc.md` trong repo này để đối chiếu.

## Tải bản chạy được

Repo này **chưa phát hành ở mục Releases**. Cách tải duy nhất hiện tại là
qua GitHub Actions:

1. Vào tab **Actions** của repo.
2. Bấm vào run **Build Windows portable** mới nhất (run nào tick xanh mới có
   file, run nào báo lỗi thì không có).
3. Kéo xuống cuối trang run, tới mục **Artifacts**, bấm tải
   **AnTamPCtest-portable** về.
4. Giải nén file zip ra, copy cả thư mục `publish` bên trong sang USB, giữ
   nguyên cấu trúc bên trong.

Lưu ý: GitHub bắt đăng nhập tài khoản GitHub mới tải được artifact, kể cả
repo public.

## Chạy portable từ USB

1. Cắm USB vào máy cần test, bấm đúp file exe chính trong thư mục gốc của
   `publish` (tên file exe vẫn giữ nguyên tên gốc, chưa đổi tên — bấm đúp
   file exe duy nhất nằm ở gốc thư mục publish là chạy).
2. Bấm Yes khi Windows hỏi quyền Admin, rồi test như bình thường.
3. Test xong, copy kết quả ra USB trước khi xóa máy để bán (xem giới hạn
   số 4 bên dưới).

Yêu cầu máy test: Windows 10 bản 2004 trở lên hoặc Windows 11, bản 64-bit,
**cần quyền Admin** (để đọc SMART của ổ và một số cảm biến). Máy test
không cần cài .NET, vì gói portable là tự chứa.

## 7 module, theo đúng thứ tự trong app

1. **Đối chiếu cấu hình** — vào trang là app tự quét CPU, card màn hình,
   RAM, ổ, card mạng, màn hình, pin, rồi đối chiếu từng mục với cấu hình
   đơn hàng, bấm "Xác nhận đúng, cho qua". Xem thêm mục kết luận ổ bên dưới.
2. **Test bàn phím** — bàn phím ANSI 104 phím, bấm phím nào thì phím đó
   sáng lên, nhả ra thì chuyển xanh, đếm "đã test X / 104".
3. **Test điểm chết màn hình** — chọn màn hình rồi xem toàn màn hình đổi
   màu trắng, đỏ, xanh lá, xanh dương, đen, bấm phím bất kỳ hoặc click chuột
   để đổi màu, Esc để thoát. Máy nhiều màn hình thì test từng cái.
4. **Camera - Mic - Loa** — camera xem trước trực tiếp, mic có thanh mức
   âm + ghi / phát lại, loa phát nhạc test. Đủ 3 mục thì bấm cho qua một lần.
5. **Test tản nhiệt** — nướng CPU bằng Prime95, nướng GPU bằng FurMark viết
   lại bằng C# / OpenTK, hoặc nướng cả hai. Có biểu đồ nhiệt độ và công suất
   CPU / GPU theo thời gian thực, dừng hoặc chuyển trang là app tự dọn
   tiến trình nướng, không để sót tiến trình chạy ngầm.
6. **Cổng kết nối & Sạc** — app tự quét cổng + ảnh chụp nhanh sạc. Cắm sạc
   rồi bấm "Test sạc (10 giây)": app lấy mẫu điện áp + công suất mỗi giây
   trong 10 giây, rồi cho điểm sức khỏe sạc. Cổng vật lý trên vỏ máy
   (HDMI, VGA, RJ45, USB-A, USB-C, đầu sạc) thì kỹ thuật viên nhìn bằng mắt
   và tick tay ở cuối trang. Xem thêm ghi chú port bên dưới.
7. **Đo tốc độ ổ** — đo tốc độ đọc / ghi tuần tự và ngẫu nhiên, kết quả lưu
   thành `KetQuaDoO.json` và `LichSuDoO.csv` trong
   `%APPDATA%\YanJi\disk_result`.

Mỗi module bấm cho qua xong là app tự nhảy sang module chưa xong tiếp theo.
Bấm "Kiểm tra lại" hoặc "Đặt lại" là xóa trạng thái đã cho qua để test lại
từ đầu.

## Splash screen

Mở app lên là thấy màn hình chờ 480×400 ở giữa màn hình, không có viền
cửa sổ, không hiện trên taskbar: logo An Tâm 120px, tên **"An Tâm PC test"**,
dòng **"Vi Tính An Tâm"** màu cam, slogan "Máy bán ra đã kiểm tra toàn bộ,
bảo hành 6 tháng", địa chỉ shop + vitinhantam.com, và thanh "Đang khởi động...".

Splash hiện trong lúc app tải 7 module, tự đóng khi cửa sổ chính đã tải
xong và đủ tối thiểu 1,5 giây (máy nhanh cũng không bị chớp tắt), rồi
chuyển focus sang cửa sổ chính. Không cần bấm gì để tắt. Màu splash theo
theme đang dùng (tối = nền xám đậm, sáng = nền kem ấm), logo cam nổi trên
cả hai nền.

## Kết luận tự động cho ổ (module Đối chiếu cấu hình)

Mỗi ổ trong nhóm "Ổ" có thêm dòng **"Kết luận ổ"**, tính từ đúng số SMART
đã hiển thị sẵn, không cần đọc thêm gì:

- Sức khỏe ≥90%: **ĐẠT — bán nguyên trạng**.
- Sức khỏe 70-89%: **CẢNH BÁO — tối đa bán có điều kiện**. Vẫn cho qua
  module được, nhưng dòng dưới cùng của trang ghi rõ điều kiện.
- Sức khỏe <70% hoặc lỗi toàn vẹn dữ liệu NVMe 0E > 0: **CHẶN — thay ổ
  rồi kiểm tra lại**. Nút "Xác nhận đúng, cho qua" bị chặn, bấm vào chỉ
  hiện lý do, không chuyển xanh. Thay ổ xong bấm "Kiểm tra lại" để quét
  lại từ đầu. Máy có nhiều ổ thì chỉ cần 1 ổ bị chặn là cả module không
  cho qua, vì bán máy kèm ổ hỏng là bán hàng lỗi.
- SMART không đọc được: **Không rõ — kiểm tra tay bằng CrystalDiskInfo,
  chưa đủ dữ liệu để kết luận**. Không tự cho là tốt, nhưng cũng không
  chặn cứng (có máy SMART vốn không đọc được qua kênh này) — kỹ thuật
  viên phải kiểm tra tay trước khi bán.

Ngưỡng này là ngưỡng shop đã chốt ngày 2026-11-03. Lỗi 0E chỉ áp dụng cho
NVMe, ổ SATA không bị chặn oan vì mục này. Sức khỏe % lấy từ SMART qua
LibreHardwareMonitor: NVMe = 100 trừ "Percentage Used", SATA = "SSD Life
Left" hoặc "Wear Leveling". SMART đọc không được thì các dòng sức khỏe,
thời gian đã chạy và lỗi 0E đều hiện "Không rõ", không bịa số.

## Module Cổng kết nối & Sạc — nguồn và những chỗ đã sửa

Module này port từ laptop-diagnostic-tool của Tanxe Studio (giấy phép MIT),
các hàm Get-ChargerInfo / Test-AdapterHealth và phần phát hiện cổng trong
`FinalDiagnostic_v15.ps1`, port sang C# thành plugin `ports` (thứ tự 60,
giữa Test tản nhiệt và Đo tốc độ ổ). Ghi công hiển thị trong cửa sổ
Quản lý Module, theo đúng cơ chế Credits của app.

Ngưỡng sạc giữ nguyên bản gốc: điện áp dao động <3% Xuất sắc, <5% Tốt,
<10% Trung bình, còn lại Kém; không sạc mà pin <99% trừ 50 điểm; điểm
≥85 Xuất sắc, ≥70 Tốt, ≥50 Trung bình, còn lại Kém. Chưa test sạc (máy có
pin) hoặc sạc đánh giá Kém thì nút cho qua không cho qua — thay sạc rồi
test lại. Máy bàn không có pin thì bỏ qua phần sạc.

Điện áp hiển thị là điện áp ở đầu pin, **không phải** điện áp ra của củ sạc
(thường 19-20 V, hệ thống không đo trực tiếp được) — trang ghi rõ điều này.

Những chỗ đã sửa so với bản gốc, cố tình không copy nguyên (chi tiết trong
đầu file `Plugins/PortAdapter/PortAdapterService.cs`): bản gốc coi
Win32_Battery mã 2 là đã cắm sạc (thực ra 2 = Unknown) — bản này dùng
PowerOnline làm căn cứ chính; bản gốc tính mức pin từ thuộc tính
MaxCapacity không tồn tại nên luôn ra 0 — bản này lấy dung lượng sạc đầy
từ BatteryFullChargedCapacity; bản gốc ước tính công suất rơi về 65 W khi
vượt mức lớn nhất — bản này trả "330 W trở lên"; bản gốc đoán số cổng USB
từ số root hub và đoán có HDMI từ đời CPU / tên máy rồi mặc định luôn là
có — bản này không đoán, chuyển sang tick tay.

Phần cổng tự động chỉ báo những gì hệ thống thật sự nhìn thấy: số USB
controller / root hub, thiết bị USB-C / Thunderbolt theo tên, dấu hiệu
USB-C PD, card mạng có dây, kết nối màn hình hiện tại. Số root hub không
phải số cổng vật lý, card mạng có dây không chứng minh là có ổ cắm RJ45,
màn hình đang cắm không phải danh sách cổng — trang ghi rõ từng chỗ này.

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
  exe, hiện ở top bar bên trái tên app và ở splash screen.
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
  đổi 2 file theme là toàn bộ app (khung chính + 7 module + cửa sổ quản
  lý module) đổi màu theo, không cần sửa từng trang. Màu đỏ lỗi giữ
  nguyên #C42B1C / #FF6B5E, không dùng cam cho lỗi để khỏi nhầm.
- Font chính đổi thứ tự thành "Segoe UI, Microsoft YaHei UI" cho chữ
  tiếng Việt có dấu hiển thị đúng; font Consolas ở các ô log giữ nguyên.
- Hệ thống theme lần đầu chạy theo theme của Windows, nút mặt trời / mặt
  trăng ở top bar đổi theme và lưu lại lựa chọn.

## Còn sót lại, cố tình giữ nguyên hoặc chưa dịch được

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

## Build trên máy Windows có .NET SDK 10

Chạy từ thư mục gốc của repo này, đúng thứ tự sau, không đổi thứ tự
(bước restore phải chạy trước, vì các plugin build bằng MSBuild target
cần file assets của NuGet, thiếu bước này là build lỗi):

```bat
dotnet restore 验机软件.slnx
dotnet publish FurMark/FurMark.csproj -p:PublishProfile=FolderProfile
dotnet publish 验机软件/验机软件.csproj -p:PublishProfile=FolderProfile
```

Thư mục `publish` thu được là gói tự chứa, máy test không cần cài .NET.

Muốn đóng gói trình cài đặt thì chạy tiếp bằng Inno Setup 6:

```bat
"%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" installer\setup.iss
```

File cài đặt thu được là `AnTamPCtestSetup-v0.0.4-VN.exe` trong thư mục
`installer`, cài mặc định vào `{autopf}\AnTamPCtest`.

Nếu đang xài Mac, đọc file `Huong_dan_build_tren_Mac.md` trong repo này
trước: cách khuyên dùng là GitHub Actions (file
`.github/workflows/build-windows.yml` đã có sẵn, push lên GitHub rồi tải
artifact về), ngoài ra còn có cách máy ảo Windows trên Mac và cách điều
khiển máy Windows từ xa. Không build trực tiếp được trên Mac, và Mac chip
M cũng không có Boot Camp. App build xong chỉ test được máy Windows.

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

## Ghi công và giấy phép

- BibaYanji của Bibapcs — code chính, giấy phép MIT, file LICENSE gốc
  giữ nguyên. Các thành phần kèm theo giữ giấy phép riêng của chúng:
  LibreHardwareMonitor (MPL-2.0), Prime95 (GIMPS freeware), PawnIO
  (GPL-2.0 có ngoại lệ), CrystalDiskMark và diskspd (MIT), NAudio (Ms-PL),
  OpenTK (MIT), FurMark bản Python gốc (MIT). Khi copy hay phát lại thư
  mục này, giữ nguyên các file LICENSE kèm theo, đừng xóa.
- Module Cổng kết nối & Sạc — port từ laptop-diagnostic-tool của Tanxe
  Studio, giấy phép MIT, ghi công trong cửa sổ Quản lý Module.
- Bài nhạc test ở trang Camera - Mic - Loa: xem `AUDIO-LICENSE.md`.
- App này không tích hợp phần mềm thương mại đóng nguồn nào (ví dụ
  HWiNFO) vào code, theo quy tắc của repo gốc. Thu thập phần cứng chỉ qua
  LibreHardwareMonitor và giao diện có sẵn của Windows (WMI, registry,
  Win32).
