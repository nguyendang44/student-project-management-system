# 14. Lecturer Use Cases

## 14.1 Mục tiêu

Tài liệu này mô tả chi tiết các Use Case mà Actor `Lecturer` có thể thực hiện trong hệ thống quản lý đề tài và theo dõi tiến độ dự án sinh viên.

Lecturer là Actor chịu trách nhiệm chính trong việc:

- Quản lý đề tài.
- Xử lý đề xuất đề tài của Student.
- Tiếp nhận và xử lý yêu cầu hướng dẫn.
- Quản lý số lượng Student tối đa.
- Quản lý Project.
- Tạo và quản lý Milestone.
- Theo dõi Progress.
- Đánh giá kết quả.
- Xem báo cáo AI.
- Theo dõi Dashboard và Notification.

---

# 14.2 Actor: Lecturer

Lecturer có thể:

- Đăng nhập.
- Xem danh sách đề tài.
- Tạo đề tài.
- Chỉnh sửa đề tài.
- Đóng/mở đăng ký đề tài.
- Xem Topic Proposal.
- Duyệt hoặc từ chối Topic Proposal.
- Xem Lecturer Request.
- Chấp nhận hoặc từ chối Student.
- Thiết lập số lượng Student tối đa.
- Xem capacity.
- Xem danh sách Project phụ trách.
- Tạo Milestone.
- Cập nhật Milestone.
- Theo dõi Progress.
- Đánh giá Milestone.
- Yêu cầu Student chỉnh sửa.
- Phê duyệt Milestone.
- Đánh giá Project.
- Xem GitHub Repository.
- Xem AI Analysis.
- Xem Notification.
- Xem Dashboard.
- Xem thống kê.

---

# 14.3 UC-LC-01: Đăng nhập

## Mục tiêu

Cho phép Lecturer đăng nhập vào hệ thống bằng tài khoản được cấp.

## Actor

Lecturer.

## Preconditions

- Lecturer đã có tài khoản.
- Tài khoản đang hoạt động.

## Main Flow

1. Lecturer mở trang đăng nhập.
2. Lecturer nhập username/email.
3. Lecturer nhập password.
4. Lecturer gửi thông tin đăng nhập.
5. System kiểm tra tài khoản.
6. System xác thực password.
7. System xác định Role là `LECTURER`.
8. System tạo phiên đăng nhập/token.
9. System chuyển Lecturer đến giao diện tương ứng.

## Alternative Flow

### A1. Sai thông tin đăng nhập

System thông báo thông tin đăng nhập không hợp lệ.

### A2. Tài khoản không hoạt động

System từ chối đăng nhập.

## Postconditions

Lecturer đăng nhập thành công.

---

# 14.4 UC-LC-02: Tạo đề tài

## Mục tiêu

Cho phép Lecturer tạo Topic để Student đăng ký.

## Actor

Lecturer.

## Preconditions

- Lecturer đã đăng nhập.

## Main Flow

1. Lecturer mở chức năng quản lý Topic.
2. Lecturer chọn `Tạo đề tài`.
3. Lecturer nhập:
   - Tên đề tài.
   - Mô tả.
   - Mục tiêu.
   - Nội dung dự kiến.
   - Thời gian thực hiện.
4. Lecturer thiết lập số lượng Student tối đa nếu cần.
5. Lecturer lưu đề tài.
6. System kiểm tra dữ liệu.
7. System tạo Topic.
8. System lưu Topic.

## Alternative Flow

### A1. Thiếu dữ liệu

System yêu cầu Lecturer bổ sung thông tin bắt buộc.

### A2. Dữ liệu không hợp lệ

System thông báo lỗi và không tạo Topic.

## Postconditions

Topic được tạo thành công.

---

# 14.5 UC-LC-03: Quản lý đề tài

## Mục tiêu

Cho phép Lecturer xem và cập nhật các Topic do mình phụ trách.

## Actor

Lecturer.

## Main Flow

1. Lecturer mở danh sách Topic.
2. System hiển thị các Topic.
3. Lecturer chọn Topic.
4. Lecturer có thể:
   - Xem chi tiết.
   - Chỉnh sửa.
   - Mở đăng ký.
   - Đóng đăng ký.
   - Xem Student đăng ký.
5. System lưu thay đổi.

## Postconditions

Thông tin Topic được cập nhật.

---

# 14.6 UC-LC-04: Xem Topic Proposal

## Mục tiêu

Cho phép Lecturer xem các đề xuất Topic do Student gửi.

## Actor

Lecturer.

## Preconditions

- Lecturer đã đăng nhập.
- Có Topic Proposal đang chờ xử lý.

## Main Flow

1. Lecturer mở danh sách Topic Proposal.
2. System hiển thị các đề xuất.
3. Lecturer chọn một đề xuất.
4. System hiển thị:
   - Student.
   - Tên Topic.
   - Mô tả.
   - Mục tiêu.
   - Nội dung dự kiến.
   - Thời gian đề xuất.
   - Trạng thái.

## Postconditions

Lecturer có đầy đủ thông tin để xử lý Topic Proposal.

---

# 14.7 UC-LC-05: Duyệt Topic Proposal

## Mục tiêu

Cho phép Lecturer chấp nhận Topic Proposal do Student đề xuất.

## Actor

Lecturer.

## Preconditions

- Topic Proposal có trạng thái:

```text
PENDING_APPROVAL
```

## Main Flow

1. Lecturer mở Topic Proposal.
2. Lecturer xem nội dung.
3. Lecturer chọn `Approve`.
4. System kiểm tra quyền.
5. System chuyển trạng thái:

```text
PENDING_APPROVAL
        |
        v
    APPROVED
```

6. System thông báo cho Student.

## Postconditions

Topic Proposal được chấp nhận.

---

# 14.8 UC-LC-06: Từ chối Topic Proposal

## Mục tiêu

Cho phép Lecturer từ chối Topic Proposal không phù hợp.

## Actor

Lecturer.

## Main Flow

1. Lecturer mở Topic Proposal.
2. Lecturer chọn `Reject`.
3. Lecturer nhập lý do.
4. System kiểm tra dữ liệu.
5. System chuyển trạng thái:

```text
PENDING_APPROVAL
        |
        v
     REJECTED
```

6. System lưu rejection reason.
7. System gửi Notification cho Student.

## Postconditions

Student biết Topic Proposal bị từ chối và lý do.

---

# 14.9 UC-LC-07: Xem Lecturer Request

## Mục tiêu

Cho phép Lecturer xem các yêu cầu Student muốn đăng ký mình hướng dẫn.

## Actor

Lecturer.

## Preconditions

- Lecturer đã đăng nhập.

## Main Flow

1. Lecturer mở danh sách Lecturer Request.
2. System hiển thị các yêu cầu.
3. Lecturer xem:
   - Student.
   - Topic.
   - Thời gian gửi.
   - Trạng thái.
   - Thông tin liên quan.

## Postconditions

Lecturer có thể xử lý các yêu cầu.

---

# 14.10 UC-LC-08: Chấp nhận Lecturer Request

## Mục tiêu

Cho phép Lecturer chấp nhận Student làm người hướng dẫn.

## Actor

Lecturer.

## Preconditions

- Request có trạng thái `PENDING`.
- Lecturer còn capacity.

## Main Flow

1. Lecturer mở Request.
2. Lecturer chọn `Accept`.
3. System kiểm tra capacity.
4. System kiểm tra Request chưa được xử lý.
5. System thực hiện transaction.
6. System tăng số Student hiện tại.
7. System chuyển Request:

```text
PENDING
   |
   v
ACCEPTED
```

8. System tạo hoặc kích hoạt Project.
9. System gửi Notification cho Student.

## Alternative Flow

### A1. Lecturer đã FULL

System không cho phép Accept và thông báo capacity đã đầy.

### A2. Request đã được xử lý

System không cho phép xử lý lại Request.

## Postconditions

Student được Lecturer chấp nhận và Project được tạo/kích hoạt.

---

# 14.11 UC-LC-09: Từ chối Lecturer Request

## Mục tiêu

Cho phép Lecturer từ chối yêu cầu của Student.

## Actor

Lecturer.

## Preconditions

- Request có trạng thái `PENDING`.

## Main Flow

1. Lecturer mở Request.
2. Lecturer chọn `Reject`.
3. Lecturer nhập lý do.
4. System lưu lý do.
5. System chuyển trạng thái:

```text
PENDING
   |
   v
REJECTED
```

6. System gửi Notification cho Student.

## Postconditions

Student biết Request bị từ chối và lý do.

---

# 14.12 UC-LC-10: Thiết lập Lecturer Capacity

## Mục tiêu

Cho phép Lecturer thiết lập số lượng Student tối đa có thể hướng dẫn.

## Actor

Lecturer.

## Main Flow

1. Lecturer mở Capacity Management.
2. Lecturer nhập `max_students`.
3. System kiểm tra giá trị.
4. System lưu capacity.
5. System tính:

```text
remaining = max_students - current_students
```

6. System cập nhật trạng thái.

Các trạng thái:

```text
AVAILABLE
FULL
```

## Alternative Flow

### A1. Capacity nhỏ hơn số Student hiện tại

System không cho phép thiết lập giá trị không hợp lệ.

## Postconditions

Capacity của Lecturer được cập nhật.

---

# 14.13 UC-LC-11: Xem Capacity

## Mục tiêu

Cho phép Lecturer theo dõi số lượng Student đang hướng dẫn.

## Actor

Lecturer.

## Main Flow

System hiển thị:

```text
Maximum Students
Current Students
Remaining Slots
Capacity Status
```

Ví dụ:

```text
Maximum: 5
Current: 3
Remaining: 2
Status: AVAILABLE
```

Nếu:

```text
Current >= Maximum
```

thì:

```text
Status = FULL
```

---

# 14.14 UC-LC-12: Xem Project

## Mục tiêu

Cho phép Lecturer xem các Project mình phụ trách.

## Actor

Lecturer.

## Main Flow

1. Lecturer mở Project Management.
2. System lấy các Project thuộc Lecturer.
3. System hiển thị:
   - Student.
   - Topic.
   - Project status.
   - Milestone.
   - Progress.
   - Deadline.
4. Lecturer chọn Project để xem chi tiết.

## Postconditions

Lecturer theo dõi được tình trạng Project.

---

# 14.15 UC-LC-13: Tạo Milestone

## Mục tiêu

Cho phép Lecturer chia Project thành các Milestone để theo dõi tiến độ.

## Actor

Lecturer.

## Preconditions

- Project đang hoạt động.

## Main Flow

1. Lecturer mở Project.
2. Lecturer chọn `Create Milestone`.
3. Lecturer nhập:
   - Tên milestone.
   - Mô tả.
   - Deadline.
   - Thứ tự.
   - Yêu cầu kết quả.
4. System kiểm tra dữ liệu.
5. System tạo Milestone.
6. System đặt trạng thái:

```text
PENDING
```

7. System gửi Notification cho Student.

## Postconditions

Milestone được tạo.

---

# 14.16 UC-LC-14: Theo dõi Progress

## Mục tiêu

Cho phép Lecturer theo dõi tiến độ thực hiện Project.

## Actor

Lecturer.

## Main Flow

1. Lecturer mở Project.
2. System lấy Progress Update.
3. System hiển thị:
   - Tiến độ từng Milestone.
   - Tiến độ tổng thể.
   - Deadline.
   - Milestone quá hạn.
   - Milestone đang chờ đánh giá.
4. Lecturer xem chi tiết từng Milestone.

## Postconditions

Lecturer nắm được tình trạng tiến độ của Student.

---

# 14.17 UC-LC-15: Xem kết quả Milestone

## Mục tiêu

Cho phép Lecturer xem kết quả Student đã submit.

## Actor

Lecturer.

## Preconditions

- Milestone có trạng thái:

```text
SUBMITTED
```

## Main Flow

1. Lecturer mở Milestone.
2. System hiển thị nội dung Student submit.
3. Lecturer xem:
   - Progress.
   - Tài liệu.
   - Kết quả.
   - GitHub nếu có.
4. Lecturer đưa ra đánh giá.

---

# 14.18 UC-LC-16: Yêu cầu chỉnh sửa Milestone

## Mục tiêu

Cho phép Lecturer yêu cầu Student chỉnh sửa kết quả chưa đạt yêu cầu.

## Actor

Lecturer.

## Preconditions

- Milestone đang `SUBMITTED`.

## Main Flow

1. Lecturer xem kết quả.
2. Lecturer chọn `Request Revision`.
3. Lecturer nhập feedback.
4. System lưu feedback.
5. System chuyển trạng thái:

```text
SUBMITTED
     |
     v
REVISION_REQUIRED
```

6. System gửi Notification cho Student.

## Postconditions

Student nhận yêu cầu chỉnh sửa.

---

# 14.19 UC-LC-17: Phê duyệt Milestone

## Mục tiêu

Cho phép Lecturer xác nhận Milestone đạt yêu cầu.

## Actor

Lecturer.

## Preconditions

- Milestone đang `SUBMITTED`.

## Main Flow

1. Lecturer xem kết quả.
2. Lecturer chọn `Approve`.
3. System lưu kết quả đánh giá.
4. System chuyển trạng thái:

```text
SUBMITTED
     |
     v
APPROVED
```

5. System gửi Notification cho Student.
6. System cập nhật Progress của Project.

## Postconditions

Milestone hoàn thành.

---

# 14.20 UC-LC-18: Đánh giá Project

## Mục tiêu

Cho phép Lecturer đánh giá kết quả cuối cùng của Project.

## Actor

Lecturer.

## Preconditions

- Các Milestone bắt buộc đã được xử lý.
- Project đủ điều kiện đánh giá.

## Main Flow

1. Lecturer mở Project.
2. Lecturer xem tổng quan Project.
3. Lecturer xem các Milestone.
4. Lecturer xem Progress.
5. Lecturer xem GitHub nếu có.
6. Lecturer xem AI Analysis nếu có.
7. Lecturer nhập Evaluation.
8. Lecturer xác nhận kết quả.
9. System lưu Evaluation.
10. Nếu đạt yêu cầu, Project có thể chuyển:

```text
IN_PROGRESS
      |
      v
COMPLETED
```

## Postconditions

Evaluation cuối cùng được lưu.

Lecturer là người đưa ra quyết định cuối cùng.

---

# 14.21 UC-LC-19: Xem GitHub Repository

## Mục tiêu

Cho phép Lecturer xem repository được liên kết với Project.

## Actor

Lecturer.

## Preconditions

- Project có GitHub Repository.

## Main Flow

1. Lecturer mở Project.
2. Lecturer chọn GitHub Integration.
3. System lấy thông tin repository.
4. System hiển thị thông tin phù hợp.
5. Lecturer có thể xem source code hoặc metadata theo quyền được cấp.

## Postconditions

Lecturer có thể sử dụng thông tin GitHub để theo dõi Project.

---

# 14.22 UC-LC-20: Xem AI Analysis

## Mục tiêu

Cho phép Lecturer xem báo cáo phân tích source code.

## Actor

Lecturer.

## Preconditions

- Project đã được liên kết GitHub.
- AI Analysis đã được thực hiện.

## Main Flow

1. Lecturer mở Project.
2. Lecturer chọn `AI Analysis`.
3. System lấy báo cáo.
4. System hiển thị kết quả.
5. Lecturer sử dụng kết quả như thông tin hỗ trợ đánh giá.

AI có thể cung cấp:

- Code structure.
- Complexity indicators.
- Một số lỗi hoặc vấn đề có thể phát hiện.
- Code quality indicators.
- Đề xuất cải thiện.

## Postconditions

Lecturer có thêm thông tin hỗ trợ đánh giá.

AI không tự động quyết định kết quả Evaluation.

---

# 14.23 UC-LC-21: Xem Notification

## Mục tiêu

Cho phép Lecturer xem các thông báo liên quan đến công việc hướng dẫn.

## Actor

Lecturer.

## Main Flow

Lecturer có thể nhận:

- Student gửi Lecturer Request.
- Student đề xuất Topic.
- Student submit Milestone.
- Student submit lại Milestone.
- Deadline sắp đến.
- Milestone quá hạn.
- AI Analysis hoàn thành.
- Các thông báo hệ thống khác.

Lecturer có thể mở và đánh dấu notification đã đọc.

---

# 14.24 UC-LC-22: Xem Dashboard

## Mục tiêu

Cho phép Lecturer theo dõi tổng quan các Project đang phụ trách.

## Actor

Lecturer.

## Main Flow

System hiển thị:

```text
Tổng số Student
Tổng số Project
Project đang thực hiện
Project hoàn thành
Milestone đang thực hiện
Milestone chờ đánh giá
Milestone quá hạn
Deadline gần nhất
```

Lecturer có thể chọn từng Project để xem chi tiết.

---

# 14.25 UC-LC-23: Xem thống kê

## Mục tiêu

Cho phép Lecturer xem thống kê liên quan đến các Project phụ trách.

## Actor

Lecturer.

## Main Flow

System có thể cung cấp:

- Số lượng Student.
- Số lượng Project.
- Tỷ lệ hoàn thành Milestone.
- Số Milestone quá hạn.
- Số Milestone cần chỉnh sửa.
- Tiến độ theo thời gian.

---

# 14.26 Quyền của Lecturer

| Chức năng | Lecturer |
|---|---|
| Đăng nhập | Có |
| Xem Topic | Có |
| Tạo Topic | Có |
| Đề xuất Topic | Không |
| Duyệt Topic Proposal | Có |
| Từ chối Topic Proposal | Có |
| Đăng ký Topic | Theo vai trò Student |
| Xem Lecturer Request | Có |
| Chấp nhận Request | Có |
| Từ chối Request | Có |
| Thiết lập Capacity | Có |
| Xem Capacity | Có |
| Tạo Project | Theo quy trình chấp nhận |
| Xem Project | Có |
| Tạo Milestone | Có |
| Cập nhật Milestone | Có |
| Theo dõi Progress | Có |
| Submit Milestone | Không |
| Yêu cầu Revision | Có |
| Approve Milestone | Có |
| Đánh giá Project | Có |
| Xem GitHub | Có |
| Xem AI Analysis | Có |
| Đánh giá bằng AI thay thế | Không |
| Xem Notification | Có |
| Xem Dashboard | Có |
| Xem Statistics | Có |

---

# 14.27 Lecturer Use Case Flow tổng quát

```text
                    Lecturer
                       |
                       v
                   Đăng nhập
                       |
             +---------+---------+
             |                   |
             v                   v
       Quản lý Topic       Quản lý Student
             |                   |
       +-----+-----+             |
       |           |             |
       v           v             |
   Tạo Topic   Duyệt Proposal   |
                   |             |
                   +------+------+
                          |
                          v
                  Xử lý Request
                          |
                   +------+------+
                   |             |
                   v             v
               ACCEPTED       REJECTED
                   |
                   v
              Tạo Project
                   |
                   v
             Tạo Milestone
                   |
                   v
            Theo dõi Progress
                   |
                   v
             Student Submit
                   |
                   v
              Đánh giá
                   |
             +-----+-----+
             |           |
             v           v
          APPROVE      REVISION
             |           |
             |           v
             |       Student sửa
             |           |
             |           v
             |       Submit lại
             |
             v
        Hoàn thành Project
```

---

# 14.28 Nguyên tắc

1. Lecturer chỉ được quản lý các Topic và Project thuộc phạm vi quyền của mình.
2. Lecturer không được tự ý thay đổi dữ liệu của Lecturer khác nếu không có quyền Admin.
3. Capacity phải được kiểm tra ở phía Server.
4. Việc Accept Lecturer Request phải đảm bảo tính nhất quán về số lượng Student.
5. Lecturer là người quyết định cuối cùng đối với Evaluation.
6. AI Analysis chỉ cung cấp thông tin hỗ trợ.
7. Lecturer có thể yêu cầu Student chỉnh sửa Milestone.
8. Lecturer chỉ Approve Milestone khi kết quả đáp ứng yêu cầu.
9. Mọi thao tác quan trọng phải được kiểm tra authorization.
10. Các trạng thái Project và Milestone phải tuân thủ state transition đã định nghĩa.

---

# 14.29 Kết luận

Lecturer là Actor chịu trách nhiệm chính trong việc quản lý và đánh giá Project.

Luồng chính:

```text
Quản lý Topic
      ↓
Xử lý Topic Proposal
      ↓
Xử lý Lecturer Request
      ↓
Kiểm soát Capacity
      ↓
Quản lý Project
      ↓
Tạo Milestone
      ↓
Theo dõi Progress
      ↓
Đánh giá Milestone
      ↓
Revision hoặc Approve
      ↓
Đánh giá Project
      ↓
Xác nhận hoàn thành
```

Các Use Case này là cơ sở để thiết kế API, giao diện Lecturer và module quản lý Project trong các giai đoạn triển khai tiếp theo.
