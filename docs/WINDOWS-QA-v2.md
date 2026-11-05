# PCcheck v2 — Checklist kiểm thử Windows thực tế

**Nhánh**: feature/reference-layout-pc-first  
**Bản tham chiếu**: ảnh Dashboard An Tâm Mac check đã chốt; chỉ thay khối MDM bằng Sức khỏe & Rủi ro PC.

## Giao diện (cần chụp màn hình)
- [ ] 1536×1024 ở 100% DPI: sidebar/2 cột/shop/nút đáy hiển thị giống bố cục tham chiếu
- [ ] 1366×768: cuộn dọc hợp lý; không cuộn ngang, không che nút
- [ ] 1920×1080 ở 125% DPI: logo, mascot và text không méo/chồng
- [ ] 980×680 (kích thước cửa sổ tối thiểu): cột phải xếp xuống, không mất nội dung
- [ ] Light/Dark mode: chữ, cảnh báo và icon đủ tương phản
- [ ] Sidebar: Tổng quan, Kiểm tra chi tiết, Phần cứng & Sức khỏe, Báo cáo, Công cụ, Hướng dẫn, Giới thiệu
- [ ] Mascot và logo tải thành công từ app resources, không vỡ ảnh

## Nút và workflow
- [ ] Mới mở app: không chạy stress/disk benchmark, 0/8 chưa thử
- [ ] Kiểm tra chi tiết mở wizard ở bài chưa hoàn thành đầu tiên
- [ ] Quay lại/Bài tiếp theo/Thoát wizard không làm mất kết quả đã ghi
- [ ] Sidebar không phá khóa điều hướng khi wizard đang chạy
- [ ] Sau một bài pass, tự tiến bài tiếp theo; tiến độ Dashboard khớp dữ liệu
- [ ] Nút Xuất báo cáo mở trang Tổng kết; lưu TXT/JSON và mở lại được
- [ ] Tham quan shop mở đúng URL https://vitinhantam.com
- [ ] Dừng test nặng: thử Prime95/FurMark/diskspd; xác nhận tiến trình thật sự dừng hoặc báo tên tiến trình còn sống

## Cảm biến và tính trung thực
- [ ] CPU/RAM Dashboard là thông tin thực; không dùng số ví dụ
- [ ] Không đọc được SMART: không hiển thị 'Đạt'
- [ ] Test lỗi bàn phím/camera/loa/ổ: báo cáo và Dashboard cùng phản ánh trạng thái
- [ ] Chưa hoàn thành thì không đưa kết luận 'Có thể mua' sai
- [ ] Thử máy có/không có pin, máy có nhiều ổ, nhiều màn hình

## Giới hạn
Build CI chỉ chứng minh biên dịch/đóng gói; không chứng minh thao tác GUI hay hành vi phần cứng. Đánh dấu từng checklist sau khi kiểm tra trên Windows thật.
