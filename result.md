# Kết quả triển khai dự án Aura Cinema (Tiến độ hiện tại)

Dự án đã được di chuyển sang ổ D (`D:\AURACINEMA`) và đã hoàn tất các Phase 0, 1, 2 và 3, đồng thời đang triển khai Phase 4 (Đặt vé & Thanh toán). Dưới đây là chi tiết các chức năng đã hoàn thiện:

## 1. Hạ tầng và Cấu trúc dự án (Phase 0 & 1)
- **Kiến trúc:** Clean Architecture với 3 lớp (Domain, Infrastructure, Services) và 1 lớp Web (ASP.NET Core MVC).
- **Cơ sở dữ liệu:** SQL Server (LocalDB) với EF Core 8.0.
- **Data Seed:** Tự động tạo dữ liệu mẫu khi khởi chạy (Admin, 3 phòng chiếu, 300 ghế với các loại ghế Thường/VIP/Đôi, 5 bộ phim mẫu, cấu hình giá, và khuyến mãi).
- **Thiết kế UI/UX:** Tạo Design System với file `site.css` dùng Dark Theme, Glassmorphism, Micro-animations cao cấp.

## 2. Hệ thống Xác thực (Phase 2)
- **Đăng ký / Đăng nhập:** Sử dụng Cookie Authentication, mật khẩu được mã hóa an toàn bằng `BCrypt`.
- **Bảo mật:** Tích hợp tính năng Quên mật khẩu / Đặt lại mật khẩu sử dụng mã OTP.
- **Email:** Tích hợp `MailKit` gửi email OTP (được cấu hình sẵn khung chuẩn).

## 3. Hệ thống Public Site (Phase 3)
- **Trang chủ (`HomeController`):**
  - Hero Section đẹp mắt giới thiệu dự án.
  - Danh sách phim đang chiếu và phim sắp chiếu.
- **Danh sách phim (`MoviesController`):**
  - Hiển thị danh sách tất cả các phim.
  - Hỗ trợ Tìm kiếm theo tên và Lọc theo trạng thái (Đang chiếu / Sắp chiếu).
  - Phân trang dữ liệu.
- **Chi tiết phim:**
  - Hiển thị thông tin phim (đạo diễn, diễn viên, thời lượng, thể loại).
  - Nút xem Trailer bằng Modal iframe.
  - **Lịch chiếu thông minh:** Gom nhóm suất chiếu theo ngày. Tự động tính toán số lượng ghế trống thực tế dựa trên các đơn hàng hiện có.
- **Trang khuyến mãi (`PromotionsController`):** Hiển thị các mã giảm giá và hỗ trợ copy mã nhanh.

## 4. Hệ thống Đặt vé & Thanh toán (Phase 4 - Đang triển khai)
- **Thiết kế Booking Service:** Đã xây dựng `IBookingService` và `BookingService` để xử lý:
  - Lấy sơ đồ phòng chiếu.
  - Thuật toán tính toán giá vé động (cộng phụ thu ghế VIP, ghế Đôi, phụ thu cuối tuần, trừ khuyến mãi).
  - Giữ ghế (Hold Seats) trong 5 phút.
  - Sinh link thanh toán qua cổng PayOS và xử lý Webhook.
- **Giao diện chọn ghế (`SelectSeats.cshtml`):**
  - Giao diện sơ đồ ghế trực quan, hỗ trợ chọn nhiều ghế (tối đa 8 ghế).
  - Phân loại bằng màu sắc/icon cho ghế Thường, VIP, Đôi.
  - Tự động khóa (disabled) các ghế đã có người mua hoặc đang được giữ.
  - Tính toán tổng tiền trực tiếp trên Client.
- **Giao diện thanh toán (`Checkout.cshtml`):**
  - Hiển thị tóm tắt đơn hàng.
  - Nhập mã khuyến mãi và áp dụng giảm giá.
  - Đồng hồ đếm ngược 5 phút (nếu hết giờ tự động hủy đơn).
  - Nút thanh toán chuyển hướng sang PayOS.

---

### *Công việc đang dang dở (Lỗi hiện tại cần fix)*:
- Đang gặp lỗi namespace `Net.payOS` không tìm thấy trong thư viện `payOS` khi build project `AuraCinema.Services`. Sẽ tiến hành sửa lỗi này để chức năng tạo link thanh toán PayOS hoạt động bình thường.
