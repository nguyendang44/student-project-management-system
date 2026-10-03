# 13. Student Use Cases

## 13.1 Mục tiêu

Tài liệu này mô tả chi tiết các Use Case mà Actor `Student` có thể thực hiện trong hệ thống quản lý đề tài và theo dõi tiến độ dự án sinh viên.

Các Use Case được xây dựng dựa trên Functional Requirements và Business Workflow đã xác định ở Tuần 1.

---

## 13.2 Actor: Student

`Student` là sinh viên sử dụng hệ thống để:

- Đăng nhập và sử dụng hệ thống.
- Xem danh sách đề tài.
- Đăng ký đề tài.
- Đề xuất đề tài mới.
- Theo dõi trạng thái đề xuất.
- Gửi yêu cầu đăng ký giảng viên.
- Theo dõi trạng thái yêu cầu.
- Xem thông tin project.
- Xem milestone.
- Cập nhật tiến độ.
- Nộp kết quả milestone.
- Liên kết GitHub repository.
- Xem kết quả phân tích AI.
- Xem notification.
- Theo dõi tiến độ project.

---

# 13.3 UC-ST-01: Đăng nhập

## Mục tiêu

Cho phép Student đăng nhập vào hệ thống bằng tài khoản đã được cấp.

## Actor

Student.

## Preconditions

- Student đã có tài khoản.
- Tài khoản đang hoạt động.

## Main Flow

1. Student mở trang đăng nhập.
2. Student nhập username/email.
3. Student nhập password.
4. Student gửi thông tin đăng nhập.
5. System kiểm tra thông tin tài khoản.
6. System xác thực password.
7. System xác định Role của Student.
8. System tạo phiên đăng nhập/token.
9. System chuyển Student đến giao diện phù hợp.

## Alternative Flow

### A1. Sai username/email

1. System không tìm thấy tài khoản.
2. System thông báo thông tin đăng nhập không hợp lệ.
3. Student nhập lại thông tin.

### A2. Sai password

1. System xác định password không đúng.
2. System thông báo lỗi.
3. Student nhập lại password.

### A3. Tài khoản bị khóa

1. System xác định tài khoản không hoạt động.
2. System từ chối đăng nhập.
3. System hiển thị thông báo phù hợp.

## Postconditions

Student đăng nhập thành công và có thể sử dụng các chức năng được cấp quyền.

---

# 13.4 UC-ST-02: Xem danh sách đề tài

## Mục tiêu

Cho phép Student xem các đề tài đang được mở để lựa chọn.

## Actor

Student.

## Preconditions

- Student đã đăng nhập.
- Hệ thống có dữ liệu đề tài.

## Main Flow

1. Student truy cập chức năng danh sách đề tài.
2. System lấy danh sách đề tài.
3. System hiển thị thông tin:
   - Tên đề tài.
   - Mô tả.
   - Lecturer phụ trách nếu có.
   - Số lượng sinh viên hiện tại.
   - Số lượng sinh viên tối đa.
   - Trạng thái đề tài.
4. Student có thể tìm kiếm hoặc lọc đề tài.
5. Student chọn một đề tài để xem chi tiết.

## Alternative Flow

### A1. Không có đề tài

System hiển thị thông báo chưa có đề tài phù hợp.

## Postconditions

Student xem được danh sách và thông tin chi tiết của các đề tài.

---

# 13.5 UC-ST-03: Đăng ký đề tài

## Mục tiêu

Cho phép Student đăng ký một đề tài đang mở.

## Actor

Student.

## Preconditions

- Student đã đăng nhập.
- Đề tài đang cho phép đăng ký.
- Student chưa đăng ký đề tài khác đang hoạt động.

## Main Flow

1. Student xem chi tiết đề tài.
2. Student chọn `Đăng ký đề tài`.
3. System kiểm tra trạng thái đề tài.
4. System kiểm tra Student có đủ điều kiện đăng ký hay không.
5. System tạo yêu cầu đăng ký.
6. System lưu trạng thái yêu cầu.
7. System thông báo đăng ký thành công.

## Alternative Flow

### A1. Đề tài đã đóng đăng ký

System từ chối đăng ký và thông báo đề tài không còn nhận đăng ký.

### A2. Student đã có project đang hoạt động

System từ chối đăng ký nếu quy tắc nghiệp vụ không cho phép Student tham gia nhiều project đồng thời.

### A3. Đăng ký trùng

System phát hiện Student đã gửi yêu cầu cho đề tài và không tạo bản ghi trùng.

## Postconditions

Yêu cầu đăng ký đề tài được lưu trong hệ thống.

---

# 13.6 UC-ST-04: Đề xuất đề tài

## Mục tiêu

Cho phép Student tự đề xuất một đề tài mới.

## Actor

Student.

## Preconditions

- Student đã đăng nhập.

## Main Flow

1. Student truy cập chức năng đề xuất đề tài.
2. Student nhập:
   - Tên đề tài.
   - Mô tả.
   - Mục tiêu.
   - Nội dung dự kiến.
3. Student gửi đề xuất.
4. System kiểm tra dữ liệu.
5. System tạo Topic Proposal.
6. System đặt trạng thái `PENDING_APPROVAL`.
7. System thông báo gửi đề xuất thành công.
8. Lecturer có quyền xem và xử lý đề xuất.

## Alternative Flow

### A1. Thiếu thông tin bắt buộc

System hiển thị lỗi và yêu cầu Student bổ sung dữ liệu.

### A2. Dữ liệu không hợp lệ

System từ chối dữ liệu không hợp lệ và yêu cầu Student chỉnh sửa.

## Postconditions

Một Topic Proposal được tạo với trạng thái:

```text
PENDING_APPROVAL
```

---

# 13.7 UC-ST-05: Theo dõi trạng thái đề xuất

## Mục tiêu

Cho phép Student theo dõi kết quả đề xuất đề tài.

## Actor

Student.

## Preconditions

- Student đã đăng nhập.
- Student đã từng gửi Topic Proposal.

## Main Flow

1. Student mở danh sách đề xuất của mình.
2. System hiển thị các đề xuất.
3. Student xem trạng thái từng đề xuất.

Các trạng thái:

```text
PENDING_APPROVAL
        |
        +----> APPROVED
        |
        +----> REJECTED
```

4. Nếu đề xuất bị từ chối, Student có thể xem lý do từ chối.
5. Student chỉnh sửa và gửi lại nếu hệ thống cho phép.

## Postconditions

Student biết được trạng thái và kết quả của Topic Proposal.

---

# 13.8 UC-ST-06: Gửi yêu cầu đăng ký giảng viên

## Mục tiêu

Cho phép Student gửi yêu cầu được Lecturer hướng dẫn.

## Actor

Student.

## Preconditions

- Student đã đăng nhập.
- Student có Topic hợp lệ.
- Lecturer đang cho phép nhận Student.
- Capacity của Lecturer còn chỗ.

## Main Flow

1. Student chọn Lecturer.
2. Student chọn `Gửi yêu cầu`.
3. System kiểm tra Lecturer Capacity.
4. Nếu còn chỗ, System tạo Lecturer Request.
5. System đặt trạng thái:

```text
PENDING
```

6. System gửi notification cho Lecturer.
7. Student có thể theo dõi yêu cầu.

## Alternative Flow

### A1. Lecturer đã FULL

1. System xác định:

```text
current_students >= max_students
```

2. System không tạo yêu cầu mới.
3. System thông báo Lecturer đã đủ số lượng.

### A2. Student đã gửi yêu cầu trước đó

System không tạo yêu cầu trùng.

## Postconditions

Lecturer Request được tạo với trạng thái `PENDING`.

---

# 13.9 UC-ST-07: Theo dõi yêu cầu đăng ký giảng viên

## Mục tiêu

Cho phép Student xem trạng thái yêu cầu đăng ký Lecturer.

## Actor

Student.

## Main Flow

1. Student mở danh sách yêu cầu.
2. System hiển thị Lecturer và trạng thái yêu cầu.
3. Student xem kết quả.

Các trạng thái:

```text
PENDING
   |
   +----> ACCEPTED
   |
   +----> REJECTED
```

Nếu `REJECTED`, Student có thể xem lý do do Lecturer cung cấp.

## Postconditions

Student biết được kết quả yêu cầu đăng ký Lecturer.

---

# 13.10 UC-ST-08: Xem Project

## Mục tiêu

Cho phép Student xem thông tin project mà mình đang tham gia.

## Actor

Student.

## Preconditions

- Student đã được Lecturer chấp nhận.
- Project đã được tạo.

## Main Flow

1. Student mở trang Project.
2. System lấy thông tin project.
3. System hiển thị:
   - Tên project.
   - Topic.
   - Lecturer.
   - Trạng thái project.
   - Danh sách milestone.
   - Deadline.
   - Tiến độ.
4. Student xem chi tiết từng milestone.

## Postconditions

Student theo dõi được tình trạng project.

---

# 13.11 UC-ST-09: Cập nhật tiến độ

## Mục tiêu

Cho phép Student cập nhật tiến độ thực hiện project.

## Actor

Student.

## Preconditions

- Student đang tham gia project.
- Project chưa hoàn thành.

## Main Flow

1. Student chọn milestone.
2. Student cập nhật nội dung tiến độ.
3. Student có thể thêm:
   - Mô tả công việc.
   - Phần trăm hoàn thành.
   - Kết quả đạt được.
   - Ghi chú.
4. Student gửi cập nhật.
5. System kiểm tra dữ liệu.
6. System lưu Progress Update.
7. System cập nhật thông tin tiến độ milestone.

## Alternative Flow

### A1. Milestone đã APPROVED

System không cho phép Student tiếp tục chỉnh sửa milestone đã hoàn thành.

### A2. Dữ liệu không hợp lệ

System yêu cầu Student sửa dữ liệu trước khi lưu.

## Postconditions

Progress Update được lưu trong hệ thống.

---

# 13.12 UC-ST-10: Nộp kết quả milestone

## Mục tiêu

Cho phép Student hoàn thành công việc và gửi kết quả cho Lecturer đánh giá.

## Actor

Student.

## Preconditions

- Student đang thực hiện milestone.
- Milestone chưa `APPROVED`.

## Main Flow

1. Student mở milestone.
2. Student cập nhật kết quả.
3. Student đính kèm tài liệu hoặc thông tin cần thiết nếu có.
4. Student chọn `Submit`.
5. System kiểm tra dữ liệu.
6. System chuyển trạng thái milestone:

```text
IN_PROGRESS
     |
     v
SUBMITTED
```

7. System gửi notification cho Lecturer.

## Alternative Flow

### A1. Milestone quá hạn

Student vẫn có thể nộp kết quả nếu hệ thống cho phép.

Trạng thái có thể chuyển:

```text
OVERDUE
   |
   v
SUBMITTED
```

### A2. Thiếu dữ liệu

System không cho submit và yêu cầu Student bổ sung.

## Postconditions

Milestone ở trạng thái `SUBMITTED` và chờ Lecturer đánh giá.

---

# 13.13 UC-ST-11: Chỉnh sửa milestone sau khi bị yêu cầu sửa

## Mục tiêu

Cho phép Student chỉnh sửa kết quả khi Lecturer yêu cầu revision.

## Actor

Student.

## Preconditions

- Milestone có trạng thái:

```text
REVISION_REQUIRED
```

## Main Flow

1. Student nhận notification yêu cầu chỉnh sửa.
2. Student mở milestone.
3. Student xem feedback của Lecturer.
4. Student thực hiện chỉnh sửa.
5. Student cập nhật kết quả.
6. Student submit lại.
7. System chuyển milestone:

```text
REVISION_REQUIRED
        |
        v
IN_PROGRESS
        |
        v
SUBMITTED
```

8. System thông báo cho Lecturer.

## Postconditions

Milestone được gửi lại Lecturer để đánh giá.

---

# 13.14 UC-ST-12: Liên kết GitHub Repository

## Mục tiêu

Cho phép Student liên kết repository GitHub với project.

## Actor

Student.

## Preconditions

- Student đã đăng nhập.
- Student đang tham gia project.
- Project cho phép tích hợp GitHub.

## Main Flow

1. Student mở trang Project.
2. Student chọn `GitHub Integration`.
3. Student cung cấp repository hoặc thực hiện OAuth nếu hệ thống hỗ trợ.
4. System xác thực repository.
5. System lưu thông tin repository.
6. System hiển thị trạng thái liên kết.

## Alternative Flow

### A1. Repository không tồn tại

System thông báo repository không hợp lệ.

### A2. Student không có quyền truy cập

System từ chối liên kết repository.

## Postconditions

Project được liên kết với GitHub Repository.

---

# 13.15 UC-ST-13: Xem kết quả phân tích AI

## Mục tiêu

Cho phép Student xem báo cáo phân tích source code do AI tạo ra.

## Actor

Student.

## Preconditions

- Project đã được liên kết với GitHub.
- Đã có kết quả AI Analysis.

## Main Flow

1. Student mở project.
2. Student chọn `AI Analysis`.
3. System lấy báo cáo phân tích.
4. System hiển thị kết quả.
5. Student xem các thông tin phân tích.

Ví dụ:

- Code structure.
- Complexity.
- Một số vấn đề có thể phát hiện.
- Code quality indicators.
- Các đề xuất cải thiện.

## Postconditions

Student xem được báo cáo AI.

AI Analysis chỉ có vai trò hỗ trợ và không thay thế kết quả đánh giá cuối cùng của Lecturer.

---

# 13.16 UC-ST-14: Xem Notification

## Mục tiêu

Cho phép Student xem các thông báo liên quan đến project.

## Actor

Student.

## Main Flow

1. Student mở Notification.
2. System hiển thị danh sách notification.
3. Student chọn một notification.
4. System hiển thị nội dung chi tiết.
5. Student có thể đánh dấu notification đã đọc.

Các notification có thể bao gồm:

- Yêu cầu Lecturer được chấp nhận.
- Yêu cầu Lecturer bị từ chối.
- Topic Proposal được duyệt.
- Topic Proposal bị từ chối.
- Milestone mới được tạo.
- Deadline sắp đến.
- Milestone quá hạn.
- Milestone yêu cầu chỉnh sửa.
- Milestone được phê duyệt.
- AI Analysis hoàn thành.

---

# 13.17 UC-ST-15: Xem Dashboard tiến độ

## Mục tiêu

Cho phép Student theo dõi tổng quan tiến độ project.

## Actor

Student.

## Preconditions

- Student đã đăng nhập.
- Student có project.

## Main Flow

1. Student mở Dashboard.
2. System tổng hợp dữ liệu project.
3. System hiển thị:
   - Project status.
   - Tổng số milestone.
   - Milestone đã hoàn thành.
   - Milestone đang thực hiện.
   - Milestone quá hạn.
   - Tiến độ tổng thể.
   - Deadline gần nhất.
4. Student chọn milestone để xem chi tiết.

## Postconditions

Student có cái nhìn tổng quan về tiến độ project.

---

# 13.18 Quyền của Student

| Chức năng | Student |
|---|---|
| Đăng nhập | Có |
| Xem đề tài | Có |
| Tạo đề tài | Theo cơ chế đề xuất |
| Đề xuất đề tài | Có |
| Duyệt đề tài | Không |
| Đăng ký đề tài | Có |
| Gửi yêu cầu Lecturer | Có |
| Chấp nhận Lecturer Request | Không |
| Từ chối Lecturer Request | Không |
| Xem capacity Lecturer | Có |
| Thiết lập capacity | Không |
| Xem project của mình | Có |
| Tạo milestone | Không |
| Cập nhật tiến độ | Có |
| Submit milestone | Có |
| Phê duyệt milestone | Không |
| Xem đánh giá | Có |
| Liên kết GitHub | Có |
| Xem AI Analysis | Có |
| Đánh giá cuối cùng | Không |
| Xem notification | Có |
| Xem dashboard | Có |

---

# 13.19 Student Use Case Flow tổng quát

```text
                 Student
                    |
                    v
                Đăng nhập
                    |
                    v
             Xem danh sách Topic
                    |
          +---------+---------+
          |                   |
          v                   v
    Đăng ký Topic       Đề xuất Topic
          |                   |
          |                   v
          |             Lecturer duyệt
          |                   |
          +---------+---------+
                    |
                    v
          Chọn / đăng ký Lecturer
                    |
                    v
            Kiểm tra Capacity
                    |
          +---------+---------+
          |                   |
       AVAILABLE             FULL
          |                   |
          v                   v
       Gửi Request        Không đăng ký
          |
          v
     Lecturer xử lý
          |
     +----+----+
     |         |
     v         v
 ACCEPTED   REJECTED
     |
     v
  Tạo Project
     |
     v
  Thực hiện Project
     |
     v
 Cập nhật Milestone
     |
     v
 Submit kết quả
     |
     v
Lecturer đánh giá
     |
 +---+---+
 |       |
 v       v
APPROVED REVISION_REQUIRED
 |       |
 |       v
 |    Chỉnh sửa
 |       |
 |       v
 |    Submit lại
 |
 v
Hoàn thành Project
```

---

# 13.20 Nguyên tắc

1. Student chỉ được truy cập dữ liệu thuộc phạm vi quyền của mình.
2. Student không được tự ý thay đổi kết quả đánh giá của Lecturer.
3. Student không được tự thay đổi Lecturer đã được chấp nhận nếu không có quy trình tương ứng.
4. Capacity phải được kiểm tra ở phía Server.
5. Student không được tự chuyển milestone sang `APPROVED`.
6. Student có thể cập nhật milestone theo trạng thái cho phép.
7. Student có thể submit lại milestone khi Lecturer yêu cầu chỉnh sửa.
8. AI Analysis chỉ cung cấp thông tin hỗ trợ.
9. Các thao tác quan trọng phải được xác thực và kiểm tra quyền.
10. Các trạng thái phải tuân thủ state transition đã định nghĩa.

---

# 13.21 Kết luận

Student là Actor trực tiếp thực hiện phần lớn hoạt động của project.

Luồng chính của Student:

```text
Đăng nhập
    ↓
Chọn / đề xuất Topic
    ↓
Đăng ký Lecturer
    ↓
Được Lecturer chấp nhận
    ↓
Project được tạo
    ↓
Thực hiện Milestone
    ↓
Cập nhật Progress
    ↓
Submit kết quả
    ↓
Lecturer Evaluation
    ↓
Approved hoặc Revision
    ↓
Hoàn thành Project
```

Use Case của Student là cơ sở để xây dựng API, giao diện và phân quyền cho module Student trong các tuần thiết kế và triển khai tiếp theo.
