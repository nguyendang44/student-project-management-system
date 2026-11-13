# Phiếu kiểm tra độc lập

Nhãn đúng, diễn giải đúng và dẫn chứng đúng ý là ba tiêu chí riêng. ID tồn tại chỉ xác nhận vị trí code, không xác nhận ý nghĩa.

Cờ chỉ yêu cầu kiểm tra, không tự sửa nhãn hay kết luận AI sai. Không có cờ không có nghĩa nhận xét đúng.

Cờ từ quy tắc/AST, không phải nhận xét Ollama. Chưa chấm độ đúng diễn giải hoặc độ liên quan dẫn chứng.

| Mục | Cờ cần kiểm tra | Diễn giải | Dẫn chứng đúng ý |
|---|---|---|---|
| A2 | AST nhận diện import trong bằng chứng; AI phân loại là tài liệu. | Chưa duyệt | Chưa duyệt |
| A3 | Không có cờ tự động | Chưa duyệt | Chưa duyệt |
| A4 | Không có cờ tự động | Chưa duyệt | Chưa duyệt |
| A5 | Bằng chứng thay đổi nằm trong docstring, nhưng phân loại AI không phải tài liệu. / Nhận xét nêu POST nhưng các dòng được dẫn không có POST; chưa xác nhận phương thức từ ngữ cảnh khác. | Chưa duyệt | Chưa duyệt |
| A6 | Bằng chứng thay đổi nằm trong docstring, nhưng phân loại AI không phải tài liệu. | Chưa duyệt | Chưa duyệt |
| A7 | Không có cờ tự động | Chưa duyệt | Chưa duyệt |
| A8 | Bằng chứng thay đổi nằm trong docstring, nhưng phân loại AI không phải tài liệu. / Nhận xét nêu POST nhưng các dòng được dẫn không có POST; chưa xác nhận phương thức từ ngữ cảnh khác. | Chưa duyệt | Chưa duyệt |
| A9 | Bằng chứng thay đổi nằm trong docstring, nhưng phân loại AI không phải tài liệu. | Chưa duyệt | Chưa duyệt |
| A10 | Code mới vẫn có câu if trong nhóm; cần phân biệt viết lại điều kiện với loại bỏ kiểm tra. | Chưa duyệt | Chưa duyệt |
| A11 | Không có cờ tự động | Chưa duyệt | Chưa duyệt |
| A16 | Không có cờ tự động | Chưa duyệt | Chưa duyệt |
| A18 | Không có cờ tự động | Chưa duyệt | Chưa duyệt |
| A21 | Không có cờ tự động | Chưa duyệt | Chưa duyệt |
| A23 | Không có cờ tự động | Chưa duyệt | Chưa duyệt |
| A24 | Không có cờ tự động | Chưa duyệt | Chưa duyệt |
| A25 | Các dòng thay đổi thuộc tests/; cần kiểm tra phân loại và phạm vi diễn giải. | Chưa duyệt | Chưa duyệt |
| A26 | Không có cờ tự động | Chưa duyệt | Chưa duyệt |
| A27 | Không có cờ tự động | Chưa duyệt | Chưa duyệt |
| A28 | Không có cờ tự động | Chưa duyệt | Chưa duyệt |
| A29 | Không có cờ tự động | Chưa duyệt | Chưa duyệt |
| A30 | Không có cờ tự động | Chưa duyệt | Chưa duyệt |

## Đối chiếu báo cáo

| Mục | Nhãn AI khớp đáp án | Sau quy tắc khớp đáp án | Diễn giải/dẫn chứng |
|---|---|---|---|
| C1 | Có | Có | Chưa duyệt từng ý |
| C2 | Có | Có | Chưa duyệt từng ý |
| C3 | Có | Có | Chưa duyệt từng ý |
| C4 | Không | Có | Chưa duyệt từng ý |
| C5 | Có | Có | Chưa duyệt từng ý |
| C6 | Có | Có | Chưa duyệt từng ý |

Có thể ghi đánh giá thủ công trong review-checklist.json: explanation và evidenceRelevance dùng CORRECT / PARTIAL / INCORRECT / UNREVIEWED, kèm notes. Phiếu không tự tính các trường chưa duyệt thành đúng. Báo cáo Markdown là bản tạo ban đầu, không tự đồng bộ các chỉnh sửa JSON.
