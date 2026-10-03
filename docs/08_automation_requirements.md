\# 8. Automation Requirements



\## 8.1. Mục tiêu



Hệ thống cần tự động hóa các công việc có tính lặp lại trong quá trình quản lý project, đặc biệt là kiểm tra deadline, cập nhật trạng thái và gửi notification.



Việc tự động hóa giúp giảm công việc thủ công cho Admin và Lecturer, đồng thời giúp Student nhận được thông tin kịp thời.



Các chức năng automation chính:



\- Kiểm tra deadline của milestone.

\- Cập nhật trạng thái milestone khi quá hạn.

\- Kiểm tra điều kiện hoàn thành project.

\- Tạo và gửi notification liên quan đến deadline.

\- Hỗ trợ xử lý các tác vụ định kỳ.



\## 8.2. Scheduled Job



Hệ thống sử dụng Scheduled Job để thực hiện các tác vụ tự động theo một khoảng thời gian định trước.



Scheduled Job có thể được chạy theo chu kỳ, ví dụ:



\- Mỗi ngày.

\- Mỗi giờ.

\- Hoặc theo khoảng thời gian được cấu hình bởi hệ thống.



Các tác vụ chính:



\- Kiểm tra deadline của milestone.

\- Kiểm tra milestone quá hạn.

\- Tạo notification liên quan đến deadline.

\- Cập nhật trạng thái milestone.

\- Kiểm tra điều kiện hoàn thành project.



Quy trình tổng quát:



```text

Scheduled Job

&#x20;     |

&#x20;     v

Kiểm tra dữ liệu

&#x20;     |

&#x20;     +------------------+

&#x20;     |                  |

&#x20;     v                  v

Milestone           Project

&#x20;     |                  |

&#x20;     v                  v

Deadline Check     Status Check

```



\## 8.3. Kiểm tra Deadline



Hệ thống tự động kiểm tra deadline của các milestone chưa được hoàn thành.



| Thời điểm | Hành động |

|---|---|

| Còn 7 ngày | Gửi reminder |

| Còn 3 ngày | Gửi warning |

| Đến deadline | Kiểm tra trạng thái |

| Quá deadline | Chuyển sang `OVERDUE` nếu chưa submit |



Quy trình:



```text

Milestone

&#x20;   |

&#x20;   v

Kiểm tra deadline

&#x20;   |

&#x20;   +---- Còn 7 ngày ----> Reminder

&#x20;   |

&#x20;   +---- Còn 3 ngày ----> Warning

&#x20;   |

&#x20;   +---- Đến deadline --> Kiểm tra trạng thái

&#x20;   |

&#x20;   +---- Quá deadline --> OVERDUE + Notification

```



Milestone đã ở trạng thái `SUBMITTED`, `APPROVED` hoặc `REVISION\_REQUIRED` không được tự động chuyển sang `OVERDUE`.



\## 8.4. Tự động cập nhật trạng thái Milestone



Scheduled Job định kỳ kiểm tra trạng thái và deadline của milestone.



Các quy tắc xử lý:



\- `PENDING`: giữ nguyên trạng thái.

\- `IN\_PROGRESS` và chưa quá deadline: giữ nguyên `IN\_PROGRESS`.

\- `IN\_PROGRESS` và đã quá deadline: chuyển sang `OVERDUE`.

\- `SUBMITTED`: giữ nguyên `SUBMITTED` và chờ Lecturer đánh giá.

\- `REVISION\_REQUIRED`: giữ nguyên trạng thái để Student chỉnh sửa và submit lại.

\- `APPROVED`: giữ nguyên `APPROVED`.

\- `OVERDUE`: giữ nguyên `OVERDUE` cho đến khi Student submit kết quả.



Luồng xử lý:



```text

&#x20;                IN\_PROGRESS

&#x20;                     |

&#x20;                     v

&#x20;             Kiểm tra deadline

&#x20;                /          \\

&#x20;               /            \\

&#x20;      Chưa quá hạn        Quá deadline

&#x20;           |                   |

&#x20;           v                   v

&#x20;     IN\_PROGRESS            OVERDUE

```



Khi Student submit milestone:



```text

IN\_PROGRESS

&#x20;    |

&#x20;    | Student submit

&#x20;    v

SUBMITTED

&#x20;    |

&#x20;    | Lecturer review

&#x20;    |

&#x20;    +-------------------+

&#x20;    |                   |

&#x20;    v                   v

&#x20;APPROVED       REVISION\_REQUIRED

&#x20;                       |

&#x20;                       v

&#x20;                  Student sửa

&#x20;                       |

&#x20;                       v

&#x20;                   SUBMITTED

```



Scheduled Job không tự động chuyển `SUBMITTED` sang `OVERDUE` vì Student đã hoàn thành bước submit và đang chờ Lecturer đánh giá.



\## 8.5. Kiểm tra trạng thái Project



Scheduled Job có thể kiểm tra trạng thái các milestone thuộc từng project để xác định project đã đủ điều kiện hoàn thành hay chưa.



Các trường hợp:



\- Nếu project đang `IN\_PROGRESS` và vẫn còn milestone chưa hoàn thành, project tiếp tục ở `IN\_PROGRESS`.

\- Nếu tất cả milestone bắt buộc đã ở `APPROVED`, project đủ điều kiện để Lecturer xác nhận hoàn thành.

\- Scheduled Job không tự động thay thế bước xác nhận cuối cùng của Lecturer.

\- Nếu project bị hủy, project chuyển sang `CANCELLED`.



Quy trình:



```text

Project IN\_PROGRESS

&#x20;       |

&#x20;       v

Kiểm tra Milestones

&#x20;       |

&#x20;       +-----------------------------+

&#x20;       |                             |

&#x20;       v                             v

Còn milestone chưa APPROVED      Tất cả APPROVED

&#x20;       |                             |

&#x20;       v                             v

&#x20;  IN\_PROGRESS              Đủ điều kiện hoàn thành

&#x20;                                     |

&#x20;                                     v

&#x20;                             Lecturer xác nhận

&#x20;                                     |

&#x20;                                     v

&#x20;                                 COMPLETED

```



\## 8.6. Tạo Notification tự động



Notification được tạo khi hệ thống phát hiện một sự kiện cần thông báo cho Student hoặc Lecturer.



Các notification liên quan đến automation:



| Sự kiện | Người nhận | Notification |

|---|---|---|

| Milestone còn 7 ngày | Student | Reminder |

| Milestone còn 3 ngày | Student | Warning |

| Milestone quá deadline | Student và Lecturer | Overdue |

| Project đủ điều kiện hoàn thành | Lecturer | Yêu cầu xác nhận hoàn thành |



Các notification khác có thể được tạo trực tiếp khi xảy ra sự kiện trong hệ thống, ví dụ:



| Sự kiện | Người nhận |

|---|---|

| Student gửi yêu cầu Lecturer | Lecturer |

| Lecturer chấp nhận yêu cầu | Student |

| Lecturer từ chối yêu cầu | Student |

| Topic proposal được duyệt | Student |

| Topic proposal bị từ chối | Student |

| Milestone được tạo | Student |

| Lecturer yêu cầu chỉnh sửa | Student |

| AI Analysis hoàn thành | Student và Lecturer |



Do đó, không phải tất cả notification đều được tạo bởi Scheduled Job.



\## 8.7. Chống tạo Notification trùng lặp



Scheduled Job có thể chạy nhiều lần nên hệ thống phải kiểm tra notification trước khi tạo mới.



Quy trình:



```text

Scheduled Job

&#x20;     |

&#x20;     v

Kiểm tra điều kiện

&#x20;     |

&#x20;     v

Notification đã tồn tại?

&#x20;     |

&#x20;  +--+--+

&#x20;  |     |

&#x20; Có    Không

&#x20;  |     |

&#x20;  v     v

Không   Tạo

tạo     mới

```



Ví dụ:



\- Một milestone còn 3 ngày chỉ tạo một notification `WARNING`.

\- Các lần Scheduled Job tiếp theo không tạo lại notification `WARNING` cho cùng milestone.

\- Khi milestone quá hạn, hệ thống có thể tạo một notification `OVERDUE` riêng.



Có thể sử dụng event type kết hợp với milestone ID để xác định notification đã được tạo hay chưa.



Ví dụ:



```text

MILESTONE\_WARNING:{milestone\_id}

MILESTONE\_OVERDUE:{milestone\_id}

```



\## 8.8. AI Analysis Workflow



AI Analysis là một workflow riêng, không phải tác vụ kiểm tra deadline của Scheduled Job.



Khi Student liên kết GitHub repository, hệ thống có thể thực hiện quy trình:



```text

Student

&#x20;  |

&#x20;  v

GitHub Repository

&#x20;  |

&#x20;  v

Lấy Source Code

&#x20;  |

&#x20;  v

AI Analysis

&#x20;  |

&#x20;  v

Analysis Report

&#x20;  |

&#x20;  +----------+

&#x20;  |          |

&#x20;  v          v

Student    Lecturer

```



AI có thể hỗ trợ phân tích:



\- Code quality.

\- Code complexity.

\- Maintainability.

\- Potential bugs.

\- Security issues.

\- Cấu trúc source code.



Kết quả phân tích được lưu lại để Student và Lecturer có thể xem.



AI chỉ đóng vai trò hỗ trợ phân tích source code và không thay thế quyết định đánh giá cuối cùng của Lecturer.



\## 8.9. Xử lý lỗi Automation



Nếu Scheduled Job gặp lỗi, hệ thống cần:



\- Ghi lại thông tin lỗi vào log.

\- Không làm ảnh hưởng đến các chức năng chính của hệ thống.

\- Cho phép tác vụ được thực hiện lại khi cần.

\- Không tạo lại notification đã xử lý thành công.

\- Có thể retry đối với các tác vụ thất bại.



Quy trình:



```text

Scheduled Job

&#x20;     |

&#x20;     v

Thực hiện Task

&#x20;     |

&#x20;  +--+--+

&#x20;  |     |

Thành   Lỗi

công     |

&#x20;  |     v

&#x20;  |   Ghi Log

&#x20;  |     |

&#x20;  |     v

&#x20;  |    Retry

&#x20;  |

&#x20;  v

Hoàn thành

```



\## 8.10. Tổng quan Automation



Automation của hệ thống bao gồm các nhóm chức năng chính:



```text

&#x20;                   Automation

&#x20;                        |

&#x20;         +--------------+--------------+

&#x20;         |              |              |

&#x20;         v              v              v

&#x20;  Deadline Check   Project Check   Notification

&#x20;         |              |              |

&#x20;         v              v              v

&#x20; Milestone Status  Completion     Reminder / Warning

&#x20;      Update          Check          / Overdue

```



Luồng tổng quát:



```text

Scheduled Job

&#x20;     |

&#x20;     +--------------------+

&#x20;     |                    |

&#x20;     v                    v

Milestone Check       Project Check

&#x20;     |                    |

&#x20;     v                    v

Deadline / Status     Check Completion

&#x20;     |                    |

&#x20;     v                    v

Notification        Lecturer Confirmation

&#x20;     |                    |

&#x20;     v                    v

&#x20;   Student             COMPLETED

```



Automation giúp hệ thống chủ động theo dõi deadline và tiến độ project, giảm các công việc kiểm tra thủ công và hỗ trợ Student, Lecturer trong quá trình thực hiện project.



AI Analysis được triển khai như một workflow riêng và không thay thế cơ chế Scheduled Job.

