\# 6. Non-functional Requirements



\## NFR-01. Security



\- Mật khẩu phải được lưu dưới dạng hash, không lưu plaintext.

\- Người dùng phải được xác thực trước khi sử dụng hệ thống.

\- Hệ thống phải kiểm tra quyền truy cập ở phía server.

\- Student không được truy cập chức năng dành cho Lecturer hoặc Admin.

\- Dữ liệu nhạy cảm phải được bảo vệ.



\## NFR-02. Performance



\- Các thao tác thông thường phải có thời gian phản hồi phù hợp.

\- Việc phân tích source code bằng AI không được làm hệ thống chính bị treo.

\- Các tác vụ tự động phải chạy ở background hoặc scheduled job khi cần thiết.

\- Hệ thống phải tránh thực hiện các truy vấn dư thừa.



\## NFR-03. Scalability



Hệ thống cần có khả năng mở rộng để hỗ trợ:



\- Số lượng sinh viên và giảng viên lớn hơn.

\- Email và notification service.

\- Ứng dụng mobile trong tương lai.

\- Các dịch vụ AI mở rộng.

\- Phân tích tiến độ dự án.

\- Các nền tảng source control khác ngoài GitHub.

\- Tích hợp với hệ thống quản lý đào tạo của trường.



\## NFR-04. Maintainability



Hệ thống cần được thiết kế theo các module rõ ràng:



\- Authentication \& Authorization

\- User Management

\- Topic Management

\- Registration Management

\- Lecturer Capacity

\- Project Management

\- Milestone Management

\- Progress Management

\- Evaluation

\- GitHub Integration

\- AI Analysis

\- Notification

\- Automation

\- Dashboard \& Reporting



Mỗi module cần có trách nhiệm rõ ràng và hạn chế phụ thuộc trực tiếp vào các module khác.



\## NFR-05. Usability



\- Giao diện dễ sử dụng.

\- Trạng thái đề tài và dự án phải rõ ràng.

\- Số lượng sinh viên hiện tại và số chỗ còn lại của giảng viên phải dễ nhận biết.

\- Deadline phải được hiển thị rõ ràng.

\- Thông báo lỗi phải dễ hiểu.

\- Người dùng phải biết được kết quả của các thao tác quan trọng.



\## NFR-06. Reliability and Consistency



\- Hệ thống phải đảm bảo dữ liệu nhất quán.

\- Không được để số lượng sinh viên vượt quá giới hạn của giảng viên.

\- Các thao tác liên quan đến đăng ký và giới hạn giảng viên cần sử dụng transaction hoặc cơ chế tương đương.

\- Trạng thái project, milestone và evaluation phải được cập nhật nhất quán.



\## NFR-07. Auditability



Hệ thống cần lưu lại lịch sử của các thao tác quan trọng như:



\- Đăng ký đề tài.

\- Gửi yêu cầu giảng viên.

\- Chấp nhận hoặc từ chối yêu cầu.

\- Phê duyệt hoặc từ chối đề xuất đề tài.

\- Cập nhật milestone.

\- Đánh giá milestone.

\- Yêu cầu chỉnh sửa.

\- Kết quả phân tích AI.



AI chỉ đóng vai trò hỗ trợ phân tích và không thay thế quyết định đánh giá cuối cùng của giảng viên.

