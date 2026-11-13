# Chẩn đoán phản hồi prompt-v32

Đọc trực tiếp ZIP người dùng gửi; không gọi Ollama và không chọn câu trả lời theo đáp án chuẩn.

## Quan sát

Trong các lần thử đã lưu của 8 ý đối chiếu, có 99 mục observations: 89 mục chỉ khác khoảng trắng đầu/cuối dòng, 2 mục khớp nguyên văn và 8 mục khác nội dung. Những con số này đếm các mục qua nhiều lần thử, không phải số câu hỏi độc lập.

Bộ kiểm tra v3.2 so sánh nguyên chuỗi, nên việc bỏ thụt đầu dòng cũng làm hỏng kết quả. Tuy nhiên, không thể chấp nhận tất cả vì vẫn có câu trích sửa nội dung. Ví dụ C1 lần đầu chọn PARTIAL với dẫn chứng test không giải thích việc đổi context; việc sửa định dạng không làm lập luận đó đúng.

Cả 3 nhóm tổng hợp bị lỗi đều trả 2 nhận xét cùng groupId, trong khi bộ kiểm tra cấm trùng. Schema cũ lại cho phép tối đa 2 item. Các nhóm là G-E0109-E0113, G-E0118-E0137, G-E0305-E0307. Bản mới đồng bộ schema với giới hạn một nhận xét mỗi nhóm.

Báo cáo cũ đếm riêng nhóm trả rỗng và không tính nhóm lỗi vào số chưa hoàn tất. Bản mới tính cả hai.

## Quyết định

Không chấm lại điểm v3.2 bằng cách bỏ mọi kiểm tra observations, không lấy câu trả lời thử lại có nhãn đúng nhất và không khẳng định đã sửa khả năng suy luận. V3.3 bỏ nhiệm vụ sao chép code khỏi đầu ra model; chương trình trích dòng nguồn nguyên bản để kiểm tra. Dữ liệu phản hồi cũ được giữ nguyên.

Chưa có kết quả model thật cho v3.3. Cần đánh giá cả ý nghĩa nhận xét lẫn nhãn sau lần chạy mới; ID hợp lệ không đủ chứng minh hiểu code.
