\# 7. Business Workflow



\## 7.1. Tổng quan



Quy trình của hệ thống bắt đầu từ việc sinh viên lựa chọn hoặc đề xuất đề tài, sau đó đăng ký giảng viên hướng dẫn, thực hiện dự án, cập nhật tiến độ và nhận đánh giá.



\## 7.2. Tạo và đăng ký đề tài



Có hai cách hình thành đề tài:



\### Cách 1: Đề tài có sẵn



1\. Admin hoặc Lecturer tạo đề tài.

2\. Đề tài được mở cho sinh viên đăng ký.

3\. Student xem thông tin đề tài.

4\. Student đăng ký đề tài.



\### Cách 2: Sinh viên tự đề xuất



1\. Student tạo đề xuất đề tài.

2\. Student gửi đề xuất.

3\. Lecturer xem và kiểm tra đề xuất.

4\. Lecturer có thể:

&#x20;  - Approve.

&#x20;  - Reject và nhập lý do.

5\. Nếu bị reject, Student có thể chỉnh sửa và gửi lại.



\## 7.3. Đăng ký giảng viên



1\. Student chọn Lecturer.

2\. Student gửi yêu cầu hướng dẫn.

3\. Hệ thống kiểm tra số lượng sinh viên hiện tại của Lecturer.

4\. Nếu Lecturer đã đủ số lượng, hệ thống không cho gửi hoặc không cho chấp nhận yêu cầu.

5\. Nếu còn chỗ, yêu cầu được chuyển sang trạng thái PENDING.

6\. Lecturer xem yêu cầu.

7\. Lecturer:

&#x20;  - Accept → tiếp tục tạo project.

&#x20;  - Reject → nhập lý do từ chối.

8\. Student nhận notification về kết quả.



\## 7.4. Quản lý dự án



Sau khi đề tài và giảng viên được xác nhận:



1\. Hệ thống tạo Project.

2\. Project chuyển sang trạng thái IN\_PROGRESS.

3\. Lecturer tạo các milestone.

4\. Mỗi milestone có deadline và yêu cầu kết quả.

5\. Student thực hiện milestone.

6\. Student cập nhật progress.

7\. Student submit kết quả.

8\. Lecturer review kết quả.



\## 7.5. Đánh giá



Sau khi Student submit:



\- Lecturer có thể APPROVE.

\- Hoặc yêu cầu REVISION.



Nếu yêu cầu chỉnh sửa:



1\. Milestone chuyển sang REVISION\_REQUIRED.

2\. Student chỉnh sửa.

3\. Student submit lại.

4\. Lecturer đánh giá lại.



\## 7.6. GitHub và AI



1\. Student liên kết GitHub repository.

2\. Hệ thống lấy thông tin repository.

3\. Source code được thu thập phục vụ phân tích.

4\. AI phân tích source code.

5\. Hệ thống lưu kết quả phân tích.

6\. Lecturer xem báo cáo.

7\. AI chỉ cung cấp thông tin hỗ trợ; Lecturer đưa ra đánh giá cuối cùng.



\## 7.7. Tự động hóa



Scheduled Job định kỳ kiểm tra:



\- Deadline milestone.

\- Milestone quá hạn.

\- Project cần cập nhật trạng thái.

\- Các notification cần gửi.



Khi phát hiện milestone quá hạn, hệ thống có thể chuyển trạng thái sang OVERDUE và gửi notification cho Student và Lecturer.

