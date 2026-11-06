# Git Progress Analysis v3.6 | AI Explainer (Ollama)

Bản nâng cấp từ v3.5 + hotfix dành cho `student-project-management-system`. Mục đích: **cho Ollama giải thích các nhóm công việc đã được trích xuất từ Git diff**, thay vì phụ thuộc vào các nhận xét riêng lẻ trên từng dòng code. Đây là *diễn giải AI tham khảo*, không phải AI tự xác minh chức năng đã chạy.

## Thay đổi

- Giữ `GitProgressFeatureSynthesis.Extract(diff.Files)` làm nguồn sự thật về các thay đổi có bằng chứng Git.
- Bổ sung bước Ollama thứ hai, nhận **chỉ** tiêu đề/mô tả nhóm công việc và đường dẫn file đã tổng hợp, không gửi thêm bản diff thô.
- Mô hình trả về JSON có `factId` (`F1`, `F2`...) và `explanation`. Backend kiểm tra ID, độ dài, route không có trong bằng chứng, khẳng định quá mức và diễn giải không liên quan. Các dòng bằng chứng **luôn gắn từ dữ liệu backend**, không do AI tự sinh.
- Đặt nguồn `ollama-feature` trên giao diện, phân biệt với `feature-rule` và `ollama` cũ. Lịch sử báo cáo cũ vẫn xem được.
- Nếu lời gọi Ollama diễn giải thất bại hoặc trả về 0 nội dung đạt điều kiện, báo cáo vẫn `COMPLETED` với phần tổng hợp từ Git. Không thay đổi bảng SQL Server.
- Mã phiên bản cache `gp-explain-v3.6` để báo cáo cùng cặp commit được phân tích lại.

## Cài đặt (Windows / PowerShell)

1. **Tắt backend** đang chạy. Giải nén thư mục `v011-git-progress-ai-explainer-v36` ngang hàng với `backend` và `frontend` trong root repository.
2. Mở PowerShell tại root repository, chạy kiểm tra trước:

```powershell
py .\v011-git-progress-ai-explainer-v36\apply_ai_explainer_v36.py . --check
```

3. Chỉ khi thấy `CHECK OK` mới áp dụng và kiểm thử:

```powershell
py .\v011-git-progress-ai-explainer-v36\apply_ai_explainer_v36.py .
dotnet build backend
dotnet test backend
cd frontend
npm run build
cd ..
```

4. Nếu build/test đều đạt, khởi động lại backend:

```powershell
dotnet run --project .\backend\StudentProjects.Api 2>&1 | Tee-Object -FilePath .\git-progress-debug.log
```

5. Vào `/git-progress`, chạy lại cặp SHA:
   - Base: `dadc6d42b74d16a532def7e2d8deda8db5f206fa`
   - Head: `fdba846c37f2cc824dfa09fff6938a260a6268df`

Xem log bằng:

```powershell
Select-String .\git-progress-debug.log -Pattern 'groupedFeatures=|aiFeatureExplanations=|AI explanation unavailable' | ForEach-Object { $_.Line }
```

`groupedFeatures=6, aiFeatureExplanations=N` nghĩa là có 6 mục theo quy tắc và N diễn giải AI vượt qua kiểm tra. **N có thể bằng 0** nếu mô hình không đáp ứng yêu cầu, khi đó 6 mục bằng chứng vẫn giữ nguyên.

## Cấu trúc bản vá

- Sửa `AiGitProgressClient.cs`, `GitProgressWorker.cs`.
- Thêm `GitProgressAiExplanationPolicy.cs` và `GitProgressAiExplanationV36Tests.cs`.
- Sửa `frontend/src/features/gitProgress/gitProgress.api.ts` và `frontend/src/views/GitProgressV11View.vue`.
- Cập nhật 3 assertion tên phiên bản trong các test có sẵn.
- Không đụng đến `GitProgressFeatureSynthesis.cs` và hotfix `using var` v3.5 đã cài.

## Giới hạn và an toàn

Bộ cài yêu cầu 4 file đang sửa phải **khớp đúng** payload v3.5, để tránh ghi đè các thay đổi không biết trước. Nếu `--check` báo khác cấu trúc, **không ép cài**. Bản sao lưu các file thay đổi được đặt tại thư mục `v011-gp-v36-backup-...` ở **ngoài** repository.

6/6 bài kiểm tra installer Python đã đạt. Chưa chạy được `dotnet build`, `dotnet test` hay Ollama thực tế trong môi trường tạo bản vá này. Báo cáo AI vẫn cần giảng viên xác minh và có thể không sinh được diễn giải nào đạt điều kiện.
