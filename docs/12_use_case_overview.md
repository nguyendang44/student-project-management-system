# 12. Use Case Overview

## 12.1 Mục tiêu

Tài liệu này tổng hợp các Use Case chính của hệ thống quản lý đề tài và theo dõi tiến độ dự án sinh viên.

Use Case được xây dựng dựa trên:

- Các Actor của hệ thống.
- Functional Requirements đã xác định ở Tuần 1.
- Quy trình nghiệp vụ của hệ thống.
- Phạm vi MVP.

Mục tiêu là xác định rõ:

- Ai sử dụng hệ thống.
- Người dùng thực hiện được những chức năng nào.
- Quan hệ giữa các chức năng.
- Phạm vi chức năng cần triển khai.

---

## 12.2 Actors

Hệ thống có 3 Actor chính:

| Actor | Mô tả |
|---|---|
| Student | Sinh viên đăng ký đề tài, đăng ký giảng viên, thực hiện dự án và cập nhật tiến độ. |
| Lecturer | Giảng viên quản lý đề tài, tiếp nhận sinh viên, quản lý dự án, milestone và đánh giá kết quả. |
| Admin | Quản trị viên quản lý người dùng, dữ liệu hệ thống và các cấu hình cần thiết. |

Ngoài 3 Actor chính, hệ thống có một số thành phần hỗ trợ hoạt động tự động:

| Thành phần | Vai trò |
|---|---|
| Scheduled Job | Kiểm tra deadline, cập nhật trạng thái và tạo notification theo lịch. |
| GitHub | Cung cấp repository và source code phục vụ theo dõi và phân tích. |
| AI Analysis Service | Phân tích source code và tạo báo cáo hỗ trợ giảng viên đánh giá. |

Scheduled Job, GitHub và AI Analysis Service là các thành phần hệ thống/tích hợp, không phải người dùng trực tiếp.

---

## 12.3 Use Case tổng quát

Các Use Case chính của hệ thống:

### Authentication & Authorization

- UC-01: Đăng nhập
- UC-02: Đăng xuất
- UC-03: Phân quyền người dùng

### User Management

- UC-04: Quản lý sinh viên
- UC-05: Quản lý giảng viên

### Topic Management

- UC-06: Xem danh sách đề tài
- UC-07: Tạo đề tài
- UC-08: Đề xuất đề tài
- UC-09: Duyệt đề xuất đề tài
- UC-10: Đăng ký đề tài

### Lecturer Registration

- UC-11: Gửi yêu cầu đăng ký giảng viên
- UC-12: Xem yêu cầu đăng ký
- UC-13: Chấp nhận yêu cầu
- UC-14: Từ chối yêu cầu

### Lecturer Capacity

- UC-15: Thiết lập số lượng sinh viên tối đa
- UC-16: Xem tình trạng capacity
- UC-17: Kiểm tra capacity khi đăng ký

### Project Management

- UC-18: Tạo project
- UC-19: Xem thông tin project
- UC-20: Cập nhật project
- UC-21: Hoàn thành project
- UC-22: Hủy project

### Milestone & Progress

- UC-23: Tạo milestone
- UC-24: Cập nhật milestone
- UC-25: Cập nhật tiến độ
- UC-26: Nộp kết quả milestone
- UC-27: Yêu cầu chỉnh sửa milestone
- UC-28: Phê duyệt milestone

### Evaluation

- UC-29: Đánh giá kết quả
- UC-30: Xem kết quả đánh giá

### GitHub Integration

- UC-31: Liên kết GitHub repository
- UC-32: Đồng bộ thông tin repository
- UC-33: Xem thông tin source code

### AI Analysis

- UC-34: Yêu cầu phân tích source code
- UC-35: Phân tích source code
- UC-36: Xem báo cáo AI

### Notification & Automation

- UC-37: Xem notification
- UC-38: Gửi notification
- UC-39: Kiểm tra deadline tự động
- UC-40: Cập nhật trạng thái milestone tự động

### Dashboard & Reporting

- UC-41: Xem dashboard
- UC-42: Xem thống kê
- UC-43: Xem báo cáo tiến độ

---

## 12.4 Use Case theo Actor

### Student

Student có thể:

- Đăng nhập.
- Xem danh sách đề tài.
- Đăng ký đề tài.
- Đề xuất đề tài mới.
- Theo dõi trạng thái đề xuất.
- Gửi yêu cầu đăng ký giảng viên.
- Theo dõi trạng thái yêu cầu.
- Xem project.
- Xem milestone.
- Cập nhật tiến độ.
- Nộp kết quả milestone.
- Liên kết GitHub repository.
- Yêu cầu phân tích source code.
- Xem báo cáo AI.
- Xem notification.
- Xem dashboard tiến độ.

---

### Lecturer

Lecturer có thể:

- Đăng nhập.
- Tạo và quản lý đề tài.
- Xem danh sách đề tài.
- Xem yêu cầu đăng ký.
- Chấp nhận yêu cầu sinh viên.
- Từ chối yêu cầu và nhập lý do.
- Thiết lập số lượng sinh viên tối đa.
- Xem capacity hiện tại.
- Xem danh sách project phụ trách.
- Tạo milestone.
- Theo dõi tiến độ.
- Xem kết quả milestone.
- Yêu cầu sinh viên chỉnh sửa.
- Phê duyệt milestone.
- Đánh giá kết quả project.
- Xem báo cáo AI.
- Xem notification.
- Xem dashboard và thống kê.

---

### Admin

Admin có thể:

- Đăng nhập.
- Quản lý tài khoản sinh viên.
- Quản lý tài khoản giảng viên.
- Quản lý dữ liệu đề tài.
- Xem dữ liệu project.
- Xem thống kê hệ thống.
- Theo dõi trạng thái hoạt động của hệ thống.

---

## 12.5 Use Case quan trọng của MVP

Các Use Case thuộc phạm vi MVP:

| ID | Use Case | Actor chính |
|---|---|---|
| UC-01 | Đăng nhập | Student, Lecturer, Admin |
| UC-03 | Phân quyền | System |
| UC-04 | Quản lý sinh viên | Admin |
| UC-05 | Quản lý giảng viên | Admin |
| UC-06 | Xem danh sách đề tài | Student, Lecturer |
| UC-07 | Tạo đề tài | Lecturer, Admin |
| UC-08 | Đề xuất đề tài | Student |
| UC-09 | Duyệt đề xuất đề tài | Lecturer |
| UC-10 | Đăng ký đề tài | Student |
| UC-11 | Gửi yêu cầu đăng ký giảng viên | Student |
| UC-13 | Chấp nhận yêu cầu | Lecturer |
| UC-14 | Từ chối yêu cầu | Lecturer |
| UC-15 | Thiết lập capacity | Lecturer |
| UC-16 | Xem capacity | Lecturer |
| UC-17 | Kiểm tra capacity | System |
| UC-18 | Tạo project | System |
| UC-19 | Xem project | Student, Lecturer |
| UC-23 | Tạo milestone | Lecturer |
| UC-24 | Cập nhật milestone | Student, Lecturer |
| UC-25 | Cập nhật tiến độ | Student |
| UC-26 | Nộp kết quả milestone | Student |
| UC-27 | Yêu cầu chỉnh sửa | Lecturer |
| UC-28 | Phê duyệt milestone | Lecturer |
| UC-29 | Đánh giá kết quả | Lecturer |
| UC-37 | Xem notification | Student, Lecturer |
| UC-38 | Gửi notification | System |
| UC-39 | Kiểm tra deadline | Scheduled Job |
| UC-40 | Cập nhật trạng thái tự động | Scheduled Job |
| UC-41 | Xem dashboard | Student, Lecturer |
| UC-42 | Xem thống kê | Lecturer, Admin |

---

## 12.6 Use Case mở rộng

Các chức năng sau được xem là mở rộng sau khi MVP ổn định:

- UC-31: Liên kết GitHub repository.
- UC-32: Đồng bộ thông tin repository.
- UC-33: Xem thông tin source code.
- UC-34: Yêu cầu phân tích source code.
- UC-35: Phân tích source code.
- UC-36: Xem báo cáo AI.
- Các chức năng phân tích nâng cao.
- Dự đoán tiến độ dự án.
- Tích hợp các nền tảng source control khác.

AI chỉ cung cấp thông tin phân tích hỗ trợ. Kết quả đánh giá cuối cùng vẫn thuộc về Lecturer.

---

## 12.7 Quan hệ giữa các Use Case

Một số Use Case có quan hệ phụ thuộc:

### Đăng ký giảng viên

```text
Student
   |
   v
Gửi yêu cầu đăng ký giảng viên
   |
   v
Kiểm tra capacity
   |
   +---- FULL ------> Từ chối yêu cầu đăng ký
   |
   +---- AVAILABLE -> Lecturer xem yêu cầu
                         |
                         +--> Chấp nhận
                         |
                         +--> Từ chối
```

---

### Quản lý milestone

```text
Lecturer
   |
   v
Tạo milestone
   |
   v
Student cập nhật tiến độ
   |
   v
Nộp kết quả
   |
   v
Lecturer đánh giá
   |
   +---- Đạt ------> APPROVED
   |
   +---- Chưa đạt -> REVISION_REQUIRED
                         |
                         v
                   Student chỉnh sửa
                         |
                         v
                      Submit
```

---

### Automation

```text
Scheduled Job
      |
      v
Kiểm tra deadline
      |
      +---- Gần deadline
      |        |
      |        v
      |    Notification
      |
      +---- Quá hạn + IN_PROGRESS
               |
               v
            OVERDUE
               |
               v
          Notification
```

---

## 12.8 Nguyên tắc xây dựng Use Case

Các Use Case của hệ thống tuân theo các nguyên tắc:

1. Mỗi Use Case mô tả một mục tiêu cụ thể của Actor.
2. Quyền truy cập phải được kiểm tra theo Role.
3. Các nghiệp vụ quan trọng phải được kiểm tra ở phía Server.
4. Lecturer có quyền quyết định cuối cùng đối với việc chấp nhận sinh viên và đánh giá milestone.
5. Capacity của Lecturer phải được kiểm tra trước khi chấp nhận yêu cầu.
6. Scheduled Job chỉ thực hiện các nghiệp vụ tự động đã được xác định.
7. AI chỉ đóng vai trò hỗ trợ phân tích, không tự động thay thế đánh giá của Lecturer.
8. Các trạng thái của Project và Milestone phải tuân thủ state transition đã định nghĩa ở Tuần 1.
9. Notification cần hạn chế việc tạo thông báo trùng lặp.
10. Các Use Case trong MVP được ưu tiên triển khai trước các chức năng mở rộng.

---

## 12.9 Kết luận

Use Case Overview xác định toàn bộ các chức năng chính của hệ thống và mối quan hệ giữa các Actor với hệ thống.

Phạm vi phát triển được chia thành:

```text
MVP
  |
  +-- Authentication
  +-- Authorization
  +-- Topic Management
  +-- Lecturer Registration
  +-- Lecturer Capacity
  +-- Project Management
  +-- Milestone Management
  +-- Progress Management
  +-- Evaluation
  +-- Notification
  +-- Automation
  +-- Dashboard
  |
  +-- Extension
       |
       +-- GitHub Integration
       +-- AI Source Code Analysis
```

Tài liệu này là cơ sở để xây dựng chi tiết Use Case cho từng Actor trong các tài liệu tiếp theo.
