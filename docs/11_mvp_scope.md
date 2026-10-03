# 11. MVP Scope

## 11.1. Mục tiêu

MVP (Minimum Viable Product) xác định các chức năng tối thiểu cần hoàn thành để hệ thống có thể hỗ trợ đầy đủ quy trình quản lý đề tài và theo dõi tiến độ Project sinh viên.

MVP tập trung vào các chức năng cốt lõi trước khi triển khai các chức năng mở rộng như GitHub Integration và AI Analysis.

---

## 11.2. Phạm vi MVP

MVP bao gồm các chức năng chính:

- Authentication và Authorization.
- Quản lý Student.
- Quản lý Lecturer.
- Quản lý Topic.
- Đăng ký Lecturer.
- Quản lý Lecturer Capacity.
- Quản lý Project.
- Quản lý Milestone.
- Cập nhật Progress.
- Đánh giá Milestone.
- Notification.
- Scheduled Job và Automation.

Luồng chính:

```text
Authentication
      |
      v
Topic Management
      |
      v
Lecturer Registration
      |
      v
Lecturer Capacity
      |
      v
Project Management
      |
      v
Milestone Management
      |
      v
Progress Update
      |
      v
Evaluation
      |
      v
Notification
      |
      v
Automation
```

---

## 11.3. Authentication và Authorization

MVP cần hỗ trợ:

- Đăng nhập.
- Xác thực tài khoản.
- Phân quyền theo Role.
- Student chỉ được truy cập các chức năng thuộc Student.
- Lecturer chỉ được truy cập các chức năng thuộc Lecturer.
- Admin có quyền quản lý hệ thống theo Role.

Các Role chính:

```text
Student
Lecturer
Admin
```

Authorization phải được kiểm tra ở phía Server và không chỉ dựa vào giao diện Frontend.

---

## 11.4. Topic Management

MVP hỗ trợ hai cách hình thành Topic.

### Lecturer hoặc Admin tạo Topic

```text
Lecturer / Admin
       |
       v
Create Topic
       |
       v
Open Registration
       |
       v
Student đăng ký
```

### Student tự đề xuất Topic

```text
Student
   |
   v
Create Topic Proposal
   |
   v
PENDING_APPROVAL
   |
   +----------------+
   |                |
   v                v
APPROVED         REJECTED
```

Nếu Topic Proposal bị từ chối, Student có thể chỉnh sửa và gửi lại theo quy trình của hệ thống.

Topic Proposal và Project là hai đối tượng khác nhau:

- Topic Proposal dùng để xử lý quá trình đề xuất Topic.
- Project được tạo hoặc kích hoạt sau khi quá trình đăng ký và xác nhận hướng dẫn hoàn tất.

---

## 11.5. Lecturer Registration

Student có thể gửi Request đăng ký Lecturer.

Quy trình:

```text
Student
   |
   v
Chọn Lecturer
   |
   v
Gửi Request
   |
   v
PENDING
   |
   +----------------+
   |                |
   v                v
ACCEPTED         REJECTED
```

Khi Lecturer từ chối, hệ thống lưu lý do từ chối để Student có thể xem.

Chỉ Request ở trạng thái hợp lệ mới được phép chuyển sang `ACCEPTED` hoặc `REJECTED`.

---

## 11.6. Lecturer Capacity

MVP phải quản lý số lượng Student mà mỗi Lecturer có thể hướng dẫn.

Thông tin cần quản lý:

- Maximum Students.
- Current Students.
- Remaining Capacity.
- Capacity Status.

Ví dụ:

```text
Maximum Students: 5
Current Students: 4
Remaining: 1
Status: AVAILABLE
```

Khi số lượng Student đạt giới hạn:

```text
Current Students = Maximum Students
           |
           v
          FULL
           |
           v
Không nhận thêm Request
```

Việc kiểm tra Capacity phải được thực hiện ở phía Server.

Đặc biệt, khi nhiều Student gửi Request đồng thời, hệ thống phải đảm bảo không xảy ra tình trạng Lecturer nhận vượt quá Capacity.

Việc Accept Request và cập nhật `Current Students` cần được xử lý theo cơ chế Transaction hoặc cơ chế tương đương để đảm bảo tính nhất quán dữ liệu.

---

## 11.7. Project Management

Sau khi Lecturer chấp nhận Request, hệ thống tạo hoặc kích hoạt Project.

Project sử dụng các trạng thái:

```text
DRAFT
  |
  v
REGISTERED
  |
  v
IN_PROGRESS
  |
  v
COMPLETED
```

Project cũng có thể chuyển sang:

```text
IN_PROGRESS → CANCELLED
```

Project không sử dụng `REJECTED` làm trạng thái.

Lecturer và Student có thể xem thông tin Project theo quyền được cấp.

Project chỉ được chuyển sang `COMPLETED` khi:

- Các Milestone bắt buộc đã `APPROVED`.
- Các yêu cầu cần thiết của Project đã hoàn thành.
- Lecturer xác nhận Project hoàn tất.

---

## 11.8. Milestone Management

Lecturer có thể tạo các Milestone cho Project.

Mỗi Milestone có các thông tin chính:

- Tên Milestone.
- Mô tả.
- Start Date.
- Deadline.
- Status.
- Progress.
- Result.
- Evaluation.

Các trạng thái:

```text
PENDING
   |
   v
IN_PROGRESS
   |
   v
SUBMITTED
   |
   +------------------+
   |                  |
   v                  v
APPROVED       REVISION_REQUIRED
                      |
                      v
                 IN_PROGRESS
```

Nếu Milestone quá Deadline và chưa Submit:

```text
IN_PROGRESS → OVERDUE
```

`OVERDUE` là trạng thái của Milestone, không phải trạng thái của Project.

---

## 11.9. Progress Management

Student có thể cập nhật tiến độ của Milestone.

Thông tin Progress có thể bao gồm:

- Phần trăm hoàn thành.
- Nội dung công việc đã thực hiện.
- Kết quả đạt được.
- Khó khăn hoặc vấn đề gặp phải.
- Link Source Code nếu có.

Quy trình:

```text
Student
   |
   v
Cập nhật Progress
   |
   v
Milestone
   |
   v
Lecturer theo dõi
```

Progress phải được lưu lại để Student và Lecturer có thể theo dõi quá trình thực hiện Project.

---

## 11.10. Evaluation

Lecturer có thể đánh giá kết quả Milestone.

Các kết quả chính:

```text
SUBMITTED
   |
   +-------------------+
   |                   |
   v                   v
APPROVED        REVISION_REQUIRED
```

Lecturer có thể cung cấp:

- Nhận xét.
- Kết quả đánh giá.
- Yêu cầu chỉnh sửa.

Nếu `REVISION_REQUIRED`, Student thực hiện chỉnh sửa và Submit lại.

AI không thay thế Lecturer trong việc đánh giá cuối cùng.

---

## 11.11. Notification

MVP hỗ trợ Notification cho các sự kiện quan trọng.

Các trường hợp:

- Student gửi Lecturer Request.
- Lecturer chấp nhận Request.
- Lecturer từ chối Request.
- Topic Proposal được duyệt.
- Topic Proposal bị từ chối.
- Milestone được tạo.
- Milestone sắp đến Deadline.
- Milestone quá Deadline.
- Lecturer yêu cầu chỉnh sửa.
- Project đủ điều kiện hoàn thành.

Notification có hai trạng thái:

```text
UNREAD → READ
```

Hệ thống cần hạn chế tạo Notification trùng lặp cho cùng một sự kiện.

---

## 11.12. Automation

MVP sử dụng Scheduled Job để thực hiện các tác vụ định kỳ.

Các tác vụ chính:

- Kiểm tra Deadline.
- Chuyển Milestone sang `OVERDUE` khi đủ điều kiện.
- Tạo Reminder trước Deadline.
- Tạo Warning trước Deadline.
- Tạo Notification khi Milestone quá hạn.
- Kiểm tra điều kiện hoàn thành Project.

Luồng:

```text
Scheduled Job
      |
      v
Deadline Check
      |
      v
Milestone Status
      |
      +----------------+
      |                |
      v                v
Notification      Project Check
```

Quy tắc Deadline:

```text
7 ngày trước Deadline
        ↓
Reminder

3 ngày trước Deadline
        ↓
Warning

Đã quá Deadline + chưa Submit
        ↓
IN_PROGRESS → OVERDUE
```

Scheduled Job không được tự động chuyển `SUBMITTED` hoặc `APPROVED` sang `OVERDUE`.

---

## 11.13. Chức năng ngoài MVP

Các chức năng sau được xem là phần mở rộng sau khi các chức năng cốt lõi ổn định:

- GitHub Integration.
- AI Source Code Analysis.
- Phân tích chất lượng Source Code nâng cao.
- Phân tích Security tự động.
- Dashboard nâng cao.
- Dự đoán tiến độ Project.
- Email Notification.
- Mobile Application.
- Tích hợp với các nền tảng quản lý Source Code khác.

Các chức năng mở rộng không được làm ảnh hưởng đến luồng nghiệp vụ cốt lõi của MVP.

---

## 11.14. GitHub Integration

GitHub Integration được triển khai như một chức năng mở rộng.

Quy trình dự kiến:

```text
Student
   |
   v
Link GitHub Repository
   |
   v
Project
   |
   v
Source Code
```

Hệ thống có thể sử dụng Repository để hỗ trợ theo dõi Source Code của Project.

GitHub Integration không phải điều kiện bắt buộc để hoàn thành các chức năng quản lý Project cốt lõi của MVP.

---

## 11.15. AI Source Code Analysis

AI Analysis được triển khai sau khi các chức năng quản lý Project và Milestone ổn định.

Quy trình:

```text
GitHub Repository
       |
       v
Source Code
       |
       v
AI Analysis
       |
       v
Analysis Report
       |
       +----------+
       |          |
       v          v
    Student    Lecturer
```

AI có thể hỗ trợ:

- Phân tích Code Quality.
- Phân tích Complexity.
- Phát hiện Potential Bugs.
- Phân tích Maintainability.
- Phát hiện một số vấn đề Security.

AI chỉ cung cấp kết quả phân tích hỗ trợ.

AI không tự động quyết định kết quả đánh giá cuối cùng và không thay thế Lecturer.

---

## 11.16. Tiêu chí hoàn thành MVP

MVP được xem là hoàn thành khi hệ thống có thể thực hiện đầy đủ quy trình cơ bản:

```text
Student / Lecturer
       |
       v
Authentication
       |
       v
Topic
       |
       v
Lecturer Registration
       |
       v
Capacity Check
       |
       v
Project
       |
       v
Milestone
       |
       v
Progress
       |
       v
Submit
       |
       v
Lecturer Evaluation
       |
       v
Project Completion
```

Đồng thời hệ thống phải hỗ trợ:

- Phân quyền Student, Lecturer và Admin.
- Kiểm soát Lecturer Capacity.
- Theo dõi trạng thái Project.
- Theo dõi trạng thái Milestone.
- Kiểm tra Deadline tự động.
- Gửi Notification cho các sự kiện quan trọng.
- Đảm bảo các quy tắc chuyển trạng thái được kiểm tra ở phía Server.
- Đảm bảo dữ liệu Capacity nhất quán khi có nhiều Request đồng thời.
- Không để Automation thay thế các quyết định nghiệp vụ của Lecturer.

---

## 11.17. Nguyên tắc triển khai MVP

- Ưu tiên các chức năng phục vụ trực tiếp quy trình quản lý Project.
- Hoàn thành Authentication và Authorization trước các chức năng nghiệp vụ.
- Hoàn thành Topic, Registration và Capacity trước khi triển khai Project.
- Hoàn thành Project và Milestone trước khi triển khai Automation.
- Automation không được làm ảnh hưởng đến các chức năng chính.
- Các chức năng cốt lõi phải hoạt động ổn định trước khi triển khai GitHub Integration và AI Analysis.
- GitHub Integration và AI Analysis được triển khai sau các chức năng cốt lõi.
- AI chỉ đóng vai trò hỗ trợ phân tích.
- Lecturer vẫn là người chịu trách nhiệm đánh giá và xác nhận kết quả cuối cùng.
- MVP cần được kiểm thử theo toàn bộ luồng nghiệp vụ trước khi mở rộng hệ thống.

---

## 11.18. Kết luận

MVP tập trung vào luồng nghiệp vụ cốt lõi:

```text
Authentication
      ↓
Topic
      ↓
Lecturer Registration
      ↓
Capacity
      ↓
Project
      ↓
Milestone
      ↓
Progress
      ↓
Evaluation
      ↓
Notification
      ↓
Automation
```

Sau khi luồng cốt lõi hoạt động ổn định, hệ thống có thể mở rộng với:

```text
GitHub Integration
        ↓
AI Source Code Analysis
        ↓
Advanced Dashboard
        ↓
Future Extensions
```

Mục tiêu của MVP là đảm bảo hệ thống có thể quản lý đầy đủ vòng đời Project sinh viên từ đăng ký Topic, lựa chọn Lecturer, quản lý Capacity, thực hiện Milestone, cập nhật Progress, đánh giá cho đến hoàn thành Project.
