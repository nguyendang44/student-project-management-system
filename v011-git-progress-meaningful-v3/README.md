# Git Progress v3: Meaningful Progress Analysis

Nâng cấp từ **Git Progress Evidence ID v2**. Mục tiêu: thay những nhận xét vô nghĩa như “class được khai báo” bằng các quan sát về API, xử lý backend/AI, giao diện, kiểm thử và CSDL. **Không suy đoán code đã chạy đúng chỉ từ diff.**

## Thay đổi

1. Chọn tối đa 10 file diff theo vòng: API, AI/Backend, Frontend, Kiểm thử, CSDL, Backend khác. File có diff lớn không còn độc chiếm input. Bỏ qua `.Designer.cs`, `*ModelSnapshot.cs`, comment XML, import và dòng chỉ khai báo class.
2. Dòng diff có trọng số theo thao tác (`MapPost`, `await`, UI event, `Assert`, `CreateTable`...), ngân sách tối đa ~11.000 ký tự và ~1.300 ký tự/file. Vẫn giữ lọc đường dẫn và bí mật của v2.
3. Ollama dùng `evidenceId` do backend cấp; bộ lọc v3 đối chiếu lại dòng gốc, loại nhận xét chung chung/khẳng định không chứng minh được, giới hạn 2 nhận xét/file và ưu tiên 1 nhận xét/nhóm trước.
4. Giao diện `/git-progress` nhóm nhận xét theo loại công việc, có thể đọc cả báo cáo v1/v2 cũ (không có trường `area`).
5. Giữ nguyên EF migration, database, endpoint API và logic phân quyền. Thêm bài kiểm thử C# cho chọn mẫu đa nhóm, chống trích dẫn bịa, chống lặp và chống khẳng định quá mức.
6. Khóa phân tích mới `qwen2.5-coder:7b|gp-meaningful-v3` làm cùng cặp commit tạo báo cáo mới, không sửa lịch sử v2.

## Cài đặt

Giải nén thư mục `v011-git-progress-meaningful-v3` vào **thư mục gốc project**, ngang hàng với `backend`, `frontend`. Mở PowerShell tại project. Nên giữ nhánh `feature/v0.11-git-progress-analysis` và chưa commit trong khi kiểm thử.

```powershell
$PWD.Path
git branch --show-current
git status --short
py .\v011-git-progress-meaningful-v3\apply_progress_meaningful_v3.py . --check
py .\v011-git-progress-meaningful-v3\apply_progress_meaningful_v3.py .
dotnet build backend
dotnet test backend
cd frontend
npm run build
cd ..
```

Nếu `--check` không in `CHECK OK`, **không dùng `git reset --hard`**. Bộ cài yêu cầu đúng mã nguồn v2 đã gửi trước đó và từ chối ghi đè file đã chỉnh khác.

Bộ cài tự lưu backup những file được thay bên ngoài repo, tại `..\v011-git-progress-meaningful-v3-backup-*`. Sau cài, tắt rồi khởi động lại backend. Không cần chạy `dotnet ef`.

## Kiểm thử thủ công

Mở `http://127.0.0.1:5173/git-progress` và chọn lại:

- Base: `dadc6d42b74d16a532def7e2d8deda8db5f206fa`
- Head: `fdba846c37f2cc824dfa09fff6938a260a6268df`

Kỳ vọng bảng thống kê GitHub vẫn là 1 commit, 27 file, +4386/-5 dòng. Đoạn diff gửi AI ưu tiên nhiều nhóm code khác nhau, nhận xét được chia nhóm có dẫn dòng diff. **Không cam kết số nhận xét sẽ lớn hơn 0**: AI chỉ được giữ các mô tả qua bộ lọc. Nhìn log backend `AI candidates=..., verified=...` để phân biệt model không sinh kết quả với kết quả bị loại. Kiểm tra kết quả Ollama có thể khác giữa các lần chạy.

## Lưu ý về chất lượng

- Bằng chứng Git diff cho biết dòng code thay đổi, không thể thay kiểm thử thực thi, review giảng viên hoặc xác nhận chức năng đã hoàn thành.
- Chỉ các dòng code được chọn mới được gửi mô hình, không phải toàn bộ 27 file; tài liệu README và file migration sinh tự động vẫn được tính trong thống kê GitHub nhưng có thể không gửi Ollama.
- Bộ lọc từ khóa là một lớp kiểm tra heuristic, có thể bỏ sót nhận xét đúng hoặc giữ lại nhận xét chưa hoàn toàn chính xác; đối chiếu file gốc trước khi chốt đánh giá.

**Trạng thái kiểm thử:** Installer và kiểm tra độc lập có thể xác nhận tại môi trường tạo bản vá. .NET SDK, SQL Server và trình duyệt dự án không có trong môi trường đó; bạn phải chạy `dotnet build`, `dotnet test`, `npm run build`, và kiểm thử UI trên Windows.
