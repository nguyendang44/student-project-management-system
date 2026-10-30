/**
 * v0.7.8: Milestone template library. Frontend-only reference data.
 * A template never changes Topic/Project data until explicitly applied.
 * Each suggested deadline is intentionally empty. Deadline is Vietnam end-of-day.
 */
export type TemplateMilestone = Readonly<{
  title: string
  description: string
  expectedResult: string
}>
export type MilestoneTemplateCategory = Readonly<{
  id: string
  name: string
  examples: string
  milestones: ReadonlyArray<TemplateMilestone>
}>

export const milestoneTemplateCategories: ReadonlyArray<MilestoneTemplateCategory> = [
  {
    id: 'software', name: 'Phát triển phần mềm', examples: 'Website, ứng dụng mobile, hệ thống quản lý',
    milestones: [
      { title: 'Khảo sát và phân tích yêu cầu', description: 'Xác định đối tượng sử dụng, nhu cầu, phạm vi và yêu cầu chức năng/phi chức năng; mô tả các luồng nghiệp vụ chính.', expectedResult: 'Tài liệu SRS, danh sách chức năng, Use Case Diagram và đặc tả Use Case chính.' },
      { title: 'Thiết kế kiến trúc và cơ sở dữ liệu', description: 'Lựa chọn kiến trúc ứng dụng; thiết kế entity, quan hệ dữ liệu, API và phương án xác thực/phân quyền.', expectedResult: 'Sơ đồ kiến trúc, ERD, database schema và API specification.' },
      { title: 'Thiết kế giao diện UI/UX', description: 'Thiết kế hành trình người dùng, wireframe và giao diện cho những chức năng cốt lõi; kiểm tra tính nhất quán.', expectedResult: 'Bộ wireframe/prototype và mô tả màn hình, luồng thao tác.' },
      { title: 'Phát triển chức năng Backend và Frontend', description: 'Hiện thực các chức năng chính, kết nối API và cơ sở dữ liệu; kiểm soát lỗi và quyền truy cập.', expectedResult: 'Mã nguồn chạy được cho các chức năng chính; hướng dẫn thiết lập và dữ liệu thử.' },
      { title: 'Tích hợp hệ thống và kiểm thử', description: 'Kiểm thử các luồng nghiệp vụ, API, giao diện và phân quyền; sửa lỗi phát hiện trong quá trình tích hợp.', expectedResult: 'Bộ test case, kết quả kiểm thử, danh sách lỗi và bản build ổn định.' },
      { title: 'Hoàn thiện sản phẩm và báo cáo', description: 'Rà soát trải nghiệm sử dụng, tài liệu kỹ thuật và chuẩn bị demo hệ thống.', expectedResult: 'Source code hoàn chỉnh, README, báo cáo và bài trình bày/demo.' }
    ]
  },
  {
    id: 'ai', name: 'Trí tuệ nhân tạo (AI/ML)', examples: 'Nhận diện hình ảnh, NLP, chatbot, dự đoán',
    milestones: [
      { title: 'Xác định bài toán và khảo sát mô hình', description: 'Xác định dữ liệu đầu vào/đầu ra, mục tiêu và giới hạn bài toán; khảo sát nghiên cứu hoặc mô hình liên quan.', expectedResult: 'Đặc tả bài toán, tổng quan tài liệu, tập tiêu chí đánh giá.' },
      { title: 'Thu thập và tiền xử lý dữ liệu', description: 'Lựa chọn nguồn dữ liệu, tổ chức/gán nhãn, làm sạch, chia tập huấn luyện/xác thực/kiểm tra và đánh giá rủi ro dữ liệu.', expectedResult: 'Bộ dữ liệu đã xử lý, mô tả dữ liệu và quy trình tiền xử lý có thể lặp lại.' },
      { title: 'Xây dựng mô hình cơ sở', description: 'Thiết lập baseline, quy trình huấn luyện và phép đo đánh giá ban đầu; ghi nhận tham số và dữ liệu thử nghiệm.', expectedResult: 'Mô hình baseline, mã huấn luyện và kết quả đánh giá ban đầu.' },
      { title: 'Huấn luyện và tối ưu mô hình', description: 'Thử nghiệm siêu tham số, lựa chọn kiến trúc/phương pháp và kiểm soát overfitting; lưu kết quả các lần chạy.', expectedResult: 'Mô hình đã huấn luyện, bảng so sánh thí nghiệm và chỉ số trên tập kiểm tra.' },
      { title: 'Tích hợp mô hình vào ứng dụng', description: 'Đóng gói inference, xây dựng API/giao diện và kiểm tra độ trễ, lỗi đầu vào, khả năng vận hành.', expectedResult: 'Ứng dụng hoặc API demo suy luận bằng mô hình đã chọn.' },
      { title: 'Đánh giá cuối và báo cáo', description: 'Phân tích sai số, giới hạn, công bằng/an toàn dữ liệu nếu liên quan; hoàn thiện báo cáo và demo.', expectedResult: 'Báo cáo chỉ số, phân tích lỗi, mã nguồn và demo hoàn chỉnh.' }
    ]
  },
  {
    id: 'data', name: 'Khoa học và phân tích dữ liệu', examples: 'Dashboard, khai phá dữ liệu, phân tích thống kê',
    milestones: [
      { title: 'Xác định mục tiêu và nguồn dữ liệu', description: 'Xác định câu hỏi phân tích, đối tượng sử dụng, nguồn dữ liệu và tiêu chí đầu ra.', expectedResult: 'Danh sách câu hỏi/KPI, danh mục nguồn dữ liệu và phạm vi phân tích.' },
      { title: 'Thu thập và làm sạch dữ liệu', description: 'Trích xuất dữ liệu, xử lý thiếu/trùng/ngoại lệ, chuẩn hóa kiểu dữ liệu và thống kê chất lượng.', expectedResult: 'Bộ dữ liệu đã làm sạch, data dictionary và báo cáo chất lượng dữ liệu.' },
      { title: 'Thiết kế mô hình dữ liệu và pipeline', description: 'Thiết kế các bảng/quan hệ, luồng ETL hoặc ELT, lịch cập nhật và kiểm tra tính nhất quán.', expectedResult: 'Mô hình dữ liệu và pipeline xử lý có thể tái chạy.' },
      { title: 'Phân tích khám phá và thống kê', description: 'Thực hiện EDA, đối chiếu phân bố/xu hướng, phân nhóm hoặc xây dựng mô hình thống kê khi phù hợp.', expectedResult: 'Notebook hoặc mã phân tích, chỉ số, bảng và kết luận có căn cứ.' },
      { title: 'Xây dựng dashboard và trực quan hóa', description: 'Thiết kế biểu đồ/bộ lọc phù hợp, kiểm thử cách hiểu số liệu và khả năng truy xuất nguồn.', expectedResult: 'Dashboard hoạt động, định nghĩa chỉ số và bộ ảnh minh họa kết quả.' },
      { title: 'Đánh giá và trình bày kết quả', description: 'Kiểm tra độ tin cậy của kết luận, giới hạn phân tích và đề xuất ứng dụng thực tiễn.', expectedResult: 'Báo cáo phân tích, kết luận, tài liệu sử dụng dashboard và demo.' }
    ]
  },
  {
    id: 'iot', name: 'IoT và hệ thống nhúng', examples: 'Nhà thông minh, cảm biến, ESP32, Arduino',
    milestones: [
      { title: 'Phân tích yêu cầu và lựa chọn thiết bị', description: 'Xác định yêu cầu cảm biến/điều khiển, điều kiện vận hành và lựa chọn vi điều khiển, phần tử kết nối.', expectedResult: 'Danh sách yêu cầu kỹ thuật, linh kiện và sơ đồ chức năng.' },
      { title: 'Thiết kế phần cứng và hệ thống điều khiển', description: 'Thiết kế sơ đồ mạch, nguồn, giao tiếp và kiến trúc xử lý dữ liệu.', expectedResult: 'Sơ đồ nguyên lý, thiết kế kết nối và sơ đồ khối hệ thống.' },
      { title: 'Lập trình vi điều khiển và cảm biến', description: 'Lập trình thu thập dữ liệu, điều khiển thiết bị, kiểm tra ngưỡng và các trường hợp lỗi.', expectedResult: 'Firmware chạy trên thiết bị thử nghiệm và nhật ký đo kiểm.' },
      { title: 'Kết nối dữ liệu và giao diện giám sát', description: 'Tích hợp giao thức kết nối, backend/lưu trữ và giao diện theo dõi hoặc điều khiển.', expectedResult: 'Luồng truyền dữ liệu đầu-cuối và màn hình giám sát hoạt động.' },
      { title: 'Tích hợp và kiểm thử thực tế', description: 'Đo độ chính xác, độ ổn định kết nối, độ trễ và hành vi khi mất điện/mất mạng.', expectedResult: 'Bản nguyên mẫu tích hợp, bảng phép đo và báo cáo kiểm thử.' },
      { title: 'Hoàn thiện nguyên mẫu và báo cáo', description: 'Hoàn thiện lắp đặt, an toàn phần cứng, hướng dẫn vận hành và chuẩn bị demo.', expectedResult: 'Mô hình thực tế, source/firmware, tài liệu cấu hình và demo.' }
    ]
  },
  {
    id: 'security', name: 'An toàn thông tin', examples: 'Bảo mật website, kiểm thử xâm nhập, mã hóa',
    milestones: [
      { title: 'Xác định phạm vi và mục tiêu bảo mật', description: 'Khảo sát bề mặt tấn công, tài sản, yêu cầu bảo mật; làm rõ phạm vi thử nghiệm có quyền cho phép.', expectedResult: 'Bản mô tả phạm vi, yêu cầu bảo mật và phương pháp thực hiện.' },
      { title: 'Mô hình đe dọa và môi trường thử nghiệm', description: 'Xác định tác nhân, rủi ro và kịch bản; thiết lập phòng lab hoặc hệ thống được ủy quyền.', expectedResult: 'Threat model, sơ đồ lab và kế hoạch kiểm thử.' },
      { title: 'Phân tích rủi ro và lỗ hổng', description: 'Đánh giá cấu hình và kiểm tra các điểm yếu trong môi trường được phép, phân loại mức độ ảnh hưởng.', expectedResult: 'Danh sách phát hiện, bằng chứng kiểm chứng và ma trận rủi ro.' },
      { title: 'Thực nghiệm và xác minh kết quả', description: 'Tái hiện có kiểm soát các phát hiện, phân tích nguyên nhân và đối chiếu kết quả với tiêu chí ban đầu.', expectedResult: 'Biên bản thực nghiệm, bằng chứng và mô tả ảnh hưởng.' },
      { title: 'Đề xuất và kiểm thử biện pháp khắc phục', description: 'Thiết kế giải pháp giảm thiểu, thực hiện hardening/vá lỗi và kiểm chứng sau sửa.', expectedResult: 'Danh sách biện pháp, cấu hình/đoạn mã khắc phục và kết quả kiểm thử lại.' },
      { title: 'Đánh giá lại và hoàn thiện báo cáo', description: 'Tổng hợp rủi ro còn lại, giới hạn đánh giá, khuyến nghị vận hành và chuẩn bị demo an toàn.', expectedResult: 'Báo cáo bảo mật, đánh giá trước/sau và demo trong phạm vi được phép.' }
    ]
  },
  {
    id: 'cloud', name: 'Cloud Computing và DevOps', examples: 'Triển khai Cloud, Docker, Kubernetes, CI/CD',
    milestones: [
      { title: 'Phân tích yêu cầu và kiến trúc Cloud', description: 'Xác định mục tiêu triển khai, tải dự kiến, ràng buộc chi phí và mức sẵn sàng.', expectedResult: 'Tài liệu yêu cầu, sơ đồ kiến trúc và tiêu chí vận hành.' },
      { title: 'Thiết kế và cấu hình hạ tầng', description: 'Thiết lập môi trường, mạng, dữ liệu, quyền truy cập và cấu hình hạ tầng tái lập.', expectedResult: 'Hạ tầng thử nghiệm hoạt động, cấu hình và tài liệu triển khai.' },
      { title: 'Container hóa và triển khai ứng dụng', description: 'Đóng gói ứng dụng, thiết lập môi trường runtime và quy trình triển khai.', expectedResult: 'Container image, manifest/deployment và ứng dụng chạy được.' },
      { title: 'Thiết lập CI/CD', description: 'Tự động kiểm tra, build, deploy và bảo vệ cấu hình bí mật trong pipeline.', expectedResult: 'Pipeline CI/CD có minh chứng chạy thành công và tài liệu sử dụng.' },
      { title: 'Giám sát và kiểm thử hiệu năng', description: 'Thu thập metric/log, thử khả năng chịu tải, phát hiện điểm nghẽn và lỗi vận hành.', expectedResult: 'Dashboard giám sát, kết quả benchmark và báo cáo độ ổn định.' },
      { title: 'Đánh giá chi phí, bảo mật và báo cáo', description: 'Tổng hợp chi phí, kiểm soát truy cập, sao lưu/phục hồi và kinh nghiệm triển khai.', expectedResult: 'Bảng chi phí, checklist bảo mật, báo cáo và demo hệ thống.' }
    ]
  },
  {
    id: 'blockchain', name: 'Blockchain và DApp', examples: 'Smart Contract, token, ứng dụng phi tập trung',
    milestones: [
      { title: 'Phân tích yêu cầu và chọn Blockchain', description: 'Xác định bài toán cần tính phi tập trung, dữ liệu on-chain/off-chain, quyền truy cập và nền tảng phù hợp.', expectedResult: 'Đặc tả nghiệp vụ, lý do chọn nền tảng và phạm vi DApp.' },
      { title: 'Thiết kế kiến trúc và Smart Contract', description: 'Thiết kế trạng thái hợp đồng, quyền hạn, event và luồng tương tác với ứng dụng.', expectedResult: 'Sơ đồ kiến trúc, đặc tả Smart Contract và giao diện chức năng.' },
      { title: 'Lập trình và kiểm thử Smart Contract', description: 'Hiện thực hợp đồng, kiểm thử unit/integration và đánh giá các rủi ro logic.', expectedResult: 'Mã hợp đồng, bộ kiểm thử và báo cáo kết quả chạy test.' },
      { title: 'Phát triển DApp và kết nối ví', description: 'Thiết kế frontend, tích hợp wallet, đọc/ghi dữ liệu và phản hồi lỗi giao dịch.', expectedResult: 'DApp kết nối ví và chạy được những luồng chức năng chính.' },
      { title: 'Triển khai và đánh giá bảo mật', description: 'Triển khai lên môi trường thử nghiệm, kiểm tra giao dịch, chi phí gas và các quyền của hợp đồng.', expectedResult: 'Địa chỉ hợp đồng testnet/local, log giao dịch và bảng kiểm bảo mật.' },
      { title: 'Hoàn thiện tài liệu và demo', description: 'Chuẩn bị hướng dẫn sử dụng ví, khởi chạy hợp đồng và trình diễn luồng giao dịch.', expectedResult: 'Mã nguồn, README, báo cáo và demo end-to-end.' }
    ]
  },
  {
    id: 'research', name: 'Nghiên cứu thuật toán', examples: 'Nghiên cứu, cải tiến và thực nghiệm thuật toán',
    milestones: [
      { title: 'Khảo sát nghiên cứu liên quan', description: 'Tổng hợp tài liệu, thuật toán hiện có, ưu nhược điểm và khoảng trống nghiên cứu.', expectedResult: 'Tổng quan tài liệu có nguồn và bảng so sánh phương pháp.' },
      { title: 'Phát biểu bài toán và tiêu chí đánh giá', description: 'Định nghĩa đầu vào/đầu ra, giả thiết, độ phức tạp mong đợi và tập bài kiểm thử.', expectedResult: 'Phát biểu bài toán chính xác, bộ tiêu chí và baseline tham chiếu.' },
      { title: 'Thiết kế hoặc cải tiến thuật toán', description: 'Xây dựng phương pháp, phân tích tính đúng đắn và các giới hạn lý thuyết.', expectedResult: 'Mô tả thuật toán, giả mã, lập luận tính đúng đắn và phân tích độ phức tạp.' },
      { title: 'Cài đặt và tiến hành thực nghiệm', description: 'Lập trình thuật toán, chuẩn bị bộ dữ liệu thử và thiết kế thí nghiệm có thể tái lập.', expectedResult: 'Mã nguồn, bộ dữ liệu/kịch bản thử và kết quả chạy đo đạc.' },
      { title: 'So sánh với thuật toán cơ sở', description: 'So sánh thời gian, bộ nhớ và chất lượng kết quả với baseline trên các điều kiện khác nhau.', expectedResult: 'Bảng và biểu đồ so sánh, phân tích nguyên nhân và giới hạn.' },
      { title: 'Kết luận và hoàn thiện báo cáo', description: 'Tổng hợp đóng góp, trường hợp thất bại và hướng phát triển tiếp theo.', expectedResult: 'Báo cáo nghiên cứu, mã nguồn và demo thực nghiệm.' }
    ]
  }
]
