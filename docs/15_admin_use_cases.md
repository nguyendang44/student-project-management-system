# 15. Admin Use Cases

## 15.1 Mục tiêu

Tài liệu này mô tả các Use Case mà Actor `Admin` có thể thực hiện trong hệ thống quản lý đề tài và theo dõi tiến độ dự án sinh viên.

Admin chịu trách nhiệm quản trị dữ liệu và hoạt động chung của hệ thống.

Các chức năng chính:

- Quản lý tài khoản Student.
- Quản lý tài khoản Lecturer.
- Quản lý dữ liệu Topic.
- Theo dõi Project.
- Theo dõi hoạt động hệ thống.
- Xem thống kê và báo cáo.
- Quản lý các dữ liệu cần thiết cho hệ thống.

---

# 15.2 Actor: Admin

Admin có quyền quản trị ở mức hệ thống.

Admin có thể:

- Đăng nhập.
- Quản lý Student.
- Quản lý Lecturer.
- Xem Topic.
- Quản lý Topic khi cần thiết.
- Xem Project.
- Xem Milestone.
- Xem Progress.
- Xem Evaluation.
- Xem Notification.
- Xem Dashboard.
- Xem Statistics và Report.

Admin không trực tiếp thay thế Lecturer trong việc đánh giá chuyên môn của Student.

---

# 15.3 UC-AD-01: Đăng nhập

## Mục tiêu

Cho phép Admin đăng nhập vào hệ thống bằng tài khoản quản trị.

## Actor

Admin.

## Preconditions

- Admin đã có tài khoản.
- Tài khoản đang hoạt động.

## Main Flow

1. Admin mở trang đăng nhập.
2. Admin nhập username/email.
3. Admin nhập password.
4. Admin gửi thông tin đăng nhập.
5. System kiểm tra tài khoản.
6. System xác thực password.
7. System xác định Role là `ADMIN`.
8. System tạo phiên đăng nhập/token.
9. System chuyển Admin đến giao diện quản trị.

## Alternative Flow

### A1. Sai thông tin đăng nhập

System thông báo thông tin đăng nhập không hợp lệ.

### A2. Tài khoản không hoạt động

System từ chối đăng nhập.

## Postconditions

Admin đăng nhập thành công.

---

# 15.4 UC-AD-02: Quản lý Student

## Mục tiêu

Cho phép Admin quản lý tài khoản và thông tin cơ bản của Student.

## Actor

Admin.

## Main Flow

1. Admin mở Student Management.
2. System hiển thị danh sách Student.
3. Admin có thể:
   - Xem Student.
   - Tìm kiếm Student.
   - Tạo Student.
   - Chỉnh sửa thông tin Student.
   - Kích hoạt tài khoản.
   - Vô hiệu hóa tài khoản.
4. System kiểm tra quyền.
5. System lưu thay đổi.

## Thông tin có thể quản lý

- Student ID.
- Họ tên.
- Email.
- Username.
- Trạng thái tài khoản.
- Thông tin học tập cần thiết.

## Alternative Flow

### A1. Student không tồn tại

System thông báo không tìm thấy Student.

### A2. Email/username đã tồn tại

System từ chối tạo tài khoản trùng.

## Postconditions

Thông tin Student được cập nhật.

---

# 15.5 UC-AD-03: Quản lý Lecturer

## Mục tiêu

Cho phép Admin quản lý tài khoản Lecturer.

## Actor

Admin.

## Main Flow

1. Admin mở Lecturer Management.
2. System hiển thị danh sách Lecturer.
3. Admin có thể:
   - Xem Lecturer.
   - Tìm kiếm Lecturer.
   - Tạo Lecturer.
   - Chỉnh sửa thông tin.
   - Kích hoạt tài khoản.
   - Vô hiệu hóa tài khoản.
4. System lưu thay đổi.

## Thông tin có thể quản lý

- Lecturer ID.
- Họ tên.
- Email.
- Username.
- Bộ môn/khoa nếu có.
- Trạng thái tài khoản.

## Alternative Flow

### A1. Dữ liệu trùng

System không cho phép tạo tài khoản trùng.

### A2. Dữ liệu không hợp lệ

System yêu cầu Admin chỉnh sửa dữ liệu.

## Postconditions

Thông tin Lecturer được cập nhật.

---

# 15.6 UC-AD-04: Xem danh sách Topic

## Mục tiêu

Cho phép Admin theo dõi toàn bộ Topic trong hệ thống.

## Actor

Admin.

## Main Flow

1. Admin mở Topic Management.
2. System lấy danh sách Topic.
3. System hiển thị:
   - Tên Topic.
   - Lecturer.
   - Student nếu đã được đăng ký.
   - Trạng thái.
   - Thời gian.
4. Admin có thể tìm kiếm hoặc lọc Topic.

## Postconditions

Admin xem được toàn bộ Topic trong hệ thống.

---

# 15.7 UC-AD-05: Quản lý Topic

## Mục tiêu

Cho phép Admin xử lý các Topic trong trường hợp cần quản trị dữ liệu hoặc hỗ trợ vận hành hệ thống.

## Actor

Admin.

## Main Flow

1. Admin chọn Topic.
2. System hiển thị thông tin.
3. Admin có thể thực hiện các thao tác được cấp quyền:
   - Chỉnh sửa thông tin.
   - Ẩn Topic.
   - Khóa Topic.
   - Điều chỉnh dữ liệu quản trị.
4. System kiểm tra quyền.
5. System ghi nhận thay đổi.

## Nguyên tắc

Admin có quyền quản trị dữ liệu nhưng không tự động thay thế quyết định chuyên môn của Lecturer.

Ví dụ:

- Admin có thể xử lý dữ liệu Topic bị lỗi.
- Admin có thể khóa một Topic khi cần.
- Admin không tự động đánh giá kết quả Project thay Lecturer.

---

# 15.8 UC-AD-06: Xem Project

## Mục tiêu

Cho phép Admin theo dõi các Project đang tồn tại trong hệ thống.

## Actor

Admin.

## Main Flow

1. Admin mở Project Management.
2. System lấy danh sách Project.
3. System hiển thị:
   - Project.
   - Topic.
   - Student.
   - Lecturer.
   - Project status.
   - Thời gian.
4. Admin có thể lọc theo:
   - Student.
   - Lecturer.
   - Status.
   - Thời gian.

## Postconditions

Admin xem được tình trạng Project trong hệ thống.

---

# 15.9 UC-AD-07: Xem Milestone

## Mục tiêu

Cho phép Admin theo dõi Milestone của các Project.

## Actor

Admin.

## Main Flow

1. Admin chọn Project.
2. System hiển thị danh sách Milestone.
3. Admin xem:
   - Tên Milestone.
   - Deadline.
   - Status.
   - Progress.
   - Kết quả.
   - Lecturer phụ trách.

Admin chủ yếu sử dụng chức năng này để theo dõi và quản trị hệ thống.

---

# 15.10 UC-AD-08: Xem Progress

## Mục tiêu

Cho phép Admin theo dõi tình trạng tiến độ của Project.

## Actor

Admin.

## Main Flow

1. Admin mở Progress Dashboard.
2. System tổng hợp dữ liệu.
3. System hiển thị:
   - Project đang thực hiện.
   - Project hoàn thành.
   - Milestone đang thực hiện.
   - Milestone quá hạn.
   - Milestone chờ đánh giá.
4. Admin có thể lọc dữ liệu.

## Postconditions

Admin có thông tin tổng quan về tiến độ toàn hệ thống.

---

# 15.11 UC-AD-09: Xem Evaluation

## Mục tiêu

Cho phép Admin xem kết quả đánh giá được lưu trong hệ thống.

## Actor

Admin.

## Preconditions

- Project hoặc Milestone đã có Evaluation.

## Main Flow

1. Admin mở Project.
2. Admin chọn Evaluation.
3. System hiển thị:
   - Người đánh giá.
   - Thời gian đánh giá.
   - Kết quả.
   - Feedback.
4. Admin xem thông tin.

## Nguyên tắc

Admin chỉ xem và quản trị dữ liệu theo quyền.

Admin không tự động thay đổi kết quả đánh giá chuyên môn của Lecturer.

---

# 15.12 UC-AD-10: Xem Notification

## Mục tiêu

Cho phép Admin theo dõi các Notification quan trọng của hệ thống.

## Actor

Admin.

## Main Flow

1. Admin mở Notification.
2. System hiển thị notification.
3. Admin xem nội dung.
4. Admin có thể lọc theo loại notification nếu hệ thống hỗ trợ.

Ví dụ:

- Lecturer Request.
- Topic Proposal.
- Deadline.
- Overdue.
- System Event.
- Automation Error.

---

# 15.13 UC-AD-11: Xem Dashboard

## Mục tiêu

Cho phép Admin xem tổng quan tình trạng hệ thống.

## Actor

Admin.

## Main Flow

1. Admin mở Dashboard.
2. System tổng hợp dữ liệu.
3. System hiển thị các chỉ số chính.

Ví dụ:

```text
Total Students
Total Lecturers
Total Topics
Total Projects
Projects In Progress
Projects Completed
Projects Cancelled
Total Milestones
Milestones Approved
Milestones Overdue
Pending Lecturer Requests
Pending Topic Proposals
```

---

# 15.14 UC-AD-12: Xem thống kê

## Mục tiêu

Cho phép Admin xem thống kê phục vụ quản trị và báo cáo.

## Actor

Admin.

## Main Flow

1. Admin mở Statistics.
2. System tổng hợp dữ liệu.
3. Admin lựa chọn khoảng thời gian hoặc tiêu chí.
4. System hiển thị kết quả.

Các thống kê có thể bao gồm:

- Số Student.
- Số Lecturer.
- Số Topic.
- Số Project.
- Project theo trạng thái.
- Milestone theo trạng thái.
- Số Project hoàn thành.
- Số Project đang thực hiện.
- Số Milestone quá hạn.
- Số Topic Proposal được duyệt/từ chối.
- Số Lecturer Request được chấp nhận/từ chối.

---

# 15.15 UC-AD-13: Xem báo cáo hệ thống

## Mục tiêu

Cho phép Admin xem các báo cáo tổng hợp phục vụ quản trị.

## Actor

Admin.

## Main Flow

1. Admin chọn loại báo cáo.
2. System lấy dữ liệu.
3. System tổng hợp kết quả.
4. System hiển thị báo cáo.
5. Nếu hệ thống hỗ trợ, Admin có thể xuất báo cáo.

Các loại báo cáo:

```text
Student Report
Lecturer Report
Topic Report
Project Report
Milestone Report
Progress Report
Evaluation Report
```

---

# 15.16 UC-AD-14: Theo dõi Automation

## Mục tiêu

Cho phép Admin theo dõi hoạt động của các tác vụ tự động.

## Actor

Admin.

## Main Flow

1. Admin mở Automation Monitoring.
2. System hiển thị các Scheduled Job.
3. Admin xem:
   - Thời gian chạy.
   - Trạng thái.
   - Số lượng bản ghi xử lý.
   - Lỗi nếu có.
4. Admin có thể xem log khi cần.

Ví dụ:

```text
Deadline Check
Status: SUCCESS

Notification Job
Status: SUCCESS

Status Update Job
Status: SUCCESS
```

---

# 15.17 UC-AD-15: Theo dõi lỗi hệ thống

## Mục tiêu

Cho phép Admin phát hiện các lỗi liên quan đến hoạt động hệ thống.

## Actor

Admin.

## Main Flow

1. Admin mở System Monitoring.
2. System hiển thị các lỗi quan trọng.
3. Admin xem:
   - Thời gian.
   - Module.
   - Error type.
   - Message.
   - Status xử lý.
4. Admin sử dụng thông tin để phối hợp xử lý.

## Nguyên tắc

Không hiển thị thông tin nhạy cảm như:

- Password.
- Access token.
- Secret key.
- Dữ liệu xác thực nhạy cảm.

---

# 15.18 Quyền của Admin

| Chức năng | Admin |
|---|---|
| Đăng nhập | Có |
| Quản lý Student | Có |
| Quản lý Lecturer | Có |
| Xem Topic | Có |
| Quản lý Topic | Có |
| Đề xuất Topic | Không |
| Duyệt Topic Proposal | Theo quyền quản trị |
| Xem Lecturer Request | Có |
| Chấp nhận Request | Không thay Lecturer nếu không có nghiệp vụ đặc biệt |
| Từ chối Request | Không thay Lecturer nếu không có nghiệp vụ đặc biệt |
| Xem Capacity | Có |
| Thiết lập Capacity | Theo quyền quản trị |
| Xem Project | Có |
| Tạo Project | Theo workflow hệ thống |
| Xem Milestone | Có |
| Chỉnh sửa Milestone chuyên môn | Không thay Lecturer |
| Xem Progress | Có |
| Xem Evaluation | Có |
| Đánh giá Project | Không thay Lecturer |
| Xem GitHub | Theo quyền |
| Xem AI Analysis | Có |
| Xem Notification | Có |
| Xem Dashboard | Có |
| Xem Statistics | Có |
| Xem Report | Có |
| Theo dõi Automation | Có |

---

# 15.19 Admin Use Case Flow tổng quát

```text
                         Admin
                           |
                           v
                       Đăng nhập
                           |
          +----------------+----------------+
          |                |                |
          v                v                v
   User Management    Topic Management   System Monitoring
          |                |                |
     +----+----+           |          +-----+------+
     |         |           |          |            |
     v         v           v          v            v
 Student   Lecturer      Topic    Automation     Error Log
 Management Management Management Monitoring
          |
          v
     Project Monitoring
          |
          v
     Progress Monitoring
          |
          v
       Statistics
          |
          v
        Reports
```

---

# 15.20 Quan hệ giữa Admin và các Actor khác

Admin quản lý dữ liệu và tài khoản ở cấp hệ thống.

```text
                    Admin
                      |
       +--------------+--------------+
       |              |              |
       v              v              v
    Student        Lecturer         Topic
       |              |              |
       +--------------+--------------+
                      |
                      v
                   Project
                      |
              +-------+-------+
              |               |
              v               v
          Milestone        Progress
              |
              v
          Evaluation
```

Admin có quyền quan sát và quản trị hệ thống nhưng không mặc định thay thế nghiệp vụ chuyên môn của Lecturer.

---

# 15.21 Phân quyền Admin

Admin có quyền cao hơn Student và Lecturer ở cấp quản trị hệ thống.

Tuy nhiên, quyền cao hơn không có nghĩa là tất cả nghiệp vụ đều được thực hiện thay Actor chuyên môn.

Phân biệt:

```text
Admin
  |
  +-- Quản trị tài khoản
  +-- Quản trị dữ liệu
  +-- Theo dõi hệ thống
  +-- Statistics
  +-- Reports
  |
  +-- Không tự động thay thế
        |
        +-- Lecturer Evaluation
        +-- Student Progress
        +-- Chuyên môn của Project
```

---

# 15.22 Nguyên tắc

1. Admin phải đăng nhập và được xác thực trước khi sử dụng chức năng quản trị.
2. Mọi API quản trị phải kiểm tra Role phía Server.
3. Admin có quyền quản lý dữ liệu trong phạm vi được cấp.
4. Admin không được xem password dạng plaintext.
5. Admin không được truy cập secret/token nếu không cần thiết.
6. Các thao tác quản trị quan trọng nên được ghi Audit Log.
7. Admin không tự động thay thế Lecturer trong đánh giá chuyên môn.
8. Admin không tự động thay thế Student trong cập nhật Progress.
9. Các thay đổi dữ liệu quan trọng phải đảm bảo tính nhất quán.
10. Các chức năng quản trị phải tuân thủ nguyên tắc bảo mật.

---

# 15.23 Audit Log

Các thao tác quan trọng của Admin nên được ghi nhận.

Ví dụ:

```text
Admin created Student
Admin disabled Student
Admin created Lecturer
Admin updated Lecturer
Admin updated Topic
Admin disabled Topic
Admin viewed system report
Admin monitored automation
```

Thông tin Audit Log có thể bao gồm:

```text
User
Action
Entity
Entity ID
Timestamp
Result
```

Không lưu password hoặc secret trong Audit Log.

---

# 15.24 Kết luận

Admin là Actor quản trị hệ thống, tập trung vào:

```text
Quản lý User
     ↓
Quản lý dữ liệu
     ↓
Theo dõi Project
     ↓
Theo dõi Progress
     ↓
Statistics
     ↓
Reports
     ↓
System Monitoring
```

Admin đảm bảo hệ thống hoạt động ổn định và dữ liệu được quản lý đúng quy trình.

Các quyết định chuyên môn về tiến độ, kết quả Milestone và Evaluation cuối cùng vẫn thuộc Lecturer theo nghiệp vụ của hệ thống.
