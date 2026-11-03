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

Vào mục **Releases** của repo, tải file **AnTamPCtest-portable.zip** ở
Release **"latest"** (luôn là bản mới nhất từ nhánh main) về, giải nén ra
USB là chạy. Link tải trực tiếp, không cần đăng nhập GitHub:

https://github.com/YHresearcher/PCcheck/releases/latest/download/AnTamPCtest-portable.zip

Release "latest" chỉ được cập nhật khi build thành công, nên bản lỗi không
ghi đè lên bản tốt đang phát hành. Nếu Releases chưa có file (ví dụ build
đang chạy hoặc vừa lỗi), cách dự phòng là vào tab **Actions**, bấm vào run
**Build Windows portable** mới nhất tick xanh, kéo xuống cuối trang run,
tới mục **Artifacts**, tải **AnTamPCtest-portable** về — cách này thì
GitHub bắt đăng nhập tài khoản mới tải được, kể cả repo public.

## Chạy portable từ USB

1. Cắm USB vào máy cần test, bấm đúp file exe chính trong thư mục gốc của
   `publish` (bấm đúp file AnTamPCtest.exe nằm ở gốc thư mục publish là chạy).
2. Bấm Yes khi Windows hỏi quyền Admin, rồi test như bình thường.
3. Test xong, copy kết quả ra USB trước khi xóa máy để bán (xem giới hạn
   số 4 bên dưới).

Yêu cầu máy test: Windows 10 bản 2004 trở lên hoặc Windows 11, bản 64-bit,
**cần quyền Admin** (để đọc SMART của ổ và một số cảm biến). Máy test
không cần cài .NET, vì gói portable là tự chứa.

## 7 module + trang Tổng kết, theo đúng thứ tự trong app

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
8. **Tổng kết** — trang cuối cùng (Terminal, không có nút cho qua riêng):
   liệt kê kết luận từng module, rồi cho kết luận tổng, xem mục Tổng kết bên dưới.

Mỗi module bấm cho qua xong là app tự nhảy sang module chưa xong tiếp theo.
Bấm "Kiểm tra lại" hoặc "Đặt lại" là xóa trạng thái đã cho qua để test lại
từ đầu.

## Splash screen

Mở app lên là thấy màn hình chờ 480×400 ở giữa màn hình, không có viền
cửa sổ, không hiện trên taskbar: logo An Tâm 120px, tên **"An Tâm PC test"**,
dòng **"Vi Tính An Tâm"** màu cam, slogan "Máy bán ra đã kiểm tra toàn bộ,
bảo hành 6 tháng", địa chỉ shop + vitinhantam.com, và thanh "Đang khởi động...".

Splash hiện trong lúc app tải các module, tự đóng khi cửa sổ chính đã tải
xong và đủ tối thiểu 1,5 giây (máy nhanh cũng không bị chớp tắt), rồi
chuyển focus sang cửa sổ chính. Không cần bấm gì để tắt. Màu splash theo
theme đang dùng (tối = nền xám đậm, sáng = nền kem ấm), logo cam nổi trên
cả hai nền.

## Tổng quan + Wizard 1 bài/lần + Sức khỏe thiết bị (chưa test Windows thật)

- Mở app lên là trang **Tổng quan** (đầu tiên trong menu): tiến độ đã xong x/y bài,
  kết luận mua máy hiện tại, và các nút: Bắt đầu kiểm tra máy / Tiếp tục phiên
  dang dở, Xem thông tin phần cứng, Xem sức khỏe thiết bị, Xem kết luận mua máy,
  Mở thư mục hồ sơ (KetQua). Dashboard KHÔNG chọn câu nhận xét riêng — câu thân
  thiện chỉ ở trang Tổng kết, để 2 nơi không hiện 2 câu khác nhau.
- Nút Bắt đầu / Tiếp tục vào **chế độ wizard**: menu bên trái bị khóa, mỗi lần chỉ
  1 bài test chính, tránh thao tác nhầm. Thanh wizard trên cùng có: Quay lại bài
  trước, Bài tiếp theo, Dừng tất cả test nặng, Thoát wizard (thoát thì mở lại menu,
  trạng thái test không mất). Cho qua 1 bài thì tự nhảy bài chưa xong tiếp theo;
  dừng giữa chừng thì về Dashboard bấm Tiếp tục là nhảy đúng bài chưa xong.
- Trang **Sức khỏe thiết bị** (giữa Đo tốc độ ổ và Tổng kết, chỉ đọc, không phải
  bài test): đánh giá theo mức độ nghiêm trọng từ kết quả đã test — Đạt / Cảnh báo /
  Chặn / Chưa test. Chưa test không được hiểu là khỏe; stress ngắn đạt không chứng
  minh ổn định lâu dài.
- Trang **Thông tin phần cứng** (Đối chiếu cấu hình) trình bày kiểu CPU-Z:
  tab CPU / Memory / Storage / Graphics / Khác, số liệu thô, không phải kết luận.
  Nhóm nào không thuộc 4 tab chính thì vào tab Khác, không bị mất.
- Trang **Cài đặt** (cuối menu, từ Dashboard cũng vào được): đường dẫn hồ sơ
  (để trống = thư mục app \ KetQua, ghi khác nếu muốn lưu thẳng vào USB / ổ
  riêng), thời lượng nướng khuyến nghị (mặc định 3 phút, 1–60), tự dừng khi đủ
  thời lượng (mặc định tắt — bật thì Tản nhiệt tự dừng bằng đường dừng đã kiểm
  chứng, không tự dừng nếu chưa bật). Giao diện sáng/tối vẫn đổi bằng công tắc
  ở góc phải trên, thư mục Tools chỉ xem. Cài đặt lưu ở %APPDATA%\YanJi\
  settings.json, theo app, không theo USB.

## Câu kết luận — file Phrasings.json, chọn theo kịch bản ưu tiên

- Câu kết luận không viết cố định trong C# nữa, mà đọc từ
  `Plugins/Summary/Phrasings.json` (đi kèm plugin Tổng kết, copy vào
  `plugins/summary/` khi build). File hỏng hoặc thiếu thì app dùng câu dự phòng,
  không crash. Sửa câu thì sửa file JSON này, không cần sửa code.
- Thứ tự ưu tiên: lỗi nghiêm trọng → lỗi chức năng → cảnh báo linh kiện →
  thiếu dữ liệu → máy đạt. Thiếu dữ liệu quan trọng thì không được xếp tốt:
  còn bài nào chưa test xong là về "chưa đủ dữ liệu", không về "có thể mua".
- Kịch bản tự chọn được hiện tại: máy tốt (5 mẫu + kết hợp A), ổ lỗi media,
  ổ bad sector, ổ hao mòn, ổ SMART chưa rõ, lỗi nghiêm trọng (kết hợp D),
  cảnh báo chung, chưa đủ dữ liệu (kết hợp E), không mua (đánh dấu tay).
  Câu pin, CPU/GPU, ngoại vi và kết hợp B/C đã có sẵn trong file nhưng chưa tự
  chọn, vì chưa có tín hiệu (cần % chai pin, nhiệt/throttling, tick xác nhận
  tay) — bước tiếp theo là nối tín hiệu, không đoán từ dữ liệu không có.
- Mỗi phiên chỉ random 1 câu phù hợp, câu đó được lưu cố định vào báo cáo
  (`BaoCao.txt` + `KetQua.json` kèm `scenario` và `phrasingLocked: true`).
  Mở lại báo cáo đã lưu sẽ thấy đúng câu đó, không random lại. Nút "Đổi cách
  nói" chỉ đổi câu trong cùng 1 kịch bản, kết luận không đổi.
- Nhóm Máy & Mainboard (đầu trang Đối chiếu cấu hình, tab Khác): đọc model
  máy, model mainboard, serial máy, serial mainboard và BIOS trực tiếp từ bảng
  SMBIOS thô của firmware, không qua WMI hay registry nên sửa registry không
  đổi được. App đối chiếu model và serial SMBIOS với WMI, lệch thì báo cảnh báo
  và bắt kỹ thuật viên kiểm tra tem máy tay (không chặn như ổ). Serial máy và
  serial mainboard khác nhau là bình thường, không phải dấu hiệu fake. Giới hạn:
  nếu BIOS bị flash giả thì SMBIOS và WMI cùng nói dối giống nhau, khi đó phải
  tra serial trên web hãng hoặc soi tem dưới đáy máy.
- Tín hiệu chi tiết: pin chai tính từ % chai lớn nhất (dưới 20% khỏe, 20–dưới
  40% chai vừa, từ 40% chai nặng — mốc là ví dụ, không phải tiêu chuẩn cứng);
  CPU quá nhiệt khi nhiệt tối đa lúc nướng từ 95°C trở lên (mốc nháp, chưa phải
  tiêu chuẩn chính thức); CPU lỗi và GPU artifact là tick tay ở trang Tản nhiệt
  và chặn cho qua; phím liệt, điểm chết, loa rè, webcam là tick tay ở từng bài
  test, vẫn cho qua được nhưng ghi nhận lỗi; bản lề lỏng và trầy nhẹ tick tay ở
  module Ngoại quan (giữa Cổng & Sạc và Đo tốc độ ổ, 8 hạng mục quan trọng:
  vỏ nứt/móp, bản lề lỏng, màn hình trầy/viền nứt, phím bong, cổng móp, pin
  phồng, dấu mở máy, trầy nhẹ; pin phồng thì chặn cho qua); sạc Kém ra câu rắc rối
  nguồn điện; CPU tốt + ổ hao mòn + pin chai cùng lúc ra câu kết hợp B. Pin
  không đọc được % hoặc máy không có pin thì không báo tín hiệu, không đoán.
  Kết hợp C chưa tự chọn vì chưa có định nghĩa "cấu hình mạnh".
- Dòng "An Tâm nhắn bạn" (vd "Máy tốt là một chuyện, mua đúng giá mới là
  chuyện đáng vui!") chỉ hiện khi máy đạt hoặc cảnh báo nhẹ, không hiện khi
  có lỗi nghiêm trọng, chưa đủ dữ liệu, hoặc đã đánh dấu không mua — để không
  làm nhẹ cảnh báo.

## Kết luận mua máy (trang Tổng kết: mua được hay không, cho cả kỹ thuật viên và người mua)

- Trang Tổng kết nằm cuối thanh điều hướng, app tự nhảy tới đây khi cả 7 module
  đã cho qua xong. Trang này không có nút cho qua riêng, vì nó chỉ tổng hợp.
  Trang này trả lời 1 câu duy nhất: máy MUA ĐƯỢC HAY KHÔNG, cho cả 2 đối tượng
  dùng chung — người mua chỉ cần đọc tiêu đề + câu thân thiện ở trên là đủ,
  kỹ thuật viên đọc chi tiết từng module ở dưới để biết vì sao.
- Kết luận từng module: Đối chiếu cấu hình và Cổng kết nối & Sạc có mức riêng
  (Đạt / Cảnh báo / Chặn / Chưa test xong, lấy từ kết luận ổ và kết luận sạc);
  các module còn lại chỉ tính theo đã cho qua (Đạt) / chưa cho qua
  (Chưa test xong). App chưa chấm điểm pin, nhiệt độ hay tốc độ ổ, vì shop
  chưa chốt ngưỡng cho các mục đó — đừng đọc kết luận tổng như là đã chấm
  hết mọi thứ.
- Kết luận mua tự động, tính từ kết luận từng module, không chấm điểm 100:
  - Có module Chặn = **KHÔNG KHUYẾN NGHỊ MUA**.
  - Không Chặn nhưng có Cảnh báo = **CẦN KIỂM TRA THÊM / THƯƠNG LƯỢNG**.
  - Tất cả Đạt = **CÓ THỂ MUA — MUA ĐƯỢC**.
  - Còn module chưa xong = **CHƯA ĐỦ DỮ LIỆU** (chưa kết luận mua được hay không).
- Mỗi kịch bản kết luận có nhiều cách nói thân thiện, app chọn ngẫu nhiên
  1 câu khi kịch bản đổi, rồi giữ nguyên câu đó cho tới khi kịch bản đổi tiếp
  hoặc lưu báo cáo (không còn nút đổi câu tay — câu đã chọn là câu của phiên
  kiểm tra đó). Các câu đều giọng shop thật, câu ngắn, không hứa thêm gì ngoài
  kết luận (ví dụ câu ĐẠT không tự thêm bảo hành).
- **KHÔNG MUA không tự sinh ra từ test**, vì test không biết chi phí sửa có đáng
  hay không (ổ chặn + sạc kém vẫn sửa được bằng cách thay linh kiện, không
  tự động thành không mua). Kỹ thuật viên tự bấm "Đánh dấu không mua máy này"
  khi máy hỏng nặng không nên mua, kết luận thành KHÔNG MUA với 5 cách nói riêng;
  bấm lại để bỏ đánh dấu và về lại kết luận tự động.
- **Lưu hồ sơ phiên này** (trang Tổng kết, ô S/N + nút lưu): mỗi máy 1
  thư mục riêng `<thư mục app>\KetQua\<S/N>_<yyyyMMdd_HHmmss>\`, gồm
  `BaoCao.txt` (kết luận + nhận xét + kết quả từng module + bằng chứng),
  `KetQua.json` (máy đọc được) và bản sao `KetQuaDoO.json` mới nhất (vì
  file gốc trong %APPDATA% sẽ bị máy sau ghi đè). Cùng S/N test lại cũng
  không đè lên nhau nhờ timestamp (+ _2, _3 nếu trùng giây). Chỉ mục
  `KetQua\DanhSach.csv` thêm 1 dòng/phiên. S/N để trống vẫn lưu được
  (tên thư mục dùng KhongCoSN). Lưu xong mới test máy tiếp theo. Nếu
  thư mục app ghi không được thì lưu vào %APPDATA%\YanJi\KetQua\ và báo
  rõ, không im lặng lưu chỗ khác.
- **Dừng tất cả test nặng** (nút trên thanh đầu app, và nút Dừng / cho qua
  ở trang Tản nhiệt): dừng thật Prime95 / FurMark / đo tốc độ ổ, có kiểm
  chứng — kill xong phải xác nhận tiến trình đã chết, chưa chết thì quét
  lại theo đường dẫn Tools và báo rõ tên tiến trình còn sống, không hiện
  "Đã dừng" giả. Trang Tản nhiệt không cho qua khi chưa dừng được thật.
  Hai quy tắc không nói quá cũng ghi ở trang Tổng kết: SMART không đọc
  được thì không thành ổ khỏe; stress ngắn đạt không chứng minh ổn định
  lâu dài.

## Kết luận tự động cho ổ (module Đối chiếu cấu hình)

Mỗi ổ trong nhóm "Ổ" có thêm dòng **"Lỗi nghiêm trọng"** và dòng
**"Kết luận ổ"**. Kết luận xét **mức độ nghiêm trọng của lỗi trước,
ngưỡng sức khỏe chỉ xét sau** (sửa ngày 2026-11-03, theo yêu cầu không
chỉ dựa vào sức khỏe SSD):

- Lỗi nghiêm trọng > 0: **CHẶN — thay ổ rồi kiểm tra lại**, bất kể sức
  khỏe cao bao nhiêu. Ổ 95% nhưng có lỗi vẫn CHẶN, không được về ĐẠT
  chỉ vì sức khỏe cao. Lỗi nghiêm trọng gồm: NVMe cảnh báo nghiêm trọng
  (Critical Warning) > 0, lỗi toàn vẹn dữ liệu NVMe 0E > 0, dự phòng
  NVMe còn dưới ngưỡng, SATA sector đã thay thế (05) / chờ thay thế
  (C5) / không sửa được (C6) / lỗi không sửa được đã báo (BB) > 0.
- Sức khỏe <70%: **CHẶN** (không có lỗi nghiêm trọng thì vẫn chặn vì
  mòn quá nhiều).
- Cảnh báo: sức khỏe 70-89%, hoặc lỗi CRC (C7) > 0 (thường do cáp/cổng,
  kiểm tra cáp rồi test lại, không chặn cứng ổ), hoặc không đọc được
  lỗi nghiêm trọng nào dù sức khỏe còn cao (chưa biết có lỗi hay không
  thì không được coi là khỏe). Vẫn cho qua module được, nhưng dòng dưới
  cùng của trang ghi rõ điều kiện.
- Sức khỏe ≥90% và đã đọc được lỗi nghiêm trọng = 0, không có lỗi nào
  > 0: **ĐẠT — bán nguyên trạng**.
- SMART không đọc được: **Không rõ — kiểm tra tay bằng CrystalDiskInfo,
  chưa đủ dữ liệu để kết luận**. Không tự cho là tốt, nhưng cũng không
  chặn cứng (có máy SMART vốn không đọc được qua kênh này) — kỹ thuật
  viên phải kiểm tra tay trước khi bán.

Ngưỡng này là ngưỡng shop đã chốt ngày 2026-11-03, sửa lại theo mức độ
nghiêm trọng ngày 2026-11-03. Lỗi NVMe chỉ áp dụng cho NVMe, lỗi SATA
chỉ áp dụng cho SATA, không bị chặn oan vì nhầm loại ổ. Sức khỏe % lấy
từ SMART qua LibreHardwareMonitor: NVMe = 100 trừ "Percentage Used",
SATA = "SSD Life Left" hoặc "Wear Leveling". SMART đọc không được thì
các dòng sức khỏe, thời gian đã chạy và lỗi đều hiện "Không rõ",
không bịa số.

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
  đổi 2 file theme là toàn bộ app (khung chính + 7 module + Tổng kết + cửa sổ quản
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
- Tên file exe chính là AnTamPCtest.exe. Tên file nhạc test vẫn giữ nguyên,
  vì đổi tên sẽ làm app không tìm thấy file nhạc.
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
