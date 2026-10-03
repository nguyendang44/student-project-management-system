# 9. Project Status

## 9.1. Mục tiêu

Project Status dùng để biểu diễn trạng thái hiện tại của một Project trong quá trình thực hiện.

Trạng thái giúp Student, Lecturer và Admin:

- Biết Project đang ở giai đoạn nào.
- Xác định các thao tác được phép thực hiện.
- Theo dõi vòng đời của Project.
- Kiểm soát các bước chuyển trạng thái.
- Đảm bảo dữ liệu Project nhất quán.

---

## 9.2. Các trạng thái của Project

Project sử dụng các trạng thái chính:

```text
DRAFT → REGISTERED → IN_PROGRESS → COMPLETED
```

Project cũng có thể được hủy trong quá trình thực hiện:

```text
IN_PROGRESS → CANCELLED
```

| Status | Ý nghĩa |
|---|---|
| `DRAFT` | Project mới được tạo nhưng chưa hoàn tất đăng ký. |
| `REGISTERED` | Project đã được đăng ký thành công và đủ điều kiện bắt đầu. |
| `IN_PROGRESS` | Project đang được thực hiện. |
| `COMPLETED` | Project đã hoàn thành và được Lecturer xác nhận. |
| `CANCELLED` | Project đã bị hủy và không tiếp tục thực hiện. |

`REJECTED` không phải là trạng thái của Project.

Trạng thái `REJECTED` được sử dụng cho các đối tượng như Topic Proposal hoặc Lecturer Request.

---

## 9.3. DRAFT

`DRAFT` là trạng thái ban đầu của Project.

Project có thể ở trạng thái `DRAFT` khi thông tin Project đang được tạo hoặc chưa hoàn tất quá trình đăng ký.

Các thông tin có thể được thiết lập:

- Student thực hiện Project.
- Lecturer hướng dẫn.
- Topic của Project.
- Thông tin mô tả Project.
- Thời gian thực hiện.

Project chưa được xem là đang thực hiện chính thức ở trạng thái này.

Quy trình:

```text
DRAFT
  |
  | Hoàn tất thông tin và đăng ký
  v
REGISTERED
```

---

## 9.4. REGISTERED

Project chuyển sang `REGISTERED` khi quá trình đăng ký đã hoàn tất.

Điều kiện:

- Student đã đăng ký Lecturer.
- Lecturer đã chấp nhận Request.
- Lecturer vẫn còn Capacity phù hợp.
- Thông tin Student, Lecturer và Topic hợp lệ.

Quy trình:

```text
Student
   |
   v
Đăng ký Lecturer
   |
   v
Lecturer Accept
   |
   v
REGISTERED
```

`REGISTERED` biểu thị rằng Project đã được xác nhận về mặt đăng ký nhưng chưa nhất thiết đã bắt đầu thực hiện.

---

## 9.5. IN_PROGRESS

Project chuyển sang `IN_PROGRESS` khi Project bắt đầu được thực hiện.

Ở trạng thái này:

- Lecturer có thể tạo và quản lý Milestone.
- Student thực hiện các Milestone.
- Student cập nhật Progress.
- Student nộp kết quả.
- Lecturer đánh giá Milestone.
- Hệ thống theo dõi Deadline.
- Hệ thống gửi Notification khi cần thiết.

Quy trình:

```text
REGISTERED
    |
    v
IN_PROGRESS
    |
    +----> Milestone 1
    |
    +----> Milestone 2
    |
    +----> Milestone 3
```

Project có thể tiếp tục ở `IN_PROGRESS` trong thời gian thực hiện, kể cả khi một Milestone bị `OVERDUE`.

`OVERDUE` là trạng thái của Milestone, không phải trạng thái của Project.

---

## 9.6. COMPLETED

Project chuyển sang `COMPLETED` khi tất cả Milestone bắt buộc đã được hoàn thành và Lecturer xác nhận Project hoàn tất.

Điều kiện:

- Các Milestone bắt buộc đã ở trạng thái `APPROVED`.
- Student đã hoàn thành các yêu cầu của Project.
- Không còn Milestone bắt buộc cần chỉnh sửa.
- Lecturer xác nhận hoàn thành Project.

Quy trình:

```text
IN_PROGRESS
    |
    v
Kiểm tra Milestones
    |
    v
Tất cả Milestone = APPROVED
    |
    v
Lecturer xác nhận
    |
    v
COMPLETED
```

Scheduled Job có thể kiểm tra xem Project đã đủ điều kiện hoàn thành hay chưa.

Tuy nhiên, Scheduled Job không tự động thay thế bước xác nhận cuối cùng của Lecturer.

---

## 9.7. CANCELLED

Project có thể chuyển sang `CANCELLED` khi Project không tiếp tục được thực hiện.

Một số trường hợp:

- Student hoặc Lecturer đề nghị hủy Project.
- Project không thể tiếp tục thực hiện.
- Admin thực hiện hủy theo quy trình của hệ thống.

Quy trình:

```text
IN_PROGRESS
     |
     v
Yêu cầu hủy Project
     |
     v
Xác nhận hủy
     |
     v
CANCELLED
```

Khi Project ở trạng thái `CANCELLED`:

- Student không được tiếp tục cập nhật Progress.
- Student không được Submit Milestone mới.
- Lecturer không được tiếp tục đánh giá Milestone mới.
- Các thao tác tiếp theo chỉ được thực hiện nếu hệ thống có quy trình mở lại Project.

---

## 9.8. Quy tắc chuyển trạng thái

| Trạng thái hiện tại | Sự kiện | Trạng thái tiếp theo |
|---|---|---|
| `DRAFT` | Hoàn tất đăng ký | `REGISTERED` |
| `REGISTERED` | Bắt đầu thực hiện | `IN_PROGRESS` |
| `IN_PROGRESS` | Tất cả Milestone được duyệt và Lecturer xác nhận | `COMPLETED` |
| `IN_PROGRESS` | Project bị hủy và được xác nhận | `CANCELLED` |

Các chuyển đổi không hợp lệ:

```text
COMPLETED → IN_PROGRESS
COMPLETED → CANCELLED
CANCELLED → IN_PROGRESS
```

Nếu cần mở lại Project sau khi `CANCELLED`, hệ thống cần có một quy trình nghiệp vụ riêng.

Không sử dụng `REJECTED` làm trạng thái của Project.

Việc từ chối được xử lý ở các đối tượng liên quan:

- Topic Proposal có thể có trạng thái `REJECTED`.
- Lecturer Request có thể có trạng thái `REJECTED`.

---

## 9.9. Project Status và Milestone Status

Project Status và Milestone Status được quản lý độc lập nhưng có quan hệ với nhau.

Ví dụ:

```text
                  PROJECT
                     |
                     v
               IN_PROGRESS
                     |
           +---------+---------+
           |         |         |
           v         v         v
         MS-01     MS-02     MS-03
           |         |         |
           v         v         v
       APPROVED   APPROVED   APPROVED
           \         |         /
            \        |        /
             +-------+-------+
                     |
                     v
            Lecturer xác nhận
                     |
                     v
                COMPLETED
```

Project chỉ được xem xét hoàn thành khi các Milestone bắt buộc đã được xử lý đầy đủ.

Ví dụ:

```text
MS-01 = APPROVED
MS-02 = APPROVED
MS-03 = IN_PROGRESS

        ↓

Project = IN_PROGRESS
```

Khi:

```text
MS-01 = APPROVED
MS-02 = APPROVED
MS-03 = APPROVED

        ↓

Project đủ điều kiện hoàn thành
        ↓
Lecturer xác nhận
        ↓
Project = COMPLETED
```

---

## 9.10. Tổng quan vòng đời Project

```text
             +--------+
             | DRAFT  |
             +---+----+
                 |
                 | Đăng ký hoàn tất
                 v
         +---------------+
         |  REGISTERED   |
         +-------+-------+
                 |
                 | Bắt đầu thực hiện
                 v
         +---------------+
         | IN_PROGRESS   |
         +-------+-------+
            |          |
            |          |
     Hủy Project       | Tất cả Milestone
            |          | được APPROVED
            v          v
      +-----------+  Lecturer xác nhận
      | CANCELLED |          |
      +-----------+          v
                       +-------------+
                       | COMPLETED   |
                       +-------------+
```

---

## 9.11. Project Status và Automation

Scheduled Job có thể hỗ trợ kiểm tra trạng thái Project.

Quy trình:

```text
Scheduled Job
      |
      v
Project đang IN_PROGRESS?
      |
      v
Kiểm tra Milestone
      |
      +-------------------------+
      |                         |
      v                         v
Còn Milestone chưa        Tất cả APPROVED
APPROVED                       |
      |                         v
      v                  Đủ điều kiện hoàn thành
IN_PROGRESS                     |
                                v
                         Lecturer xác nhận
                                |
                                v
                            COMPLETED
```

Scheduled Job chỉ kiểm tra điều kiện và thông báo cho Lecturer.

Không tự động chuyển Project sang `COMPLETED` nếu chưa có xác nhận của Lecturer.

---

## 9.12. Quyền thao tác theo Project Status

| Project Status | Student | Lecturer | Admin |
|---|---|---|---|
| `DRAFT` | Xem thông tin phù hợp | Quản lý theo quyền | Quản lý theo quyền |
| `REGISTERED` | Xem Project | Quản lý Project | Theo dõi |
| `IN_PROGRESS` | Cập nhật Progress, Submit Milestone | Quản lý, theo dõi và đánh giá | Theo dõi |
| `COMPLETED` | Xem kết quả | Xem và quản lý theo quyền | Theo dõi |
| `CANCELLED` | Chỉ xem | Chỉ xem hoặc xử lý theo quy trình | Theo dõi / quản lý |

Quyền thực tế phải được kiểm tra ở phía Server.

---

## 9.13. Nguyên tắc

- Project phải có trạng thái rõ ràng trong toàn bộ vòng đời.
- Chỉ các trạng thái hợp lệ mới được phép chuyển đổi.
- Việc chuyển trạng thái phải được kiểm tra ở phía Server.
- Project `COMPLETED` không được tiếp tục cập nhật Milestone.
- Project `CANCELLED` không được tiếp tục thực hiện nếu chưa được mở lại theo quy trình.
- `OVERDUE` chỉ là trạng thái của Milestone, không phải Project.
- Scheduled Job chỉ hỗ trợ kiểm tra điều kiện và cập nhật các trạng thái được phép tự động.
- Scheduled Job không được thay thế quyết định nghiệp vụ của Lecturer.
- Lecturer chịu trách nhiệm xác nhận Project hoàn thành cuối cùng.
- Các thay đổi trạng thái quan trọng cần được ghi lại để phục vụ Audit.

---

## 9.14. Kết luận

Project Status giúp hệ thống quản lý rõ ràng vòng đời của một Project.

Các trạng thái chính:

```text
DRAFT
  ↓
REGISTERED
  ↓
IN_PROGRESS
  ↓
COMPLETED
```

Ngoài ra:

```text
IN_PROGRESS
      ↓
  CANCELLED
```

Project Status được quản lý độc lập với Milestone Status nhưng có quan hệ về mặt nghiệp vụ.

Project chỉ được chuyển sang `COMPLETED` sau khi các Milestone bắt buộc đã `APPROVED` và Lecturer xác nhận hoàn thành.
