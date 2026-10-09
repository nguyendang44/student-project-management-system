namespace StudentProjects.Domain.Enums;
public enum UserRole { Student, Lecturer, Admin }
public enum TopicStatus { DRAFT, PENDING_APPROVAL, APPROVED, REJECTED, IN_PROGRESS, COMPLETED, CANCELLED, PUBLISHED }
public enum RequestStatus { PENDING, OFFERED, ACCEPTED, REJECTED, CANCELLED, REVISION_REQUIRED }
public enum ProjectStatus { DRAFT, REGISTERED, IN_PROGRESS, COMPLETED, CANCELLED }
public enum MilestoneStatus { PENDING, IN_PROGRESS, SUBMITTED, REVISION_REQUIRED, APPROVED, OVERDUE }
public enum LecturerCapacityStatus { AVAILABLE, FULL }
public enum AnalysisStatus { PENDING, RUNNING, COMPLETED, FAILED }
public enum AutomationRunStatus { PENDING, SUCCEEDED, FAILED }
