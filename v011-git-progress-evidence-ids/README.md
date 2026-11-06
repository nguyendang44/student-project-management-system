# v0.11 Git Progress: Evidence ID v2

## Vấn đề

Lần chạy `dadc6d4 → fdba846`: GitHub trả **27 file, +4.386/-5**, 8 file diff được gửi Ollama nhưng **0 nhận xét khớp dòng diff**. Điều này không chứng minh Ollama sai; có thể AI trả `[]` hoặc không chép đúng nguyên văn dòng theo schema cũ. Báo cáo cũ không có log số ứng viên trước khi sàng lọc.

## Thay đổi

- Backend đánh ID riêng `L1`, `L2`... cho các dòng diff **đã lọc bí mật và thực sự gửi**. Ollama chỉ trả `evidenceId + description`.
- Backend giải ID sang **file + dòng gốc nguyên văn**, vẫn dùng kiểm tra `Screen` cũ để bác bỏ ID bịa, dòng không gửi, nội dung sai hoặc trùng.
- Log backend thêm `AI candidates`, `verified`, `diff files sent` để phân biệt AI trả 0 với bị loại hết.
- Summary có thêm thống kê **nhóm file theo đường dẫn** (backend, frontend, tests, migration, tài liệu), hoàn toàn từ metadata GitHub, không giả là nhận xét AI.
- Bản phân tích được lưu với `ModelName = <Ollama:Model>|gp-evidence-v2` để có thể **chạy lại cùng cặp commit** mà không xóa báo cáo trước. Trong request Ollama, `model` vẫn là tên model thật, chẳng hạn `qwen2.5-coder:7b`.
- Thêm 7 regression tests, gồm giả lập phản hồi Ollama mà không cần gọi mạng.
- Không đổi schema SQL Server, EF migration, quyền truy cập, giao diện, dữ liệu báo cáo cũ.

## Cài trên Windows PowerShell

Giải nén `v011-git-progress-evidence-ids` **ngang cấp** `backend`, `frontend` trong `student-project-management-system`, trên nhánh `feature/v0.11-git-progress-analysis`.

```powershell
git branch --show-current
git status --short
py .\v011-git-progress-evidence-ids\apply_progress_evidence_ids.py . --check
py .\v011-git-progress-evidence-ids\apply_progress_evidence_ids.py .
dotnet build backend
dotnet test backend
```

**Quan trọng:** Bộ cài so khớp nguyên văn bốn file v1, từ chối ghi đè nếu bạn đã sửa nội dung sau khi cài bản Git Progress ban đầu. Nếu `--check` thất bại, không cố `git reset --hard`; hãy đối chiếu khác biệt.

Restart API rồi mở `/git-progress`. Dùng lại Base SHA `dadc6d42b74d16a532def7e2d8deda8db5f206fa` và Head SHA `fdba846c37f2cc824dfa09fff6938a260a6268df`. Bản cũ vẫn trong lịch sử, bản v2 phải tạo **một bản mới** (mã `ModelName` có hậu tố `|gp-evidence-v2`).

Quan sát log backend dạng:

```
Git progress <id>: diff files sent=8, AI candidates=..., verified=...
```

Nếu `candidates=0`, model không sinh nhận xét theo schema. Nếu `candidates>0` nhưng `verified=0`, AI đã chọn ID không hợp lệ hoặc phần mô tả không qua lọc. Không tự tạo kết luận AI giả chỉ để đạt số lượng lớn hơn 0.

Trạng thái phát triển: các bài kiểm tra cài đặt Python chạy ở môi trường tạo bản vá; **C#/.NET build và test thực tế phải chạy trên máy Windows của bạn**.
