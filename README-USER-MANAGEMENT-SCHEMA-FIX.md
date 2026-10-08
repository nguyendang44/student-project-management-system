# Sửa API 500 của User Management v0.4

## Nguyên nhân đã xác minh trong source

`GET /api/v1/users` JOIN `Users`, `Students`, `Lecturers`; `POST`/`PUT`/`PATCH` cần `AuditEntries`. Bản v0.4 đã dùng ba bảng này trong API nhưng `StudentProjectsDbContext` còn `ExcludeFromMigrations()` cho chúng. Trong SQL Server, migration `InitialAuth` của v0.3 chỉ tạo bảng xác thực. Tests chạy trên EF Core InMemory nên không phát hiện thiếu bảng SQL Server.

## Cách cài đặt

1. Sao lưu thư mục project và database. Dừng backend `StudentProjects.Api` (`Ctrl+C`); frontend có thể giữ chạy.
2. Giải nén ZIP. Copy **nội dung bên trong** `student-project-management-system/` vào thư mục dự án thật, chỉ ghi đè `StudentProjectsDbContext.cs` và thêm script. Không xóa bất kỳ migration nào đang có.
3. Trong PowerShell ở thư mục gốc dự án:

```powershell
cd C:\Users\dawn\student-project-management-system
powershell -ExecutionPolicy Bypass -File .\scripts\apply-users-v04-schema.ps1
```

Script xác minh migration gốc `InitialAuth` còn tồn tại, build code, tạo migration `UserManagementV04Schema` nếu chưa có, **kiểm tra migration Up() chỉ tạo 3 bảng** và không có lệnh Drop/Alter, sau đó mới chạy `dotnet ef database update`. Nó không chạy `setup-auth.ps1`, không thay đổi JWT secret/password Admin và không xóa DB. Nếu migration không khớp, script dừng **trước khi cập nhật DB** để kiểm tra thủ công.

4. Nếu chạy thành công, khởi động lại backend:

```powershell
dotnet run --project .\backend\StudentProjects.Api
```

5. Tải lại trang `/users` và xác nhận bảng hiển thị không còn lỗi 500. Tiếp tục kiểm thử:

```powershell
dotnet test .\backend\StudentProjects.IntegrationTests\StudentProjects.IntegrationTests.csproj
```

## Giới hạn QA

Bản sửa được QA tĩnh trong môi trường này. Môi trường này không có .NET SDK và không truy cập LocalDB của bạn, nên chưa xác nhận migration chạy thành công trên máy Windows. Nếu script lỗi, gửi nguyên văn dòng lỗi, không xóa database.
