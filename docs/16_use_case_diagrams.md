# 16. Use Case Diagrams

## 16.1 Mục tiêu

Use Case Diagram được xây dựng nhằm mô tả trực quan các chức năng chính của hệ thống quản lý đề tài và theo dõi tiến độ dự án sinh viên.

Use Case Diagram giúp:

- Xác định các Actor tương tác với hệ thống.
- Xác định phạm vi chức năng của hệ thống.
- Thể hiện mối quan hệ giữa Actor và Use Case.
- Làm cơ sở cho thiết kế kiến trúc, API và giao diện.
- Hỗ trợ quá trình triển khai và kiểm thử hệ thống.

Hệ thống có ba Actor chính:

- Student
- Lecturer
- Admin

Ngoài ra, hệ thống có các thành phần hỗ trợ:

- Scheduled Job
- GitHub
- AI Analysis Service

---

## 16.2 Tổng quan hệ thống

Hệ thống quản lý toàn bộ vòng đời của một đề tài sinh viên:

```text
Đăng nhập
   ↓
Quản lý đề tài
   ↓
Đăng ký đề tài / Đề xuất đề tài
   ↓
Đăng ký giảng viên
   ↓
Kiểm tra Capacity
   ↓
Tạo Project
   ↓
Tạo Milestone
   ↓
Sinh viên cập nhật tiến độ
   ↓
Nộp kết quả
   ↓
Giảng viên đánh giá
   ↓
APPROVED / REVISION_REQUIRED
   ↓
Hoàn thành Project
```

GitHub và AI Analysis được tích hợp vào giai đoạn thực hiện Project.

Scheduled Job chịu trách nhiệm kiểm tra deadline, cập nhật trạng thái và tạo notification tự động.

---

## 16.3 System Boundary

System Boundary của hệ thống bao gồm các nhóm chức năng:

1. Authentication & Authorization
2. User Management
3. Topic Management
4. Registration Management
5. Lecturer Capacity
6. Project Management
7. Milestone Management
8. Progress Management
9. Evaluation
10. GitHub Integration
11. AI Analysis
12. Notification
13. Automation
14. Dashboard & Reporting

Các hệ thống bên ngoài như GitHub và AI Analysis Service tương tác với hệ thống thông qua các chức năng tích hợp tương ứng.

---

## 16.4 Use Case Diagram tổng quan

```mermaid
flowchart LR

    Student["Student"]
    Lecturer["Lecturer"]
    Admin["Admin"]

    System["Student Project Management System"]

    Student --> UC1["Authentication"]
    Student --> UC2["Topic Management"]
    Student --> UC3["Topic Registration"]
    Student --> UC4["Lecturer Registration"]
    Student --> UC5["Project Management"]
    Student --> UC6["Milestone & Progress"]
    Student --> UC7["Notification"]
    Student --> UC8["Dashboard"]

    Lecturer --> UC1
    Lecturer --> UC2
    Lecturer --> UC4
    Lecturer --> UC5
    Lecturer --> UC6
    Lecturer --> UC9["Evaluation"]
    Lecturer --> UC10["GitHub Integration"]
    Lecturer --> UC11["AI Analysis"]
    Lecturer --> UC7
    Lecturer --> UC8
    Lecturer --> UC12["Reporting"]

    Admin --> UC1
    Admin --> UC13["User Management"]
    Admin --> UC2
    Admin --> UC5
    Admin --> UC6
    Admin --> UC7
    Admin --> UC8
    Admin --> UC12
    Admin --> UC14["Automation Monitoring"]

    Student -.-> System
    Lecturer -.-> System
    Admin -.-> System
```

---

## 16.5 Student Use Case Diagram

Student là người thực hiện Project và cập nhật tiến độ.

Các chức năng chính:

- Đăng nhập.
- Xem danh sách đề tài.
- Đăng ký đề tài.
- Đề xuất đề tài.
- Theo dõi trạng thái đề xuất.
- Gửi yêu cầu đăng ký giảng viên.
- Theo dõi yêu cầu đăng ký giảng viên.
- Xem Project.
- Cập nhật tiến độ.
- Nộp kết quả Milestone.
- Chỉnh sửa Milestone khi được yêu cầu.
- Liên kết GitHub Repository.
- Xem kết quả AI Analysis.
- Xem Notification.
- Xem Dashboard.

```mermaid
flowchart LR

    Student["Student"]

    subgraph System["Student Project Management System"]
        ST01["UC-ST-01<br/>Đăng nhập"]
        ST02["UC-ST-02<br/>Xem danh sách đề tài"]
        ST03["UC-ST-03<br/>Đăng ký đề tài"]
        ST04["UC-ST-04<br/>Đề xuất đề tài"]
        ST05["UC-ST-05<br/>Theo dõi đề xuất"]
        ST06["UC-ST-06<br/>Đăng ký giảng viên"]
        ST07["UC-ST-07<br/>Theo dõi yêu cầu GV"]
        ST08["UC-ST-08<br/>Xem Project"]
        ST09["UC-ST-09<br/>Cập nhật tiến độ"]
        ST10["UC-ST-10<br/>Nộp Milestone"]
        ST11["UC-ST-11<br/>Chỉnh sửa Milestone"]
        ST12["UC-ST-12<br/>Liên kết GitHub"]
        ST13["UC-ST-13<br/>Xem AI Analysis"]
        ST14["UC-ST-14<br/>Xem Notification"]
        ST15["UC-ST-15<br/>Xem Dashboard"]
    end

    Student --> ST01
    Student --> ST02
    Student --> ST03
    Student --> ST04
    Student --> ST05
    Student --> ST06
    Student --> ST07
    Student --> ST08
    Student --> ST09
    Student --> ST10
    Student --> ST11
    Student --> ST12
    Student --> ST13
    Student --> ST14
    Student --> ST15

    ST04 --> ST05
    ST03 --> ST06
    ST06 --> ST08
    ST08 --> ST09
    ST09 --> ST10
    ST10 --> ST11
    ST12 --> ST13
```

---

## 16.6 Lecturer Use Case Diagram

Lecturer chịu trách nhiệm quản lý đề tài, tiếp nhận sinh viên, quản lý Project và đánh giá kết quả.

Các chức năng chính:

- Đăng nhập.
- Tạo đề tài.
- Quản lý đề tài.
- Xem và duyệt Topic Proposal.
- Xem và xử lý Lecturer Request.
- Thiết lập Capacity.
- Xem Capacity.
- Xem Project.
- Tạo Milestone.
- Theo dõi Progress.
- Xem kết quả Milestone.
- Yêu cầu chỉnh sửa.
- Phê duyệt Milestone.
- Đánh giá Project.
- Xem GitHub Repository.
- Xem AI Analysis.
- Xem Notification.
- Xem Dashboard.
- Xem thống kê.

```mermaid
flowchart LR

    Lecturer["Lecturer"]

    subgraph System["Student Project Management System"]
        LC01["UC-LC-01<br/>Đăng nhập"]
        LC02["UC-LC-02<br/>Tạo đề tài"]
        LC03["UC-LC-03<br/>Quản lý đề tài"]
        LC04["UC-LC-04<br/>Xem Topic Proposal"]
        LC05["UC-LC-05<br/>Duyệt Topic Proposal"]
        LC06["UC-LC-06<br/>Từ chối Topic Proposal"]
        LC07["UC-LC-07<br/>Xem Lecturer Request"]
        LC08["UC-LC-08<br/>Chấp nhận Request"]
        LC09["UC-LC-09<br/>Từ chối Request"]
        LC10["UC-LC-10<br/>Thiết lập Capacity"]
        LC11["UC-LC-11<br/>Xem Capacity"]
        LC12["UC-LC-12<br/>Xem Project"]
        LC13["UC-LC-13<br/>Tạo Milestone"]
        LC14["UC-LC-14<br/>Theo dõi Progress"]
        LC15["UC-LC-15<br/>Xem kết quả Milestone"]
        LC16["UC-LC-16<br/>Yêu cầu chỉnh sửa"]
        LC17["UC-LC-17<br/>Phê duyệt Milestone"]
        LC18["UC-LC-18<br/>Đánh giá Project"]
        LC19["UC-LC-19<br/>Xem GitHub"]
        LC20["UC-LC-20<br/>Xem AI Analysis"]
        LC21["UC-LC-21<br/>Xem Notification"]
        LC22["UC-LC-22<br/>Xem Dashboard"]
        LC23["UC-LC-23<br/>Xem thống kê"]
    end

    Lecturer --> LC01
    Lecturer --> LC02
    Lecturer --> LC03
    Lecturer --> LC04
    Lecturer --> LC05
    Lecturer --> LC06
    Lecturer --> LC07
    Lecturer --> LC08
    Lecturer --> LC09
    Lecturer --> LC10
    Lecturer --> LC11
    Lecturer --> LC12
    Lecturer --> LC13
    Lecturer --> LC14
    Lecturer --> LC15
    Lecturer --> LC16
    Lecturer --> LC17
    Lecturer --> LC18
    Lecturer --> LC19
    Lecturer --> LC20
    Lecturer --> LC21
    Lecturer --> LC22
    Lecturer --> LC23

    LC04 --> LC05
    LC04 --> LC06

    LC07 --> LC08
    LC07 --> LC09

    LC08 --> LC12
    LC12 --> LC13
    LC13 --> LC14
    LC14 --> LC15
    LC15 --> LC16
    LC15 --> LC17
    LC19 --> LC20
```

---

## 16.7 Admin Use Case Diagram

Admin chịu trách nhiệm quản lý dữ liệu và giám sát hoạt động của hệ thống.

Các chức năng chính:

- Đăng nhập.
- Quản lý Student.
- Quản lý Lecturer.
- Xem và quản lý Topic.
- Xem Project.
- Xem Milestone.
- Xem Progress.
- Xem Evaluation.
- Xem Notification.
- Xem Dashboard.
- Xem thống kê.
- Xem báo cáo hệ thống.
- Theo dõi Automation.
- Theo dõi lỗi hệ thống.

```mermaid
flowchart LR

    Admin["Admin"]

    subgraph System["Student Project Management System"]
        AD01["UC-AD-01<br/>Đăng nhập"]
        AD02["UC-AD-02<br/>Quản lý Student"]
        AD03["UC-AD-03<br/>Quản lý Lecturer"]
        AD04["UC-AD-04<br/>Xem Topic"]
        AD05["UC-AD-05<br/>Quản lý Topic"]
        AD06["UC-AD-06<br/>Xem Project"]
        AD07["UC-AD-07<br/>Xem Milestone"]
        AD08["UC-AD-08<br/>Xem Progress"]
        AD09["UC-AD-09<br/>Xem Evaluation"]
        AD10["UC-AD-10<br/>Xem Notification"]
        AD11["UC-AD-11<br/>Xem Dashboard"]
        AD12["UC-AD-12<br/>Xem thống kê"]
        AD13["UC-AD-13<br/>Xem báo cáo"]
        AD14["UC-AD-14<br/>Theo dõi Automation"]
        AD15["UC-AD-15<br/>Theo dõi lỗi hệ thống"]
    end

    Admin --> AD01
    Admin --> AD02
    Admin --> AD03
    Admin --> AD04
    Admin --> AD05
    Admin --> AD06
    Admin --> AD07
    Admin --> AD08
    Admin --> AD09
    Admin --> AD10
    Admin --> AD11
    Admin --> AD12
    Admin --> AD13
    Admin --> AD14
    Admin --> AD15

    AD04 --> AD05
    AD06 --> AD07
    AD07 --> AD08
    AD08 --> AD09
    AD11 --> AD12
    AD12 --> AD13
```

---

## 16.8 Topic và Registration Use Case

Quá trình hình thành Project có thể bắt đầu theo hai hướng:

### Trường hợp 1: Sinh viên đăng ký đề tài có sẵn

```text
Student
   ↓
Xem danh sách Topic
   ↓
Đăng ký Topic
   ↓
Hệ thống ghi nhận Registration
   ↓
Đăng ký Lecturer
   ↓
Kiểm tra Lecturer Capacity
   ↓
Lecturer Accept / Reject
```

### Trường hợp 2: Sinh viên tự đề xuất đề tài

```text
Student
   ↓
Đề xuất Topic
   ↓
PENDING_APPROVAL
   ↓
Lecturer xem Proposal
   ↓
      ┌───────────────┐
      │               │
   APPROVED        REJECTED
      │               │
      ↓               ↓
Đăng ký Lecturer    Chỉnh sửa
                      │
                      ↓
                   Resubmit
```

Use Case relationship:

```mermaid
flowchart TD

    Student["Student"]
    Lecturer["Lecturer"]

    Student --> Proposal["Đề xuất Topic"]
    Proposal --> Pending["PENDING_APPROVAL"]

    Pending --> Review["Lecturer Review"]

    Lecturer --> Review

    Review --> Approved["APPROVED"]
    Review --> Rejected["REJECTED"]

    Rejected --> Edit["Student chỉnh sửa"]
    Edit --> Proposal

    Student --> RegisterTopic["Đăng ký Topic"]
    RegisterTopic --> LecturerRequest["Lecturer Request"]

    LecturerRequest --> Capacity["Kiểm tra Capacity"]

    Capacity --> Accept["ACCEPTED"]
    Capacity --> Reject["REJECTED"]

    Lecturer --> Accept
    Lecturer --> Reject

    Accept --> Project["Create / Activate Project"]
```

---

## 16.9 Project và Milestone Use Case

Sau khi Lecturer chấp nhận yêu cầu đăng ký, hệ thống tạo hoặc kích hoạt Project.

Lecturer có thể tạo nhiều Milestone cho Project.

Student thực hiện từng Milestone và cập nhật tiến độ.

```mermaid
flowchart TD

    Request["Lecturer Request"]
    Accept["Lecturer Accept"]
    Project["Project"]
    Milestone["Milestone"]
    Progress["Update Progress"]
    Submit["Submit Milestone"]
    Review["Lecturer Review"]

    Request --> Accept
    Accept --> Project
    Project --> Milestone
    Milestone --> Progress
    Progress --> Submit
    Submit --> Review

    Review --> Approved["APPROVED"]
    Review --> Revision["REVISION_REQUIRED"]

    Revision --> Progress

    Approved --> Next["Next Milestone"]
    Next --> Milestone
```

---

## 16.10 GitHub và AI Analysis

GitHub và AI Analysis là các chức năng mở rộng của hệ thống.

Student liên kết Repository với Project.

Lecturer có thể xem Repository.

AI Analysis Service lấy source code từ Repository và thực hiện phân tích.

AI chỉ cung cấp kết quả hỗ trợ. Lecturer vẫn là người chịu trách nhiệm đánh giá cuối cùng.

```mermaid
flowchart LR

    Student["Student"]
    Lecturer["Lecturer"]

    Project["Project"]

    GitHub["GitHub Repository"]
    AI["AI Analysis Service"]
    Report["AI Analysis Report"]

    Student --> Link["Link Repository"]
    Link --> Project
    Project --> GitHub

    GitHub --> AI
    AI --> Report

    Lecturer --> GitHub
    Lecturer --> Report

    Report --> Evaluation["Lecturer Evaluation"]
```

---

## 16.11 Notification và Automation

Automation được thực hiện bởi Scheduled Job.

Scheduled Job định kỳ kiểm tra:

- Deadline Milestone.
- Milestone quá hạn.
- Trạng thái Project.
- Các sự kiện cần tạo Notification.

```mermaid
flowchart TD

    Scheduler["Scheduled Job"]

    Scheduler --> Deadline["Check Deadline"]
    Scheduler --> Status["Check Status"]
    Scheduler --> Project["Check Project"]

    Deadline --> Reminder["Create Reminder"]
    Deadline --> Warning["Create Warning"]
    Deadline --> Overdue["Mark OVERDUE"]

    Status --> MilestoneStatus["Update Milestone Status"]
    Project --> ProjectStatus["Check Project Completion"]

    Reminder --> Notification["Notification"]
    Warning --> Notification
    Overdue --> Notification

    Notification --> Student["Student"]
    Notification --> Lecturer["Lecturer"]
```

Các sự kiện khác như Lecturer Accept/Reject Request hoặc Lecturer yêu cầu chỉnh sửa Milestone có thể tạo Notification theo cơ chế event-driven thay vì chờ Scheduled Job.

---

## 16.12 Mapping Actor và Use Case

| Actor | Nhóm chức năng | Use Case |
|---|---|---|
| Student | Authentication | UC-ST-01 |
| Student | Topic | UC-ST-02 → UC-ST-05 |
| Student | Lecturer Registration | UC-ST-06 → UC-ST-07 |
| Student | Project | UC-ST-08 |
| Student | Progress | UC-ST-09 → UC-ST-11 |
| Student | GitHub | UC-ST-12 |
| Student | AI | UC-ST-13 |
| Student | Notification | UC-ST-14 |
| Student | Dashboard | UC-ST-15 |
| Lecturer | Authentication | UC-LC-01 |
| Lecturer | Topic | UC-LC-02 → UC-LC-06 |
| Lecturer | Registration | UC-LC-07 → UC-LC-09 |
| Lecturer | Capacity | UC-LC-10 → UC-LC-11 |
| Lecturer | Project | UC-LC-12 |
| Lecturer | Milestone | UC-LC-13 → UC-LC-17 |
| Lecturer | Evaluation | UC-LC-18 |
| Lecturer | GitHub | UC-LC-19 |
| Lecturer | AI | UC-LC-20 |
| Lecturer | Notification | UC-LC-21 |
| Lecturer | Dashboard | UC-LC-22 |
| Lecturer | Reporting | UC-LC-23 |
| Admin | Authentication | UC-AD-01 |
| Admin | User Management | UC-AD-02 → UC-AD-03 |
| Admin | Topic | UC-AD-04 → UC-AD-05 |
| Admin | Project | UC-AD-06 |
| Admin | Milestone | UC-AD-07 |
| Admin | Progress | UC-AD-08 |
| Admin | Evaluation | UC-AD-09 |
| Admin | Notification | UC-AD-10 |
| Admin | Dashboard | UC-AD-11 |
| Admin | Statistics | UC-AD-12 |
| Admin | Reporting | UC-AD-13 |
| Admin | Automation | UC-AD-14 → UC-AD-15 |

---

## 16.13 Các thành phần hỗ trợ

### Scheduled Job

Scheduled Job không phải là người dùng trực tiếp mà là thành phần thực thi tự động.

Nhiệm vụ:

- Kiểm tra deadline.
- Cập nhật Milestone thành OVERDUE khi phù hợp.
- Tạo Notification.
- Kiểm tra trạng thái Project.
- Ghi log lỗi Automation.

### GitHub

GitHub là hệ thống bên ngoài cung cấp Repository cho Project.

Hệ thống có thể sử dụng GitHub để:

- Lưu source code.
- Liên kết Repository với Project.
- Lấy thông tin Repository.
- Hỗ trợ AI Analysis.

### AI Analysis Service

AI Analysis Service nhận source code và tạo báo cáo phân tích.

Kết quả AI có thể hỗ trợ Lecturer xem:

- Cấu trúc source code.
- Một số vấn đề trong code.
- Chất lượng code.
- Một số chỉ số hoặc nhận xét kỹ thuật.

AI không tự động thay thế quyết định đánh giá cuối cùng của Lecturer.

---

## 16.14 Quan hệ giữa các nhóm Use Case

Các nhóm Use Case chính có quan hệ theo chuỗi:

```text
Authentication
      ↓
Topic Management
      ↓
Registration Management
      ↓
Lecturer Capacity
      ↓
Project Management
      ↓
Milestone Management
      ↓
Progress Management
      ↓
Evaluation
      ↓
Project Completion
```

Các chức năng hỗ trợ:

```text
Project
 ├── GitHub Integration
 │       ↓
 │   AI Analysis
 │
 ├── Notification
 │
 ├── Dashboard
 │
 └── Reporting
```

Automation hoạt động xuyên suốt vòng đời:

```text
Scheduled Job
      ↓
Deadline / Status Checking
      ↓
Milestone / Project Status
      ↓
Notification
```

---

## 16.15 Nguyên tắc xây dựng Use Case Diagram

1. Mỗi Actor chỉ được kết nối với những Use Case mà Actor đó có quyền thực hiện.

2. Không cho phép Student thực hiện các chức năng quản trị hệ thống.

3. Lecturer có quyền quản lý đề tài của mình và đánh giá Project được phân công.

4. Admin có quyền quản lý và giám sát dữ liệu hệ thống.

5. Capacity của Lecturer phải được kiểm tra ở phía Server.

6. Việc Accept Lecturer Request cần đảm bảo tính nhất quán dữ liệu khi có nhiều request đồng thời.

7. AI Analysis chỉ đóng vai trò hỗ trợ phân tích.

8. Lecturer vẫn là người đánh giá cuối cùng.

9. Scheduled Job thực hiện các tác vụ định kỳ và không thay thế thao tác nghiệp vụ của người dùng.

10. Deadline là thuộc tính thời gian của Milestone, không phải một trạng thái riêng.

---

## 16.16 Mapping với Functional Requirements

Use Case Diagram được xây dựng dựa trên các Functional Requirements đã xác định:

| Functional Requirement | Use Case liên quan |
|---|---|
| FR-01 Authentication | UC-ST-01, UC-LC-01, UC-AD-01 |
| FR-02 Authorization | Tất cả Use Case |
| FR-03 Student Management | UC-AD-02 |
| FR-04 Lecturer Management | UC-AD-03, UC-LC-10 |
| FR-05 Lecturer Capacity | UC-LC-10, UC-LC-11 |
| FR-06 Topic Management | UC-ST-02, UC-ST-04, UC-LC-02 → UC-LC-06 |
| FR-07 Topic Registration | UC-ST-03 |
| FR-08 Lecturer Request | UC-ST-06 |
| FR-09 Accept / Reject Request | UC-LC-08, UC-LC-09 |
| FR-10 Capacity Enforcement | UC-LC-08 |
| FR-11 Project Management | UC-ST-08, UC-LC-12 |
| FR-12 Milestone Management | UC-LC-13 |
| FR-13 Progress | UC-ST-09 |
| FR-14 Evaluation | UC-LC-15 → UC-LC-18 |
| FR-15 GitHub | UC-ST-12, UC-LC-19 |
| FR-16 AI Analysis | UC-ST-13, UC-LC-20 |
| FR-17 Notification | UC-ST-14, UC-LC-21, UC-AD-10 |
| FR-18 Scheduled Deadline Checking | UC-AD-14 |
| FR-19 Automatic Status Update | UC-AD-14 |
| FR-20 Progress Dashboard | UC-ST-15, UC-LC-22, UC-AD-11 |
| FR-21 Statistics / Reporting | UC-LC-23, UC-AD-12, UC-AD-13 |

---

## 16.17 Kết luận

Use Case Diagram mô tả toàn bộ phạm vi chức năng của hệ thống quản lý đề tài và theo dõi tiến độ dự án sinh viên.

Ba Actor chính gồm:

- Student: thực hiện và theo dõi Project.
- Lecturer: quản lý đề tài, hướng dẫn và đánh giá Project.
- Admin: quản lý và giám sát hệ thống.

Các thành phần GitHub, AI Analysis Service và Scheduled Job đóng vai trò hỗ trợ cho quá trình quản lý Project và tự động hóa.

Use Case Diagram là cơ sở để chuyển sang các bước thiết kế tiếp theo, bao gồm:

- Thiết kế kiến trúc hệ thống.
- Thiết kế Database và ERD.
- Thiết kế API.
- Thiết kế Backend.
- Thiết kế Frontend.
- Thiết kế Automation.
- Thiết kế GitHub Integration và AI Analysis.
