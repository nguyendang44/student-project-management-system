# Git Progress v3.5 — Tích hợp vào frontend chính

Bộ cài bổ sung **Phân tích tiến độ Git** vào menu trái của hệ thống `StudentProjects`, nhóm **Tích hợp**, ngay sau **AI code analysis**, và thêm liên kết ở trang **Dashboard**. Giữ nguyên giao diện `/git-progress` cùng tính năng, API, quyền và lịch sử báo cáo đã có.

## Thay đổi

- `frontend/src/app/modules.ts`: thêm `ModuleId` và mục menu, vai trò Student / Lecturer / Admin.
- `frontend/src/app/router.ts`: chuyển `/git-progress` từ route riêng sang module route tự động, tránh trùng đường dẫn; vẫn dùng `GitProgressV11View.vue`.
- `frontend/src/views/DashboardV08View.vue`: thêm lối tắt **Phân tích tiến độ Git**.
- `AppLayout.vue` **không cần sửa**, vì sidebar tự đọc `modules.ts` và lọc theo vai trò.

Admin có quyền xem báo cáo theo API hiện tại; chỉ Student và Lecturer được chỉ định mới tạo yêu cầu phân tích. Bộ cài không mở rộng quyền truy cập backend.

## Cài trên Windows

Giải nén thư mục `v011-git-progress-main-navigation` vào root dự án, ngang hàng `backend` và `frontend`. Tại PowerShell root:

```powershell
py .\v011-git-progress-main-navigation\apply_main_navigation.py . --check
py .\v011-git-progress-main-navigation\apply_main_navigation.py .
cd frontend
npm run build
npm run dev
```

Mở web, đăng nhập và xem menu trái nhóm **Tích hợp**. Nút menu **Phân tích tiến độ Git** mở `/git-progress` ngay trong `AppLayout`, với header, thanh điều hướng và phân quyền giống các module khác. Dashboard cũng có liên kết. Không chạy lại bộ cài hai lần.

## Kiểm tra Git

```powershell
git diff --check
git diff -- frontend/src/app/modules.ts frontend/src/app/router.ts frontend/src/views/DashboardV08View.vue
git status --short
```

Không có thay đổi backend hay migration. Bộ cài sao lưu 3 file gốc sang thư mục `v011-gp-nav-backup-...` nằm **bên ngoài** repository. Nếu source của bạn đã đổi cấu trúc menu/router, `--check` sẽ dừng an toàn, không ghi đè.
