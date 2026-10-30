
# Student Project Management System

**Hệ thống quản lý đề tài và theo dõi tiến độ dự án sinh viên**

**Phiên bản hiện tại:** v0.10 (GitHub Integration)  
**Trạng thái:** Đã nghiệm thu các chức năng chính, hoàn thành tích hợp GitHub Repository Public.

## 1. Giới thiệu

Student Project Management System là hệ thống hỗ trợ quản lý toàn bộ quá trình thực hiện đồ án sinh viên, từ đăng ký đề tài, lựa chọn giảng viên hướng dẫn đến lập kế hoạch, theo dõi tiến độ, nộp kết quả và đánh giá.

Hệ thống tích hợp thông báo tự động, nhật ký hướng dẫn và GitHub nhằm tăng tính minh bạch, giảm công việc quản lý thủ công và hỗ trợ giảng viên theo dõi quá trình thực hiện đồ án.

Hệ thống gồm ba vai trò:

- **Admin:** Quản lý tài khoản, sinh viên, giảng viên, đề tài, đợt đăng ký, sức chứa hướng dẫn và dữ liệu hệ thống.
- **Lecturer:** Quản lý đề tài, tiếp nhận yêu cầu hướng dẫn, duyệt kế hoạch Milestone, đánh giá kết quả, xác nhận nhật ký hướng dẫn và theo dõi mã nguồn GitHub.
- **Student:** Tìm kiếm hoặc đề xuất đề tài, đăng ký giảng viên, lập kế hoạch Milestone, cập nhật tiến độ, nộp kết quả và liên kết GitHub Repository.

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
| GitHub Integration | GitHub REST API |
| Version Control | Git, GitHub Desktop |

## 3. Các chức năng đã triển khai

### 3.1. Authentication & Authorization

- Đăng nhập, đăng xuất bằng JWT.
- Phân quyền Admin, Lecturer và Student.
- Kiểm soát quyền truy cập API theo vai trò và dữ liệu thuộc quyền quản lý.
- Tự khôi phục phiên khi tải lại trang nếu JWT còn hiệu lực.
- Tự đăng xuất khi phiên hết hạn.
- Ngăn sinh viên và giảng viên truy cập dữ liệu không thuộc quyền.

### 3.2. User Management

- Admin tạo và quản lý tài khoản.
- Quản lý hồ sơ sinh viên và giảng viên.
- Phân quyền người dùng theo vai trò.
- Sinh viên và giảng viên có thể xem hồ sơ cá nhân.
- Chỉ Admin có quyền cập nhật hồ sơ theo quy trình hiện tại.

### 3.3. Topic Management

- Tạo, chỉnh sửa, tìm kiếm và quản lý đề tài.
- Giảng viên quản lý đề tài do mình tạo.
- Admin có quyền quản lý toàn bộ đề tài.
- Sinh viên chọn đề tài có sẵn hoặc đề xuất đề tài mới.
- Sinh viên có thể chỉnh sửa bản đề xuất riêng mà không thay đổi đề tài gốc.

**Quy tắc lựa chọn giảng viên:**

- Nếu đề tài do giảng viên tạo, sinh viên chỉ được đăng ký với chính giảng viên tạo đề tài.
- Nếu giảng viên đó hết suất hướng dẫn, sinh viên không thể chuyển sang giảng viên khác cho cùng đề tài.
- Với đề tài do Admin tạo hoặc sinh viên tự đề xuất, sinh viên vẫn được lựa chọn giảng viên phù hợp.
- Backend kiểm tra quy tắc này để ngăn các yêu cầu đăng ký không hợp lệ.

### 3.4. Registration Period Management

Admin quản lý các đợt đăng ký đề tài.

- Tạo, mở và đóng đợt đăng ký.
- Tên đợt được tự động đánh số: `1`, `2`, `3`,...
- Ngày bắt đầu mặc định là ngày mai.
- Giờ bắt đầu mặc định là `07:00`.
- Sử dụng định dạng 24 giờ.
- Chỉ chọn giờ nguyên từ `00` đến `23`, không cần nhập phút.
- Trạng thái mặc định khi tạo là **Mở**.
- Thời điểm kết thúc phải sau thời điểm bắt đầu.
- Các đợt cũ được giữ nguyên dữ liệu.

### 3.5. Lecturer Management & Registration

- Theo dõi số lượng Project đang hướng dẫn.
- Quản lý sức chứa tối đa của giảng viên.
- Hiển thị số suất hướng dẫn còn lại.
- Giảng viên điều chỉnh sức chứa của mình.
- Admin điều chỉnh sức chứa của từng giảng viên.
- Sinh viên gửi yêu cầu đăng ký đề tài và giảng viên.
- Giảng viên có thể đồng ý hướng dẫn, yêu cầu chỉnh sửa hoặc từ chối.
- Sinh viên xem góp ý, sửa bản đề xuất và gửi lại.
- Sinh viên xác nhận một giảng viên trong số các giảng viên đã đồng ý.
- Project chỉ được tạo sau khi sinh viên xác nhận giảng viên.

### 3.6. Project Management

- Quản lý Project theo sinh viên, đề tài và giảng viên hướng dẫn.
- Hiển thị tiến độ tổng hợp của Project.
- Thống kê Milestone đã duyệt, đang thực hiện, chờ duyệt và cần chỉnh sửa.
- Sinh viên có một Project phù hợp được tự động chọn Project.
- Khi tất cả Milestone hoàn thành và được duyệt, Project chuyển sang `COMPLETED`.
- Khi bổ sung Milestone hợp lệ, Project có thể trở lại `IN_PROGRESS`.
- Giữ nguyên kết quả các Milestone đã được phê duyệt.

### 3.7. Milestone Management

- Giảng viên tạo Milestone trực tiếp.
- Sinh viên đề xuất Milestone riêng lẻ.
- Sinh viên có thể đề xuất kế hoạch theo mẫu.
- Hỗ trợ các nhóm mẫu Milestone theo loại đề tài.
- Cho phép điều chỉnh nội dung, Deadline và thứ tự Milestone.
- Tách biệt xác nhận kế hoạch và phê duyệt kết quả hoàn thành.

Mỗi Milestone bao gồm:

- Tên Milestone.
- Mô tả công việc.
- Deadline.
- Kết quả cần đạt.
- Tiến độ thực hiện.
- Trạng thái kế hoạch và trạng thái kết quả.

Deadline được tính đến **23:59:59 theo giờ Việt Nam** của ngày đã chọn.

**Chỉnh sửa Deadline:**

Sinh viên thực hiện trong phần **Chi tiết Milestone**.

- Khi giảng viên chưa xác nhận kế hoạch, sinh viên được sửa ngày trực tiếp và không gửi thông báo mới.
- Khi kế hoạch đã được xác nhận, sinh viên vẫn được đề xuất sửa ngày.
- Sau khi sửa ngày của kế hoạch đã duyệt, Milestone chuyển sang trạng thái chờ xác nhận lại.
- Giảng viên nhận thông báo để xem xét Deadline mới.
- Nếu sinh viên tiếp tục sửa ngày trong lúc đang chờ xác nhận lại, hệ thống không gửi thông báo trùng.
- Milestone đã nộp kết quả hoặc đã được duyệt kết quả không được sửa Deadline theo quy tắc hiện tại.

### 3.8. Progress Updates & Submissions

- Sinh viên cập nhật phần trăm tiến độ.
- Ghi nội dung công việc đã thực hiện và ghi chú.
- Nộp kết quả Milestone.
- Đính kèm đường dẫn tài liệu.
- Có thể nộp lại khi được phép.
- Lưu lịch sử các lần nộp, không ghi đè bản cũ.
- Giảng viên xem và đánh giá kết quả.
- Giảng viên yêu cầu sửa hoặc phê duyệt.
- Milestone đã được duyệt kết quả không cho nộp lại.

### 3.9. Automation & Notifications (v0.8)

Hệ thống hỗ trợ thông báo và cảnh báo tự động.

**Thông báo Deadline:**

- Nhắc trước Deadline 7 ngày.
- Nhắc trước Deadline 3 ngày.
- Nhắc trước Deadline 1 ngày.
- Cảnh báo Milestone quá hạn.
- Hiển thị các cảnh báo liên quan trên hệ thống.

**Thông báo đăng ký đề tài:**

- Sinh viên gửi yêu cầu đăng ký.
- Sinh viên gửi lại bản đề xuất sau khi chỉnh sửa.
- Sinh viên hủy yêu cầu.
- Giảng viên đồng ý hướng dẫn.
- Giảng viên yêu cầu chỉnh sửa.
- Giảng viên từ chối.

**Quản lý thông báo:**

- Thông báo được lưu trong Database.
- Mỗi người dùng chỉ xem thông báo của mình.
- Đánh dấu đã đọc.
- Đánh dấu tất cả đã đọc.
- Làm mới danh sách thông báo.
- Chuyển đến Project, Milestone hoặc yêu cầu liên quan.

### 3.10. Guidance Journal (v0.8)

Nhật ký hướng dẫn giúp ghi nhận các buổi làm việc giữa sinh viên và giảng viên.

- Sinh viên tạo nhật ký hướng dẫn.
- Ghi ngày giờ và nội dung trao đổi.
- Ghi kết quả buổi hướng dẫn.
- Giảng viên xem và xác nhận nhật ký.
- Lưu lịch sử các buổi hướng dẫn.
- Giới hạn quyền truy cập theo Project.
- Hỗ trợ chọn thời gian hiện tại khi tạo nhật ký.

### 3.11. Security & Integration Testing (v0.9)

Giai đoạn v0.9 tập trung vào độ ổn định và bảo mật.

- Rà soát phân quyền Backend.
- Kiểm tra truy cập dữ liệu ngoài phạm vi quản lý.
- Kiểm tra vòng đời Project.
- Kiểm tra các thao tác Milestone.
- Kiểm tra thông báo riêng tư.
- Kiểm tra nhật ký hướng dẫn.
- Rà soát các cảnh báo NuGet.
- Cập nhật các dependency liên quan đến bảo mật.
- Bổ sung Integration Tests.
- Kiểm tra Frontend Build và Backend Build.

**Kết quả nghiệm thu thủ công v0.9:**

| Nhóm kiểm thử | Kết quả |
|---|---|
| Quy trình nghiệp vụ | 11/11 PASS |
| Phân quyền | 7/7 PASS |
| Độ ổn định | 6/6 PASS |
| Tổng cộng | **24/24 PASS** |

Các chức năng chính của v0.9 đã được nghiệm thu.

### 3.12. GitHub Integration (v0.10)

Cho phép liên kết GitHub Repository với Project của sinh viên.

**Student:**

- Liên kết GitHub Repository Public với Project.
- Xem thông tin repository.
- Xem branch mặc định.
- Xem danh sách branch.
- Xem 20 commit gần nhất.
- Làm mới dữ liệu commit.
- Gỡ liên kết repository.

**Lecturer:**

- Xem repository của Project đang hướng dẫn.
- Theo dõi branch và lịch sử commit.
- Xem nội dung và thời gian commit.
- Truy cập commit trên GitHub.

**Admin:**

- Xem thông tin repository theo quyền quản lý.

Thông tin GitHub hiển thị gồm:

- Repository URL.
- Default Branch.
- Danh sách branch, tối đa 20.
- Mã commit.
- Nội dung commit.
- Người thực hiện.
- Thời gian commit.
- Đường dẫn commit.

Hệ thống sử dụng GitHub REST API để lấy dữ liệu repository public.

**Giới hạn phiên bản v0.10:**

- Chưa hỗ trợ repository private.
- Chưa tích hợp GitHub OAuth.
- Chưa xác minh quyền sở hữu GitHub của sinh viên.
- Chưa triển khai AI Code Analysis.
- Không yêu cầu lưu mật khẩu hoặc personal access token GitHub.

## 4. Cấu trúc dự án

```text
student-project-management-system/
│
├── backend/
│   ├── StudentProjects.Api/
│   ├── StudentProjects.Application/
│   ├── StudentProjects.Domain/
│   ├── StudentProjects.Infrastructure/
│   └── StudentProjects.IntegrationTests/
│
├── frontend/
│
├── scripts/
│
└── README.md
```

Thư mục `scripts/` chứa các script cài đặt, cập nhật, kiểm thử và nâng cấp Database.

Các script cũ được giữ lại để hỗ trợ việc bảo trì và khôi phục các phiên bản trước.

## 5. Hướng dẫn cài đặt

### Yêu cầu môi trường

- Windows.
- .NET SDK 10.
- Node.js và npm.
- SQL Server LocalDB.
- SQL Server Management Studio (SSMS).
- Git và GitHub Desktop nếu cần quản lý source.

### Cấu hình Database

Database Development:

```text
StudentProjectsDev
```

SQL Server:

```text
(localdb)\MSSQLLocalDB
```

File cấu hình:

```text
backend/StudentProjects.Api/appsettings.Development.json
```

Ví dụ:

```json
{
  "ConnectionStrings": {
    "Default": "Server=(localdb)\\MSSQLLocalDB;Database=StudentProjectsDev;Trusted_Connection=True;TrustServerCertificate=True"
  }
}
```

Không đưa JWT secret, mật khẩu tài khoản hoặc token truy cập vào repository công khai.

Các thông tin bí mật nên được lưu trong User Secrets hoặc biến môi trường.

## 6. Hướng dẫn chạy chương trình

### Backend

Mở PowerShell tại thư mục dự án:

```powershell
cd .\backend\StudentProjects.Api
dotnet run
```

### Frontend

Mở terminal thứ hai:

```powershell
cd .\frontend
npm ci
npm run dev
```

Sau khi cài dependencies lần đầu, những lần chạy tiếp theo thường chỉ cần:

```powershell
npm run dev
```

Mở địa chỉ Frontend được Vite hiển thị.

## 7. Build và kiểm thử

### Backend Build

```powershell
dotnet build .\backend\StudentProjects.sln
```

### Integration Tests

```powershell
dotnet test .\backend\StudentProjects.IntegrationTests\StudentProjects.IntegrationTests.csproj
```

### Frontend Build

```powershell
cd .\frontend
npm run build
```

Nếu Windows báo lỗi DLL đang được sử dụng, hãy dừng Backend trước khi build hoặc test.

## 8. Nâng cấp Database v0.10

GitHub Integration yêu cầu bảng lưu liên kết repository trong SQL Server.

File nâng cấp:

```text
scripts/upgrade-github-v010.sql
```

**Quy trình:**

1. Sao lưu Database hiện tại.
2. Mở SQL Server Management Studio.
3. Kết nối `(localdb)\MSSQLLocalDB`.
4. Chọn Database `StudentProjectsDev`.
5. Mở file `upgrade-github-v010.sql` đã sửa lỗi cú pháp.
6. Nhấn Execute.
7. Kiểm tra kết quả.

Kết quả mong đợi:

```text
Result                      DatabaseName        ExistingRepositoryLinks
v0.10 GitHub schema ready    StudentProjectsDev  0
```

Số repository có thể lớn hơn 0 nếu đã có dữ liệu.

Nếu đã nâng cấp thành công, không cần chạy lại script.

Không xóa hoặc khởi tạo lại Database khi nâng cấp.

## 9. Sao lưu và quản lý phiên bản

### Source Code

Sử dụng GitHub Desktop để quản lý lịch sử phát triển.

Repository:

https://github.com/nguyendang44/student-project-management-system

Khi hoàn thành một phiên bản:

1. Kiểm tra các thay đổi.
2. Loại trừ file chứa thông tin bí mật.
3. Commit source.
4. Push lên GitHub.
5. Gắn Git Tag cho bản ổn định khi cần.

### Database

Database phải được sao lưu riêng, không tự động đi kèm Git.

Bản sao lưu v0.9 đã được kiểm tra bằng SQL Server:

```text
The backup set on file 1 is valid.
```

Thời gian xác nhận: **30/10/2026**.

Đường dẫn sao lưu trên máy phát triển:

```text
E:\SPMS-Backup\v0.9\StudentProjectsDev_v0.9.bak
```

`RESTORE VERIFYONLY` kiểm tra tính hợp lệ của bản backup; để xác minh hoàn toàn khả năng phục hồi, nên thử restore sang Database khác.

Không đưa file `.bak`, khóa bí mật hoặc dữ liệu nhạy cảm lên repository công khai.

## 10. Tiến độ phát triển

| Phiên bản | Nội dung | Trạng thái |
|---|---|---|
| v0.3 | Authentication & Authorization | Hoàn thành |
| v0.4 | User Management | Hoàn thành |
| v0.5 | Topic Management | Hoàn thành |
| v0.6 → v0.6.14 | Lecturer Management, Registration, phân quyền và sức chứa | Hoàn thành |
| v0.7 → v0.7.7 | Project, Milestone, Progress, Submission, Review | Hoàn thành |
| v0.8 → v0.8.4 | Automation, Notifications, Guidance Journal | Hoàn thành |
| v0.9 | Integration Testing, Security & Stabilization | Nghiệm thu 24/24 PASS |
| v0.9.1 → v0.9.6 | Hoàn thiện nghiệp vụ đăng ký, thông báo và Deadline Milestone | Hoàn thành |
| **v0.10** | **GitHub Integration** | **Đã nghiệm thu** |
| v0.11 | AI Code Analysis | Kế hoạch |
| v0.12 | Automated Reports & Export | Kế hoạch |
| v1.0 | Hoàn thiện, tài liệu và demo | Kế hoạch |

## 11. Kết quả nghiệm thu

### v0.9

Đã nghiệm thu 24/24 trường hợp:

- 11/11 quy trình nghiệp vụ.
- 7/7 phân quyền.
- 6/6 độ ổn định.

### v0.10

Đã kiểm thử và xác nhận:

- Sinh viên liên kết repository GitHub Public.
- Hiển thị branch mặc định và danh sách branch.
- Hiển thị 20 commit gần nhất.
- Giảng viên xem repository theo quyền hướng dẫn.
- Sinh viên và giảng viên không truy cập dữ liệu ngoài quyền.
- Dữ liệu liên kết vẫn tồn tại sau khi khởi động lại.
- Gỡ và liên kết lại repository.
- Nâng cấp Database GitHub thành công.

Các trường hợp trên đã được xác nhận qua nghiệm thu thực tế.

Khi source tiếp tục thay đổi, cần chạy lại Backend Build, Integration Tests và Frontend Build để kiểm tra tương thích.

## 12. Kế hoạch phát triển tiếp theo

### v0.11 - AI Code Analysis

Dự kiến bổ sung:

- Phân tích mã nguồn từ GitHub Repository.
- Đánh giá cấu trúc dự án.
- Nhận diện lỗi và rủi ro kỹ thuật.
- Đưa ra nhận xét về chất lượng code.
- Hỗ trợ giảng viên theo dõi quá trình phát triển mã nguồn.
- Tổng hợp nhận xét để phục vụ đánh giá đồ án.

### v0.12 - Automated Reports & Export

Dự kiến bổ sung:

- Tự động tổng hợp tiến độ Project.
- Tổng hợp Milestone và kết quả đánh giá.
- Tổng hợp nhật ký hướng dẫn.
- Tạo báo cáo tiến độ cho sinh viên.
- Tạo báo cáo theo dõi cho giảng viên.
- Xuất Word, PDF và Excel.
- Giảm công việc nhập liệu và lập báo cáo thủ công.

### v1.0 - Final Release

- Hoàn thiện giao diện và trải nghiệm người dùng.
- Kiểm thử toàn hệ thống.
- Rà soát bảo mật và hiệu năng.
- Hoàn thiện tài liệu kỹ thuật.
- Chuẩn bị dữ liệu demo.
- Đóng gói phiên bản phát hành chính thức.

---

## 13. Ghi chú

- Phiên bản hiện tại: **v0.10**.
- Các chức năng từ v0.3 đến v0.10 đã được phát triển và kiểm tra theo từng giai đoạn.
- Không xóa Database hoặc script nâng cấp cũ khi chưa kiểm tra sự phụ thuộc.
- Giữ nguyên các chức năng đã nghiệm thu khi triển khai phiên bản mới.
- GitHub Integration hiện chỉ hỗ trợ repository public.
- AI Code Analysis và Automated Reports vẫn nằm trong kế hoạch phát triển.

**Student Project Management System v0.10 - GitHub Integration**
