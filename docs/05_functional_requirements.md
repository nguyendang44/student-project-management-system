# 5. Functional Requirements

## 5.1. Danh sách yêu cầu chức năng

| ID | Requirement | Description |
|---|---|---|
| FR-01 | Authentication | Người dùng đăng nhập vào hệ thống. |
| FR-02 | Authorization | Hệ thống phân quyền theo ba vai trò: Student, Lecturer và Admin. |
| FR-03 | Student Management | Quản lý thông tin sinh viên. |
| FR-04 | Lecturer Management | Quản lý thông tin giảng viên và số lượng sinh viên tối đa mà giảng viên có thể hướng dẫn. |
| FR-05 | Lecturer Capacity | Theo dõi số lượng sinh viên hiện tại, số lượng tối đa và số chỗ còn lại của giảng viên. |
| FR-06 | Topic Management | Quản lý đề tài và trạng thái của đề tài. |
| FR-07 | Topic Registration | Cho phép sinh viên đăng ký đề tài có sẵn. |
| FR-08 | Lecturer Request | Cho phép sinh viên gửi yêu cầu đăng ký giảng viên hướng dẫn. |
| FR-09 | Request Approval | Cho phép giảng viên chấp nhận hoặc từ chối yêu cầu và nhập lý do khi từ chối. |
| FR-10 | Capacity Validation | Hệ thống kiểm tra giới hạn số lượng sinh viên của giảng viên ở phía server trước khi chấp nhận đăng ký. |
| FR-11 | Project Management | Quản lý dự án sau khi đề tài và giảng viên được xác nhận. |
| FR-12 | Milestone Management | Cho phép tạo, cập nhật và quản lý các milestone của dự án. |
| FR-13 | Progress Update | Cho phép sinh viên cập nhật tiến độ thực hiện dự án. |
| FR-14 | Evaluation | Cho phép giảng viên đánh giá kết quả milestone và yêu cầu chỉnh sửa khi cần thiết. |
| FR-15 | GitHub Integration | Cho phép liên kết repository GitHub với dự án. |
| FR-16 | AI Code Analysis | Phân tích source code và tạo báo cáo hỗ trợ đánh giá. |
| FR-17 | Notification | Gửi thông báo cho người dùng khi xảy ra các sự kiện quan trọng. |
| FR-18 | Deadline Checking | Tự động kiểm tra deadline của milestone theo lịch định kỳ. |
| FR-19 | Automatic Status Update | Tự động cập nhật trạng thái dựa trên tiến độ và deadline. |
| FR-20 | Dashboard | Hiển thị thông tin tổng quan về dự án, milestone và tiến độ. |
| FR-21 | Reporting | Cung cấp thống kê và báo cáo về hoạt động của hệ thống. |

## 5.2. Phân nhóm Functional Requirements

Các yêu cầu chức năng được chia thành các nhóm chính:

### 5.2.1. Authentication và Authorization

- FR-01: Authentication
- FR-02: Authorization

Nhóm này đảm bảo người dùng có thể đăng nhập và chỉ được phép thực hiện các chức năng phù hợp với vai trò.

### 5.2.2. User Management

- FR-03: Student Management
- FR-04: Lecturer Management
- FR-05: Lecturer Capacity

Nhóm này quản lý thông tin người dùng và khả năng tiếp nhận sinh viên của giảng viên.

### 5.2.3. Topic và Registration

- FR-06: Topic Management
- FR-07: Topic Registration
- FR-08: Lecturer Request
- FR-09: Request Approval
- FR-10: Capacity Validation

Nhóm này quản lý quá trình sinh viên lựa chọn hoặc đề xuất đề tài và đăng ký giảng viên hướng dẫn.

### 5.2.4. Project và Progress

- FR-11: Project Management
- FR-12: Milestone Management
- FR-13: Progress Update
- FR-14: Evaluation

Nhóm này quản lý quá trình thực hiện dự án từ khi được xác nhận đến khi hoàn thành.

### 5.2.5. GitHub và AI

- FR-15: GitHub Integration
- FR-16: AI Code Analysis

Nhóm này cung cấp các chức năng mở rộng để theo dõi source code và hỗ trợ phân tích dự án.

### 5.2.6. Notification và Automation

- FR-17: Notification
- FR-18: Deadline Checking
- FR-19: Automatic Status Update

Nhóm này hỗ trợ tự động hóa việc kiểm tra deadline, cập nhật trạng thái và gửi thông báo.

### 5.2.7. Dashboard và Reporting

- FR-20: Dashboard
- FR-21: Reporting

Nhóm này cung cấp thông tin tổng quan, thống kê và báo cáo cho người dùng.

## 5.3. Nguyên tắc xử lý

### 5.3.1. Phân quyền

Mọi chức năng yêu cầu quyền truy cập phải được kiểm tra ở phía server.

Không chỉ dựa vào việc ẩn chức năng trên giao diện để đảm bảo bảo mật.

### 5.3.2. Lecturer Capacity

Khi sinh viên gửi yêu cầu đăng ký giảng viên, hệ thống phải kiểm tra:

```text
current_students < max_students
```

Nếu giảng viên đã đủ số lượng:

```text
current_students >= max_students
```

hệ thống không được phép chấp nhận thêm sinh viên.

Việc kiểm tra phải được thực hiện ở phía server và đảm bảo tính nhất quán dữ liệu khi có nhiều yêu cầu đồng thời.

### 5.3.3. Evaluation

AI chỉ cung cấp thông tin hỗ trợ phân tích source code.

Kết quả đánh giá cuối cùng của milestone và project thuộc về Lecturer.

### 5.3.4. Automation

Scheduled Job có trách nhiệm:

- Kiểm tra deadline.
- Phát hiện milestone quá hạn.
- Cập nhật trạng thái phù hợp.
- Tạo notification.
- Kiểm tra điều kiện hoàn thành project.

Automation không thay thế các quyết định nghiệp vụ cần sự xác nhận của Lecturer.

## 5.4. Luồng chức năng tổng quát

```text
Authentication
       ↓
Topic Management
       ↓
Topic Registration
       ↓
Lecturer Request
       ↓
Capacity Validation
       ↓
Lecturer Approval
       ↓
Project Management
       ↓
Milestone Management
       ↓
Progress Update
       ↓
Milestone Submission
       ↓
Lecturer Evaluation
       ↓
Project Completion
```

Các chức năng hỗ trợ hoạt động song song:

```text
Project
   ├── GitHub Integration
   │       ↓
   │   AI Code Analysis
   │
   ├── Notification
   │
   ├── Deadline Checking
   │
   └── Dashboard / Reporting
```
