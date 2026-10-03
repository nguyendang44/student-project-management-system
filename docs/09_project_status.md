\# 9. Project Status



\## 9.1. Mục tiêu



Project Status dùng để biểu diễn trạng thái hiện tại của một project trong quá trình thực hiện.



Trạng thái giúp Student, Lecturer và Admin biết project đang ở giai đoạn nào và xác định các bước xử lý tiếp theo.



\## 9.2. Các trạng thái của Project



Project sử dụng các trạng thái chính:



```text

DRAFT → REGISTERED → IN\_PROGRESS → COMPLETED

```



Project cũng có thể được hủy trong quá trình thực hiện:



```text

IN\_PROGRESS → CANCELLED

```



| Status | Ý nghĩa |

|---|---|

| `DRAFT` | Project mới được tạo nhưng chưa hoàn tất đăng ký |

| `REGISTERED` | Project đã được đăng ký thành công |

| `IN\_PROGRESS` | Project đang được thực hiện |

| `COMPLETED` | Project đã hoàn thành và được Lecturer xác nhận |

| `CANCELLED` | Project đã bị hủy |



\## 9.3. DRAFT



`DRAFT` là trạng thái ban đầu của project.



Project có thể ở trạng thái `DRAFT` khi thông tin project đang được tạo hoặc chưa hoàn tất quá trình đăng ký.



Các thông tin có thể được thiết lập:



\- Student thực hiện project.

\- Lecturer hướng dẫn.

\- Topic của project.

\- Thông tin mô tả project.

\- Thời gian thực hiện.



Project chưa được xem là đang thực hiện chính thức ở trạng thái này.



\## 9.4. REGISTERED



Project chuyển sang `REGISTERED` khi quá trình đăng ký đã hoàn tất.



Điều kiện:



\- Student đã đăng ký Lecturer.

\- Lecturer đã chấp nhận yêu cầu.

\- Lecturer vẫn còn capacity.

\- Thông tin Student, Lecturer và Topic hợp lệ.



Quy trình:



```text

Student

&#x20;  |

&#x20;  v

Đăng ký Lecturer

&#x20;  |

&#x20;  v

Lecturer Accept

&#x20;  |

&#x20;  v

REGISTERED

```



\## 9.5. IN\_PROGRESS



Project chuyển sang `IN\_PROGRESS` khi project bắt đầu được thực hiện.



Ở trạng thái này:



\- Lecturer có thể tạo và quản lý milestone.

\- Student thực hiện các milestone.

\- Student cập nhật tiến độ.

\- Student nộp kết quả.

\- Lecturer đánh giá milestone.

\- Hệ thống theo dõi deadline.

\- Hệ thống gửi notification khi cần thiết.



Quy trình:



```text

REGISTERED

&#x20;    |

&#x20;    v

IN\_PROGRESS

&#x20;    |

&#x20;    +----> Milestone 1

&#x20;    |

&#x20;    +----> Milestone 2

&#x20;    |

&#x20;    +----> Milestone 3

```



\## 9.6. COMPLETED



Project chuyển sang `COMPLETED` khi tất cả milestone cần thiết đã được hoàn thành và Lecturer xác nhận project hoàn tất.



Điều kiện:



\- Các milestone bắt buộc đã ở trạng thái `APPROVED`.

\- Student đã hoàn thành các yêu cầu của project.

\- Lecturer xác nhận hoàn thành project.



Quy trình:



```text

IN\_PROGRESS

&#x20;    |

&#x20;    v

Kiểm tra Milestones

&#x20;    |

&#x20;    v

Tất cả milestone = APPROVED

&#x20;    |

&#x20;    v

Lecturer xác nhận

&#x20;    |

&#x20;    v

COMPLETED

```



Scheduled Job có thể kiểm tra xem project đã đủ điều kiện hoàn thành hay chưa, nhưng không thay thế bước xác nhận cuối cùng của Lecturer.



\## 9.7. CANCELLED



Project có thể chuyển sang `CANCELLED` khi project không tiếp tục được thực hiện.



Một số trường hợp:



\- Student hoặc Lecturer đề nghị hủy project.

\- Project không thể tiếp tục thực hiện.

\- Admin thực hiện hủy theo quy trình của hệ thống.



Quy trình:



```text

IN\_PROGRESS

&#x20;     |

&#x20;     v

Yêu cầu hủy Project

&#x20;     |

&#x20;     v

Xác nhận hủy

&#x20;     |

&#x20;     v

CANCELLED

```



Khi project ở trạng thái `CANCELLED`, Student không được tiếp tục cập nhật tiến độ hoặc milestone nếu chưa có quy trình mở lại project.



\## 9.8. Quy tắc chuyển trạng thái



| Trạng thái hiện tại | Sự kiện | Trạng thái tiếp theo |

|---|---|---|

| `DRAFT` | Hoàn tất đăng ký | `REGISTERED` |

| `REGISTERED` | Bắt đầu thực hiện | `IN\_PROGRESS` |

| `IN\_PROGRESS` | Tất cả milestone được duyệt và Lecturer xác nhận | `COMPLETED` |

| `IN\_PROGRESS` | Project bị hủy | `CANCELLED` |



Không sử dụng `REJECTED` làm trạng thái của Project.



Việc từ chối được xử lý ở các đối tượng liên quan:



\- Topic proposal có thể có trạng thái `REJECTED`.

\- Lecturer request có thể có trạng thái `REJECTED`.



\## 9.9. Project Status và Milestone Status



Project Status và Milestone Status được quản lý độc lập nhưng có quan hệ với nhau.



```text

&#x20;            PROJECT

&#x20;               |

&#x20;               v

&#x20;         IN\_PROGRESS

&#x20;               |

&#x20;       +-------+-------+

&#x20;       |       |       |

&#x20;       v       v       v

&#x20;     MS-01   MS-02   MS-03

&#x20;       |       |       |

&#x20;       v       v       v

&#x20;   APPROVED APPROVED APPROVED

&#x20;       \\       |       /

&#x20;        \\      |      /

&#x20;         +-----+-----+

&#x20;               |

&#x20;               v

&#x20;      Lecturer xác nhận

&#x20;               |

&#x20;               v

&#x20;          COMPLETED

```



Project chỉ được xem xét hoàn thành khi các milestone bắt buộc đã được xử lý đầy đủ.



\## 9.10. Tổng quan vòng đời Project



```text

&#x20;             +--------+

&#x20;             | DRAFT  |

&#x20;             +---+----+

&#x20;                 |

&#x20;                 | Đăng ký hoàn tất

&#x20;                 v

&#x20;         +---------------+

&#x20;         |  REGISTERED   |

&#x20;         +-------+-------+

&#x20;                 |

&#x20;                 | Bắt đầu thực hiện

&#x20;                 v

&#x20;         +---------------+

&#x20;         | IN\_PROGRESS   |

&#x20;         +-------+-------+

&#x20;            |          |

&#x20;            |          |

&#x20;     Hủy Project       | Tất cả milestone

&#x20;            |          | được APPROVED

&#x20;            v          v

&#x20;      +-----------+  Lecturer xác nhận

&#x20;      | CANCELLED |          |

&#x20;      +-----------+          v

&#x20;                      +-------------+

&#x20;                      | COMPLETED   |

&#x20;                      +-------------+

```



\## 9.11. Nguyên tắc



\- Project phải có trạng thái rõ ràng trong toàn bộ vòng đời.

\- Chỉ các trạng thái hợp lệ mới được phép chuyển đổi.

\- Việc chuyển trạng thái phải được kiểm tra ở phía server.

\- Project `COMPLETED` không được tiếp tục cập nhật milestone.

\- Project `CANCELLED` không được tiếp tục thực hiện nếu chưa được mở lại theo quy trình.

\- Scheduled Job chỉ hỗ trợ kiểm tra điều kiện và cập nhật các trạng thái được phép tự động.

\- Lecturer chịu trách nhiệm xác nhận project hoàn thành cuối cùng.

