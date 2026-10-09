# Student Project Management System

**Hệ thống quản lý đề tài và theo dõi tiến độ dự án sinh viên**

**Phiên bản hiện tại:** v0.6.12  
**Trạng thái:** Hoàn thành giai đoạn v0.6, chuẩn bị triển khai v0.7.

## 1. Giới thiệu

Student Project Management System là hệ thống hỗ trợ quản lý đề tài, đăng ký giảng viên hướng dẫn và theo dõi quá trình thực hiện dự án sinh viên.

Hệ thống có ba vai trò:

- **Admin:** Quản lý tài khoản, thông tin người dùng, đề tài, đợt đăng ký và dữ liệu hệ thống.
- **Lecturer:** Quản lý đề tài do mình tạo, thiết lập sức chứa hướng dẫn, tiếp nhận và phản hồi yêu cầu của sinh viên.
- **Student:** Tìm kiếm đề tài, đăng ký giảng viên, chỉnh sửa đề xuất theo góp ý, xác nhận giảng viên và theo dõi Project.

## 2. Công nghệ sử dụng

| Thành phần | Công nghệ |
|---|---|
| Backend | ASP.NET Core 10 |
| Frontend | Vue 3, TypeScript, Vite |
| Database | SQL Server LocalDB |
| ORM | Entity Framework Core 10 |
| Authentication | JWT |
| Testing | xUnit Integration Tests |
| API | REST API |

## 3. Các chức năng đã triển khai

### Authentication & Authorization
- Đăng nhập, đăng xuất bằng JWT.
- Phân quyền Admin, Lecturer và Student.
- Bảo vệ API theo vai trò.

### User Management
- Admin tạo, chỉnh sửa và quản lý tài khoản.
- Admin quản lý thông tin sinh viên và giảng viên.
- Student và Lecturer chỉ được xem hồ sơ cá nhân, không được tự chỉnh sửa.

### Topic Management
- Tạo và quản lý đề tài.
- Tìm kiếm và xem danh sách đề tài.
- Lecturer mở/đóng đăng ký đề tài mình tạo.
- Admin có quyền quản lý tất cả đề tài.
- Sinh viên lựa chọn đề tài có sẵn hoặc tự đề xuất.
- Sinh viên chỉnh sửa bản đề xuất riêng mà không thay đổi đề tài gốc.

### Lecturer Management
- Xem danh sách giảng viên.
- Thiết lập giới hạn sinh viên hướng dẫn.
- Theo dõi số sinh viên đang hướng dẫn và số suất còn lại.
- Sức chứa được tổng hợp qua các đợt đăng ký.

### Topic & Lecturer Registration
- Sinh viên gửi yêu cầu đăng ký đề tài và giảng viên.
- Cho phép gửi yêu cầu đến nhiều giảng viên.
- Giảng viên đồng ý, từ chối hoặc yêu cầu chỉnh sửa.
- Sinh viên xem nhận xét, chỉnh sửa và gửi lại đề xuất.
- Theo dõi trạng thái các yêu cầu.
- Sinh viên lựa chọn một giảng viên đã đồng ý nhận.
- Hệ thống tạo Project sau khi sinh viên xác nhận giảng viên.
- Các yêu cầu còn lại được xử lý theo quy tắc đăng ký.

### Project Management
- Tạo Project từ yêu cầu đã được xác nhận.
- Liên kết Project với sinh viên, giảng viên và đề tài.
- Theo dõi thông tin và trạng thái Project cơ bản.

## 4. Hướng dẫn chạy chương trình

### Yêu cầu môi trường

- .NET SDK 10
- Node.js và npm
- SQL Server LocalDB
- Database đã được khởi tạo và cấu hình kết nối

### Khởi động Backend

```powershell
cd backend/StudentProjects.Api
dotnet run
```

### Khởi động Frontend

Mở terminal khác:

```powershell
cd frontend
npm install
npm run dev
```

### Kiểm thử

Chạy từ thư mục gốc dự án:

```powershell
dotnet build ./backend/StudentProjects.sln

dotnet test ./backend/StudentProjects.IntegrationTests/StudentProjects.IntegrationTests.csproj
```

Kiểm tra Frontend:

```powershell
cd frontend
npm run build
```

## 5. Tiến độ phát triển

| Phiên bản | Nội dung | Trạng thái |
|---|---|---|
| v0.3 | Authentication & Authorization | Hoàn thành |
| v0.4 | User Management | Hoàn thành |
| v0.5 | Topic Management | Hoàn thành |
| v0.6 → v0.6.12 | Lecturer Management, Registration, Project Creation, Permissions | Hoàn thành giai đoạn |
| v0.7 | Milestone & Progress Management | Kế hoạch |
| v0.8 | Automation & Notifications | Kế hoạch |
| v0.9 | Integration Testing & Stabilization | Kế hoạch |
| v1.0 | Hoàn thiện hệ thống và báo cáo | Kế hoạch |

## 6. Kế hoạch v0.7

Triển khai module quản lý tiến độ dự án:

- Tạo, cập nhật và quản lý Milestone.
- Thiết lập deadline.
- Sinh viên cập nhật tiến độ và kết quả.
- Sinh viên nộp kết quả Milestone.
- Giảng viên nhận xét, yêu cầu chỉnh sửa hoặc phê duyệt.
- Theo dõi trạng thái thực hiện Project.

## 7. Ghi chú

- Phiên bản nền của giai đoạn tiếp theo là **v0.6.12**.
- Các module hiện có cần được duy trì khi triển khai chức năng mới.
- Chưa xác nhận kết quả chạy đầy đủ bộ Integration Tests trên phiên bản v0.6.12.
- Các chức năng quản lý Milestone, Automation, Notification và tích hợp nâng cao tiếp tục được triển khai theo lộ trình.
