# Week 3 | Class Diagram (mô hình domain ở mức thiết kế)

Các class bên dưới đã được khai báo trong `StudentProjects.Domain/Entities/CoreEntities.cs`. Đường liên hệ mô tả FK, **không** biểu thị navigation property C# hiện hữu. Vì vậy sơ đồ phản ánh logical model, không khẳng định đầy đủ phương thức nghiệp vụ.

```mermaid
classDiagram
  class Entity {
    +Guid Id
    +DateTimeOffset CreatedAt
    +DateTimeOffset UpdatedAt
  }
  class User {
    +string FullName
    +string Email
    +string PasswordHash
    +UserRole Role
    +bool IsActive
  }
  class StudentProfile {
    +Guid UserId
    +string StudentCode
  }
  class LecturerProfile {
    +Guid UserId
    +string Specialty
  }
  class RegistrationPeriod {
    +DateTimeOffset StartsAt
    +DateTimeOffset EndsAt
    +bool IsOpen
  }
  class LecturerCapacity {
    +Guid LecturerUserId
    +Guid RegistrationPeriodId
    +int MaxStudents
    +int CurrentStudents
    +byte[] RowVersion
  }
  class Topic {
    +Guid ProposedByUserId
    +TopicStatus Status
    +bool IsRegistrationOpen
  }
  class TopicStateHistory {
    +Guid TopicId
    +TopicStatus FromStatus
    +TopicStatus ToStatus
  }
  class TopicRegistration {
    +Guid TopicId
    +Guid StudentUserId
    +RequestStatus Status
  }
  class LecturerRequest {
    +Guid StudentUserId
    +Guid LecturerUserId
    +RequestStatus Status
  }
  class Project {
    +Guid TopicId
    +Guid StudentUserId
    +Guid LecturerUserId
    +ProjectStatus Status
  }
  class Milestone {
    +Guid ProjectId
    +DateTimeOffset DeadlineAt
    +MilestoneStatus Status
    +int PercentComplete
  }
  class ProgressUpdate {
    +Guid MilestoneId
    +int PercentComplete
  }
  class MilestoneSubmission {
    +Guid MilestoneId
    +DateTimeOffset SubmittedAt
  }
  class ProjectEvaluation {
    +Guid ProjectId
    +Guid LecturerUserId
    +bool IsApproved
  }
  class RepositoryLink {
    +Guid ProjectId
    +string RepositoryUrl
  }
  class CodeAnalysisReport {
    +Guid RepositoryLinkId
    +AnalysisStatus Status
  }

  Entity <|-- User
  Entity <|-- StudentProfile
  Entity <|-- LecturerProfile
  Entity <|-- RegistrationPeriod
  Entity <|-- LecturerCapacity
  Entity <|-- Topic
  Entity <|-- TopicStateHistory
  Entity <|-- TopicRegistration
  Entity <|-- LecturerRequest
  Entity <|-- Project
  Entity <|-- Milestone
  Entity <|-- ProgressUpdate
  Entity <|-- MilestoneSubmission
  Entity <|-- ProjectEvaluation
  Entity <|-- RepositoryLink
  Entity <|-- CodeAnalysisReport
  User "1" --> "0..1" StudentProfile : profile
  User "1" --> "0..1" LecturerProfile : profile
  User "1" --> "0..*" LecturerCapacity : lecturer
  RegistrationPeriod "1" --> "0..*" LecturerCapacity : period
  Topic "1" --> "0..*" TopicRegistration : registered_for
  Topic "1" --> "0..*" TopicStateHistory : transitions
  Topic "1" --> "0..*" LecturerRequest : requested_for
  LecturerRequest "0..1" --> "0..1" Project : activates
  Project "1" --> "0..*" Milestone : contains
  Milestone "1" --> "0..*" ProgressUpdate : updates
  Milestone "1" --> "0..*" MilestoneSubmission : submits
  Project "1" --> "0..*" ProjectEvaluation : reviewed
  Project "1" --> "0..*" RepositoryLink : linked_to
  RepositoryLink "1" --> "0..*" CodeAnalysisReport : analyzed
```

Các class vận hành `Notification`, `AutomationRun`, `AuditEntry`, `SystemError`, `SystemSetting` vẫn thuộc domain và được biểu diễn ở ERD. Phương thức Application service (ví dụ `AcceptLecturerRequest`) **chưa được code**; không thêm method giả vào class diagram.
