# Week 3 | Quyết định thiết kế dữ liệu (đề xuất v0.2)

## Căn cứ tài liệu

- *Lộ trình 10 tuần.docx*: FR-01..FR-21, NFR bảo mật/tính nhất quán, đặc biệt FR-05 và FR-08..FR-10 (capacity), FR-06/07 (topic), FR-11..FR-14 (project/milestone/progress) và FR-15..FR-19 (GitHub/AI/notification/automation).
- *Tuan_2_Phan_tich_yeu_cau_Use_Case.docx*: UC-01..UC-43; các luồng UC-ST-03, UC-ST-06, UC-LC-08, UC-ST-10, UC-LC-17. Trạng thái Project/Request/Milestone tuân theo bảng mục 10.
- **Phần thiết kế thêm** (không phải quy định có sẵn trong tài liệu): danh sách cột, SQL Server types, PK/FK, độ dài varchar, index, unique và delete behaviors. Chúng phải được duyệt trước migration.

## Phân cụm 22 bảng (v0.3)

| Phạm vi | Các bảng | Quy tắc thiết kế |
|---|---|---|
| Tài khoản | `Role`, `User`, `StudentProfile`, `LecturerProfile` | User.RoleId FK -> Roles (Student, Lecturer, Admin); password hash, không plaintext |
| Đợt đăng ký và capacity | `RegistrationPeriod`, `LecturerCapacity` | Mỗi lecturer có một capacity trong một đợt, unique lecturer+period và rowversion |
| Đề tài và đăng ký | `Topic`, `TopicStateHistory`, `TopicRegistration`, `LecturerRequest` | Proposal thể hiện bằng Topic.Status, lịch sử riêng; request lưu trạng thái |
| Dự án và tiến độ | `Project`, `Milestone`, `ProgressUpdate`, `MilestoneSubmission`, `ProjectEvaluation` | Từ Project ra nhiều milestone, nhiều lần cập nhật/nộp/đánh giá |
| Tích hợp GitHub/AI | `RepositoryLink`, `CodeAnalysisReport` | Dữ liệu phân tích gắn repo và project, AI chỉ hỗ trợ |
| Thông báo/vận hành | `Notification`, `AutomationRun`, `SystemError`, `AuditEntry`, `SystemSetting` | Lưu theo dòng sự kiện/bản ghi, chưa chạy scheduler |

`DbContext` đã khai báo 22 `DbSet` và model mapping; 20 bảng nghiệp vụ dùng `ExcludeFromMigrations` để migration đầu tiên chỉ tạo Users/Roles, **chưa sinh migration** và chưa được thử nghiệm trực tiếp với SQL Server.

## Chọn ràng buộc dữ liệu

1. PK của các entity: `Guid Id`; các timestamp chung `CreatedAt`, `UpdatedAt` lấy từ `Entity`. **Chưa có interceptor tự cập nhật UpdatedAt**.
2. FK tương ứng user, topic, period, project, milestone, repository và request được khai báo rõ trong EF Fluent API, `DeleteBehavior.NoAction` để hạn chế cascade/multiple-cascade-path với SQL Server.
3. Unique: `User.Email`, `StudentProfile.StudentCode`, profile `UserId`, cặp (`LecturerCapacity.LecturerUserId`, `RegistrationPeriodId`), `SystemSetting.Key`, và (không null) `Project.AcceptedLecturerRequestId`.
4. Check constraint: `RegistrationPeriod.EndsAt > StartsAt`, `Milestone.DeadlineAt >= StartAt`, `0 ≤ PercentComplete ≤ 100` (Milestone & ProgressUpdate), `0 ≤ CurrentStudents ≤ MaxStudents` (LecturerCapacity).
5. `RequestStatus`, `ProjectStatus`, `TopicStatus`, `MilestoneStatus`, `AnalysisStatus`, `AutomationRunStatus`, Role được lưu bằng FK tới bảng `Roles`. Không gán `REJECTED` cho Project.
6. `DocumentReference` là đường dẫn hoặc mã tham chiếu file, không phải dữ liệu file nhị phân. Cơ chế upload, ACL và object storage chưa xác định.

## Bắt buộc xử lý ở Application/Transaction (CHƯA code)

- **Capacity an toàn khi cạnh tranh**: tạo request sau khi kiểm tra đợt mở, topic hợp lệ và capacity có chỗ (không giữ chỗ đối với request `PENDING`). Khi lecturer chấp nhận, thực hiện trong **một transaction**: kiểm tra request `PENDING`; xác định đúng lecturer, student, period và topic; cập nhật `CurrentStudents` với điều kiện nhỏ hơn `MaxStudents` (atomic conditional update hoặc row-version concurrency); cập nhật request `ACCEPTED`; tạo Project; tạo Notification. Nếu bất kỳ bước nào lỗi, rollback; kiểm thử chấp nhận đồng thời.
- **Ownership/role**: StudentUserId phải thuộc user role Student; LecturerUserId phải thuộc Lecturer; actor duyệt milestone phải chính là lecturer được phân công; submission chỉ đến từ student của project. FK đến User không tự kiểm chứng các role đó.
- **Một project mỗi student?** Tài liệu chưa định nghĩa dự án nhóm; mô hình v0.2 chọn **1 student / project** để tương thích skeleton. Phải xác nhận trước migration nếu hỗ trợ nhóm.
- **Đăng ký trùng**: tránh các request/topic registrations PENDING/ACCEPTED xung đột ở tầng nghiệp vụ bằng transaction và unique filtered indexes khi quy tắc chính thức được chốt; không đặt unique toàn phần để giữ lịch sử request bị từ chối và gửi lại.
- **Giới hạn liên kết đồng nhất**: Project.AcceptedLecturerRequestId phải có đúng student/lecturer/topic/period như Project; ProjectEvaluation.MilestoneId phải thuộc ProjectId; CodeAnalysisReport.RepositoryLinkId phải thuộc ProjectId; kiểm tra tại Application hoặc bổ sung composite key sau khi chốt nghiệp vụ.
- **Hệ thống tự động**: xử lý deadline tạo OVERDUE/notification bằng job riêng sau này, không chạy trong DbContext. AI không quyết định đánh giá cuối cùng.
- **An toàn dữ liệu**: mật khẩu hash mạnh, `email` chuẩn hóa trước khi lưu, mã khóa/secret dùng cấu hình riêng; policy lưu và xóa dữ liệu cá nhân chưa thiết kế.

## Những điểm cần xác nhận trước khi tạo migration

1. Project chỉ có một Student hay cho phép **nhóm nhiều Student**? Nếu nhóm, cần `ProjectMember`, không chỉ `Project.StudentUserId`.
2. Đăng ký giảng viên được giới hạn theo **từng đợt** (thiết kế hiện tại) hay còn quy tắc tổng năm học?
3. Một Student có thể gửi nhiều LecturerRequest `PENDING` đồng thời hay chỉ một? Lịch sử request bị reject phải bảo toàn.
4. Một Topic được nhiều Project đăng ký hay giới hạn slot / 1 Project? 
5. Một Project được liên kết nhiều GitHub repository hay chỉ 1 repository chính? Mô hình hiện tại là 1:N.
6. Có cần điểm số định lượng cho ProjectEvaluation không? Tài liệu chỉ yêu cầu nhận xét/đánh giá; không triển khai quản lý điểm chính thức.

## Triển khai DB sau khi duyệt (chưa chạy)

Trong bản v0.2, `DependencyInjection` chỉ đăng ký DbContext **khi có** `ConnectionStrings:Default`. Không cấu hình database thì API `/health` và 501 stubs vẫn hoạt động. Để sang tuần 4, chốt ERD, cung cấp SQL Server và connection string bảo mật, sau đó mới tạo EF Core migration và kiểm tra constraints/quan hệ qua integration tests.

## v0.3 | Auth implementation update

- `Role` (3 seed values Student/Lecturer/Admin), `User.RoleId` FK, `User.TokenVersion` account-wide JWT revocation.
- Core Roles/Users relations are implemented. Existing other tables are still schema draft.
- `scripts/setup-auth.ps1` generates and applies the auth-only initial migration (Roles and Users) **on Windows in a fresh SQL Server LocalDB database**, after dependency restore/build. Migration is not committed with this deliverable.
- For authentication and integration tests see [`AUTH-IMPLEMENTATION.md`](AUTH-IMPLEMENTATION.md).
