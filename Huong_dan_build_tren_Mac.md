# Build An Tâm PC test khi đang xài Mac

## Nếu upload lên GitHub bằng web bị chặn vì file > 25 MB — đọc đoạn này trước

Đây không phải lỗi của code, mà là giới hạn của GitHub khi upload bằng web:
upload bằng trình duyệt chỉ cho file tối đa 25 MB. Trong repo này có 2 file
vượt ngưỡng đó: `prime95.exe` 29 MB (nằm trong `验机软件/Tools/prime95/`, file
này BẮT BUỘC phải có vì module Test tản nhiệt dùng nó để nướng CPU) và
`gwnum64.lib` 27 MB (nằm trong mã nguồn prime95, xem dưới).

Cách sửa đúng là **đừng upload bằng web nữa, push bằng git** (GitHub Desktop
hoặc Terminal). Git cho phép file tới 100 MB, nên cả 2 file trên đều qua
được. Các bước push bằng GitHub Desktop trên Mac:

1. Tạo repo mới trên GitHub, để trống hoàn toàn (đừng tick thêm README hay
   .gitignore, để khỏi bị xung đột khi push).
2. Giải nén gói mã nguồn ra Mac.
3. Mở GitHub Desktop, chọn File > Add Local Repository, chọn thư mục vừa
   giải nén. Nếu nó hỏi thư mục chưa phải repo git thì bấm Create Repository.
4. Bấm Publish repository, chọn repo vừa tạo ở bước 1, để Private, chờ nó
   push xong. Repo này khoảng 82 MB, push lần đầu sẽ hơi lâu, cứ để nó chạy.
5. Lên GitHub, vào tab Actions, chạy workflow như Cách 1 bên dưới.

Cách push bằng Terminal trên Mac (nếu ông quen Terminal):

```
cd <thư mục vừa giải nén>
git init -b main
git add .
git commit -m "Test May An Tam VN"
git remote add origin https://github.com/<tên ông>/<tên repo>.git
git push -u origin main
```

Lưu ý: GitHub không còn nhận mật khẩu tài khoản để push nữa, nên nếu dùng
Terminal thì phải dùng Personal Access Token hoặc đăng nhập bằng `gh auth
login`. Dùng GitHub Desktop thì khỏi lo vụ này, vì nó đăng nhập bằng trình
duyệt.

Muốn repo nhẹ hơn thì có thể xóa trước khi push 3 thư mục sau, build vẫn
chạy bình thường vì không có file .csproj nào trong repo dùng tới chúng:
`集成开源项目代码/Prime95-master/` (mã nguồn để tự build lại prime95, không
cần vì đã có sẵn prime95.exe), `集成开源项目代码/LibreHardwareMonitor-master/
LibreHardwareMonitor.Windows.Forms/` và `prime95-build/`. Xóa 3 thư mục này
thì repo còn khoảng 45 MB. Nhưng lưu ý: xóa vậy vẫn KHÔNG sửa được lỗi
upload web, vì `prime95.exe` 29 MB vẫn còn đó và vẫn vượt 25 MB — chỉ có
push bằng git mới qua được file đó. Tuyệt đối đừng xóa
`验机软件/Tools/prime95/`, xóa là mất module nướng CPU.

Còn một hiểu lầm hay gặp nữa: upload file zip lên GitHub bằng web thì
GitHub chỉ lưu đúng file zip đó, nó KHÔNG tự giải nén ra thành mã nguồn,
nên workflow Actions cũng không chạy được từ file zip. Muốn Actions chạy
thì trong repo phải có sẵn từng file mã nguồn và file
`.github/workflows/build-windows.yml`, tức là phải push bằng git như trên.

## Nói trước cho khỏi mất thời gian

App này là WPF, chỉ chạy và chỉ build được trên Windows. Cài .NET SDK trên
Mac cũng không build được, vì Mac không có bộ WindowsDesktop SDK và các API
Windows mà app dùng. Boot Camp cũng không phải lựa chọn trên Mac chip M
(M2 Pro của ông), vì Boot Camp chỉ có trên Mac Intel cũ.

Vì vậy muốn có bản portable chạy trên máy Windows để test, phải build ở một
nơi có Windows thật. Có 3 cách, xếp theo độ dễ:

## Cách 1 — GitHub Actions, khuyên dùng (không cần máy Windows, không cần mua gì)

Máy chủ Windows của GitHub sẽ build giúp, ông chỉ cần tải file về.

1. Tạo 1 repo mới trên GitHub (nên để Private, vì đây là bản mang thương hiệu
   shop), ví dụ tên `AnTamPCtest`.
2. Giải nén gói mã nguồn này ra Mac, push toàn bộ lên repo đó. Dùng GitHub
   Desktop cho dễ: mở GitHub Desktop, chọn Add Local Repository, chọn thư mục
   vừa giải nén, rồi Publish. Hoặc dùng Terminal trên Mac:
   `git init`, `git add .`, `git commit -m "Test May An Tam VN"`,
   `git remote add origin <đường dẫn repo>`, `git push -u origin main`.
   Lưu ý: file `.github/workflows/build-windows.yml` đã có sẵn trong gói này,
   đừng xóa nó, vì chính file đó là lệnh build.
3. Trên GitHub, vào tab Actions của repo, chọn workflow "Build Windows
   portable", bấm Run workflow, chờ build xong (thường vài phút).
4. Tải artifact tên `AnTamPCtest-portable` về, giải nén ra USB.
5. Cắm USB vào máy Windows cần test, bấm đúp file exe chính ở gốc thư mục,
   bấm Yes khi hỏi quyền Admin là chạy.

Ưu điểm: không cần cài Windows, không cần bản quyền Windows, build trên máy
x64 thật nên đúng đích win-x64. Nhược điểm: mỗi lần sửa code lại phải push
rồi chờ build lại, và tài khoản GitHub miễn phí có giới hạn phút build cho
repo Private (repo Public thì thoải mái hơn, nhưng sẽ công khai mã mang
thương hiệu shop — cân nhắc trước khi để Public).

## Cách 2 — Máy ảo Windows trên Mac

Cài 1 máy ảo Windows 11 ARM trên Mac M2 Pro, rồi build trong máy ảo đó.

- Phần mềm máy ảo: VMware Fusion Pro (hiện miễn phí cho dùng cá nhân) hoặc
  Parallels Desktop (trả phí, dễ dùng hơn) hoặc UTM (miễn phí, mã nguồn mở,
  cài đặt thủ công hơn).
- Tải file ISO Windows 11 ARM64 từ trang chính thức của Microsoft, cài vào
  máy ảo. Windows trong máy ảo vẫn cần bản quyền để dùng lâu dài; để build
  thử thì chạy chưa kích hoạt cũng build được, chỉ bị watermark và khóa vài
  tùy chỉnh giao diện.
- Trong máy ảo, cài .NET SDK 10 bản Windows ARM64, giải nén mã nguồn vào,
  bấm đúp `Build_Windows.bat`. File publish thu được vẫn là bản win-x64,
  vì profile publish đã ghi rõ win-x64, nên copy ra USB chạy được trên máy
  Windows x64 để test, không phải bản ARM.
- Muốn kiểm tra giao diện tiếng Việt ngay trong máy ảo cũng được: Windows
  ARM chạy được app x64 qua giả lập, hơi chậm hơn nhưng đủ để soi chữ có
  vỡ layout không.

Ưu điểm: làm được ngay trên Mac, sửa code xong build lại nhanh, không phụ
thuộc GitHub. Nhược điểm: tốn dung lượng ổ (máy ảo Windows khoảng vài chục
GB), máy Mac phải đủ RAM (máy ông 16 GB thì chia cho máy ảo khoảng 8 GB là
vừa, đừng chia hết), và build win-x64 trên máy ARM về lý thuyết là được
nhưng chưa kiểm chứng trực tiếp ở đây — nếu gặp lỗi lạ về kiến trúc thì
quay về Cách 1 cho chắc.

## Cách 3 — Dùng máy Windows sẵn có, điều khiển từ xa từ Mac

Nếu shop hoặc nhà ông còn máy Windows (máy chính của ông vốn là Windows),
không cần build trên Mac làm gì:

- Bật Remote Desktop trên máy Windows đó, rồi từ Mac mở app Microsoft
  Remote Desktop (miễn phí trên App Store) để điều khiển từ xa.
- Trong phiên remote đó, giải nén mã nguồn, bấm đúp `Build_Windows.bat`,
  lấy thư mục `publish` ra USB.

Cách này nhanh nhất nếu máy Windows đó đang bật và có mạng, nhưng không
dùng được khi ông chỉ có mỗi Mac bên cạnh.

## Cách nào KHÔNG làm được

- Build trực tiếp trên Mac bằng .NET SDK cho macOS: không được, vì WPF
  chỉ có trên Windows.
- Boot Camp trên Mac M2 Pro: không có, chỉ Mac Intel mới có.
- Chạy BibaYanji / An Tâm PC test trực tiếp trên Mac để test máy Mac:
  không được, app này chỉ test máy Windows. Muốn test máy Mac để bán thì
  dùng bộ web AIO bản Mac (`Server_Mac.py`) đã có trong gói AIO trước đó,
  không dùng app này.

## Sau khi có bản portable rồi thì sao

Dù build bằng cách nào, bước sau đó giống nhau: copy cả thư mục `publish`
sang USB, giữ nguyên cấu trúc bên trong, cắm USB vào máy Windows cần test,
bấm đúp file exe chính, bấm Yes khi hỏi quyền Admin. Nhớ 4 giới hạn đã ghi
trong `README-VN.md`: cần Admin, máy test Windows 10 2004+/11 64-bit,
driver PawnIO không đi theo USB, kết quả nằm trong `%APPDATA%\YanJi` trên
máy test nên test xong phải copy ra USB trước khi xóa máy để bán.
