# Student Project Management System

**Hệ thống quản lý đề tài và theo dõi tiến độ dự án sinh viên**

**Phiên bản hiện tại:** v0.7.7  
**Trạng thái:** Hoàn thành các luồng chính của giai đoạn v0.7; chuẩn bị triển khai v0.8 (Automation & Notifications).

## 1. Giới thiệu

Student Project Management System hỗ trợ quản lý tập trung quy trình từ đề xuất, đăng ký đề tài và lựa chọn giảng viên hướng dẫn đến triển khai Project, quản lý Milestone, cập nhật tiến độ, nộp kết quả và đánh giá.

Hệ thống có ba vai trò:

- **Admin:** Quản lý tài khoản, hồ sơ người dùng, đề tài, đợt đăng ký và sức chứa giảng viên; theo dõi dữ liệu hệ thống.
- **Lecturer:** Quản lý đề tài do mình tạo, điều chỉnh sức chứa hướng dẫn, phản hồi yêu cầu của sinh viên, tạo hoặc duyệt kế hoạch Milestone, nhận xét và đánh giá kết quả.
- **Student:** Tìm và đề xuất đề tài, đăng ký giảng viên, chỉnh sửa theo góp ý, xác nhận giảng viên, quản lý Milestone được phép và cập nhật/nộp kết quả dự án.

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

- Đăng nhập, đăng xuất với JWT và phân quyền Admin, Lecturer, Student.
- API kiểm tra quyền truy cập theo vai trò và dữ liệu thuộc quyền quản lý.
- Khôi phục phiên khi tải lại trang (F5) nếu JWT còn hợp lệ; đăng xuất khi phiên hết hiệu lực.

### User Management

- Admin tạo, chỉnh sửa và quản lý tài khoản, hồ sơ sinh viên và giảng viên.
- Student và Lecturer chỉ xem hồ sơ cá nhân; chỉ Admin được cập nhật thông tin hồ sơ.

### Topic Management

- Tạo, chỉnh sửa, tìm kiếm và xem đề tài.
- Lecturer chỉ được mở/đóng đăng ký đề tài mình tạo; Admin được quản lý tất cả đề tài.
- Student có thể chọn đề tài có sẵn hoặc tự đề xuất đề tài.
- Khi chọn đề tài có sẵn, Student có thể sửa **bản đề xuất riêng** mà không ghi đè nội dung đề tài gốc.

### Lecturer Management & Registration

- Xem danh sách giảng viên, sức chứa tối đa, số Project đang hướng dẫn và số suất còn lại.
- Sức chứa được tổng hợp qua tất cả các đợt; Lecturer được chỉnh giới hạn của mình, Admin được chỉnh giới hạn của từng Lecturer, không phải chọn đợt.
- Student gửi yêu cầu đề tài và giảng viên đến nhiều Lecturer; Lecturer có thể đồng ý nhận, từ chối hoặc yêu cầu sửa.
- Student xem nhận xét, sửa bản đề xuất, gửi lại và chọn **một** Lecturer trong số những người đã đồng ý nhận.
- Chỉ sau khi Student xác nhận Lecturer, hệ thống mới tạo Project và xử lý các yêu cầu còn lại.

### Project Management

- Liên kết Project với sinh viên, giảng viên hướng dẫn và đề tài đã được xác nhận.
- Hiển thị **Tiến độ tổng hợp dự án**: tỷ lệ hoàn thành, số Milestone đã duyệt, đang làm, chờ duyệt, cần sửa và chi tiết từng Milestone.
- Khi tất cả Milestone được duyệt, Project chuyển sang `COMPLETED`.
- Khi bổ sung Milestone hợp lệ vào Project đã hoàn thành, Project có thể trở lại `IN_PROGRESS` và tính lại tiến độ; các Milestone đã duyệt vẫn được giữ nguyên.
- Student có một Project được tự động chọn Project tại các trang tiến độ, không cần chọn lặp lại.

### Milestone Management

- Lecturer tạo Milestone trực tiếp; Milestone do Lecturer tạo có thể được thực hiện ngay theo quy trình hiện có.
- Student có thể **đề xuất Milestone** cho Project của mình. Lecturer được sửa kế hoạch, yêu cầu Student chỉnh sửa hoặc xác nhận để thực hiện.
- Quy trình **xác nhận kế hoạch** được tách khỏi **phê duyệt kết quả hoàn thành**, tránh đánh dấu đã hoàn thành khi chỉ mới duyệt đề xuất.
- Form tạo/đề xuất Milestone gồm: tên, mô tả, **ngày Deadline**, kết quả cần đạt; không yêu cầu nhập ngày bắt đầu hoặc giờ.
- Deadline được hiểu là **cuối ngày đã chọn (23:59:59 theo giờ Việt Nam)**.

### Progress Updates & Submissions

- Trang **Milestone**, **Cập nhật tiến độ**, **Nộp kết quả** và **Đánh giá/nhận xét** được tách riêng theo chức năng.
- Student cập nhật phần trăm hoàn thành, nội dung đã thực hiện và ghi chú.
- Student nộp tóm tắt kết quả, đường dẫn tài liệu và có thể **nộp lại nhiều lần** trong khi chờ Lecturer duyệt.
- Mỗi lần nộp được lưu thành bản riêng trong lịch sử, không ghi đè bản trước.
- Lecturer xem bản nộp mới nhất, nhận xét, yêu cầu chỉnh sửa hoặc phê duyệt.
- Milestone đã được phê duyệt không cho nộp lại kết quả theo quy tắc hiện tại.
- Giao diện ngày giờ sử dụng định dạng 24 giờ khi được hỗ trợ.

## 4. Hướng dẫn chạy chương trình

### Yêu cầu môi trường

- .NET SDK 10
- Node.js và npm
- SQL Server LocalDB
- Database SQL Server đã khởi tạo, được cấu hình kết nối cho Backend

### Khởi động Backend

Từ thư mục gốc của dự án:

```powershell
cd .\backend\StudentProjects.Api
dotnet run
```

### Khởi động Frontend

Mở terminal thứ hai, từ thư mục gốc của dự án:

```powershell
cd .\frontend
npm install
npm run dev
```

### Build và kiểm thử

Từ thư mục gốc dự án:

```powershell
dotnet build .\backend\StudentProjects.sln
dotnet test .\backend\StudentProjects.IntegrationTests\StudentProjects.IntegrationTests.csproj

cd .\frontend
npm run build
```

**Lưu ý:** Dừng Backend đang chạy trước khi build/test nếu Windows báo file DLL bị khóa. Các lệnh chạy ứng dụng thông thường không tự reset dữ liệu SQL Server.

## 5. Tiến độ phát triển

| Phiên bản | Nội dung | Trạng thái |
|---|---|---|
| v0.3 | Authentication & Authorization | Hoàn thành |
| v0.4 | User Management | Hoàn thành |
| v0.5 | Topic Management | Hoàn thành |
| v0.6 → v0.6.14 | Đăng ký đề tài, giảng viên, xác nhận hướng dẫn, phân quyền, quản lý sức chứa | Hoàn thành giai đoạn |
| v0.7 → v0.7.7 | Milestone, đề xuất/xác nhận kế hoạch, tiến độ, nộp lại, đánh giá, tổng hợp Project | Hoàn thành các luồng chính |
| v0.8 | Automation & Notifications | Kế hoạch |
| v0.9 | Integration Testing & Stabilization | Kế hoạch |
| v1.0 | Hoàn thiện, báo cáo và demo | Kế hoạch |

## 6. Kế hoạch v0.8

- Tự động kiểm tra Deadline và phát hiện Milestone sắp đến hạn, quá hạn.
- Gửi thông báo và nhắc nhở phù hợp cho Student và Lecturer.
- Tự cập nhật trạng thái/cảnh báo theo điều kiện nghiệp vụ.
- Theo dõi lịch sử thông báo và trạng thái xử lý.

Các nội dung này **chưa được ghi nhận là đã triển khai** trong v0.7.7.

## 7. Ghi chú và kiểm thử

- **Phiên bản nền hiện tại:** v0.7.7. Khi phát triển v0.8, giữ nguyên các luồng đã hoàn thiện từ v0.3 đến v0.7.7.
- Đã có lần chạy **54/54 integration tests thành công** ở giai đoạn v0.7.1. Kết quả này **không thay thế** việc chạy lại toàn bộ bộ test trên v0.7.7.
- Các luồng tạo/đề xuất Milestone, sửa và xác nhận kế hoạch, cập nhật tiến độ, nộp/nộp lại, giảng viên đánh giá và hiển thị tổng hợp Project đã được kiểm tra thủ công trong quá trình phát triển.
- Cần chạy lại `dotnet build`, `dotnet test` và `npm run build` trên source v0.7.7 hiện tại trước khi đánh dấu bản phát hành cuối cùng là đã kiểm thử tự động đầy đủ.
- GitHub Integration, AI Code Analysis và automation/notification chưa được xác nhận là hoàn tất trong bản hiện tại.
