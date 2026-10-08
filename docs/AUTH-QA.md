# QA matrix (Auth v0.3)

| Case | Expected result |
|---|---|
| GET /health | 200 |
| POST /auth/login correct credentials | 200, signed accessToken, role |
| POST /auth/login incorrect password | 401, no account information leak |
| GET /auth/me without JWT | 401 |
| GET /users authenticated Student | 403 |
| GET /dashboard authenticated Student | 501 (business stub) |
| POST /auth/logout authenticated user | 204; older access tokens revoked (401) |
| Disabled account with older token | 401 |
| Login wrong/disabled account | 401 |
| Login rate-limited IP | 429 after configured limit |
| Frontend login | fetch /api/v1/auth/login; menu matches role |
| Frontend reload | logged out (in-memory token by design) |

Environment with SDK must still execute `dotnet build`, `dotnet test`, migration generation/application and SQL Server smoke tests. Static review alone is not a pass for those checks.
