# Mapping: analysis → scaffold

Based on *Lộ trình 10 tuần.docx* (FR-01…FR-21) and *Tuan_2_Phan_tich_yeu_cau_Use_Case.docx* (UC-01…UC-43).

| FR | Modules | Corresponding UC |
|---|---|---|
| FR-01, 02 | Authentication, user/role boundary | UC-01–02 |
| FR-03, 04 | Students, lecturers | UC-03–04 |
| FR-05 | Lecturer capacity | UC-15–17 |
| FR-06, 07 | Topics, proposals, topic registrations | UC-05–10 |
| FR-08–10 | Lecturer requests, capacity | UC-11–14, 17 |
| FR-11 | Projects | UC-18–19 |
| FR-12–14 | Milestones, progress, submissions, evaluation | UC-20–26 |
| FR-15, 16 | GitHub repositories, AI analysis | UC-27–30 |
| FR-17 | Notifications | UC-31–32 |
| FR-18, 19 | Automation jobs | UC-33–34 |
| FR-20, 21 | Dashboard, statistics, reports | UC-35–37, 41–42 |
| Support UC | Monitoring, system configuration, audit | UC-38–40, 43 |

Each module has a frontend route, an API client stub and at least one backend placeholder endpoint. Dedicated admin settings and registration-period UI follow the documented Admin actor, but those pages have no implementation. Login and logout are nonfunctional scaffolds. Public OpenAPI and health routes are operational architecture checks only.

Implementation of FR/UC **is not claimed** by this table; it maps where future code belongs.
