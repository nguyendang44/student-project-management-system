# Week 3 | ERD đề xuất (SQL Server / EF Core)

**Trạng thái:** bản thiết kế v0.2 để rà soát. Chưa tạo database, migration hoặc dữ liệu giả. Sơ đồ được chia làm hai phần để dễ đọc; cùng một `USER` / `PROJECT` là cùng bảng.

## A. Tài khoản, đăng ký, phân công và dự án

```mermaid
erDiagram
  USER {
    uniqueidentifier Id PK
    nvarchar Email UK
    nvarchar PasswordHash
    nvarchar Role
    bit IsActive
  }
  STUDENT_PROFILE {
    uniqueidentifier Id PK
    uniqueidentifier UserId FK,UK
    nvarchar StudentCode UK
  }
  LECTURER_PROFILE {
    uniqueidentifier Id PK
    uniqueidentifier UserId FK,UK
    nvarchar Specialty
  }
  REGISTRATION_PERIOD {
    uniqueidentifier Id PK
    nvarchar Name
    datetimeoffset StartsAt
    datetimeoffset EndsAt
    bit IsOpen
  }
  LECTURER_CAPACITY {
    uniqueidentifier Id PK
    uniqueidentifier LecturerUserId FK
    uniqueidentifier RegistrationPeriodId FK
    int MaxStudents
    int CurrentStudents
    rowversion RowVersion
  }
  TOPIC {
    uniqueidentifier Id PK
    uniqueidentifier ProposedByUserId FK
    nvarchar Title
    nvarchar Status
    bit IsRegistrationOpen
  }
  TOPIC_STATE_HISTORY {
    uniqueidentifier Id PK
    uniqueidentifier TopicId FK
    uniqueidentifier ActorUserId FK
    nvarchar FromStatus
    nvarchar ToStatus
  }
  TOPIC_REGISTRATION {
    uniqueidentifier Id PK
    uniqueidentifier StudentUserId FK
    uniqueidentifier TopicId FK
    uniqueidentifier RegistrationPeriodId FK
    nvarchar Status
  }
  LECTURER_REQUEST {
    uniqueidentifier Id PK
    uniqueidentifier StudentUserId FK
    uniqueidentifier LecturerUserId FK
    uniqueidentifier TopicId FK
    uniqueidentifier RegistrationPeriodId FK
    nvarchar Status
  }
  PROJECT {
    uniqueidentifier Id PK
    uniqueidentifier TopicId FK
    uniqueidentifier StudentUserId FK
    uniqueidentifier LecturerUserId FK
    uniqueidentifier RegistrationPeriodId FK
    uniqueidentifier AcceptedLecturerRequestId FK
    nvarchar Status
  }

  USER ||--o| STUDENT_PROFILE : has
  USER ||--o| LECTURER_PROFILE : has
  USER ||--o{ LECTURER_CAPACITY : is_lecturer
  REGISTRATION_PERIOD ||--o{ LECTURER_CAPACITY : has
  USER ||--o{ TOPIC : proposes
  USER ||--o{ TOPIC_STATE_HISTORY : changes
  TOPIC ||--o{ TOPIC_STATE_HISTORY : history
  USER ||--o{ TOPIC_REGISTRATION : registers
  TOPIC ||--o{ TOPIC_REGISTRATION : receives
  REGISTRATION_PERIOD ||--o{ TOPIC_REGISTRATION : includes
  USER ||--o{ LECTURER_REQUEST : student_or_lecturer
  TOPIC ||--o{ LECTURER_REQUEST : subject
  REGISTRATION_PERIOD ||--o{ LECTURER_REQUEST : includes
  TOPIC ||--o{ PROJECT : becomes
  USER ||--o{ PROJECT : student_or_lecturer
  REGISTRATION_PERIOD ||--o{ PROJECT : contains
  LECTURER_REQUEST |o--o| PROJECT : accepted_as
```

**Ghi chú:** các quan hệ `USER` tới `LECTURER_REQUEST` và `PROJECT` tương ứng hai FK độc lập (StudentUserId/LecturerUserId), rút gọn thành một đường trong hình để giảm nhiễu. Chính xác từng cặp FK nằm trong `StudentProjectsDbContext.cs`.

## B. Theo dõi tiến độ, đánh giá, tích hợp và vận hành

```mermaid
erDiagram
  USER {
    uniqueidentifier Id PK
  }
  PROJECT {
    uniqueidentifier Id PK
  }
  MILESTONE {
    uniqueidentifier Id PK
    uniqueidentifier ProjectId FK
    datetimeoffset DeadlineAt
    int PercentComplete
    nvarchar Status
  }
  PROGRESS_UPDATE {
    uniqueidentifier Id PK
    uniqueidentifier MilestoneId FK
    uniqueidentifier StudentUserId FK
    int PercentComplete
  }
  MILESTONE_SUBMISSION {
    uniqueidentifier Id PK
    uniqueidentifier MilestoneId FK
    uniqueidentifier SubmittedByStudentUserId FK
    nvarchar DocumentReference
  }
  PROJECT_EVALUATION {
    uniqueidentifier Id PK
    uniqueidentifier ProjectId FK
    uniqueidentifier LecturerUserId FK
    uniqueidentifier MilestoneId FK
    bit IsApproved
  }
  REPOSITORY_LINK {
    uniqueidentifier Id PK
    uniqueidentifier ProjectId FK
    nvarchar RepositoryUrl
  }
  CODE_ANALYSIS_REPORT {
    uniqueidentifier Id PK
    uniqueidentifier ProjectId FK
    uniqueidentifier RepositoryLinkId FK
    nvarchar Status
  }
  NOTIFICATION {
    uniqueidentifier Id PK
    uniqueidentifier RecipientUserId FK
    bit IsRead
  }
  AUDIT_ENTRY {
    uniqueidentifier Id PK
    uniqueidentifier ActorUserId FK
    nvarchar Action
  }
  AUTOMATION_RUN {
    uniqueidentifier Id PK
    nvarchar JobName
    nvarchar Status
  }
  SYSTEM_ERROR {
    uniqueidentifier Id PK
    nvarchar Origin
    nvarchar Severity
  }
  SYSTEM_SETTING {
    uniqueidentifier Id PK
    nvarchar Key UK
  }

  PROJECT ||--o{ MILESTONE : milestones
  MILESTONE ||--o{ PROGRESS_UPDATE : progress
  USER ||--o{ PROGRESS_UPDATE : student_author
  MILESTONE ||--o{ MILESTONE_SUBMISSION : submissions
  USER ||--o{ MILESTONE_SUBMISSION : student_author
  PROJECT ||--o{ PROJECT_EVALUATION : evaluations
  MILESTONE |o--o{ PROJECT_EVALUATION : optional_review
  USER ||--o{ PROJECT_EVALUATION : lecturer_author
  PROJECT ||--o{ REPOSITORY_LINK : repositories
  PROJECT ||--o{ CODE_ANALYSIS_REPORT : analyses
  REPOSITORY_LINK ||--o{ CODE_ANALYSIS_REPORT : source
  USER ||--o{ NOTIFICATION : receives
  USER |o--o{ AUDIT_ENTRY : actor
```

`AUTOMATION_RUN`, `SYSTEM_ERROR`, `SYSTEM_SETTING` là các bảng vận hành độc lập, không có FK ở bản thiết kế hiện tại. Tất cả các entity kế thừa `Id`, `CreatedAt`, `UpdatedAt` từ `Entity`; sơ đồ lược bớt hai trường thời gian để dễ đọc.

## Cardinality và ràng buộc đã đề xuất

- `User` 1–0..1 `StudentProfile` / `LecturerProfile` (unique `UserId`, nhưng quyền/role phải kiểm tra tại application).
- `LecturerCapacity` là một bản ghi / (`LecturerUserId`, `RegistrationPeriodId`), unique composite, `0 <= CurrentStudents <= MaxStudents`, có `RowVersion`.
- `Project.AcceptedLecturerRequestId` có thể null khi dự án còn DRAFT; khi được sử dụng, một LecturerRequest không được dùng cho hai Project (unique index).
- Tất cả FK cấu hình `NoAction` khi xóa để không làm mất nhật ký/tiến độ do cascade; chiến lược lưu trữ và xóa mềm sẽ quyết định sau.
- Deadline là `datetimeoffset`, `OVERDUE` là **trạng thái milestone**, không phải trạng thái Project. `REJECTED` chỉ cho Topic/Proposal hoặc request.
- Trạng thái `Topic` hiện đại diện cả Topic Proposal, không có bảng TopicProposal riêng.

Xem `docs/DATABASE-DESIGN.md` cho các quyết định còn chờ chốt và những gì **chưa được kiểm tra bằng runtime**.
