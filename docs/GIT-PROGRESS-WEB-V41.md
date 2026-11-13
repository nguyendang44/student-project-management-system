# Chạy thử AI trên web — v4.1

Bản vá dựa trên commit `bc68296020fc01a58e83cfb9ccc09c2819723ee1`, nhánh `feature/v0.11-git-progress-analysis`.

## Áp dụng

Dừng backend/frontend đang chạy. Copy các thư mục `backend`, `frontend`, `docs` trong ZIP vào thư mục dự án và chọn ghi đè. ZIP chỉ chứa file mới/sửa so với commit trên. Không chạy lại script Python v397. Không cần migration mới; giữ cấu hình SQL và tài khoản hiện có.

## Chạy trong VS Code

Mở Ollama trên Windows. Mở terminal PowerShell ở thư mục dự án:

```powershell
cd C:\Users\dawn\student-project-management-system
ollama list
```

Cần có `qwen2.5-coder:7b` (hoặc model bạn đã cấu hình ở `Ollama:Model`). Nếu chưa có model mặc định:

```powershell
ollama pull qwen2.5-coder:7b
```

Terminal 1 — backend, yêu cầu .NET 10 và SQL đang hoạt động:

```powershell
dotnet run --project .\backend\StudentProjects.Api
```

Terminal 2 — frontend:

```powershell
cd C:\Users\dawn\student-project-management-system\frontend
npm install
npm run dev
```

Mở `http://localhost:5173/git-progress`. Nếu Vite báo cổng khác, dùng cổng terminal hiển thị.

## Thử với GitHub của bạn

1. Đăng nhập Student sở hữu Project hoặc Lecturer hướng dẫn Project đó. Admin chỉ xem được báo cáo.
2. Nếu chưa có liên kết repo, vào trang GitHub của dự án để liên kết URL gốc `https://github.com/nguyendang44/student-project-management-system`.
3. Chọn Project và repository trong trang tiến độ Git.
4. Chọn hai commit khác nhau. Danh sách hiện tại là branch mặc định; muốn kiểm tra nhánh `feature/v0.11-git-progress-analysis`, bật nhập SHA thủ công và lấy hai SHA đủ 40 ký tự trên nhánh đó. Base là mốc cũ, Head là hậu duệ mới.
5. Bấm **Phân tích tiến độ**, chờ backend xử lý. Model key mới có đuôi `gp-web-v4.1`, không dùng lại báo cáo v4.0.
6. Xem nhóm công việc, bằng chứng và **Chi tiết kiểm tra AI**. Bấm **Tải kết quả JSON** để gửi lại kết quả cần kiểm tra.

Nếu Ollama không kết nối được, hết thời gian hoặc trả sai cấu trúc: phần Git vẫn hoàn tất và có cảnh báo AI chưa hoàn tất. Mở/sửa Ollama rồi bấm **Thử lại AI cho báo cáo này** (hoặc Phân tích tiến độ với cùng cặp commit). Lượt này cập nhật cùng báo cáo chưa hoàn tất AI; tải JSON trước nếu muốn giữ bản đó. Báo cáo hoàn tất AI vẫn được cache như trước.

## Thay đổi

- Giữ dữ kiện Git khi lượt phân tích Ollama lỗi, tách trạng thái lỗi AI khỏi phản hồi rỗng.
- Lấy ngữ cảnh trước/sau ở tối đa 3 vùng thay đổi mỗi file, vẫn giới hạn tổng 6.000 ký tự. Đây là mẫu code, không phải toàn dự án.
- Rút gọn prompt cho model 7B, bỏ ví dụ chức năng dễ bị sao chép; vẫn kiểm tra ID và dòng diff.
- Nút tải kết quả JSON, cảnh báo AI chưa hoàn tất và cho phép thử lại cùng cặp commit khi lượt AI đầu lỗi.
- Không đưa dữ liệu/đáp án Flask vào web; benchmark không huấn luyện lại trọng số Ollama.

Bản này phục vụ tổng hợp thay đổi Git và diễn giải có bằng chứng. Chưa thêm ô nhập báo cáo sinh viên C1…Ck hoặc lựa chọn release trực tiếp. Có thể dùng SHA của hai release để so sánh. Không tự chấm điểm sinh viên hay đánh giá cách sử dụng Git. Nhận xét qua bộ lọc vẫn cần người xem đối chiếu, không chứng minh phần mềm chạy đúng.

## Kiểm tra tại máy bạn

```powershell
dotnet test .\backend\StudentProjects.IntegrationTests --filter "FullyQualifiedName~GitProgress"
```

Không cần chạy lại benchmark Flask để mở web.

## Kết quả kiểm tra bản vá

- Backend biên dịch thành công; 161/161 test GitProgress đạt, gồm 6 test mới về lỗi AI, hủy tác vụ, ngữ cảnh nhiều vùng và chẩn đoán.
- Frontend: typecheck và production build đạt.
- Kiểm tra lựa chọn commit: 10 assertion đạt.
- Hai kỳ vọng test cũ cũng thất bại trên commit gốc bc68296 đã được cập nhật: bằng chứng route đã gộp không cần fallback trùng lặp; mô tả assertion chính xác không đồng nghĩa test đã chạy. Vẫn kiểm tra từ chối ID giả và khẳng định test thành công không có bằng chứng. Không nới bộ lọc production.
- Chưa chạy end-to-end với SQL/Ollama trên máy người dùng. Các test giả lập phản hồi không đo độ chính xác thực tế của model 7B.
