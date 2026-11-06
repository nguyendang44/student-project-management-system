# Student Project Management System v0.11 — Git Progress Analysis

Bản nâng cấp dành cho source **v0.11 v7** đã commit. Không sửa nội dung báo cáo Code Analysis đã lưu; bổ sung báo cáo Git Progress riêng.

## Chức năng

- Chọn 2 **SHA commit đầy đủ (40 ký tự)**, lấy từ GitHub v0.10 hoặc nhập thủ công.
- Chỉ chấp nhận `base -> head` khi `head` kế thừa `base` (`GitHub Compare API` có `status = ahead`). Không suy luận tiến độ từ số commit hoặc thông điệp commit.
- GitHub API cố định `https://api.github.com/`, kiểm tra URL repository đã liên kết, sử dụng `GitHub:Token` server-side nếu có. Không chuyển token ra frontend.
- Hàng đợi nền `PENDING -> RUNNING -> COMPLETED/FAILED`; lưu SQL Server ở bảng mới `GitProgressRuns` với khóa phân biệt repository, base SHA, head SHA và tên model.
- AI Ollama nhận tối đa 12 file, các dòng diff được lọc, không gửi toàn bộ repo. Phát hiện AI chỉ được giữ nếu `file + dòng +/-` khớp chính xác với prompt đã gửi. **Có bằng chứng thay đổi không đồng nghĩa code chạy đúng, feature đã hoàn thành hoặc cá nhân nào đã viết code.**
- Tối đa 100 file metadata lưu trong DB, tối đa 6 mục AI có bằng chứng; không lưu raw patch. API GitHub có giới hạn, nếu kết quả chạm giới hạn thì báo phạm vi có thể chưa đầy đủ.
- Student và giảng viên được phân công có thể tạo báo cáo. Student, giảng viên được phân công và Admin xem báo cáo. Nhóm khác nhận 403.
- Giao diện `/git-progress` truy cập từ trang `/ai-analysis`, có lịch sử, trạng thái nền, thống kê GitHub, file thay đổi và trích dẫn.

## Cài đặt trên Windows (PowerShell)

Điều kiện: đã chuyển sang `feature/v0.11-git-progress-analysis`, Git working tree sạch, v7 build/test tốt, `Ollama` và SQL Server LocalDB hoạt động. Giải nén thư mục `v011-git-progress-upgrade` ở **ngang cấp** `backend` và `frontend`.

Từ root repository:

```powershell
git branch --show-current
git status --short
py .\v011-git-progress-upgrade\apply_git_progress.py . --check
py .\v011-git-progress-upgrade\apply_git_progress.py .
dotnet build backend
```

**Migration bắt buộc**: Installer không cố tự sửa file snapshot, không tự can thiệp SQL Server. Dùng EF Core CLI tạo migration khớp mô hình và snapshot hiện có:

```powershell
dotnet ef migrations add V011GitProgressAnalysis --project backend/StudentProjects.Infrastructure --startup-project backend/StudentProjects.Api
```

**Trước khi database update:** mở file migration mới trong `backend/StudentProjects.Infrastructure/Persistence/Migrations` và đọc `Up/Down`:

- `Up` chỉ được tạo bảng `GitProgressRuns`, các khóa ngoại và index liên quan.
- `Down` chỉ được xóa bảng `GitProgressRuns`.
- **Nếu có lệnh DropIndex, AlterColumn, DropTable khác**, không chạy `database update`; dừng để xử lý khác biệt schema trước.

Khi migration đúng:

```powershell
dotnet ef database update --project backend/StudentProjects.Infrastructure --startup-project backend/StudentProjects.Api
dotnet test backend
cd frontend
npm run build
cd ..
dotnet run --project backend/StudentProjects.Api
```

Mở web `http://localhost:5173/ai-analysis`, chọn liên kết **Phân tích tiến độ Git giữa hai commit**, hoặc vào `/git-progress`. Dev server Vite cần khởi động nếu chưa chạy (`cd frontend; npm run dev`).

Lần đầu chạy ví dụ chọn commit trước làm base, commit sau làm head. API `/v11/repositories/{id}/progress` tạo một yêu cầu; frontend tự làm mới khoảng 4 giây/lần. Báo cáo đã lưu ở SQL được xem lại mà không cần gọi GitHub.

## Endpoint

- `POST /api/v1/v11/repositories/{id}/progress` JSON `{"baseSha":"...40 hex...","headSha":"...40 hex..."}`; 202 (hoặc 200 nếu trùng).
- `GET /api/v1/v11/repositories/{id}/progress` lịch sử tối đa 40 lần.
- `GET /api/v1/v11/progress/{id}` báo cáo và trích dẫn.

## Test và kiểm tra an toàn

```powershell
dotnet test backend --filter FullyQualifiedName~GitProgressPolicyTests
dotnet test backend
git diff --check
git status --short
```

- Các mục AI không có chính xác file + dòng diff sẽ bị loại. Các dòng nghi lộ bí mật và file nhạy cảm không được đưa vào AI.
- Repo lớn, nhánh phân kỳ, diff quá lớn hoặc GitHub rate-limit được báo lỗi; không giả làm báo cáo đầy đủ.
- Dự án hiện dùng **single-instance background worker** với LocalDB. Không chạy đồng thời nhiều API worker khi chưa thêm distributed lease.
- Khi `FAILED`, nhấn phân tích lại cặp SHA để retry, nhưng nên đọc `errorMessage` trước.
- Bộ test Python của installer không thay thế cho .NET compilation, EF migration và thực thi kết nối GitHub/Ollama thực tế.

## Quay lại nếu cần

`apply_git_progress.py` sao lưu bốn file chỉnh sửa vào thư mục `v011-git-progress-backup-...` **bên ngoài** repo. Không `git reset --hard` khi có thay đổi riêng. Nếu EF migration đã được áp dụng, phải xử lý rollback migration riêng trước khi gỡ code mới.

Không commit token từ user-secrets hoặc thư mục bộ cài. Sau khi test đạt, stage riêng `backend frontend`, xem `git diff --cached --stat` rồi commit lên nhánh tính năng.
