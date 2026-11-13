# Phiếu kiểm tra độc lập

Nhãn đúng, diễn giải đúng và dẫn chứng đúng ý là ba tiêu chí riêng. ID tồn tại chỉ xác nhận vị trí code, không xác nhận ý nghĩa.

Cờ chỉ yêu cầu kiểm tra, không tự sửa nhãn hay kết luận AI sai. Không có cờ không có nghĩa nhận xét đúng.

Cờ từ quy tắc/AST, không phải nhận xét Ollama. Chưa chấm độ đúng diễn giải hoặc độ liên quan dẫn chứng.

| Mục | Cờ cần kiểm tra | Diễn giải | Dẫn chứng đúng ý |
|---|---|---|---|
| A1 | Không có cờ tự động | Chưa duyệt | Chưa duyệt |
| A2 | AST nhận diện import trong bằng chứng; AI phân loại là tài liệu. | Chưa duyệt | Chưa duyệt |
| A3 | Không có cờ tự động | Chưa duyệt | Chưa duyệt |
| A4 | Không có cờ tự động | Chưa duyệt | Chưa duyệt |
| A5 | Không có cờ tự động | Chưa duyệt | Chưa duyệt |
| A7 | Không có cờ tự động | Chưa duyệt | Chưa duyệt |
| A8 | Không có cờ tự động | Chưa duyệt | Chưa duyệt |
| A9 | Không có cờ tự động | Chưa duyệt | Chưa duyệt |
| A10 | Không có cờ tự động | Chưa duyệt | Chưa duyệt |
| A11 | Không có cờ tự động | Chưa duyệt | Chưa duyệt |
| A12 | Không có cờ tự động | Chưa duyệt | Chưa duyệt |
| A13 | AST nhận diện import trong bằng chứng; AI phân loại là tài liệu. | Chưa duyệt | Chưa duyệt |
| A14 | Không có cờ tự động | Chưa duyệt | Chưa duyệt |
| A15 | Không có cờ tự động | Chưa duyệt | Chưa duyệt |
| A16 | Không có cờ tự động | Chưa duyệt | Chưa duyệt |
| A17 | Các dòng thay đổi thuộc tests/; cần kiểm tra phân loại và phạm vi diễn giải. | Chưa duyệt | Chưa duyệt |
| A18 | Không có cờ tự động | Chưa duyệt | Chưa duyệt |
| A19 | Các dòng thay đổi thuộc tests/; cần kiểm tra phân loại và phạm vi diễn giải. | Chưa duyệt | Chưa duyệt |
| A20 | Các dòng thay đổi thuộc tests/; cần kiểm tra phân loại và phạm vi diễn giải. | Chưa duyệt | Chưa duyệt |
| A21 | Các dòng thay đổi thuộc tests/; cần kiểm tra phân loại và phạm vi diễn giải. | Chưa duyệt | Chưa duyệt |
| A22 | Không có cờ tự động | Chưa duyệt | Chưa duyệt |

## Đối chiếu báo cáo

| Mục | Nhãn AI khớp đáp án | Sau quy tắc khớp đáp án | Diễn giải/dẫn chứng |
|---|---|---|---|
| C1 | Có | Có | Chưa duyệt từng ý |
| C2 | Có | Có | Chưa duyệt từng ý |
| C3 | Không | Không | Chưa duyệt từng ý |
| C4 | Có | Có | Chưa duyệt từng ý |
| C5 | Có | Có | Chưa duyệt từng ý |
| C6 | Không | Không | Chưa duyệt từng ý |

Có thể ghi đánh giá thủ công trong review-checklist.json: explanation và evidenceRelevance dùng CORRECT / PARTIAL / INCORRECT / UNREVIEWED, kèm notes. Phiếu không tự tính các trường chưa duyệt thành đúng. Báo cáo Markdown là bản tạo ban đầu, không tự đồng bộ các chỉnh sửa JSON.
