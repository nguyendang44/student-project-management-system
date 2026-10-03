# 8. Automation Requirements

## 8.1. Mục tiêu

Hệ thống cần tự động hóa các công việc có tính lặp lại trong quá trình quản lý Project, đặc biệt là kiểm tra Deadline, cập nhật trạng thái và gửi Notification.

Việc tự động hóa giúp:

- Giảm công việc thủ công cho Admin và Lecturer.
- Giúp Student nhận được thông tin kịp thời.
- Theo dõi Deadline và trạng thái Milestone.
- Hạn chế bỏ sót các Milestone quá hạn.
- Hỗ trợ kiểm tra điều kiện hoàn thành Project.

Các chức năng Automation chính:

- Kiểm tra Deadline của Milestone.
- Cập nhật trạng thái Milestone khi quá hạn.
- Kiểm tra điều kiện hoàn thành Project.
- Tạo Notification liên quan đến Deadline.
- Thực hiện các tác vụ định kỳ.
- Ghi log và xử lý lỗi Automation.

---

## 8.2. Scheduled Job

Hệ thống sử dụng Scheduled Job để thực hiện các tác vụ tự động theo một khoảng thời gian định trước.

Scheduled Job có thể được chạy theo chu kỳ:

- Mỗi giờ.
- Mỗi ngày.
- Hoặc theo khoảng thời gian được cấu hình bởi hệ thống.

Các tác vụ chính:

- Kiểm tra Deadline của Milestone.
- Kiểm tra Milestone quá hạn.
- Tạo Notification liên quan đến Deadline.
- Cập nhật trạng thái Milestone.
- Kiểm tra điều kiện hoàn thành Project.

Quy trình tổng quát:

```text
Scheduled Job
      |
      v
Kiểm tra dữ liệu
      |
      +------------------+
      |                  |
      v                  v
Milestone            Project
      |                  |
      v                  v
Deadline Check      Status Check
```

---

## 8.3. Kiểm tra Deadline

Hệ thống tự động kiểm tra Deadline của các Milestone chưa hoàn thành.

| Thời điểm | Hành động |
|---|---|
| Còn 7 ngày | Gửi Reminder |
| Còn 3 ngày | Gửi Warning |
| Đến Deadline | Kiểm tra trạng thái |
| Quá Deadline | Chuyển sang `OVERDUE` nếu chưa Submit |

Quy trình:

```text
Milestone
    |
    v
Kiểm tra Deadline
    |
    +---- Còn 7 ngày ----> Reminder
    |
    +---- Còn 3 ngày ----> Warning
    |
    +---- Đến Deadline --> Kiểm tra trạng thái
    |
    +---- Quá Deadline --> OVERDUE + Notification
```

Milestone đã ở trạng thái sau không được tự động chuyển sang `OVERDUE`:

- `SUBMITTED`
- `REVISION_REQUIRED`
- `APPROVED`

Lý do:

- `SUBMITTED`: Student đã Submit và đang chờ Lecturer đánh giá.
- `REVISION_REQUIRED`: Student cần chỉnh sửa theo yêu cầu của Lecturer.
- `APPROVED`: Milestone đã được hoàn thành.

---

## 8.4. Tự động cập nhật trạng thái Milestone

Scheduled Job định kỳ kiểm tra trạng thái và Deadline của Milestone.

Các quy tắc xử lý:

| Trạng thái hiện tại | Điều kiện | Trạng thái sau Automation |
|---|---|---|
| `PENDING` | Bất kỳ | `PENDING` |
| `IN_PROGRESS` | Chưa quá Deadline | `IN_PROGRESS` |
| `IN_PROGRESS` | Đã quá Deadline | `OVERDUE` |
| `SUBMITTED` | Bất kỳ | `SUBMITTED` |
| `REVISION_REQUIRED` | Bất kỳ | `REVISION_REQUIRED` |
| `APPROVED` | Bất kỳ | `APPROVED` |
| `OVERDUE` | Chưa Submit | `OVERDUE` |

Luồng xử lý:

```text
              IN_PROGRESS
                   |
                   v
           Kiểm tra Deadline
              /          \
             /            \
    Chưa quá hạn        Quá Deadline
         |                   |
         v                   v
   IN_PROGRESS            OVERDUE
```

Khi Student Submit Milestone:

```text
IN_PROGRESS
     |
     | Student Submit
     v
SUBMITTED
     |
     | Lecturer Review
     |
     +-------------------+
     |                   |
     v                   v
 APPROVED        REVISION_REQUIRED
                         |
                         v
                    Student sửa
                         |
                         v
                     IN_PROGRESS
                         |
                         v
                      SUBMITTED
```

Scheduled Job không tự động chuyển `SUBMITTED` sang `OVERDUE` vì Student đã hoàn thành bước Submit và đang chờ Lecturer đánh giá.

---

## 8.5. Kiểm tra trạng thái Project

Scheduled Job có thể kiểm tra trạng thái các Milestone thuộc từng Project để xác định Project đã đủ điều kiện hoàn thành hay chưa.

Các trường hợp:

- Nếu Project đang `IN_PROGRESS` và vẫn còn Milestone chưa `APPROVED`, Project tiếp tục ở `IN_PROGRESS`.
- Nếu tất cả Milestone bắt buộc đã ở `APPROVED`, Project đủ điều kiện để Lecturer xác nhận hoàn thành.
- Scheduled Job không tự động thay thế bước xác nhận cuối cùng của Lecturer.
- Nếu Project bị hủy, Project chuyển sang `CANCELLED`.

Quy trình:

```text
Project IN_PROGRESS
        |
        v
Kiểm tra Milestones
        |
        +-----------------------------+
        |                             |
        v                             v
Còn Milestone chưa APPROVED      Tất cả APPROVED
        |                             |
        v                             v
   IN_PROGRESS              Đủ điều kiện hoàn thành
                                      |
                                      v
                              Lecturer xác nhận
                                      |
                                      v
                                  COMPLETED
```

---

## 8.6. Tạo Notification tự động

Notification được tạo khi hệ thống phát hiện một sự kiện cần thông báo cho Student hoặc Lecturer.

### Notification do Automation tạo

| Sự kiện | Người nhận | Notification |
|---|---|---|
| Milestone còn 7 ngày | Student | Reminder |
| Milestone còn 3 ngày | Student | Warning |
| Milestone quá Deadline | Student và Lecturer | Overdue |
| Project đủ điều kiện hoàn thành | Lecturer | Yêu cầu xác nhận hoàn thành |

### Notification do sự kiện nghiệp vụ tạo

Các Notification khác có thể được tạo trực tiếp khi xảy ra sự kiện trong hệ thống:

| Sự kiện | Người nhận |
|---|---|
| Student gửi Lecturer Request | Lecturer |
| Lecturer chấp nhận Request | Student |
| Lecturer từ chối Request | Student |
| Topic Proposal được duyệt | Student |
| Topic Proposal bị từ chối | Student |
| Milestone được tạo | Student |
| Lecturer yêu cầu chỉnh sửa | Student |
| AI Analysis hoàn thành | Student và Lecturer |

Do đó, không phải tất cả Notification đều được tạo bởi Scheduled Job.

Có hai cơ chế chính:

```text
Scheduled Event
      |
      v
Scheduled Job
      |
      v
Notification


Business Event
      |
      v
Event Handler
      |
      v
Notification
```

---

## 8.7. Chống tạo Notification trùng lặp

Scheduled Job có thể chạy nhiều lần nên hệ thống phải kiểm tra Notification trước khi tạo mới.

Quy trình:

```text
Scheduled Job
      |
      v
Kiểm tra điều kiện
      |
      v
Notification đã tồn tại?
      |
   +--+--+
   |     |
  Có    Không
   |     |
   v     v
Không   Tạo
tạo     mới
```

Ví dụ:

- Một Milestone còn 3 ngày chỉ tạo một Notification `WARNING`.
- Các lần Scheduled Job tiếp theo không tạo lại Notification `WARNING` cho cùng Milestone.
- Khi Milestone quá hạn, hệ thống có thể tạo một Notification `OVERDUE` riêng.

Có thể sử dụng Event Type kết hợp với Milestone ID để xác định Notification đã được tạo hay chưa.

Ví dụ:

```text
MILESTONE_WARNING:{milestone_id}

MILESTONE_OVERDUE:{milestone_id}
```

Có thể áp dụng Unique Constraint hoặc cơ chế tương đương để hạn chế việc tạo Notification trùng lặp.

---

## 8.8. AI Analysis Workflow

AI Analysis là một Workflow riêng và không phải tác vụ kiểm tra Deadline của Scheduled Job.

Khi Student liên kết GitHub Repository, hệ thống có thể thực hiện quy trình:

```text
Student
   |
   v
GitHub Repository
   |
   v
Lấy Source Code
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

AI có thể hỗ trợ phân tích:

- Code Quality.
- Code Complexity.
- Maintainability.
- Potential Bugs.
- Security Issues.
- Cấu trúc Source Code.

Kết quả phân tích được lưu lại để Student và Lecturer có thể xem.

AI chỉ đóng vai trò hỗ trợ phân tích Source Code và không thay thế quyết định đánh giá cuối cùng của Lecturer.

---

## 8.9. Xử lý lỗi Automation

Nếu Scheduled Job gặp lỗi, hệ thống cần:

- Ghi lại thông tin lỗi vào Log.
- Không làm ảnh hưởng đến các chức năng chính của hệ thống.
- Cho phép tác vụ được thực hiện lại khi cần.
- Không tạo lại Notification đã xử lý thành công.
- Có thể Retry đối với các tác vụ thất bại.
- Theo dõi số lần Retry để tránh vòng lặp vô hạn.

Quy trình:

```text
Scheduled Job
      |
      v
Thực hiện Task
      |
   +--+--+
   |     |
Thành   Lỗi
công     |
   |     v
   |   Ghi Log
   |     |
   |     v
   |    Retry
   |
   v
Hoàn thành
```

Nếu Retry vượt quá số lần cho phép, hệ thống cần ghi nhận lỗi để Admin có thể kiểm tra.

---

## 8.10. Tổng quan Automation

Automation của hệ thống bao gồm các nhóm chức năng chính:

```text
                     Automation
                          |
          +---------------+---------------+
          |               |               |
          v               v               v
   Deadline Check   Project Check   Notification
          |               |               |
          v               v               v
 Milestone Status    Completion      Reminder /
      Update            Check        Warning /
                                      Overdue
```

Luồng tổng quát:

```text
Scheduled Job
      |
      +--------------------+
      |                    |
      v                    v
Milestone Check       Project Check
      |                    |
      v                    v
Deadline / Status     Check Completion
      |                    |
      v                    v
Notification        Lecturer Confirmation
      |                    |
      v                    v
   Student             COMPLETED
```

---

## 8.11. Nguyên tắc Automation

Automation cần tuân thủ các nguyên tắc:

1. Scheduled Job không được làm thay đổi dữ liệu nghiệp vụ ngoài các quy tắc đã định nghĩa.

2. Scheduled Job không thay thế quyết định của Lecturer.

3. Không tự động chuyển `SUBMITTED` thành `OVERDUE`.

4. Không tự động chuyển `REVISION_REQUIRED` thành `OVERDUE`.

5. Không tự động chuyển `APPROVED` thành `OVERDUE`.

6. Notification phải có cơ chế chống trùng lặp.

7. Các tác vụ Automation cần được ghi Log.

8. Các tác vụ thất bại có thể được Retry.

9. Automation phải có khả năng chạy độc lập với các Request chính của người dùng.

10. AI Analysis là Workflow riêng và không phụ thuộc vào Scheduled Job kiểm tra Deadline.

---

## 8.12. Kết luận

Automation giúp hệ thống chủ động theo dõi Deadline và trạng thái Project, giảm các công việc kiểm tra thủ công và hỗ trợ Student, Lecturer trong quá trình thực hiện Project.

Các thành phần Automation chính gồm:

- Scheduled Job.
- Deadline Checking.
- Milestone Status Update.
- Project Completion Checking.
- Notification.
- Error Handling và Retry.

AI Analysis được triển khai như một Workflow riêng và không thay thế cơ chế Scheduled Job hoặc quyết định đánh giá cuối cùng của Lecturer.
