# Bản v3.9 — kiểm tra loại bằng chứng

Cập nhật cho v3.8 đã cài. Chép đè scripts vào gốc dự án; giữ nguyên data/results và các file khác. ZIP chỉ có 6 file sửa đổi hoặc mới.

## Không cần chạy lại Ollama

Đã kiểm tra trực tiếp ZIP regression-v38 bạn gửi. Mở phiếu đính kèm:

scripts/ai-benchmark/review/report-v38-audited.md

C3 được cảnh báo thiếu bằng chứng phép gán và thiếu hai vị trí lời gọi để kiểm tra thứ tự. C1 không có hai cờ đó. Kết quả model giữ nguyên 5/6; đây là cải thiện kiểm tra kết quả, không phải cải thiện suy luận.

## Tự tạo phiếu từ kết quả cũ (tùy chọn)

```powershell
node .\scripts\ai-benchmark\audit.mjs --case flask-3.1.1-holdout --run regression-v38
```

Cần summary.json, assessment.json và input-C*.json còn nguyên. Kết quả nằm ở results/flask-3.1.1-holdout/regression-v38/review-v39 dưới scripts/ai-benchmark. Nếu thư mục đã tồn tại, dùng --output review-v39-2. Không sửa nhãn/kết quả gốc.

## Phạm vi kiểm tra

- Câu khẳng định khởi tạo nhưng không dẫn AST Assign/AnnAssign ở phiên bản cần xét được gắn cờ; liệt kê ID phép gán có sẵn trong đầu vào để người đọc kiểm tra.
- Câu về thứ tự nhưng không dẫn đủ hai vị trí Call trong cùng hàm/phiên bản được gắn cờ. Đây là kiểm tra cần thiết cho thứ tự lời gọi, không phải bộ phân tích mọi loại thao tác. Phép gán, thao tác cùng dòng, nhánh và vòng lặp cần duyệt thủ công.
- Có phép gán không đồng nghĩa đúng biến/giá trị; đủ lời gọi không chứng minh đúng thứ tự thực thi. Không có AST thì báo chưa kiểm tra, không suy đoán.
- Quy tắc chỉ gắn cờ, không sửa nhãn, không chọn câu trả lời khác, không thay điểm AI. Không dùng đáp án chuẩn để gắn cờ.
- Khi audit, metadata AST lấy từ fixture đã kiểm chứng, không tin metadata tùy ý trong input lưu lại. Nội dung và loại dòng vẫn phải khớp nguồn.
- Prompt suy luận giữ nguyên v3.8; chưa cần chạy model mới. Các lần chạy tương lai tự có phiếu review-v39. Đây vẫn là bộ dữ liệu phát triển, không phải đánh giá độc lập.

## Kiểm thử

```powershell
node --test scripts/ai-benchmark/benchmark.test.mjs
```

37/37 kiểm thử công cụ đạt. Đã chạy audit ngoại tuyến trên ZIP thực tế và giữ nguyên nhãn. Chưa chạy Ollama thật cho phiên bản này. Chưa tích hợp backend .NET/Vue.
