# Phiếu kiểm tra độc lập

Nhãn đúng, diễn giải đúng và dẫn chứng đúng ý là ba tiêu chí riêng. ID tồn tại chỉ xác nhận vị trí code, không xác nhận ý nghĩa.

Cờ chỉ yêu cầu kiểm tra, không tự sửa nhãn hay kết luận AI sai. Không có cờ không có nghĩa nhận xét đúng.

Cờ từ quy tắc/AST, không phải nhận xét Ollama. Chưa chấm độ đúng diễn giải hoặc độ liên quan dẫn chứng.

| Mục | Cờ cần kiểm tra | Diễn giải | Dẫn chứng đúng ý |
|---|---|---|---|
| A1 | Không có cờ tự động | Chưa duyệt | Chưa duyệt |
| A3 | Không có cờ tự động | Chưa duyệt | Chưa duyệt |
| A4 | Không có cờ tự động | Chưa duyệt | Chưa duyệt |
| A5 | Không có cờ tự động | Chưa duyệt | Chưa duyệt |
| A7 | Không có cờ tự động | Chưa duyệt | Chưa duyệt |
| A8 | Không có cờ tự động | Chưa duyệt | Chưa duyệt |
| A9 | Không có cờ tự động | Chưa duyệt | Chưa duyệt |
| A10 | AST nhận diện import trong bằng chứng; AI phân loại là tài liệu. | Chưa duyệt | Chưa duyệt |
| A11 | Không có cờ tự động | Chưa duyệt | Chưa duyệt |
| A12 | Không có cờ tự động | Chưa duyệt | Chưa duyệt |
| A13 | AST nhận diện import trong bằng chứng; AI phân loại là tài liệu. | Chưa duyệt | Chưa duyệt |
| A14 | Không có cờ tự động | Chưa duyệt | Chưa duyệt |
| A15 | Không có cờ tự động | Chưa duyệt | Chưa duyệt |
| A16 | Không có cờ tự động | Chưa duyệt | Chưa duyệt |
| A17 | Các dòng thay đổi thuộc tests/; cần kiểm tra phân loại và phạm vi diễn giải. | Chưa duyệt | Chưa duyệt |
| A18 | Các dòng thay đổi thuộc tests/; cần kiểm tra phân loại và phạm vi diễn giải. | Chưa duyệt | Chưa duyệt |
| A19 | Các dòng thay đổi thuộc tests/; cần kiểm tra phân loại và phạm vi diễn giải. | Chưa duyệt | Chưa duyệt |
| A20 | Các dòng thay đổi thuộc tests/; cần kiểm tra phân loại và phạm vi diễn giải. | Chưa duyệt | Chưa duyệt |
| A21 | Các dòng thay đổi thuộc tests/; cần kiểm tra phân loại và phạm vi diễn giải. | Chưa duyệt | Chưa duyệt |
| A22 | Các dòng thay đổi thuộc tests/; cần kiểm tra phân loại và phạm vi diễn giải. | Chưa duyệt | Chưa duyệt |

## Đối chiếu báo cáo

| Mục | Nhãn AI khớp đáp án | Sau quy tắc khớp đáp án | Diễn giải/dẫn chứng |
|---|---|---|---|
| C1 | Có | Có | Chưa duyệt từng ý |
| C2 | Có | Có | Chưa duyệt từng ý |
| C3 | Không | Không | Chưa duyệt từng ý |
| C4 | Có | Có | Chưa duyệt từng ý |
| C5 | Có | Có | Chưa duyệt từng ý |
| C6 | Có | Có | Chưa duyệt từng ý |

C2.2: claimPart không phải trích đoạn nguyên văn của ý báo cáo; diễn đạt đồng nghĩa vẫn cần duyệt trích dẫn. Nhãn gốc vẫn được giữ; không dùng dòng này để kiểm tra quan hệ. Nhãn gốc được giữ nguyên.

C1.1 — Đổi cách xây dựng danh sách khóa trong get_signing_serializer: thêm các fallback trước, sau đó append app.secret_key.: E0110=BEFORE+AFTER, E0111=BEFORE+AFTER, E0112=BEFORE+AFTER, E0113=AFTER. Quan hệ AI: MATCH; không xác nhận quan hệ này đúng.

C2.1 — FlaskCliRunner.invoke đổi type hint trả về thành Result: E0170=AFTER, E0171=BEFORE+AFTER. Quan hệ AI: MATCH; không xác nhận quan hệ này đúng.

C2.2 — tất cả kiểm thử đã pass trên Windows: . Quan hệ AI: UNKNOWN; không xác nhận quan hệ này đúng.

C3.1 — Danh sách khóa mới khởi tạo bằng [app.secret_key] rồi mới extend các fallback như cũ.: E0113=AFTER. Quan hệ AI: MATCH; không xác nhận quan hệ này đúng.

C4.1 — Thay đổi này làm tốc độ chạy ứng dụng tăng 30% qua benchmark thực tế.: . Quan hệ AI: UNKNOWN; không xác nhận quan hệ này đúng.

C5.1 — ResponseValue thêm cả cabc.AsyncIterable[str] và cabc.AsyncIterable[bytes].: E0198=AFTER, E0199=AFTER. Quan hệ AI: MATCH; không xác nhận quan hệ này đúng.

C6.1 — FlaskCliRunner.invoke được đổi type hint trả về từ t.Any sang str.: E0170=AFTER. Quan hệ AI: CONFLICT; không xác nhận quan hệ này đúng.

Có thể ghi đánh giá thủ công trong review-checklist.json: explanation và evidenceRelevance dùng CORRECT / PARTIAL / INCORRECT / UNREVIEWED, kèm notes. Phiếu không tự tính các trường chưa duyệt thành đúng. Báo cáo Markdown là bản tạo ban đầu, không tự đồng bộ các chỉnh sửa JSON.
