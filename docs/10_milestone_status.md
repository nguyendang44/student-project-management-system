\# 10. Milestone Status



\## 10.1. Mục tiêu



Milestone Status dùng để biểu diễn trạng thái của từng milestone trong quá trình thực hiện project.



Trạng thái milestone giúp Student và Lecturer theo dõi tiến độ, deadline và kết quả của từng công việc.



Milestone có các trạng thái chính:



```text

PENDING → IN\_PROGRESS → SUBMITTED → APPROVED

&#x20;                             |

&#x20;                             v

&#x20;                    REVISION\_REQUIRED

&#x20;                             |

&#x20;                             v

&#x20;                        IN\_PROGRESS

```



Ngoài ra, milestone có thể chuyển sang `OVERDUE` khi Student chưa submit và đã quá deadline.



\## 10.2. Các trạng thái của Milestone



| Status | Ý nghĩa |

|---|---|

| `PENDING` | Milestone chưa bắt đầu thực hiện |

| `IN\_PROGRESS` | Student đang thực hiện milestone |

| `SUBMITTED` | Student đã nộp kết quả và đang chờ Lecturer đánh giá |

| `REVISION\_REQUIRED` | Lecturer yêu cầu Student chỉnh sửa và nộp lại |

| `APPROVED` | Lecturer đã phê duyệt milestone |

| `OVERDUE` | Milestone chưa được submit và đã quá deadline |



\## 10.3. PENDING



`PENDING` là trạng thái ban đầu của milestone.



Milestone ở trạng thái này khi:



\- Milestone đã được Lecturer tạo.

\- Student chưa bắt đầu thực hiện.

\- Deadline chưa được xử lý.



Khi Student bắt đầu thực hiện milestone, trạng thái chuyển sang `IN\_PROGRESS`.



Quy trình:



```text

PENDING

&#x20;  |

&#x20;  | Student bắt đầu

&#x20;  v

IN\_PROGRESS

```



\## 10.4. IN\_PROGRESS



`IN\_PROGRESS` thể hiện Student đang thực hiện milestone.



Trong trạng thái này:



\- Student có thể cập nhật progress.

\- Student có thể cập nhật nội dung thực hiện.

\- Student có thể liên kết source code hoặc GitHub repository nếu cần.

\- Hệ thống theo dõi deadline của milestone.



Nếu Student submit kết quả, milestone chuyển sang `SUBMITTED`.



Nếu milestone quá deadline nhưng chưa submit, Scheduled Job chuyển milestone sang `OVERDUE`.



Quy trình:



```text

&#x20;                IN\_PROGRESS

&#x20;               /            \\

&#x20;              /              \\

&#x20;     Student submit       Quá deadline

&#x20;            |                  |

&#x20;            v                  v

&#x20;       SUBMITTED            OVERDUE

```



\## 10.5. SUBMITTED



`SUBMITTED` thể hiện Student đã hoàn thành bước nộp kết quả của milestone.



Ở trạng thái này:



\- Student không tiếp tục chỉnh sửa kết quả nếu chưa được Lecturer yêu cầu.

\- Lecturer có thể xem kết quả.

\- Lecturer có thể đánh giá milestone.

\- Hệ thống không chuyển milestone sang `OVERDUE`.



Lecturer có hai lựa chọn:



```text

SUBMITTED

&#x20;   |

&#x20;   +-------------------+

&#x20;   |                   |

&#x20;   v                   v

&#x20;APPROVED        REVISION\_REQUIRED

```



\## 10.6. REVISION\_REQUIRED



`REVISION\_REQUIRED` được sử dụng khi Lecturer đánh giá kết quả và yêu cầu Student chỉnh sửa.



Lecturer có thể cung cấp:



\- Nội dung cần chỉnh sửa.

\- Nhận xét.

\- Yêu cầu bổ sung.

\- Thời hạn chỉnh sửa nếu cần.



Student sau đó tiếp tục thực hiện và submit lại milestone.



Quy trình:



```text

SUBMITTED

&#x20;   |

&#x20;   | Lecturer yêu cầu chỉnh sửa

&#x20;   v

REVISION\_REQUIRED

&#x20;   |

&#x20;   | Student chỉnh sửa

&#x20;   v

IN\_PROGRESS

&#x20;   |

&#x20;   | Submit lại

&#x20;   v

SUBMITTED

```



`REVISION\_REQUIRED` không tự động chuyển sang `OVERDUE` bởi Scheduled Job.



\## 10.7. APPROVED



`APPROVED` là trạng thái milestone đã được Lecturer phê duyệt.



Điều kiện:



\- Student đã submit kết quả.

\- Lecturer đã kiểm tra kết quả.

\- Lecturer xác nhận milestone đạt yêu cầu.



Quy trình:



```text

SUBMITTED

&#x20;   |

&#x20;   | Lecturer approve

&#x20;   v

APPROVED

```



Milestone ở trạng thái `APPROVED` được xem là đã hoàn thành.



Scheduled Job không thay đổi trạng thái `APPROVED`.



\## 10.8. OVERDUE



`OVERDUE` thể hiện milestone đã quá deadline nhưng Student chưa submit kết quả.



Scheduled Job định kỳ kiểm tra deadline và chuyển:



```text

IN\_PROGRESS

&#x20;     |

&#x20;     | Quá deadline

&#x20;     v

&#x20; OVERDUE

```



Khi milestone `OVERDUE`, hệ thống có thể:



\- Gửi notification cho Student.

\- Gửi notification cho Lecturer.

\- Ghi nhận milestone đã quá hạn.

\- Cho phép Student tiếp tục thực hiện và submit kết quả.



Sau khi Student submit kết quả:



```text

OVERDUE

&#x20;   |

&#x20;   | Student submit

&#x20;   v

SUBMITTED

&#x20;   |

&#x20;   | Lecturer review

&#x20;   v

APPROVED

```



Hoặc Lecturer có thể yêu cầu chỉnh sửa:



```text

OVERDUE

&#x20;   |

&#x20;   v

SUBMITTED

&#x20;   |

&#x20;   v

REVISION\_REQUIRED

&#x20;   |

&#x20;   v

IN\_PROGRESS

```



\## 10.9. Quy tắc chuyển trạng thái



| Trạng thái hiện tại | Sự kiện | Trạng thái tiếp theo |

|---|---|---|

| `PENDING` | Student bắt đầu | `IN\_PROGRESS` |

| `IN\_PROGRESS` | Student submit | `SUBMITTED` |

| `IN\_PROGRESS` | Quá deadline | `OVERDUE` |

| `SUBMITTED` | Lecturer approve | `APPROVED` |

| `SUBMITTED` | Lecturer yêu cầu chỉnh sửa | `REVISION\_REQUIRED` |

| `REVISION\_REQUIRED` | Student bắt đầu chỉnh sửa | `IN\_PROGRESS` |

| `OVERDUE` | Student submit | `SUBMITTED` |



\## 10.10. Không chuyển OVERDUE trong một số trường hợp



Scheduled Job chỉ chuyển milestone sang `OVERDUE` khi milestone chưa submit và đã quá deadline.



Các trạng thái sau không tự động chuyển sang `OVERDUE`:



\- `PENDING`

\- `SUBMITTED`

\- `REVISION\_REQUIRED`

\- `APPROVED`



Đặc biệt:



\- `SUBMITTED` nghĩa là Student đã nộp và đang chờ Lecturer đánh giá.

\- `REVISION\_REQUIRED` nghĩa là Lecturer đã yêu cầu chỉnh sửa.

\- `APPROVED` nghĩa là milestone đã hoàn thành.



\## 10.11. Deadline và Status



Deadline là một thuộc tính thời gian của milestone và không phải là một status.



Ví dụ:



```text

Milestone

&#x20;  |

&#x20;  +-- status: IN\_PROGRESS

&#x20;  |

&#x20;  +-- start\_date: 2026-10-01

&#x20;  |

&#x20;  +-- deadline: 2026-10-15

```



Không sử dụng `DEADLINE` làm trạng thái của milestone.



Khi deadline đã qua, hệ thống dựa trên status hiện tại để quyết định có chuyển sang `OVERDUE` hay không.



\## 10.12. Quan hệ giữa Milestone và Project



Milestone là thành phần dùng để theo dõi tiến độ của Project.



Một project có thể có nhiều milestone:



```text

Project

&#x20;  |

&#x20;  +---- Milestone 1

&#x20;  |

&#x20;  +---- Milestone 2

&#x20;  |

&#x20;  +---- Milestone 3

&#x20;  |

&#x20;  +---- Milestone 4

```



Khi tất cả milestone bắt buộc đều ở trạng thái `APPROVED`, project có thể đủ điều kiện để Lecturer xác nhận hoàn thành.



```text

Milestone 1 → APPROVED

Milestone 2 → APPROVED

Milestone 3 → APPROVED

Milestone 4 → APPROVED

&#x20;         |

&#x20;         v

Tất cả milestone hoàn thành

&#x20;         |

&#x20;         v

Lecturer xác nhận

&#x20;         |

&#x20;         v

Project COMPLETED

```



\## 10.13. Tổng quan vòng đời Milestone



```text

&#x20;                +---------+

&#x20;                | PENDING |

&#x20;                +----+----+

&#x20;                     |

&#x20;                     | Student bắt đầu

&#x20;                     v

&#x20;              +-------------+

&#x20;              | IN\_PROGRESS |

&#x20;              +------+------+

&#x20;                 |         |

&#x20;                 |         | Quá deadline

&#x20;                 |         v

&#x20;                 |     +---------+

&#x20;                 |     | OVERDUE |

&#x20;                 |     +----+----+

&#x20;                 |          |

&#x20;                 |          | Submit

&#x20;                 |          v

&#x20;                 |     +-----------+

&#x20;                 +---->| SUBMITTED |

&#x20;                       +-----+-----+

&#x20;                         |       |

&#x20;                         |       |

&#x20;                    Approve   Revision

&#x20;                         |       |

&#x20;                         v       v

&#x20;                   +---------+  +------------------+

&#x20;                   | APPROVED|  | REVISION\_REQUIRED|

&#x20;                   +---------+  +--------+---------+

&#x20;                                        |

&#x20;                                        | Chỉnh sửa

&#x20;                                        v

&#x20;                                   IN\_PROGRESS

```



\## 10.14. Nguyên tắc



\- Mỗi milestone phải có một trạng thái xác định.

\- Chỉ được chuyển sang các trạng thái hợp lệ.

\- Việc chuyển trạng thái phải được kiểm tra ở phía server.

\- Scheduled Job chỉ tự động chuyển `IN\_PROGRESS` sang `OVERDUE` khi đã quá deadline và chưa submit.

\- `SUBMITTED` không được tự động chuyển sang `OVERDUE`.

\- `REVISION\_REQUIRED` không được tự động chuyển sang `OVERDUE`.

\- `APPROVED` là trạng thái hoàn thành của milestone.

\- Lecturer chịu trách nhiệm đánh giá và phê duyệt milestone.

\- Student chịu trách nhiệm cập nhật tiến độ và submit kết quả.

\- Deadline được lưu dưới dạng ngày/giờ và không được sử dụng như một status.

