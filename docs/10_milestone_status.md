# 10. Milestone Status

## 10.1. Mục tiêu

Milestone Status dùng để biểu diễn trạng thái của từng Milestone trong quá trình thực hiện Project.

Trạng thái Milestone giúp Student và Lecturer:

- Theo dõi tiến độ từng công việc.
- Theo dõi Deadline.
- Biết Milestone đang chờ thực hiện, chờ đánh giá hay cần chỉnh sửa.
- Xác định các thao tác tiếp theo.
- Theo dõi kết quả của từng Milestone.

Milestone sử dụng các trạng thái chính:

```text
PENDING → IN_PROGRESS → SUBMITTED → APPROVED
                              |
                              v
                       REVISION_REQUIRED
                              |
                              v
                         IN_PROGRESS
```

Ngoài ra, Milestone có thể chuyển sang `OVERDUE` khi Student chưa Submit và đã quá Deadline:

```text
IN_PROGRESS → OVERDUE
```

---

## 10.2. Các trạng thái của Milestone

| Status | Ý nghĩa |
|---|---|
| `PENDING` | Milestone đã được tạo nhưng chưa bắt đầu thực hiện. |
| `IN_PROGRESS` | Student đang thực hiện Milestone. |
| `SUBMITTED` | Student đã nộp kết quả và đang chờ Lecturer đánh giá. |
| `REVISION_REQUIRED` | Lecturer yêu cầu Student chỉnh sửa và nộp lại. |
| `APPROVED` | Lecturer đã phê duyệt Milestone. |
| `OVERDUE` | Milestone chưa được Submit và đã quá Deadline. |

---

## 10.3. PENDING

`PENDING` là trạng thái ban đầu của Milestone.

Milestone ở trạng thái này khi:

- Milestone đã được Lecturer tạo.
- Student chưa bắt đầu thực hiện.
- Milestone chưa được Submit.

Khi Student bắt đầu thực hiện Milestone, trạng thái chuyển sang `IN_PROGRESS`.

Quy trình:

```text
PENDING
   |
   | Student bắt đầu
   v
IN_PROGRESS
```

---

## 10.4. IN_PROGRESS

`IN_PROGRESS` thể hiện Student đang thực hiện Milestone.

Trong trạng thái này:

- Student có thể cập nhật Progress.
- Student có thể cập nhật nội dung thực hiện.
- Student có thể liên kết Source Code hoặc GitHub Repository nếu cần.
- Hệ thống theo dõi Deadline.
- Student có thể Submit kết quả khi hoàn thành.

Nếu Student Submit kết quả:

```text
IN_PROGRESS
     |
     | Student Submit
     v
SUBMITTED
```

Nếu Milestone quá Deadline nhưng chưa Submit:

```text
IN_PROGRESS
     |
     | Quá Deadline
     v
OVERDUE
```

Scheduled Job chịu trách nhiệm kiểm tra điều kiện Deadline và thực hiện chuyển trạng thái `IN_PROGRESS → OVERDUE`.

---

## 10.5. SUBMITTED

`SUBMITTED` thể hiện Student đã nộp kết quả của Milestone và đang chờ Lecturer đánh giá.

Ở trạng thái này:

- Student không tiếp tục chỉnh sửa kết quả nếu chưa được yêu cầu.
- Lecturer có thể xem kết quả.
- Lecturer có thể đánh giá Milestone.
- Hệ thống không chuyển Milestone sang `OVERDUE`.

Lecturer có hai lựa chọn:

```text
                 SUBMITTED
                /         \
               /           \
              v             v
         APPROVED     REVISION_REQUIRED
```

Nếu kết quả đạt yêu cầu:

```text
SUBMITTED → APPROVED
```

Nếu cần chỉnh sửa:

```text
SUBMITTED → REVISION_REQUIRED
```

---

## 10.6. REVISION_REQUIRED

`REVISION_REQUIRED` được sử dụng khi Lecturer đánh giá kết quả và yêu cầu Student chỉnh sửa.

Lecturer có thể cung cấp:

- Nội dung cần chỉnh sửa.
- Nhận xét.
- Yêu cầu bổ sung.
- Thời hạn chỉnh sửa nếu cần.

Student sau đó tiếp tục thực hiện và Submit lại Milestone.

Quy trình:

```text
SUBMITTED
    |
    | Lecturer yêu cầu chỉnh sửa
    v
REVISION_REQUIRED
    |
    | Student bắt đầu chỉnh sửa
    v
IN_PROGRESS
    |
    | Submit lại
    v
SUBMITTED
```

`REVISION_REQUIRED` không tự động chuyển sang `OVERDUE` bởi Scheduled Job.

Nếu Project có quy định Deadline riêng cho việc chỉnh sửa, hệ thống có thể lưu Deadline đó như một thuộc tính thời gian và xử lý theo quy tắc nghiệp vụ riêng.

---

## 10.7. APPROVED

`APPROVED` là trạng thái Milestone đã được Lecturer phê duyệt.

Điều kiện:

- Student đã Submit kết quả.
- Lecturer đã kiểm tra kết quả.
- Lecturer xác nhận Milestone đạt yêu cầu.

Quy trình:

```text
SUBMITTED
    |
    | Lecturer Approve
    v
APPROVED
```

Milestone ở trạng thái `APPROVED` được xem là đã hoàn thành.

Scheduled Job không thay đổi trạng thái `APPROVED`.

Milestone `APPROVED` không được tiếp tục chỉnh sửa, trừ khi hệ thống có quy trình đặc biệt cho phép mở lại.

---

## 10.8. OVERDUE

`OVERDUE` thể hiện Milestone đã quá Deadline nhưng Student chưa Submit kết quả.

Scheduled Job định kỳ kiểm tra Deadline và chuyển:

```text
IN_PROGRESS
     |
     | Quá Deadline và chưa Submit
     v
OVERDUE
```

Khi Milestone `OVERDUE`, hệ thống có thể:

- Gửi Notification cho Student.
- Gửi Notification cho Lecturer.
- Ghi nhận Milestone đã quá hạn.
- Hiển thị cảnh báo trên Dashboard.
- Cho phép Student tiếp tục thực hiện và Submit kết quả.

Sau khi Student Submit:

```text
OVERDUE
    |
    | Student Submit
    v
SUBMITTED
    |
    | Lecturer Review
    v
APPROVED
```

Hoặc Lecturer có thể yêu cầu chỉnh sửa:

```text
OVERDUE
    |
    | Student Submit
    v
SUBMITTED
    |
    | Lecturer yêu cầu chỉnh sửa
    v
REVISION_REQUIRED
    |
    | Student chỉnh sửa
    v
IN_PROGRESS
```

`OVERDUE` không có nghĩa là Milestone bị hủy hoặc bị từ chối.

---

## 10.9. Quy tắc chuyển trạng thái

| Trạng thái hiện tại | Sự kiện | Trạng thái tiếp theo |
|---|---|---|
| `PENDING` | Student bắt đầu | `IN_PROGRESS` |
| `IN_PROGRESS` | Student Submit | `SUBMITTED` |
| `IN_PROGRESS` | Quá Deadline và chưa Submit | `OVERDUE` |
| `SUBMITTED` | Lecturer Approve | `APPROVED` |
| `SUBMITTED` | Lecturer yêu cầu chỉnh sửa | `REVISION_REQUIRED` |
| `REVISION_REQUIRED` | Student bắt đầu chỉnh sửa | `IN_PROGRESS` |
| `OVERDUE` | Student Submit | `SUBMITTED` |

Các chuyển đổi không hợp lệ cần được ngăn ở phía Server.

Ví dụ:

```text
PENDING → APPROVED
PENDING → SUBMITTED
SUBMITTED → IN_PROGRESS
APPROVED → IN_PROGRESS
APPROVED → OVERDUE
```

không được phép nếu không có quy trình nghiệp vụ đặc biệt.

---

## 10.10. Không chuyển OVERDUE trong một số trường hợp

Scheduled Job chỉ chuyển Milestone sang `OVERDUE` khi đồng thời thỏa mãn:

```text
Status = IN_PROGRESS
AND
Current Time > Deadline
AND
Milestone chưa được Submit
```

Các trạng thái sau không tự động chuyển sang `OVERDUE`:

- `PENDING`
- `SUBMITTED`
- `REVISION_REQUIRED`
- `APPROVED`

Đặc biệt:

- `SUBMITTED` nghĩa là Student đã nộp và đang chờ Lecturer đánh giá.
- `REVISION_REQUIRED` nghĩa là Lecturer đã yêu cầu chỉnh sửa.
- `APPROVED` nghĩa là Milestone đã hoàn thành.
- `PENDING` nghĩa là Milestone chưa bắt đầu.

---

## 10.11. Deadline và Status

Deadline là một thuộc tính thời gian của Milestone và không phải là một Status.

Ví dụ:

```text
Milestone
   |
   +-- status: IN_PROGRESS
   |
   +-- start_date: 2026-10-01
   |
   +-- deadline: 2026-10-15
```

Không sử dụng `DEADLINE` làm trạng thái của Milestone.

Khi Deadline đã qua, hệ thống dựa trên Status hiện tại để quyết định có chuyển sang `OVERDUE` hay không.

Ví dụ:

```text
status = IN_PROGRESS
deadline = 2026-10-15
current_time > deadline
```

→ có thể chuyển:

```text
IN_PROGRESS → OVERDUE
```

Trong khi:

```text
status = SUBMITTED
deadline = 2026-10-15
current_time > deadline
```

→ không chuyển sang `OVERDUE`.

---

## 10.12. Quan hệ giữa Milestone và Project

Milestone là thành phần dùng để theo dõi tiến độ của Project.

Một Project có thể có nhiều Milestone:

```text
Project
   |
   +---- Milestone 1
   |
   +---- Milestone 2
   |
   +---- Milestone 3
   |
   +---- Milestone 4
```

Khi tất cả Milestone bắt buộc đều ở trạng thái `APPROVED`, Project có thể đủ điều kiện để Lecturer xác nhận hoàn thành.

```text
Milestone 1 → APPROVED
Milestone 2 → APPROVED
Milestone 3 → APPROVED
Milestone 4 → APPROVED
        |
        v
Tất cả Milestone hoàn thành
        |
        v
Lecturer xác nhận
        |
        v
Project COMPLETED
```

Scheduled Job có thể kiểm tra điều kiện này nhưng không thay thế xác nhận cuối cùng của Lecturer.

---

## 10.13. Quyền thao tác theo Milestone Status

| Milestone Status | Student | Lecturer |
|---|---|---|
| `PENDING` | Bắt đầu thực hiện | Xem và quản lý |
| `IN_PROGRESS` | Cập nhật Progress, Submit | Theo dõi |
| `SUBMITTED` | Chờ đánh giá | Xem và đánh giá |
| `REVISION_REQUIRED` | Chỉnh sửa và Submit lại | Theo dõi |
| `APPROVED` | Xem kết quả | Xem kết quả |
| `OVERDUE` | Tiếp tục thực hiện và Submit | Theo dõi, nhắc nhở |

Quyền thực tế phải được kiểm tra ở phía Server.

---

## 10.14. Tổng quan vòng đời Milestone

```text
                +---------+
                | PENDING |
                +----+----+
                     |
                     | Student bắt đầu
                     v
              +-------------+
              | IN_PROGRESS |
              +------+------+
                 |         |
                 |         | Quá Deadline
                 |         | và chưa Submit
                 |         v
                 |     +---------+
                 |     | OVERDUE |
                 |     +----+----+
                 |          |
                 |          | Student Submit
                 |          v
                 |     +-----------+
                 +---->| SUBMITTED |
                       +-----+-----+
                         |       |
                         |       |
                    Approve   Revision
                         |       |
                         v       v
                   +---------+ +------------------+
                   | APPROVED| | REVISION_REQUIRED|
                   +---------+ +--------+---------+
                                         |
                                         | Chỉnh sửa
                                         v
                                    IN_PROGRESS
```

---

## 10.15. Nguyên tắc

- Mỗi Milestone phải có một Status xác định.
- Chỉ được chuyển sang các Status hợp lệ.
- Việc chuyển Status phải được kiểm tra ở phía Server.
- Scheduled Job chỉ tự động chuyển `IN_PROGRESS` sang `OVERDUE` khi đã quá Deadline và chưa Submit.
- `SUBMITTED` không được tự động chuyển sang `OVERDUE`.
- `REVISION_REQUIRED` không được tự động chuyển sang `OVERDUE`.
- `APPROVED` là trạng thái hoàn thành của Milestone.
- Lecturer chịu trách nhiệm đánh giá và phê duyệt Milestone.
- Student chịu trách nhiệm cập nhật Progress và Submit kết quả.
- Deadline được lưu dưới dạng ngày/giờ và không được sử dụng như một Status.
- `OVERDUE` không đồng nghĩa với `REJECTED`.
- Milestone `APPROVED` không được tiếp tục chỉnh sửa nếu không có quy trình mở lại.
- Các thay đổi Status quan trọng nên được ghi nhận để phục vụ Audit.

---

## 10.16. Kết luận

Milestone Status giúp hệ thống quản lý rõ ràng vòng đời của từng Milestone:

```text
PENDING
   ↓
IN_PROGRESS
   ↓
SUBMITTED
   ↓
APPROVED
```

Khi cần chỉnh sửa:

```text
SUBMITTED
   ↓
REVISION_REQUIRED
   ↓
IN_PROGRESS
   ↓
SUBMITTED
```

Khi quá Deadline trước khi Submit:

```text
IN_PROGRESS
   ↓
OVERDUE
   ↓
SUBMITTED
```

Milestone Status có quan hệ trực tiếp với Project Status. Khi tất cả Milestone bắt buộc đạt `APPROVED`, Project có thể đủ điều kiện chuyển sang `COMPLETED` sau khi Lecturer xác nhận.
