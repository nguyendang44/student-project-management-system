\# 11. MVP Scope



\## 11.1. Mục tiêu



MVP (Minimum Viable Product) xác định các chức năng tối thiểu cần hoàn thành để hệ thống có thể hỗ trợ đầy đủ quy trình quản lý đề tài và theo dõi tiến độ project sinh viên.



MVP tập trung vào các chức năng cốt lõi trước khi triển khai các chức năng mở rộng như GitHub Integration và AI Analysis.



\## 11.2. Phạm vi MVP



MVP bao gồm các chức năng chính:



\- Authentication và Authorization.

\- Quản lý Student.

\- Quản lý Lecturer.

\- Quản lý Topic.

\- Đăng ký Lecturer.

\- Quản lý Lecturer Capacity.

\- Quản lý Project.

\- Quản lý Milestone.

\- Cập nhật Progress.

\- Đánh giá Milestone.

\- Notification.

\- Scheduled Job và Automation.



Luồng chính:



```text

Authentication

&#x20;     |

&#x20;     v

Topic Management

&#x20;     |

&#x20;     v

Lecturer Registration

&#x20;     |

&#x20;     v

Lecturer Capacity

&#x20;     |

&#x20;     v

Project Management

&#x20;     |

&#x20;     v

Milestone Management

&#x20;     |

&#x20;     v

Progress Update

&#x20;     |

&#x20;     v

Evaluation

&#x20;     |

&#x20;     v

Notification

&#x20;     |

&#x20;     v

Automation

```



\## 11.3. Authentication và Authorization



MVP cần hỗ trợ:



\- Đăng nhập.

\- Xác thực tài khoản.

\- Phân quyền theo role.

\- Student chỉ được truy cập các chức năng thuộc Student.

\- Lecturer chỉ được truy cập các chức năng thuộc Lecturer.

\- Admin có quyền quản lý hệ thống theo role.



Các role chính:



```text

Student

Lecturer

Admin

```



\## 11.4. Topic Management



MVP hỗ trợ hai cách hình thành Topic:



\### Lecturer hoặc Admin tạo Topic



```text

Lecturer / Admin

&#x20;      |

&#x20;      v

Create Topic

&#x20;      |

&#x20;      v

Open Registration

&#x20;      |

&#x20;      v

Student đăng ký

```



\### Student tự đề xuất Topic



```text

Student

&#x20;  |

&#x20;  v

Create Topic Proposal

&#x20;  |

&#x20;  v

PENDING\_APPROVAL

&#x20;  |

&#x20;  +----------------+

&#x20;  |                |

&#x20;  v                v

APPROVED         REJECTED

```



Nếu Topic Proposal bị từ chối, Student có thể chỉnh sửa và gửi lại theo quy trình của hệ thống.



\## 11.5. Lecturer Registration



Student có thể gửi yêu cầu đăng ký Lecturer.



Quy trình:



```text

Student

&#x20;  |

&#x20;  v

Chọn Lecturer

&#x20;  |

&#x20;  v

Gửi Request

&#x20;  |

&#x20;  v

PENDING

&#x20;  |

&#x20;  +----------------+

&#x20;  |                |

&#x20;  v                v

ACCEPTED         REJECTED

```



Khi Lecturer từ chối, hệ thống lưu lý do từ chối để Student có thể xem.



\## 11.6. Lecturer Capacity



MVP phải quản lý số lượng Student mà mỗi Lecturer có thể hướng dẫn.



Thông tin cần quản lý:



\- Maximum Students.

\- Current Students.

\- Remaining Capacity.

\- Capacity Status.



Ví dụ:



```text

Maximum Students: 5

Current Students: 4

Remaining: 1

Status: AVAILABLE

```



Khi số lượng Student đạt giới hạn:



```text

Current Students = Maximum Students

&#x20;            |

&#x20;            v

&#x20;           FULL

&#x20;            |

&#x20;            v

Không nhận thêm Request

```



Việc kiểm tra capacity phải được thực hiện ở phía server.



\## 11.7. Project Management



Sau khi Lecturer chấp nhận yêu cầu, hệ thống tạo hoặc kích hoạt Project.



Project sử dụng các trạng thái:



```text

DRAFT

&#x20; |

&#x20; v

REGISTERED

&#x20; |

&#x20; v

IN\_PROGRESS

&#x20; |

&#x20; v

COMPLETED

```



Project cũng có thể chuyển sang:



```text

IN\_PROGRESS → CANCELLED

```



Lecturer và Student có thể xem thông tin Project theo quyền được cấp.



\## 11.8. Milestone Management



Lecturer có thể tạo các milestone cho Project.



Mỗi milestone có các thông tin chính:



\- Tên milestone.

\- Mô tả.

\- Start Date.

\- Deadline.

\- Status.

\- Progress.

\- Result.

\- Evaluation.



Các trạng thái:



```text

PENDING

&#x20;  |

&#x20;  v

IN\_PROGRESS

&#x20;  |

&#x20;  v

SUBMITTED

&#x20;  |

&#x20;  +------------------+

&#x20;  |                  |

&#x20;  v                  v

APPROVED       REVISION\_REQUIRED

&#x20;                     |

&#x20;                     v

&#x20;                IN\_PROGRESS

```



Nếu milestone quá deadline và chưa submit:



```text

IN\_PROGRESS → OVERDUE

```



\## 11.9. Progress Management



Student có thể cập nhật tiến độ của milestone.



Thông tin progress có thể bao gồm:



\- Phần trăm hoàn thành.

\- Nội dung công việc đã thực hiện.

\- Kết quả đạt được.

\- Khó khăn hoặc vấn đề gặp phải.

\- Link source code nếu có.



Quy trình:



```text

Student

&#x20;  |

&#x20;  v

Cập nhật Progress

&#x20;  |

&#x20;  v

Milestone

&#x20;  |

&#x20;  v

Lecturer theo dõi

```



\## 11.10. Evaluation



Lecturer có thể đánh giá kết quả milestone.



Các kết quả chính:



```text

SUBMITTED

&#x20;   |

&#x20;   +-------------------+

&#x20;   |                   |

&#x20;   v                   v

&#x20;APPROVED        REVISION\_REQUIRED

```



Lecturer có thể cung cấp:



\- Nhận xét.

\- Kết quả đánh giá.

\- Yêu cầu chỉnh sửa.



AI không thay thế Lecturer trong việc đánh giá cuối cùng.



\## 11.11. Notification



MVP hỗ trợ notification cho các sự kiện quan trọng.



Các trường hợp:



\- Student gửi Lecturer Request.

\- Lecturer chấp nhận Request.

\- Lecturer từ chối Request.

\- Topic Proposal được duyệt.

\- Topic Proposal bị từ chối.

\- Milestone được tạo.

\- Milestone sắp đến deadline.

\- Milestone quá deadline.

\- Lecturer yêu cầu chỉnh sửa.

\- Project đủ điều kiện hoàn thành.



Notification có hai trạng thái:



```text

UNREAD → READ

```



\## 11.12. Automation



MVP sử dụng Scheduled Job để thực hiện các tác vụ định kỳ.



Các tác vụ chính:



\- Kiểm tra deadline.

\- Chuyển milestone sang `OVERDUE` khi đủ điều kiện.

\- Tạo reminder trước deadline.

\- Tạo warning trước deadline.

\- Tạo notification khi milestone quá hạn.

\- Kiểm tra điều kiện hoàn thành Project.



Luồng:



```text

Scheduled Job

&#x20;     |

&#x20;     v

Deadline Check

&#x20;     |

&#x20;     v

Milestone Status

&#x20;     |

&#x20;     +----------------+

&#x20;     |                |

&#x20;     v                v

Notification       Project Check

```



\## 11.13. Chức năng ngoài MVP



Các chức năng sau được xem là phần mở rộng sau khi các chức năng cốt lõi ổn định:



\- GitHub Integration.

\- AI Source Code Analysis.

\- Phân tích chất lượng source code nâng cao.

\- Phân tích security tự động.

\- Dashboard nâng cao.

\- Dự đoán tiến độ project.

\- Email Notification.

\- Mobile Application.

\- Tích hợp với các nền tảng quản lý source code khác.



\## 11.14. GitHub Integration



GitHub Integration được triển khai như một chức năng mở rộng.



Quy trình dự kiến:



```text

Student

&#x20;  |

&#x20;  v

Link GitHub Repository

&#x20;  |

&#x20;  v

Project

&#x20;  |

&#x20;  v

Source Code

```



Hệ thống có thể sử dụng repository để hỗ trợ theo dõi source code của project.



GitHub Integration không phải điều kiện bắt buộc để hoàn thành các chức năng quản lý project cốt lõi của MVP.



\## 11.15. AI Source Code Analysis



AI Analysis được triển khai sau khi các chức năng quản lý project và milestone ổn định.



Quy trình:



```text

GitHub Repository

&#x20;      |

&#x20;      v

Source Code

&#x20;      |

&#x20;      v

AI Analysis

&#x20;      |

&#x20;      v

Analysis Report

&#x20;      |

&#x20;      +----------+

&#x20;      |          |

&#x20;      v          v

&#x20;   Student    Lecturer

```



AI có thể hỗ trợ:



\- Phân tích code quality.

\- Phân tích complexity.

\- Phát hiện potential bugs.

\- Phân tích maintainability.

\- Phát hiện một số vấn đề security.



AI chỉ cung cấp kết quả phân tích hỗ trợ và không tự động thay thế đánh giá của Lecturer.



\## 11.16. Tiêu chí hoàn thành MVP



MVP được xem là hoàn thành khi hệ thống có thể thực hiện đầy đủ quy trình cơ bản:



```text

Student / Lecturer

&#x20;       |

&#x20;       v

Authentication

&#x20;       |

&#x20;       v

Topic

&#x20;       |

&#x20;       v

Lecturer Registration

&#x20;       |

&#x20;       v

Capacity Check

&#x20;       |

&#x20;       v

Project

&#x20;       |

&#x20;       v

Milestone

&#x20;       |

&#x20;       v

Progress

&#x20;       |

&#x20;       v

Submit

&#x20;       |

&#x20;       v

Lecturer Evaluation

&#x20;       |

&#x20;       v

Project Completion

```



Đồng thời hệ thống phải hỗ trợ:



\- Phân quyền Student, Lecturer và Admin.

\- Kiểm soát Lecturer Capacity.

\- Theo dõi trạng thái Project.

\- Theo dõi trạng thái Milestone.

\- Kiểm tra deadline tự động.

\- Gửi Notification cho các sự kiện quan trọng.

\- Đảm bảo các quy tắc chuyển trạng thái được kiểm tra ở phía server.



\## 11.17. Nguyên tắc triển khai MVP



\- Ưu tiên các chức năng phục vụ trực tiếp quy trình quản lý Project.

\- Hoàn thành Authentication và Authorization trước các chức năng nghiệp vụ.

\- Hoàn thành Topic, Registration và Capacity trước khi triển khai Project.

\- Hoàn thành Project và Milestone trước khi triển khai Automation.

\- Automation không được làm ảnh hưởng đến các chức năng chính.

\- GitHub Integration và AI Analysis được triển khai sau các chức năng cốt lõi.

\- AI chỉ đóng vai trò hỗ trợ phân tích và không thay thế Lecturer.

