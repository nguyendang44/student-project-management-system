import type { Role } from '../features/auth/auth.types'
export type ModuleId =
  | 'dashboard'
  | 'topics'
  | 'proposals'
  | 'topicregistrations'
  | 'lecturers'
  | 'capacity'
  | 'lecturerrequests'
  | 'projects'
  | 'milestones'
  | 'progress'
  | 'submissions'
  | 'evaluations'
  | 'github'
  | 'ai'
  | 'notifications'
  | 'statistics'
  | 'reports'
  | 'users'
  | 'periods'
  | 'automation'
  | 'audit'
  | 'errors'
  | 'settings'
export interface ModuleSpec {
  id: ModuleId
  path: string
  group: string
  title: string
  description: string
  roles: Role[]
  requirements: string
  useCases: string[]
  columns: string[]
  endpoints: string[]
}
export const modules: ModuleSpec[] = [
  {
    "id": "dashboard",
    "path": "/dashboard",
    "group": "Chung",
    "title": "Dashboard",
    "description": "Tổng quan dự án và tiến độ theo vai trò",
    "roles": [
      "Student",
      "Lecturer",
      "Admin"
    ],
    "requirements": "FR-20, FR-21",
    "useCases": [
      "UC-35"
    ],
    "columns": [
      "Chỉ số",
      "Trạng thái",
      "Tiến độ"
    ],
    "endpoints": [
      "GET /dashboard"
    ]
  },
  {
    "id": "topics",
    "path": "/topics",
    "group": "Đề tài",
    "title": "Danh sách đề tài",
    "description": "Tra cứu đề tài, mở/đóng đăng ký và xem chi tiết",
    "roles": [
      "Student",
      "Lecturer",
      "Admin"
    ],
    "requirements": "FR-06, FR-07",
    "useCases": [
      "UC-05",
      "UC-06"
    ],
    "columns": [
      "Mã đề tài",
      "Tên đề tài",
      "Trạng thái",
      "Người đề xuất"
    ],
    "endpoints": [
      "GET /topics",
      "POST /topics",
      "PATCH /topics/{id}"
    ]
  },
  {
    "id": "proposals",
    "path": "/topic-proposals",
    "group": "Đề tài",
    "title": "Đề xuất cũ",
    "description": "Chỉ xem dữ liệu cũ. Đề xuất mới thực hiện trong Đăng ký đề tài & giảng viên hướng dẫn",
    "roles": [
      "Admin"
    ],
    "requirements": "FR-06",
    "useCases": [
      "UC-08",
      "UC-09",
      "UC-10"
    ],
    "columns": [
      "Đề tài",
      "Người đề xuất",
      "Trạng thái",
      "Phê duyệt"
    ],
    "endpoints": [
      "GET /topic-proposals",
      "POST /topic-proposals",
      "POST /topic-proposals/{id}/approve"
    ]
  },
  {
    "id": "topicregistrations",
    "path": "/topic-registrations",
    "group": "Đề tài",
    "title": "Đăng ký đề tài",
    "description": "Theo dõi yêu cầu đăng ký đề tài và trạng thái",
    "roles": [
      "Student",
      "Lecturer",
      "Admin"
    ],
    "requirements": "FR-07",
    "useCases": [
      "UC-07",
      "UC-41"
    ],
    "columns": [
      "Sinh viên",
      "Đề tài",
      "Ngày đăng ký",
      "Trạng thái"
    ],
    "endpoints": [
      "GET /topic-registrations",
      "POST /topic-registrations"
    ]
  },
  {
    "id": "lecturers",
    "path": "/lecturers",
    "group": "Hướng dẫn",
    "title": "Giảng viên",
    "description": "Tra cứu danh sách, chuyên môn và thông tin hướng dẫn",
    "roles": [
      "Student",
      "Lecturer",
      "Admin"
    ],
    "requirements": "FR-04",
    "useCases": [
      "UC-04",
      "UC-16"
    ],
    "columns": [
      "Giảng viên",
      "Chuyên môn",
      "Còn chỗ",
      "Trạng thái"
    ],
    "endpoints": [
      "GET /lecturers"
    ]
  },
  {
    "id": "capacity",
    "path": "/lecturer-capacity",
    "group": "Hướng dẫn",
    "title": "Sức chứa giảng viên",
    "description": "Giới hạn hướng dẫn và số lượng sinh viên còn trống",
    "roles": [
      "Student",
      "Lecturer",
      "Admin"
    ],
    "requirements": "FR-05, FR-10",
    "useCases": [
      "UC-15",
      "UC-16",
      "UC-17"
    ],
    "columns": [
      "Giảng viên",
      "Tối đa",
      "Đã nhận",
      "Còn trống"
    ],
    "endpoints": [
      "GET /lecturer-capacity",
      "PUT /lecturer-capacity/me"
    ]
  },
  {
    "id": "lecturerrequests",
    "path": "/lecturer-requests",
    "group": "Hướng dẫn",
    "title": "Yêu cầu hướng dẫn",
    "description": "Đăng ký giảng viên; chấp nhận hoặc từ chối request",
    "roles": [
      "Student",
      "Lecturer",
      "Admin"
    ],
    "requirements": "FR-08, FR-09, FR-10",
    "useCases": [
      "UC-11",
      "UC-12",
      "UC-13",
      "UC-14"
    ],
    "columns": [
      "Sinh viên",
      "Giảng viên",
      "Đề tài",
      "Trạng thái"
    ],
    "endpoints": [
      "GET /lecturer-requests",
      "POST /lecturer-requests",
      "POST /lecturer-requests/{id}/accept"
    ]
  },
  {
    "id": "projects",
    "path": "/projects",
    "group": "Dự án",
    "title": "Quản lý dự án",
    "description": "Theo dõi vòng đời dự án và giảng viên hướng dẫn",
    "roles": [
      "Student",
      "Lecturer",
      "Admin"
    ],
    "requirements": "FR-11",
    "useCases": [
      "UC-18",
      "UC-19"
    ],
    "columns": [
      "Dự án",
      "Đề tài",
      "Giảng viên",
      "Trạng thái"
    ],
    "endpoints": [
      "GET /projects",
      "GET /projects/{id}"
    ]
  },
  {
    "id": "milestones",
    "path": "/milestones",
    "group": "Dự án",
    "title": "Milestone",
    "description": "Các mốc công việc, deadline và trạng thái duyệt",
    "roles": [
      "Student",
      "Lecturer",
      "Admin"
    ],
    "requirements": "FR-12",
    "useCases": [
      "UC-20",
      "UC-21"
    ],
    "columns": [
      "Milestone",
      "Dự án",
      "Deadline",
      "Trạng thái"
    ],
    "endpoints": [
      "GET /milestones",
      "POST /milestones"
    ]
  },
  {
    "id": "progress",
    "path": "/progress",
    "group": "Dự án",
    "title": "Cập nhật tiến độ",
    "description": "Nhật ký tiến độ, % hoàn thành và ghi chú",
    "roles": [
      "Student",
      "Lecturer",
      "Admin"
    ],
    "requirements": "FR-13",
    "useCases": [
      "UC-22",
      "UC-42"
    ],
    "columns": [
      "Dự án",
      "Milestone",
      "Hoàn thành",
      "Cập nhật"
    ],
    "endpoints": [
      "GET /progress",
      "POST /progress"
    ]
  },
  {
    "id": "submissions",
    "path": "/submissions",
    "group": "Dự án",
    "title": "Nộp kết quả",
    "description": "Sinh viên nộp kết quả milestone để giảng viên kiểm tra",
    "roles": [
      "Student",
      "Lecturer",
      "Admin"
    ],
    "requirements": "FR-13, FR-14",
    "useCases": [
      "UC-23",
      "UC-24",
      "UC-25"
    ],
    "columns": [
      "Milestone",
      "Người nộp",
      "Tài liệu",
      "Trạng thái"
    ],
    "endpoints": [
      "GET /submissions",
      "POST /submissions"
    ]
  },
  {
    "id": "evaluations",
    "path": "/evaluations",
    "group": "Dự án",
    "title": "Đánh giá và nhận xét",
    "description": "Giảng viên kiểm tra và đánh giá kết quả, yêu cầu sửa",
    "roles": [
      "Lecturer",
      "Admin"
    ],
    "requirements": "FR-14",
    "useCases": [
      "UC-24",
      "UC-25",
      "UC-26"
    ],
    "columns": [
      "Dự án",
      "Mốc tiến độ",
      "Nhận xét",
      "Kết quả"
    ],
    "endpoints": [
      "GET /evaluations",
      "POST /evaluations"
    ]
  },
  {
    "id": "github",
    "path": "/repositories",
    "group": "Tích hợp",
    "title": "GitHub repositories",
    "description": "Liên kết repository và thông tin commit/branch",
    "roles": [
      "Student",
      "Lecturer",
      "Admin"
    ],
    "requirements": "FR-15",
    "useCases": [
      "UC-27",
      "UC-28"
    ],
    "columns": [
      "Repository",
      "Dự án",
      "Branch",
      "Cập nhật"
    ],
    "endpoints": [
      "GET /repositories",
      "POST /repositories"
    ]
  },
  {
    "id": "ai",
    "path": "/ai-analysis",
    "group": "Tích hợp",
    "title": "AI code analysis",
    "description": "Báo cáo chất lượng code mang tính tham khảo",
    "roles": [
      "Student",
      "Lecturer",
      "Admin"
    ],
    "requirements": "FR-16",
    "useCases": [
      "UC-29",
      "UC-30"
    ],
    "columns": [
      "Dự án",
      "Lần phân tích",
      "Trạng thái",
      "Báo cáo"
    ],
    "endpoints": [
      "GET /ai-analysis",
      "POST /ai-analysis"
    ]
  },
  {
    "id": "notifications",
    "path": "/notifications",
    "group": "Chung",
    "title": "Thông báo",
    "description": "Nhận nhắc hạn, trạng thái đăng ký và nhận xét",
    "roles": [
      "Student",
      "Lecturer",
      "Admin"
    ],
    "requirements": "FR-17",
    "useCases": [
      "UC-31",
      "UC-32"
    ],
    "columns": [
      "Ngày",
      "Loại",
      "Nội dung",
      "Đã xem"
    ],
    "endpoints": [
      "GET /notifications"
    ]
  },
  {
    "id": "statistics",
    "path": "/statistics",
    "group": "Báo cáo",
    "title": "Thống kê",
    "description": "Dữ liệu tổng hợp được phân quyền",
    "roles": [
      "Lecturer",
      "Admin"
    ],
    "requirements": "FR-20, FR-21",
    "useCases": [
      "UC-36"
    ],
    "columns": [
      "Chỉ số",
      "Giá trị",
      "Phạm vi",
      "Thời gian"
    ],
    "endpoints": [
      "GET /statistics"
    ]
  },
  {
    "id": "reports",
    "path": "/reports",
    "group": "Báo cáo",
    "title": "Báo cáo",
    "description": "Báo cáo tổng hợp tình trạng đề tài và tiến độ",
    "roles": [
      "Admin"
    ],
    "requirements": "FR-21",
    "useCases": [
      "UC-37"
    ],
    "columns": [
      "Báo cáo",
      "Kỳ",
      "Trạng thái",
      "Xuất file"
    ],
    "endpoints": [
      "GET /reports"
    ]
  },
  {
    "id": "users",
    "path": "/users",
    "group": "Quản trị",
    "title": "Người dùng",
    "description": "Tài khoản sinh viên, giảng viên và phân quyền",
    "roles": [
      "Admin"
    ],
    "requirements": "FR-02, FR-03, FR-04",
    "useCases": [
      "UC-03",
      "UC-04"
    ],
    "columns": [
      "Mã",
      "Họ tên",
      "Vai trò",
      "Trạng thái"
    ],
    "endpoints": [
      "GET /users",
      "POST /users",
      "GET /users/{id}",
      "PUT /users/{id}",
      "PATCH /users/{id}/status"
    ]
  },
  {
    "id": "periods",
    "path": "/registration-periods",
    "group": "Quản trị",
    "title": "Đợt đăng ký",
    "description": "Mở/đóng đợt đăng ký và quy định",
    "roles": [
      "Admin"
    ],
    "requirements": "FR-07, FR-08",
    "useCases": [
      "UC-41"
    ],
    "columns": [
      "Đợt",
      "Bắt đầu",
      "Kết thúc",
      "Trạng thái"
    ],
    "endpoints": [
      "GET /registration-periods",
      "POST /registration-periods"
    ]
  },
  {
    "id": "automation",
    "path": "/automation",
    "group": "Quản trị",
    "title": "Automation",
    "description": "Kiểm tra deadline và nhật ký tác vụ theo lịch",
    "roles": [
      "Admin"
    ],
    "requirements": "FR-18, FR-19",
    "useCases": [
      "UC-33",
      "UC-34",
      "UC-38"
    ],
    "columns": [
      "Job",
      "Lần chạy",
      "Trạng thái",
      "Lỗi"
    ],
    "endpoints": [
      "GET /automation/runs"
    ]
  },
  {
    "id": "audit",
    "path": "/audit-log",
    "group": "Quản trị",
    "title": "Lịch sử xử lý",
    "description": "Theo dõi thay đổi trạng thái và thao tác quan trọng",
    "roles": [
      "Admin"
    ],
    "requirements": "NFR-01",
    "useCases": [
      "UC-43"
    ],
    "columns": [
      "Thời gian",
      "Tác nhân",
      "Hành động",
      "Đối tượng"
    ],
    "endpoints": [
      "GET /audit-log"
    ]
  },
  {
    "id": "errors",
    "path": "/system-errors",
    "group": "Quản trị",
    "title": "Lỗi hệ thống",
    "description": "Theo dõi lỗi kỹ thuật và tình trạng xử lý",
    "roles": [
      "Admin"
    ],
    "requirements": "NFR-04",
    "useCases": [
      "UC-39"
    ],
    "columns": [
      "Thời gian",
      "Mức độ",
      "Nguồn",
      "Thông báo"
    ],
    "endpoints": [
      "GET /system-errors"
    ]
  },
  {
    "id": "settings",
    "path": "/settings",
    "group": "Quản trị",
    "title": "Cấu hình hệ thống",
    "description": "Quy tắc vận hành và tùy chọn chung",
    "roles": [
      "Admin"
    ],
    "requirements": "NFR-04",
    "useCases": [
      "UC-40"
    ],
    "columns": [
      "Khóa",
      "Giá trị",
      "Nhóm",
      "Cập nhật"
    ],
    "endpoints": [
      "GET /settings",
      "PUT /settings/{key}"
    ]
  }
] as ModuleSpec[]
export const moduleById = (id: ModuleId) => modules.find((mod) => mod.id === id)!
