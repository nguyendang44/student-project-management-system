# Git Progress Analysis v3.7 | AI Purpose & Clean Report

Bản vá **v3.6 → v3.7** dành cho `student-project-management-system` (.NET 10 + Vue). Mục tiêu: diễn giải **ý nghĩa có căn cứ** của từng nhóm công việc và hiển thị **mỗi nhóm đúng một lần**. Dữ liệu GitHub, lời gọi Ollama hiện có và lịch sử SQL Server được giữ nguyên.

## Thay đổi

1. **Không chấp nhận AI chỉ kể lại bản gốc.** `GitProgressAiExplanationPolicy` yêu cầu diễn giải có mục đích cụ thể, hành động liên quan và thông tin bổ sung so với mô tả theo quy tắc; chặn câu lặp từ hoặc lợi ích chung chung. Vẫn kiểm tra ID nhóm `F1...F7`, route thật, tên công nghệ/khả năng chưa có bằng chứng, giới hạn độ dài và khẳng định test/chức năng chạy thành công. Khi AI không đủ căn cứ, không hiển thị diễn giải.
2. **Prompt Ollama tập trung vào “để làm gì?”.** Mô hình chỉ nhận các nhóm được xác minh, không được tạo bằng chứng mới. Đầu ra vẫn được kiểm tra ở backend trước khi lưu.
3. **Một nhóm công việc = một record trong `EvidenceJson`.** Trường `aiExplanation` (có thể null) được lưu **bên trong** mục `feature-rule` cùng `factId`; không tạo thêm một mục `ollama-feature` cho cùng một việc. Không cần migration.
4. **Vue hiển thị 6 nhóm công việc, không đếm thành 12.** Phần giải thích AI nằm dưới nhóm tương ứng, bằng chứng Git chỉ hiển thị một lần. Đã loại cảnh báo lặp tại mỗi dòng; vẫn giữ chú thích chung về giới hạn bằng chứng. Báo cáo v3.6 cũ vẫn được ghép cặp trên giao diện từ `source=ollama-feature` mà không thay dữ liệu SQL lịch sử. Nếu còn nhận xét dòng riêng, chúng vào mục phụ, không cộng vào số nhóm.
5. **Cập nhật kiểm thử hồi quy:** AI chép lại API/migration, lặp chữ, bịa Redis, ghép 6 diễn giải với 6 nhóm, JSON chỉ có 1 mục/nhóm, không mất citation khi diễn giải rỗng.

Version phân tích mới: `qwen2.5-coder:7b|gp-purpose-v3.7` (tên model thực tế vẫn lấy từ cấu hình Ollama). Các cặp commit cũ sẽ được phân tích lại thay vì trả cache phiên bản trước.

## Cài đặt (Windows PowerShell)

**Tắt backend** trước khi cài. Giải nén thư mục `v011-git-progress-ai-explainer-v37` ngang hàng với `backend` và `frontend` trong repository. Chạy tại thư mục gốc:

```powershell
py .\v011-git-progress-ai-explainer-v37\apply_ai_explainer_v37.py . --check
```

Chỉ nếu xuất hiện `CHECK OK`:

```powershell
py .\v011-git-progress-ai-explainer-v37\apply_ai_explainer_v37.py .
dotnet build backend
dotnet test backend
cd frontend
npm run build
cd ..
```

Nếu build/test thành công, khởi động backend và frontend như bình thường. Bảo đảm Ollama đang phục vụ `http://127.0.0.1:11434` với model `qwen2.5-coder:7b`. Phân tích tại `/git-progress` bằng cặp:

- Base: `dadc6d42b74d16a532def7e2d8deda8db5f206fa`
- Head: `fdba846c37f2cc824dfa09fff6938a260a6268df`

Đánh giá: phần tổng hợp theo quy tắc vẫn phải có 6 nhóm như trước khi mã nguồn so sánh không đổi. Số diễn giải AI **có thể từ 0 đến 6**; không được bịa nội dung chỉ để đủ số. Không nên commit nếu có test thất bại hoặc báo cáo sai.

## An toàn và tương thích

- Cài đặt là **one-time**, đối chiếu nội dung chính xác của 5 file v3.6 trước khi sửa. Nếu đã tùy chỉnh các file, installer **từ chối ghi đè**. Không ép cài.
- Backup các file bị sửa nằm ngoài repo, tại `v011-gp-v37-backup-...`, ngang cấp thư mục dự án.
- Thêm 2 file C#, sửa 5 file C#/Vue/TS, cập nhật 3 assertion version trong test. Không thay SQL schema, API endpoint hoặc migrations.
- Kiểm thử Python của installer: `py .\v011-git-progress-ai-explainer-v37\test_installer.py`.
- Môi trường tạo bản vá không có .NET SDK hoặc node_modules của dự án: cần **chạy `dotnet build`, `dotnet test`, `npm run build` trên máy bạn** để xác nhận C#/Vue và kiểm thử end-to-end.

Nếu `--check` báo file không khớp, hãy giữ nguyên repo và gửi dòng lỗi. Không tự copy payload đè lên source vì có nguy cơ mất thay đổi khác.
