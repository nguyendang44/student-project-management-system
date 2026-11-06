using StudentProjects.Api.Services;
using Xunit;

namespace StudentProjects.IntegrationTests;

public sealed class AiFindingGuardTests
{
    [Fact]
    public void Rejects_unsupported_login_status_claim()
    {
        var finding = new AiFinding("security", "HIGH",
            "backend/StudentProjects.Api/Endpoints/AuthEndpoints.cs", 33,
            "if (user is null || !user.IsActive)",
            "Trả 401 khi người dùng không hoạt động, thiếu kiểm tra tài khoản bị khóa.",
            "Đổi thành 403 và thêm kiểm tra khóa tài khoản.");

        Assert.Null(AiFindingGuard.Screen(finding));
    }

    [Fact]
    public void Rejects_csrf_claim_based_only_on_frontend_login_call()
    {
        var finding = new AiFinding("security", "HIGH",
            "frontend/src/features/auth/auth.api.ts", 4,
            "login: (payload: LoginRequest) => request<LoginResponse>('/auth/login', { method: 'POST' })",
            "Không có token CSRF trong request login.",
            "Thêm CSRF token.");

        Assert.Null(AiFindingGuard.Screen(finding));
    }

    [Fact]
    public void Rejects_auth_claim_from_interface_signature_alone()
    {
        var finding = new AiFinding("security", "LOW",
            "backend/StudentProjects.Application/Abstractions/Contracts.cs", 17,
            "Task ScanDueMilestonesAsync(CancellationToken cancellationToken = default);",
            "Phương thức không có kiểm tra người dùng đăng nhập hoặc quyền truy cập.",
            "Thêm kiểm tra xác thực người dùng vào phương thức.");

        Assert.Null(AiFindingGuard.Screen(finding));
    }

    [Fact]
    public void Rejects_sql_injection_xss_claim_from_login_wrapper_alone()
    {
        var finding = new AiFinding("security", "LOW",
            "frontend/src/features/auth/auth.api.ts", 4,
            "login: (payload: LoginRequest) => request<LoginResponse>('/auth/login', { method: 'POST', body: JSON.stringify(payload) }),",
            "Thiếu kiểm tra đầu vào ở frontend có thể gây SQL Injection hoặc XSS.",
            "Thêm kiểm tra dữ liệu trước khi gửi request login.");

        Assert.Null(AiFindingGuard.Screen(finding));
    }

    [Fact]
    public void Does_not_blanket_suppress_real_frontend_findings()
    {
        var finding = new AiFinding("security", "MEDIUM",
            "frontend/src/features/auth/auth.api.ts", 12,
            "localStorage.setItem('token', token);",
            "Lưu token trong localStorage có thể bị đọc khi phát sinh XSS.",
            "Kiểm tra mô hình phiên và bảo vệ khỏi XSS.");

        Assert.Equal(finding, AiFindingGuard.Screen(finding));
    }

    [Fact]
    public void Downgrades_missing_security_check_claim_in_partial_excerpt()
    {
        var finding = new AiFinding("security", "HIGH", "backend/StudentProjects.Api/Endpoints/Other.cs", 10,
            "var project = await db.Projects.FindAsync(id);",
            "Thiếu kiểm tra quyền truy cập ở endpoint này.",
            "Xác minh cấu hình authorization và quyền Project.");

        var screened = AiFindingGuard.Screen(finding);
        Assert.NotNull(screened);
        Assert.Equal("MEDIUM", screened.Severity);
    }

    [Fact]
    public void Keeps_code_quality_finding_for_human_review()
    {
        var finding = new AiFinding("quality", "LOW", "backend/StudentProjects.Api/Endpoints/Other.cs", 10,
            "return input.Trim();", "Có khả năng trùng logic xử lý.", "Kiểm tra các service trước khi refactor.");

        Assert.Equal(finding, AiFindingGuard.Screen(finding));
    }

    [Fact]
    public void Removes_rejected_csrf_claim_from_summary()
    {
        var result = AiFindingGuard.CautiousNarrative("Frontend có thể bị tấn công CSRF.", []);
        Assert.DoesNotContain("bị tấn công", result);
        Assert.Contains("Chưa đủ bằng chứng", result);
    }
}
