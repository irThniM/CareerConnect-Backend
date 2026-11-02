using CareerConnect.Application.Candidates.Services;
using CareerConnect.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using PDFtoImage;
using SkiaSharp;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UglyToad.PdfPig;

namespace CareerConnect.Infrastructure.Ai
{
    /// <summary>
    /// Trích xuất nội dung PDF/DOCX bằng thư viện cục bộ, nhận diện kỹ năng từ
    /// danh mục trong database và dùng quy tắc để tìm phần giới thiệu CV.
    /// Dịch vụ này không gọi Gemini hay dịch vụ AI bên ngoài.
    /// </summary>
    public sealed class ResumeProcessingService : IResumeProcessingService
    {
        private const int MaxExtractedCharacters = 100_000;
        private const int MaxOcrPages = 10;
        private static readonly string TesseractExecutablePath =
            Environment.GetEnvironmentVariable("TESSERACT_EXECUTABLE_PATH")
            ?? (OperatingSystem.IsWindows()
                ? @"C:\Program Files\Tesseract-OCR\tesseract.exe"
                : "tesseract");
        private const int MaxSummaryCharacters = 700;
        private static readonly XNamespace WordNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        private static readonly Regex ContactPattern = new(
            @"(?:\b[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}\b|https?://|www\.|(?:\+?\d[\d\s().-]{7,}\d))",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex VietnamesePhonePattern = new(
            @"(?<![\p{L}\p{N}])(?:\+?84|0)(?:[\s().-]*\d){8,10}(?!\d)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex HumanNamePattern = new(
            @"^[\p{L}\p{M}][\p{L}\p{M}.'’’-]*(?:\s+[\p{L}\p{M}][\p{L}\p{M}.'’’-]*){1,4}$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex LocationIndicatorPattern = new(
            @"(?:\b(?:quận|quan|huyện|huyen|thành phố|thanh pho|tỉnh|tinh|phường|phuong|district|city|province|ward|vietnam|viet nam)\b|\b(?:hồ chí minh|ho chi minh|hà nội|ha noi|đà nẵng|da nang|cần thơ|can tho|hải phòng|hai phong|bình dương|binh duong|đồng nai|dong nai|khánh hòa|khanh hoa)\b)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex InlineAddressPattern = new(
            @"(?<![\p{L}\p{N}])(?:current location|current city|address|location|residence|city|province|địa chỉ hiện tại|địa chỉ|địa điểm|thành phố|tỉnh thành|nơi ở|khu vực sinh sống|nơi sinh sống)\s*[:：]\s*(?<value>.*?)(?=\s+(?:e-?mail|email|phone|mobile|telephone|số điện thoại|điện thoại|sdt)\s*[:：]|$)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // Các bí danh giúp nhận diện cách viết khác nhau. Tên và nhóm chuẩn vẫn lấy từ skills trong Neon.
        private static readonly SkillDefinition[] BuiltInSkills =
        [
            new("C#", "Công nghệ thông tin", ["C Sharp"]),
            new(".NET", "Công nghệ thông tin", ["dotnet", ".NET Framework"]),
            new("ASP.NET Core", "Công nghệ thông tin", ["ASP.NET", "ASP Net Core"]),
            new("JavaScript", "Công nghệ thông tin", ["JS", "ECMAScript"]),
            new("TypeScript", "Công nghệ thông tin", ["TS"]),
            new("React", "Công nghệ thông tin", ["React.js"]),
            new("Vue.js", "Công nghệ thông tin", ["Vue", "VueJS"]),
            new("Node.js", "Công nghệ thông tin", ["NodeJS"]),
            new("PostgreSQL", "Công nghệ thông tin", ["Postgres"]),
            new("SQL Server", "Công nghệ thông tin", ["Microsoft SQL Server", "MSSQL"]),
            new("Entity Framework Core", "Công nghệ thông tin", ["EF Core"]),
            new("REST API", "Công nghệ thông tin", ["RESTful API"]),
            new("Kubernetes", "Công nghệ thông tin", ["K8s"]),
            new("AWS", "Công nghệ thông tin", ["Amazon Web Services"]),
            new("Microsoft Azure", "Công nghệ thông tin", ["Azure"]),
            // GitHub/GitLab are hosting platforms, not proof that Git is listed as a skill.
            new("Git", "Công nghệ thông tin", []),
            new("Flutter", "Công nghệ thông tin", []),
            new("Dart", "Công nghệ thông tin", []),
            new("Android", "Công nghệ thông tin", ["Android Development"]),
            new("XML", "Công nghệ thông tin", ["XML Layout"]),
            new("Python", "Công nghệ thông tin", ["Python Programming"]),
            new("Java", "Công nghệ thông tin", ["Java Programming"]),
            new("OOP", "Công nghệ thông tin", ["Object-Oriented Programming", "Lập trình hướng đối tượng"]),
            new("Database Management", "Công nghệ thông tin", ["Quản lý cơ sở dữ liệu", "Database Management System"]),
            new("HTML", "Công nghệ thông tin", ["HTML5"]),
            new("CSS", "Công nghệ thông tin", ["CSS3"]),
            new("Tailwind CSS", "Công nghệ thông tin", ["Tailwind"]),
            new("Bootstrap", "Công nghệ thông tin", []),
            new("GraphQL", "Công nghệ thông tin", []),
            new("Microservices", "Công nghệ thông tin", ["Microservice"]),
            new("CI/CD", "Công nghệ thông tin", ["Continuous Integration", "Continuous Deployment"]),
            new("An toàn thông tin", "Công nghệ thông tin", ["Cybersecurity", "Information Security"]),
            new("Kiểm thử phần mềm", "Công nghệ thông tin", ["Software Testing", "Quality Assurance"]),
            new("QA", "Công nghệ thông tin", ["Quality Assurance"]),
            new("Selenium", "Công nghệ thông tin", []),
            new("Playwright", "Công nghệ thông tin", []),
            new("Postman", "Công nghệ thông tin", []),
            new("Power BI", "Dữ liệu và phân tích", []),
            new("Tableau", "Dữ liệu và phân tích", []),
            new("Phân tích dữ liệu", "Dữ liệu và phân tích", ["Data Analysis", "Data Analytics"]),
            new("Microsoft Excel", "Văn phòng", ["Excel", "MS Excel"]),
            new("Tư vấn bán hàng", "Kinh doanh và bán hàng", ["Sales Consulting", "Sales Consultation"]),
            new("Chăm sóc khách hàng", "Kinh doanh và bán hàng", ["Customer Service", "Customer Care"]),
            new("Đàm phán", "Kinh doanh và bán hàng", ["Negotiation"]),
            new("Chốt đơn", "Kinh doanh và bán hàng", ["Closing Sales"]),
            new("Phát triển khách hàng", "Kinh doanh và bán hàng", ["Business Development"]),
            new("Quản lý khách hàng", "Kinh doanh và bán hàng", ["Account Management"]),
            new("Bán hàng B2B", "Kinh doanh và bán hàng", ["B2B Sales"]),
            new("Bán hàng B2C", "Kinh doanh và bán hàng", ["B2C Sales"]),
            new("Nghiên cứu thị trường", "Kinh doanh và bán hàng", ["Market Research"]),
            new("Quản lý kênh phân phối", "Kinh doanh và bán hàng", ["Channel Management"]),
            new("SEO", "Marketing và truyền thông", ["Search Engine Optimization"]),
            new("Google Ads", "Marketing và truyền thông", ["Google Adwords"]),
            new("Facebook Ads", "Marketing và truyền thông", ["Meta Ads"]),
            new("TikTok Ads", "Marketing và truyền thông", []),
            new("Content Marketing", "Marketing và truyền thông", ["Tiếp thị nội dung"]),
            new("Viết nội dung", "Marketing và truyền thông", ["Content Writing", "Copywriting"]),
            new("Quản lý mạng xã hội", "Marketing và truyền thông", ["Social Media Management"]),
            new("Email Marketing", "Marketing và truyền thông", []),
            new("Digital Marketing", "Marketing và truyền thông", ["Tiếp thị số"]),
            new("Tổ chức sự kiện", "Marketing và truyền thông", ["Event Management"]),
            new("Quan hệ công chúng", "Marketing và truyền thông", ["Public Relations", "PR"]),
            new("Kế toán tổng hợp", "Kế toán và tài chính", ["General Accounting"]),
            new("Kế toán thuế", "Kế toán và tài chính", ["Tax Accounting"]),
            new("Kế toán công nợ", "Kế toán và tài chính", ["Accounts Receivable", "Accounts Payable"]),
            new("Lập báo cáo tài chính", "Kế toán và tài chính", ["Financial Reporting"]),
            new("Kiểm toán", "Kế toán và tài chính", ["Auditing"]),
            new("Phân tích tài chính", "Kế toán và tài chính", ["Financial Analysis"]),
            new("Lập ngân sách", "Kế toán và tài chính", ["Budgeting"]),
            new("Quản lý dòng tiền", "Kế toán và tài chính", ["Cash Flow Management"]),
            new("Kê khai thuế", "Kế toán và tài chính", ["Tax Filing"]),
            new("Tuyển dụng", "Nhân sự", ["Recruitment", "Talent Acquisition"]),
            new("Phỏng vấn ứng viên", "Nhân sự", ["Candidate Interviewing"]),
            new("Đào tạo nhân viên", "Nhân sự", ["Employee Training"]),
            new("Tính lương và phúc lợi", "Nhân sự", ["Payroll", "Compensation and Benefits"]),
            new("Quản lý hồ sơ nhân sự", "Nhân sự", ["HR Administration"]),
            new("Quan hệ lao động", "Nhân sự", ["Employee Relations"]),
            new("Xây dựng chính sách nhân sự", "Nhân sự", ["HR Policy Development"]),
            new("Đánh giá hiệu suất", "Nhân sự", ["Performance Management"]),
            new("Thiết kế đồ họa", "Thiết kế và sáng tạo", ["Graphic Design"]),
            new("UI/UX Design", "Thiết kế và sáng tạo", ["UI UX", "User Experience Design"]),
            new("Adobe Photoshop", "Thiết kế và sáng tạo", ["Photoshop"]),
            new("Adobe Illustrator", "Thiết kế và sáng tạo", ["Illustrator"]),
            new("Dựng video", "Thiết kế và sáng tạo", ["Video Editing"]),
            new("Chụp ảnh", "Thiết kế và sáng tạo", ["Photography"]),
            new("Biên tập nội dung", "Thiết kế và sáng tạo", ["Content Editing"]),
            new("Thiết kế bao bì", "Thiết kế và sáng tạo", ["Packaging Design"]),
            new("Sáng tạo ý tưởng", "Thiết kế và sáng tạo", ["Creative Thinking"]),
            new("Tổng đài viên", "Dịch vụ khách hàng", ["Call Center"]),
            new("Hỗ trợ khách hàng qua điện thoại", "Dịch vụ khách hàng", ["Phone Support"]),
            new("Hỗ trợ khách hàng qua email", "Dịch vụ khách hàng", ["Email Support"]),
            new("Tiếp nhận và xử lý khiếu nại", "Dịch vụ khách hàng", ["Complaint Handling"]),
            new("Quản lý trải nghiệm khách hàng", "Dịch vụ khách hàng", ["Customer Experience Management"]),
            new("Quản lý kho", "Vận hành và logistics", ["Warehouse Management"]),
            new("Kiểm kê hàng hóa", "Vận hành và logistics", ["Inventory Management"]),
            new("Điều phối vận chuyển", "Vận hành và logistics", ["Transportation Coordination"]),
            new("Quản lý chuỗi cung ứng", "Vận hành và logistics", ["Supply Chain Management"]),
            new("Xuất nhập khẩu", "Vận hành và logistics", ["Import Export"]),
            new("Khai báo hải quan", "Vận hành và logistics", ["Customs Declaration"]),
            new("Lập kế hoạch sản xuất", "Vận hành và logistics", ["Production Planning"]),
            new("Quản lý đơn hàng", "Vận hành và logistics", ["Order Management"]),
            new("Mua hàng", "Vận hành và logistics", ["Procurement", "Purchasing"]),
            new("Vận hành máy móc", "Sản xuất và kỹ thuật", ["Machine Operation"]),
            new("Bảo trì thiết bị", "Sản xuất và kỹ thuật", ["Equipment Maintenance"]),
            new("Kiểm soát chất lượng", "Sản xuất và kỹ thuật", ["Quality Control", "QC"]),
            new("Đọc bản vẽ kỹ thuật", "Sản xuất và kỹ thuật", ["Technical Drawing"]),
            new("An toàn lao động", "Sản xuất và kỹ thuật", ["Occupational Safety"]),
            new("Điện công nghiệp", "Sản xuất và kỹ thuật", ["Industrial Electricity"]),
            new("Cơ khí", "Sản xuất và kỹ thuật", ["Mechanical Engineering"]),
            new("Tự động hóa", "Sản xuất và kỹ thuật", ["Automation"]),
            new("Lean Manufacturing", "Sản xuất và kỹ thuật", ["Lean Production"]),
            new("Giảng dạy", "Giáo dục", ["Teaching", "Instruction"]),
            new("Soạn giáo án", "Giáo dục", ["Lesson Planning"]),
            new("Quản lý lớp học", "Giáo dục", ["Classroom Management"]),
            new("Đánh giá kết quả học tập", "Giáo dục", ["Student Assessment"]),
            new("Thiết kế chương trình đào tạo", "Giáo dục", ["Curriculum Design"]),
            new("Tư vấn giáo dục", "Giáo dục", ["Educational Consulting"]),
            new("Điều dưỡng", "Y tế và chăm sóc sức khỏe", ["Nursing"]),
            new("Sơ cứu", "Y tế và chăm sóc sức khỏe", ["First Aid"]),
            new("Chăm sóc bệnh nhân", "Y tế và chăm sóc sức khỏe", ["Patient Care"]),
            new("Quản lý hồ sơ bệnh án", "Y tế và chăm sóc sức khỏe", ["Medical Records Management"]),
            new("Tư vấn sức khỏe", "Y tế và chăm sóc sức khỏe", ["Health Counseling"]),
            new("Nghiệp vụ lễ tân", "Nhà hàng, khách sạn và du lịch", ["Front Desk", "Reception"]),
            new("Nghiệp vụ buồng phòng", "Nhà hàng, khách sạn và du lịch", ["Housekeeping"]),
            new("Phục vụ nhà hàng", "Nhà hàng, khách sạn và du lịch", ["Restaurant Service"]),
            new("Pha chế", "Nhà hàng, khách sạn và du lịch", ["Bartending", "Barista"]),
            new("Nấu ăn", "Nhà hàng, khách sạn và du lịch", ["Cooking", "Culinary"]),
            new("Điều hành tour", "Nhà hàng, khách sạn và du lịch", ["Tour Operations"]),
            new("Đọc bản vẽ xây dựng", "Xây dựng và bất động sản", ["Construction Drawing"]),
            new("Giám sát công trình", "Xây dựng và bất động sản", ["Construction Supervision"]),
            new("Dự toán công trình", "Xây dựng và bất động sản", ["Construction Cost Estimation"]),
            new("Quản lý dự án xây dựng", "Xây dựng và bất động sản", ["Construction Project Management"]),
            new("Tư vấn bất động sản", "Xây dựng và bất động sản", ["Real Estate Consulting"]),
            new("Môi giới bất động sản", "Xây dựng và bất động sản", ["Real Estate Brokerage"]),
            new("Thiết kế kiến trúc", "Xây dựng và bất động sản", ["Architectural Design"]),
            new("AutoCAD", "Xây dựng và bất động sản", []),
            new("Soạn thảo văn bản", "Hành chính và pháp lý", ["Document Preparation"]),
            new("Quản lý văn thư và lưu trữ", "Hành chính và pháp lý", ["Records Management"]),
            new("Quản lý lịch và cuộc họp", "Hành chính và pháp lý", ["Calendar Management"]),
            new("Soạn thảo hợp đồng", "Hành chính và pháp lý", ["Contract Drafting"]),
            new("Nghiên cứu pháp lý", "Hành chính và pháp lý", ["Legal Research"]),
            new("Tuân thủ pháp luật", "Hành chính và pháp lý", ["Legal Compliance"]),
            new("Giao tiếp", "Kỹ năng chung", ["Communication"]),
            new("Làm việc nhóm", "Kỹ năng chung", ["Teamwork", "Collaboration"]),
            new("Quản lý thời gian", "Kỹ năng chung", ["Time Management"]),
            new("Giải quyết vấn đề", "Kỹ năng chung", ["Problem Solving"]),
            new("Tư duy phản biện", "Kỹ năng chung", ["Critical Thinking"]),
            new("Thuyết trình", "Kỹ năng chung", ["Presentation Skills"]),
            new("Tiếng Anh", "Ngoại ngữ", ["English"]),
            new("Tiếng Nhật", "Ngoại ngữ", ["Japanese"]),
            new("Tiếng Hàn", "Ngoại ngữ", ["Korean"]),
            new("Tiếng Trung", "Ngoại ngữ", ["Chinese", "Mandarin"]),
        ];

        private static readonly string[] SummaryHeadings =
        [
            "professional summary", "career objective", "career goal", "career summary", "about me",
            "profile", "profile summary", "personal profile", "professional profile", "professional objective",
            "summary", "objective", "mục tiêu", "mục tiêu nghề nghiệp", "định hướng nghề nghiệp",
            "tóm tắt hồ sơ", "giới thiệu bản thân", "giới thiệu", "tóm tắt"
        ];

        private static readonly string[] WorkExperienceHeadings =
        [
            "work experience", "professional experience", "employment history", "work history",
            "career history", "experience", "work experiences", "kinh nghiệm làm việc",
            "kinh nghiệm công tác", "quá trình làm việc", "kinh nghiệm"
        ];

        private static readonly string[] ProjectHeadings =
        [
            "selected projects", "project experience", "personal projects", "key projects",
            "projects", "academic projects", "selected academic projects", "đồ án học thuật",
            "các đồ án học thuật tiêu biểu", "đồ án tiêu biểu", "đồ án", "dự án tiêu biểu",
            "dự án cá nhân", "dự án đã thực hiện", "dự án"
        ];

        private static readonly string[] EducationHeadings =
        [
            "education background", "academic background", "education and training", "education",
            "academic qualifications", "qualifications", "education & certifications",
            "học vấn & chứng chỉ", "học vấn và chứng chỉ", "trình độ học vấn",
            "quá trình học tập", "học vấn", "đào tạo"
        ];

        private static readonly string[] CertificateHeadings =
        [
            "professional certifications", "certifications", "certificates", "licenses",
            "chứng chỉ", "chứng nhận", "giấy phép"
        ];

        private static readonly string[] AddressLabels =
        [
            "current location", "current city", "address", "location", "residence", "city", "province",
            "địa chỉ hiện tại", "địa chỉ", "địa điểm", "thành phố", "tỉnh thành", "nơi ở",
            "khu vực sinh sống", "nơi sinh sống"
        ];

        private static readonly string[] FullNameLabels =
        ["full name", "candidate name", "name", "họ và tên", "họ tên"];

        private static readonly string[] PhoneLabels =
        ["phone number", "mobile number", "phone", "mobile", "telephone", "số điện thoại", "điện thoại", "sdt"];

        private static readonly string[] SkillHeadings =
        [
            "technical skills", "core skills", "skills", "core competencies", "relevant skills",
            "kỹ năng liên quan", "kỹ năng chuyên môn", "kỹ năng mềm", "kỹ năng kỹ thuật", "kỹ năng"
        ];

        private static readonly Regex ExperienceFieldPattern = new(
            @"(?<![\p{L}\p{N}])(?<label>vị trí|vi trí|vi tri|chức danh|chuc danh|công việc làm thêm[^:：]{0,40}|cong viec lam them[^:：]{0,40}|công ty|cong ty|đơn vị|don vi|thời gian bắt đầu|thoi gian bat dau|thời gian kết thúc|thoi gian ket thuc|mô tả công việc|mo ta cong viec)\s*[:：]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex DateRangePattern = new(
            @"(?:(?:0?[1-9]|1[0-2])\s*[/.-]\s*)?(?:19|20)\d{2}\s*(?:[-–—]|to|đến)\s*(?:(?:(?:0?[1-9]|1[0-2])\s*[/.-]\s*)?(?:19|20)\d{2}|present|current|now|hiện tại|nay|đang làm việc)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex EducationInstitutionPattern = new(
            @"(?<institution>(?:(?:trường|truong)\s+)?(?:đại\s+học|dai\s+hoc|cao\s+đẳng|cao\s+dang|university|college|institute|học\s+viện|hoc\s+vien)\s+(?!chuy[eéê]n\s+ng[aàá]nh\b|ngành\b|nganh\b|hiện\s+tại\b|hien\s+tai\b|hoàn\s+thành\b|hoan\s+thanh\b|gpa\b|điểm\b|diem\b|[-–—]|(?:19|20)\d{2}\b)[\p{L}\p{M}\p{N}][\p{L}\p{M}\p{N}&.'’()/-]*(?:\s+(?!chuy[eéê]n\s+ng[aàá]nh\b|ngành\b|nganh\b|hiện\s+tại\b|hien\s+tai\b|hoàn\s+thành\b|hoan\s+thanh\b|gpa\b|điểm\b|diem\b|[-–—]|(?:19|20)\d{2}\b)[\p{L}\p{M}\p{N}][\p{L}\p{M}\p{N}&.'’()/-]*){0,5})(?=\s+(?:(?:trường|truong)\s+)?(?:đại\s+học|dai\s+hoc|cao\s+đẳng|cao\s+dang|university|college|institute|học\s+viện|hoc\s+vien)\b|\s+(?:chuy[eéê]n\s+ng[aàá]nh|ngành|nganh|major|gpa|điểm|diem|hiện\s+tại|hien\s+tai|hoàn\s+thành|hoan\s+thanh|sinh\s+viên|sinh\s+vien|tốt\s+nghiệp|tot\s+nghiep)\b|[,|;.\-–—]|$)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly string[] SectionHeadings =
        [
            .. SummaryHeadings,
            .. WorkExperienceHeadings,
            .. EducationHeadings,
            .. CertificateHeadings,
            .. ProjectHeadings,
            .. SkillHeadings,
            "certificates", "certifications",
            "languages", "references", "kinh nghiệm", "kinh nghiệm làm việc", "học vấn", "trình độ học vấn",
            "dự án", "chứng chỉ", "ngoại ngữ", "người tham khảo",
            "thông tin cá nhân", "thông tin liên hệ", "liên hệ"
        ];

        private static readonly string[] IntroStopHeadings =
        [
            .. WorkExperienceHeadings,
            .. EducationHeadings,
            .. CertificateHeadings,
            .. ProjectHeadings,
            .. SkillHeadings,
            "certificates", "certifications", "kinh nghiệm",
            "kinh nghiệm làm việc", "học vấn", "trình độ học vấn", "kỹ năng", "kỹ năng chuyên môn",
            "dự án", "chứng chỉ"
        ];

        private readonly AppDbContext _context;

        public ResumeProcessingService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ResumeProcessingResult> ProcessAsync(
            Stream file,
            string extension,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalizedExtension = extension.TrimStart('.').ToLowerInvariant();
            string text;
            string method;
            switch (normalizedExtension)
            {
                case "pdf":
                    var pdfBytes = await ReadAllBytesAsync(file, cancellationToken);
                    text = ExtractPdfText(new MemoryStream(pdfBytes, writable: false));
                    method = "PDF text extraction";

                    if (NormalizeText(text).Length < 40)
                    {
                        text = await ExtractPdfTextWithOcrAsync(pdfBytes, cancellationToken);
                        method = "PDF text extraction + Tesseract OCR";
                    }
                    break;
                case "docx":
                    text = ExtractDocxText(file);
                    method = "DOCX text extraction";
                    break;
                case "doc":
                    throw new NotSupportedException("Định dạng DOC cũ chưa hỗ trợ phân tích. Hãy lưu CV thành PDF hoặc DOCX rồi tải lại.");
                default:
                    throw new NotSupportedException("Định dạng CV này chưa được hỗ trợ phân tích.");
            }

            text = NormalizeText(text);
            if (text.Length < 40)
            {
                throw new InvalidDataException("Không trích xuất đủ nội dung chữ từ CV. Hãy kiểm tra chất lượng file hoặc tải lên PDF/DOCX có thể chọn và sao chép văn bản.");
            }

            if (text.Length > MaxExtractedCharacters)
            {
                text = text[..MaxExtractedCharacters];
            }

            // If a CV has a dedicated skills section, only match inside that section.
            // Searching the whole document turned project text such as a GitHub URL into a Git skill.
            var skillsText = ExtractSectionContent(text, SkillHeadings, continueAcrossMatchingHeadings: true);
            var skills = await RecognizeSkillsAsync(
                string.IsNullOrWhiteSpace(skillsText) ? text : skillsText,
                cancellationToken);
            var suggestedSummary = ExtractSummarySection(text)
                ?? ExtractIntroParagraph(text)
                ?? ExtractExperienceDescription(text)
                ?? BuildSkillsBasedSummary(skills);
            var suggestedFullName = ExtractSuggestedFullName(text);
            var suggestedPhoneNumber = ExtractSuggestedPhoneNumber(text);
            var suggestedLocation = ExtractSuggestedLocation(text);

            var workExperiences = ExtractLabeledWorkExperiences(text)
                .Concat(ExtractSectionEntries(text, WorkExperienceHeadings))
                .DistinctBy(item => $"{NormalizeForMatching(item.Title)}|{NormalizeForMatching(item.Organization ?? string.Empty)}|{NormalizeForMatching(item.Period ?? string.Empty)}")
                .Take(20)
                .ToArray();
            var projects = ExtractProjectEntries(text);
            var education = ExtractEducationEntries(text)
                .Concat(ExtractSectionEntries(text, CertificateHeadings).Where(IsCertificateEntry))
                .DistinctBy(item => $"{NormalizeForMatching(item.Title)}|{NormalizeForMatching(item.Organization ?? string.Empty)}")
                .Take(20)
                .ToArray();

            return new ResumeProcessingResult(
                text,
                method,
                skills,
                suggestedSummary,
                suggestedFullName,
                suggestedPhoneNumber,
                suggestedLocation,
                workExperiences,
                projects,
                education);
        }

        private static string ExtractPdfText(Stream stream)
        {
            stream.Position = 0;
            using var document = PdfDocument.Open(stream);
            var pages = document.GetPages().Select(page => page.Text);
            return string.Join("\n", pages);
        }

        private static async Task<byte[]> ReadAllBytesAsync(Stream stream, CancellationToken cancellationToken)
        {
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken);
            return buffer.ToArray();
        }

        private static async Task<string> ExtractPdfTextWithOcrAsync(
            byte[] pdfBytes,
            CancellationToken cancellationToken)
        {
            if (Path.IsPathRooted(TesseractExecutablePath) && !File.Exists(TesseractExecutablePath))
            {
                throw new InvalidOperationException(
                    $"Không tìm thấy Tesseract tại '{TesseractExecutablePath}'. Hãy cài Tesseract hoặc đặt biến môi trường TESSERACT_EXECUTABLE_PATH.");
            }

            using var countStream = new MemoryStream(pdfBytes, writable: false);
            var pageCount = Conversion.GetPageCount(countStream, leaveOpen: true);
            if (pageCount == 0)
            {
                throw new InvalidDataException("PDF không có trang để nhận dạng.");
            }

            if (pageCount > MaxOcrPages)
            {
                throw new InvalidDataException($"CV dạng scan chỉ hỗ trợ tối đa {MaxOcrPages} trang để nhận dạng OCR.");
            }

            var temporaryDirectory = Path.Combine(
                Path.GetTempPath(),
                "CareerConnect",
                "ResumeOcr",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryDirectory);

            try
            {
                using var pdfStream = new MemoryStream(pdfBytes, writable: false);
                // Higher resolution helps Tesseract recognize Vietnamese tone marks
                // in small text on scanned CVs.
                var renderOptions = new RenderOptions(Dpi: 450, Grayscale: true);
                var pageTexts = new List<string>(pageCount);
                var pageIndex = 0;

                foreach (var bitmap in Conversion.ToImages(
                    pdfStream,
                    Enumerable.Range(0, pageCount),
                    leaveOpen: true,
                    options: renderOptions))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    using (bitmap)
                    {
                        var imagePath = Path.Combine(temporaryDirectory, $"page-{pageIndex}.png");
                        var outputBasePath = Path.Combine(temporaryDirectory, $"page-{pageIndex}-ocr");
                        using (var imageData = bitmap.Encode(SKEncodedImageFormat.Png, 100))
                        await using (var imageStream = File.Create(imagePath))
                        {
                            imageData.SaveTo(imageStream);
                        }

                        pageTexts.Add(await RunTesseractAsync(
                            imagePath,
                            outputBasePath,
                            cancellationToken));
                    }

                    pageIndex++;
                }

                return string.Join("\n", pageTexts);
            }
            finally
            {
                try
                {
                    Directory.Delete(temporaryDirectory, recursive: true);
                }
                catch (IOException)
                {
                    // Best-effort cleanup; temporary OCR files are isolated per request.
                }
                catch (UnauthorizedAccessException)
                {
                    // Best-effort cleanup; temporary OCR files are isolated per request.
                }
            }
        }

        private static async Task<string> RunTesseractAsync(
            string imagePath,
            string outputBasePath,
            CancellationToken cancellationToken)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = TesseractExecutablePath,
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add(imagePath);
            startInfo.ArgumentList.Add(outputBasePath);
            startInfo.ArgumentList.Add("-l");
            // Vietnamese-only recognition preserves tone marks better for the
            // Vietnamese CVs this parser is designed to read. Common English and
            // technical names remain readable in the Vietnamese model.
            startInfo.ArgumentList.Add("vie");
            startInfo.ArgumentList.Add("--psm");
            // Auto-detect columns and blocks; CV scans are commonly multi-column layouts.
            startInfo.ArgumentList.Add("3");

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Không khởi chạy được Tesseract để nhận dạng CV.");
            var errorTask = process.StandardError.ReadToEndAsync();
            using var cancellationRegistration = cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch (InvalidOperationException)
                {
                    // The OCR process may exit while cancellation is being handled.
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    // The process may no longer be available to terminate.
                }
            });

            await process.WaitForExitAsync(cancellationToken);
            var error = await errorTask;
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(error)
                        ? "Tesseract không nhận dạng được nội dung của trang CV."
                        : $"Tesseract OCR lỗi: {error.Trim()}");
            }

            var outputPath = outputBasePath + ".txt";
            return File.Exists(outputPath)
                ? await File.ReadAllTextAsync(outputPath, Encoding.UTF8, cancellationToken)
                : string.Empty;
        }

        private static string ExtractDocxText(Stream stream)
        {
            stream.Position = 0;
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            var documentEntry = archive.GetEntry("word/document.xml")
                ?? throw new InvalidDataException("Không tìm thấy nội dung văn bản trong file DOCX.");
            using var documentStream = documentEntry.Open();
            var document = XDocument.Load(documentStream);
            var paragraphs = document.Descendants(WordNamespace + "p")
                .Select(paragraph => string.Concat(paragraph.Descendants(WordNamespace + "t").Select(text => text.Value)).Trim())
                .Where(paragraph => paragraph.Length > 0);
            return string.Join("\n", paragraphs);
        }

        private async Task<List<ResumeSkill>> RecognizeSkillsAsync(string text, CancellationToken cancellationToken)
        {
            var catalog = await _context.Skills
                .AsNoTracking()
                .Select(skill => new { skill.Name, skill.Category, skill.IsActive })
                .ToListAsync(cancellationToken);

            var definitions = new Dictionary<string, SkillDefinition>(StringComparer.Ordinal);
            var catalogKeys = catalog
                .Where(skill => !string.IsNullOrWhiteSpace(skill.Name))
                .Select(skill => NormalizeForMatching(skill.Name))
                .ToHashSet(StringComparer.Ordinal);

            foreach (var skill in catalog.Where(skill => skill.IsActive))
            {
                if (string.IsNullOrWhiteSpace(skill.Name)) continue;
                var key = NormalizeForMatching(skill.Name);
                var knownAliases = BuiltInSkills.FirstOrDefault(item => NormalizeForMatching(item.Name) == key)?.Aliases ?? [];
                definitions[key] = new SkillDefinition(
                    skill.Name.Trim(),
                    string.IsNullOrWhiteSpace(skill.Category) ? "Kỹ năng khác" : skill.Category.Trim(),
                    [skill.Name, .. knownAliases]);
            }

            foreach (var builtIn in BuiltInSkills)
            {
                var key = NormalizeForMatching(builtIn.Name);
                if (catalogKeys.Contains(key) && !definitions.ContainsKey(key)) continue;
                if (definitions.TryGetValue(key, out var existing))
                {
                    definitions[key] = existing with { Aliases = existing.Aliases.Concat(builtIn.Aliases).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() };
                }
                else
                {
                    definitions[key] = builtIn;
                }
            }

            var normalizedText = NormalizeForMatching(text);
            var found = new List<ResumeSkill>();
            foreach (var definition in definitions.Values)
            {
                var isPresent = definition.Aliases
                    .Append(definition.Name)
                    .Select(NormalizeForMatching)
                    .Where(alias => alias.Length > 0)
                    .Distinct(StringComparer.Ordinal)
                    .Any(alias => ContainsWholePhrase(normalizedText, alias));

                if (isPresent)
                {
                    found.Add(new ResumeSkill(definition.Name, definition.Category));
                }
            }

            return found
                .OrderBy(skill => skill.Category, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(skill => skill.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private static bool ContainsWholePhrase(string text, string phrase)
        {
            var pattern = $"(?<![\\p{{L}}\\p{{N}}]){Regex.Escape(phrase)}(?![\\p{{L}}\\p{{N}}])";
            return Regex.IsMatch(text, pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        }

        private static string? ExtractSummarySection(string text)
        {
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            for (var index = 0; index < lines.Length; index++)
            {
                if (!TryGetHeadingRemainder(lines[index], SummaryHeadings, out var firstLine)) continue;

                var content = new List<string>();
                if (!string.IsNullOrWhiteSpace(firstLine) && !IsContactLine(firstLine)) content.Add(firstLine);

                for (var contentIndex = index + 1; contentIndex < lines.Length; contentIndex++)
                {
                    if (IsSectionHeading(lines[contentIndex])) break;
                    if (!IsContactLine(lines[contentIndex])) content.Add(CleanContentLine(lines[contentIndex]));
                    if (string.Join(" ", content).Length >= MaxSummaryCharacters) break;
                }

                var summary = NormalizeParagraph(string.Join(" ", content));
                if (summary.Length > MaxSummaryCharacters) summary = summary[..MaxSummaryCharacters].TrimEnd();
                if (summary.Length >= 35) return summary;
            }

            return null;
        }

        private static string ExtractSectionContent(
            string text,
            IEnumerable<string> headings,
            bool continueAcrossMatchingHeadings = false)
        {
            var headingList = headings.ToArray();
            var content = new List<string>();
            var inTargetSection = false;
            foreach (var rawLine in text.Split('\n'))
            {
                var line = rawLine.Trim();
                if (TryGetSectionHeadingRemainder(line, headingList, out var headingRemainder))
                {
                    if (inTargetSection && !continueAcrossMatchingHeadings) break;
                    inTargetSection = true;
                    if (!string.IsNullOrWhiteSpace(headingRemainder)) content.Add(headingRemainder);
                    continue;
                }

                if (IsSectionHeading(line))
                {
                    if (inTargetSection) break;
                    continue;
                }

                if (!inTargetSection || IsContactLine(line)) continue;
                content.Add(line);
            }

            return string.Join("\n", content);
        }

        private static IReadOnlyList<ResumeSectionItem> ExtractSectionEntries(string text, IEnumerable<string> headings)
        {
            var sectionText = ExtractSectionContent(text, headings);
            if (string.IsNullOrWhiteSpace(sectionText)) return Array.Empty<ResumeSectionItem>();
            var sectionLines = sectionText.Split('\n').ToList();

            var groups = new List<List<string>>();
            var current = new List<string>();
            void FinishCurrent()
            {
                if (current.Count > 0) groups.Add(current);
                current = new List<string>();
            }

            foreach (var rawLine in sectionLines)
            {
                var line = rawLine.Trim();
                if (line.Length == 0)
                {
                    FinishCurrent();
                    continue;
                }

                // CVs often put each role or project on a date-bearing line.
                // A second date range in one block usually starts another item.
                if (DateRangePattern.IsMatch(line) && current.Any(DateRangePattern.IsMatch))
                {
                    FinishCurrent();
                }
                current.Add(line);
            }
            FinishCurrent();

            return groups
                .Select(CreateSectionItem)
                .Where(item => item is not null)
                .Cast<ResumeSectionItem>()
                .Take(20)
                .ToArray();
        }

        private static IReadOnlyList<ResumeSectionItem> ExtractLabeledWorkExperiences(string text)
        {
            var matches = ExperienceFieldPattern.Matches(text).Cast<Match>().ToArray();
            if (matches.Length == 0) return Array.Empty<ResumeSectionItem>();

            var items = new List<ResumeSectionItem>();
            string? title = null;
            string? organization = null;
            string? startedAt = null;
            string? endedAt = null;
            string? description = null;

            void FinishCurrent()
            {
                if (string.IsNullOrWhiteSpace(title)) return;
                var period = string.Join(" – ", new[] { startedAt, endedAt }
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => NormalizeParagraph(value!)));
                var cleanedDescription = CleanLabeledExperienceDescription(description ?? string.Empty);
                items.Add(new ResumeSectionItem(
                    NormalizeParagraph(title),
                    string.IsNullOrWhiteSpace(organization) ? null : NormalizeParagraph(organization),
                    string.IsNullOrWhiteSpace(period) ? null : period,
                    string.IsNullOrWhiteSpace(cleanedDescription) ? null : cleanedDescription));
                title = organization = startedAt = endedAt = description = null;
            }

            for (var index = 0; index < matches.Length; index++)
            {
                var match = matches[index];
                var valueStart = match.Index + match.Length;
                var valueEnd = index + 1 < matches.Length ? matches[index + 1].Index : text.Length;
                var value = text[valueStart..valueEnd].Trim();
                var label = NormalizeForMatching(match.Groups["label"].Value);

                if (label is "vi tri" or "chuc danh" || label.StartsWith("cong viec lam them", StringComparison.Ordinal))
                {
                    FinishCurrent();
                    title = value;

                    // Some CVs put a whole part-time job on one labeled line instead of
                    // using separate position/company/date fields.
                    var compactJob = Regex.Match(
                        value,
                        @"^(?<title>.+?)\s*\((?<period>[^)]{4,80})\)\s*(?:tại|tai|at)\s*(?<company>.+)$",
                        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);
                    if (compactJob.Success)
                    {
                        title = compactJob.Groups["title"].Value.Trim();
                        startedAt = compactJob.Groups["period"].Value.Trim();
                        organization = compactJob.Groups["company"].Value.Trim();
                    }
                    continue;
                }

                if (string.IsNullOrWhiteSpace(title)) continue;
                switch (label)
                {
                    case "cong ty":
                    case "don vi":
                        organization = value;
                        break;
                    case "thoi gian bat dau":
                        startedAt = value;
                        break;
                    case "thoi gian ket thuc":
                        endedAt = value;
                        break;
                    case "mo ta cong viec":
                        description = value;
                        break;
                }
            }

            FinishCurrent();
            return items
                .Where(item => item.Title.Length >= 2)
                .Take(20)
                .ToArray();
        }

        private static string CleanLabeledExperienceDescription(string value)
        {
            var lines = new List<string>();
            foreach (var rawLine in value.Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.Length == 0) continue;
                if (IsContactLine(line) || IsSectionHeading(line)) break;
                lines.Add(CleanContentLine(line));
            }

            var description = NormalizeParagraph(string.Join(" ", lines));
            return description.Length > 2400 ? description[..2400].TrimEnd() : description;
        }

        private static IReadOnlyList<ResumeSectionItem> ExtractProjectEntries(string text)
        {
            var section = ExtractSectionContent(text, ProjectHeadings);
            if (string.IsNullOrWhiteSpace(section)) return Array.Empty<ResumeSectionItem>();

            var projectHeaderPattern = new Regex(
                @"(?im)^\s*(?<title>(?:ứng dụng|do an|đồ án|du an|dự án)\s+[^\r\n]+?)\s*\r?\n\s*\((?<code>[^)\r\n]{2,100})\)\s*[:：]\s*",
                RegexOptions.CultureInvariant);
            var headers = projectHeaderPattern.Matches(section).Cast<Match>().ToArray();
            if (headers.Length == 0) return ExtractSectionEntries(text, ProjectHeadings);

            var projects = new List<ResumeSectionItem>();
            for (var index = 0; index < headers.Length; index++)
            {
                var header = headers[index];
                var bodyStart = header.Index + header.Length;
                var bodyEnd = index + 1 < headers.Length ? headers[index + 1].Index : section.Length;
                var body = NormalizeParagraph(section[bodyStart..bodyEnd]);
                var code = header.Groups["code"].Value.Trim();
                if (body.Length > 2400) body = body[..2400].TrimEnd();
                projects.Add(new ResumeSectionItem(
                    NormalizeParagraph(header.Groups["title"].Value),
                    null,
                    null,
                    string.IsNullOrWhiteSpace(body) ? $"Mã dự án: {code}" : $"Mã dự án: {code}. {body}"));
            }

            return projects.Take(20).ToArray();
        }

        private static IReadOnlyList<ResumeSectionItem> ExtractEducationEntries(string text)
        {
            var section = ExtractSectionContent(text, EducationHeadings);
            if (string.IsNullOrWhiteSpace(section)) return Array.Empty<ResumeSectionItem>();

            var educationItems = new List<ResumeSectionItem>();
            var flattenedSection = NormalizeParagraph(section);
            foreach (Match institution in EducationInstitutionPattern.Matches(flattenedSection))
            {
                var prefix = flattenedSection[..institution.Index].Trim(' ', ',', '-', '–', '|');
                var institutionName = institution.Groups["institution"].Value.Trim();
                var repeatedInstitution = Regex.Match(
                    institutionName,
                    @"\s+(?:(?:trường|truong)\s+)?(?:đại\s+học|dai\s+hoc|cao\s+đẳng|cao\s+dang|university|college|institute|học\s+viện|hoc\s+vien)\b",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                if (repeatedInstitution.Success)
                {
                    institutionName = institutionName[..repeatedInstitution.Index].Trim();
                }
                if (institutionName.Length < 3) continue;

                var institutionEnd = institution.Index + institutionName.Length;
                var suffix = flattenedSection[institutionEnd..];
                var majorPattern =
                    @"(?:chuy[eéê]n\s+ng[aàá]nh|ngành|nganh|major(?:\s+in)?)\s+(?<major>.+?)(?=\s+(?:với|voi|with|gpa|điểm|diem|kết quả|ket qua|hiện tại|hien tai|tốt nghiệp|tot nghiep)\b|[,.|;]|$)";
                var major = Regex.Match(
                    prefix,
                    majorPattern,
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                if (!major.Success)
                {
                    major = Regex.Match(suffix, majorPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                }

                var title = major.Success
                    ? major.Groups["major"].Value.Trim()
                    : institutionName;
                if (title.Length > 140) title = title[..140].TrimEnd();

                var contextStart = Math.Max(0, institution.Index - 180);
                var contextEnd = Math.Min(flattenedSection.Length, institutionEnd + 180);
                var description = flattenedSection[contextStart..contextEnd].Trim();
                var date = DateRangePattern.Match(description);
                var period = date.Success ? date.Value : null;
                if (string.IsNullOrWhiteSpace(period))
                {
                    var currentStudy = Regex.Match(
                        description,
                        @"\b(?:hiện tại|hien tai|nay|present|current)\b",
                        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                    if (currentStudy.Success) period = currentStudy.Value;
                }
                var gpa = Regex.Match(
                    description,
                    @"(?:gpa(?:\s+(?:hiện\s+tại|hien\s+tai))?|điểm\s+trung\s+bình|diem\s+trung\s+binh)\s*[:：]?\s*\d+(?:[,.]\d+)?(?:\s*/\s*\d+(?:[,.]\d+)?)?",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                var studyLevel = Regex.Match(
                    description,
                    @"(?:sinh\s+viên|sinh\s+vien)\s+(?:năm|nam)\s+\d+|(?:đang\s+học|dang\s+hoc|đã\s+tốt\s+nghiệp|da\s+tot\s+nghiep|tốt\s+nghiệp|tot\s+nghiep)",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                var details = new[]
                    {
                        studyLevel.Success ? NormalizeParagraph(studyLevel.Value) : null,
                        gpa.Success ? NormalizeParagraph(gpa.Value) : null
                    }
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase);
                educationItems.Add(new ResumeSectionItem(
                    title,
                    NormalizeParagraph(institutionName),
                    period,
                    string.Join(" · ", details)));
            }

            return educationItems.Count > 0
                ? educationItems
                    .Where(IsEducationEntry)
                    .DistinctBy(item => $"{NormalizeForMatching(item.Title)}|{NormalizeForMatching(item.Organization ?? string.Empty)}")
                    .Take(20)
                    .ToArray()
                : ExtractSectionEntries(text, EducationHeadings)
                    .Where(IsEducationOrCertificateEntry)
                    .Take(20)
                    .ToArray();
        }

        private static bool IsEducationOrCertificateEntry(ResumeSectionItem item)
        {
            return IsEducationEntry(item) || IsCertificateEntry(item);
        }

        private static bool IsEducationEntry(ResumeSectionItem item)
        {
            var titleAndOrganization = $"{item.Title} {item.Organization}";
            return EducationInstitutionPattern.IsMatch(titleAndOrganization)
                || Regex.IsMatch(
                    titleAndOrganization,
                    @"(?:cử\s+nhân|cu\s+nhan|thạc\s+sĩ|thac\s+si|tiến\s+sĩ|tien\s+si|bachelor(?:'s)?|master(?:'s)?|ph\.?d\.?|associate(?:'s)?\s+degree|bằng\s+cấp|bang\s+cap)",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        private static bool IsCertificateEntry(ResumeSectionItem item)
        {
            var titleAndOrganization = $"{item.Title} {item.Organization}";
            return Regex.IsMatch(
                titleAndOrganization,
                @"(?:chứng\s+chỉ|chung\s+chi|chứng\s+nhận|chung\s+nhan|certificate|certified|certification|license|licensed|chứng\s+chỉ\s+hành\s+nghề|ielts|toeic|toefl|pmp)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        private static string? ExtractLabeledValue(string text, IEnumerable<string> labels, int maxLength)
        {
            var lines = text.Split('\n', StringSplitOptions.TrimEntries);
            for (var index = 0; index < lines.Length; index++)
            {
                if (!TryGetHeadingRemainder(lines[index], labels, out var value)) continue;
                if (string.IsNullOrWhiteSpace(value) && index + 1 < lines.Length)
                {
                    value = lines[index + 1].Trim();
                }

                value = Regex.Replace(
                    value,
                    @"\s+(?:e-?mail|điện thoại|dien thoai|phone|mobile|số điện thoại|so dien thoai)\s*[:：].*$",
                    string.Empty,
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                value = NormalizeParagraph(value);
                if (value.Length < 3 || value.Length > maxLength || ContactPattern.IsMatch(value)) continue;
                return value;
            }

            return null;
        }

        private static string? ExtractSuggestedFullName(string text)
        {
            var lines = text.Split('\n', StringSplitOptions.TrimEntries);
            foreach (var line in lines.Take(18))
            {
                if (!TryGetHeadingRemainder(line, FullNameLabels, out var value) || string.IsNullOrWhiteSpace(value)) continue;
                var labeledName = CleanPossibleName(value);
                if (IsLikelyPersonName(labeledName) && !IsLikelyRoleOrHeading(labeledName)) return FormatPersonName(labeledName);
            }

            var headerLines = lines
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Take(18)
                .ToArray();
            for (var index = 0; index < headerLines.Length; index++)
            {
                var candidate = CleanPossibleName(headerLines[index]);
                candidate = Regex.Replace(candidate, @"^[^\p{L}\p{N}]+", string.Empty).Trim();
                if (!IsLikelyPersonName(candidate)) continue;
                if (IsLikelyRoleOrHeading(candidate) || IsSectionHeading(candidate)) continue;
                if (EducationInstitutionPattern.IsMatch(candidate)) continue;
                if (candidate.Any(char.IsDigit)) continue;

                // CV headers commonly render names in uppercase. Only use an
                // unlabeled fallback when the text looks like a personal name.
                var words = candidate.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var isUppercaseHeader = words.All(word => word.Any(char.IsLetter)
                    && word.Where(char.IsLetter).All(char.IsUpper));
                // OCR can lower-case a person's name even when the CV displays it
                // correctly. A short, standalone name near the top of the CV is
                // still a strong candidate, then FormatPersonName restores casing.
                var isEarlyNameHeader = index < 4;
                if (isUppercaseHeader || LooksLikeTitleCaseName(candidate) || isEarlyNameHeader)
                {
                    return FormatPersonName(candidate);
                }
            }

            // Some OCR layouts merge the name and job title onto one line, or
            // place an icon-like character before the name. Recover the longest
            // valid name prefix from those first CV header lines in that case.
            foreach (var rawLine in text.Split('\n', StringSplitOptions.TrimEntries)
                         .Where(line => !string.IsNullOrWhiteSpace(line))
                         .Take(24))
            {
                var line = CleanPossibleName(rawLine);
                line = Regex.Replace(line, @"^[^\p{L}\p{N}]+", string.Empty).Trim();
                var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                for (var wordCount = Math.Min(words.Length, 5); wordCount >= 2; wordCount--)
                {
                    var candidate = string.Join(' ', words.Take(wordCount));
                    if (!IsLikelyPersonName(candidate)
                        || IsLikelyRoleOrHeading(candidate)
                        || IsSectionHeading(candidate)
                        || EducationInstitutionPattern.IsMatch(candidate)
                        || candidate.Any(char.IsDigit)) continue;

                    return FormatPersonName(candidate);
                }
            }

            return null;
        }

        private static string? ExtractSuggestedPhoneNumber(string text)
        {
            var lines = text.Split('\n', StringSplitOptions.TrimEntries);
            var prioritizedLines = lines
                .Select((line, index) => (line, index, isLabeled: TryGetHeadingRemainder(line, PhoneLabels, out _)))
                .OrderByDescending(item => item.isLabeled)
                .ThenBy(item => item.index)
                .Select(item => item.line);

            foreach (var line in prioritizedLines)
            {
                var match = VietnamesePhonePattern.Match(line);
                if (!match.Success) continue;

                var digits = Regex.Replace(match.Value, @"\D", string.Empty);
                if (digits.Length == 10 && digits.StartsWith('0')) return digits;
                if (digits.Length == 11 && digits.StartsWith("84", StringComparison.Ordinal)) return $"+{digits}";
            }

            return null;
        }

        private static string? ExtractSuggestedLocation(string text)
        {
            var lines = text.Split('\n', StringSplitOptions.TrimEntries);
            foreach (var line in lines)
            {
                var inlineAddress = InlineAddressPattern.Match(line);
                if (!inlineAddress.Success) continue;
                var value = NormalizeLocationText(CleanContactDetails(inlineAddress.Groups["value"].Value));
                if (IsUsableLocation(value)) return value;
            }

            for (var index = 0; index < lines.Length; index++)
            {
                if (!TryGetHeadingRemainder(lines[index], AddressLabels, out var value)) continue;
                if (string.IsNullOrWhiteSpace(value) && index + 1 < lines.Length) value = lines[index + 1];
                value = NormalizeLocationText(CleanContactDetails(value));
                if (IsUsableLocation(value)) return NormalizeParagraph(value);
            }

            // Some CV templates show the city as one item in a contact row
            // without an explicit "Address" label (for example, after email).
            foreach (var line in lines.Take(24))
            {
                var candidate = NormalizeLocationText(CleanContactDetails(line));
                if (candidate.Length is < 3 or > 120 || !LocationIndicatorPattern.IsMatch(candidate)) continue;
                if (IsSectionHeading(candidate) || IsLikelyRoleOrHeading(candidate)) continue;
                if (IsUsableLocation(candidate)) return NormalizeParagraph(candidate);
            }

            return null;
        }

        private static string NormalizeLocationText(string value)
        {
            var normalized = NormalizeParagraph(value).Trim('"', '\'', '“', '”', '‘', '’', '`', '!', '.', ',', ';', ':', '|', '•', '·');
            // Tesseract can interpret the location-pin icon before the address
            // as a leading "9". It is noise when followed by a ward label.
            normalized = Regex.Replace(normalized, @"^\s*\d{1,2}\s+(?=(?:phường|phuong|phương|ward)\b)", string.Empty,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            // The sample CV's small-font OCR confuses the tone mark in "Phường".
            normalized = Regex.Replace(normalized, @"^phương(?=\s+phú\s+an\b)", "Phường",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return NormalizeParagraph(normalized);
        }

        private static string CleanPossibleName(string value)
        {
            var candidate = Regex.Replace(value, @"\b(?:e-?mail|email|phone|mobile|điện thoại|số điện thoại|sdt|địa chỉ|address|location)\b\s*[:：].*$", string.Empty,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            candidate = Regex.Replace(candidate, @"^(?:curriculum vitae|resume|cv)\s+", string.Empty,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            candidate = Regex.Replace(candidate, @"[|•·,;].*$", string.Empty);
            candidate = VietnamesePhonePattern.Replace(candidate, string.Empty);
            candidate = Regex.Replace(candidate, @"[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}", string.Empty);
            candidate = Regex.Replace(candidate, @"https?://\S+|www\.\S+", string.Empty, RegexOptions.IgnoreCase);
            return NormalizeParagraph(candidate).Trim('-', '—', '–', ':');
        }

        private static bool IsLikelyPersonName(string value) =>
            value.Length is >= 4 and <= 80 && HumanNamePattern.IsMatch(value);

        private static bool LooksLikeTitleCaseName(string value)
        {
            var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return words.Length is >= 2 and <= 5
                && words.All(word => char.IsUpper(word[0]) && word.Skip(1).All(char.IsLower));
        }

        private static bool IsLikelyRoleOrHeading(string value)
        {
            var normalized = NormalizeForMatching(value);
            string[] excluded =
            [
                "curriculum vitae", "resume", "cv", "profile", "software engineer", "software developer",
                "web developer", "backend developer", "frontend developer", "full stack developer", "data analyst",
                "marketing executive", "business development", "accountant", "graphic designer", "student",
                "professional summary", "career objective", "work experience", "education", "skills", "projects",
                "liên hệ", "học vấn", "kỹ năng", "ngôn ngữ", "giới thiệu", "dự án", "chứng chỉ",
                "hiện tại", "hiện nay", "đang làm", "hoàn thành", "tốt nghiệp", "ngày hiện tại"
            ];
            if (excluded.Any(item => normalized == NormalizeForMatching(item))) return true;

            var roleTerms = new[]
            {
                "developer", "engineer", "designer", "manager", "analyst", "accountant", "marketing", "intern",
                "assistant", "specialist", "consultant", "recruiter", "student", "sales", "executive", "technician",
                "teacher", "nurse", "doctor", "lawyer", "tester", "programmer", "coordinator", "nhan vien",
                "lap trinh", "ky su", "chuyen vien", "quan ly", "giao vien", "ke toan", "ban hang",
                "dai hoc", "cao dang", "university", "college", "school", "backend", "frontend", "front end",
                "full stack", "senior", "junior", "trainee", "internship", "software", "quality assurance",
                "thuc tap", "sinh vien", "careerconnect", "candidate", "applicant", "hien tai", "hien nay",
                "currently", "present", "now", "today", "nay", "dang lam", "hoan thanh", "tot nghiep"
            };
            return roleTerms.Any(term => normalized.Contains(NormalizeForMatching(term), StringComparison.Ordinal));
        }

        private static string FormatPersonName(string value)
        {
            var normalized = NormalizeParagraph(value);
            var vietnameseCulture = CultureInfo.GetCultureInfo("vi-VN");
            return vietnameseCulture.TextInfo.ToTitleCase(normalized.ToLower(vietnameseCulture));
        }

        private static string CleanContactDetails(string value)
        {
            var cleaned = Regex.Replace(value, @"[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}", string.Empty);
            cleaned = VietnamesePhonePattern.Replace(cleaned, string.Empty);
            cleaned = Regex.Replace(cleaned, @"https?://\S+|www\.\S+", string.Empty, RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"\b(?:e-?mail|email|phone|mobile|telephone|điện thoại|số điện thoại|sdt)\b\s*[:：]?", string.Empty,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            cleaned = Regex.Replace(cleaned, @"\s*[|•·;,]\s*", ", ");
            return NormalizeParagraph(cleaned).Trim(',', '|', '•', '·', ';', ':', '-', '—', '–');
        }

        private static bool IsUsableLocation(string value) =>
            value.Length is >= 5 and <= 120
            && Regex.Replace(value, @"[^\p{L}\p{N}]+", " ").Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 2
            && !ContactPattern.IsMatch(value)
            && !IsSectionHeading(value)
            && !new[] { "of birth", "place of birth", "native place", "quê quán", "ngày sinh", "date of birth" }
                .Any(noise => NormalizeForMatching(value) == NormalizeForMatching(noise));

        private static ResumeSectionItem? CreateSectionItem(IReadOnlyList<string> rawLines)
        {
            var lines = rawLines
                .Select(raw => (Raw: raw.Trim(), Text: CleanContentLine(raw)))
                .Where(line => !string.IsNullOrWhiteSpace(line.Text))
                .ToList();
            var titleIndex = lines.FindIndex(line => !DateRangePattern.IsMatch(line.Text));
            if (titleIndex < 0) return null;

            var title = NormalizeParagraph(lines[titleIndex].Text);
            string? organization = null;
            string? period = lines
                .Select(line => NormalizeParagraph(line.Text))
                .FirstOrDefault(DateRangePattern.IsMatch);
            var consumed = new HashSet<int> { titleIndex };
            for (var index = 0; index < lines.Count; index++)
            {
                if (DateRangePattern.IsMatch(lines[index].Text)) consumed.Add(index);
            }

            foreach (var separator in new[] { " | ", " — ", " – ", " @ " })
            {
                var separatorIndex = title.IndexOf(separator, StringComparison.Ordinal);
                if (separatorIndex <= 0 || separatorIndex + separator.Length >= title.Length) continue;
                organization = title[(separatorIndex + separator.Length)..].Trim();
                title = title[..separatorIndex].Trim();
                break;
            }

            if (organization is null && titleIndex + 1 < lines.Count)
            {
                var next = lines[titleIndex + 1];
                var startsWithBullet = next.Raw.TrimStart().StartsWith("•", StringComparison.Ordinal)
                    || next.Raw.TrimStart().StartsWith("▪", StringComparison.Ordinal)
                    || next.Raw.TrimStart().StartsWith("◦", StringComparison.Ordinal)
                    || next.Raw.TrimStart().StartsWith("*", StringComparison.Ordinal);
                if (!DateRangePattern.IsMatch(next.Text)
                    && !startsWithBullet
                    && next.Text.Length <= 120
                    && !next.Text.EndsWith(".", StringComparison.Ordinal))
                {
                    organization = next.Text;
                    consumed.Add(titleIndex + 1);
                }
            }

            var description = string.Join(" • ", lines
                .Where((_, index) => !consumed.Contains(index))
                .Select(line => NormalizeParagraph(CleanContentLine(line.Raw)))
                .Where(value => value.Length > 0));

            if (title.Length > 180) title = title[..180].TrimEnd();
            if (organization?.Length > 180) organization = organization[..180].TrimEnd();
            if (period?.Length > 80) period = period[..80].TrimEnd();
            if (description.Length > 2400) description = description[..2400].TrimEnd();

            return title.Length < 2
                ? null
                : new ResumeSectionItem(title, organization, period, description.Length == 0 ? null : description);
        }

        private static string? ExtractIntroParagraph(string text)
        {
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var content = new List<string>();
            var length = 0;

            foreach (var rawLine in lines.Take(24))
            {
                if (TryGetSectionHeadingRemainder(rawLine, IntroStopHeadings, out _)) break;
                if (IsContactLine(rawLine)) continue;

                var line = CleanContentLine(rawLine);
                if (line.Length < 18 || IsSectionHeading(line)) continue;
                content.Add(line);
                length += line.Length;
                if (length >= 500) break;
            }

            var intro = NormalizeParagraph(string.Join(" ", content));
            if (intro.Length < 45) return null;
            if (intro.Length > MaxSummaryCharacters) intro = intro[..MaxSummaryCharacters].TrimEnd();
            return intro;
        }

        private static string? ExtractExperienceDescription(string text)
        {
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var inExperienceSection = false;
            var content = new List<string>();
            var length = 0;

            foreach (var rawLine in lines)
            {
                if (IsSectionHeading(rawLine))
                {
                    if (IsExperienceHeading(rawLine))
                    {
                        inExperienceSection = true;
                        continue;
                    }

                    if (inExperienceSection) break;
                    continue;
                }

                if (!inExperienceSection || IsContactLine(rawLine)) continue;
                var line = CleanContentLine(rawLine);
                if (line.Length < 45 || IsDateOnlyLine(line)) continue;

                content.Add(line);
                length += line.Length;
                if (length >= 500) break;
            }

            var description = NormalizeParagraph(string.Join(" ", content));
            if (description.Length < 45) return null;
            if (description.Length > MaxSummaryCharacters) description = description[..MaxSummaryCharacters].TrimEnd();
            return $"Kinh nghiệm được trình bày trong CV: {description}";
        }

        private static bool IsExperienceHeading(string value)
        {
            var normalized = NormalizeForMatching(value);
            return new[]
            {
                "experience", "work experience", "employment history", "professional experience",
                "career history", "kinh nghiệm", "kinh nghiệm làm việc"
            }.Any(heading => normalized == NormalizeForMatching(heading)
                || normalized.StartsWith(NormalizeForMatching(heading) + " ", StringComparison.Ordinal));
        }

        private static bool IsDateOnlyLine(string value) =>
            Regex.IsMatch(value, @"^\s*(?:(?:19|20)\d{2}|0?[1-9]|1[0-2])\s*[-–—/]\s*(?:(?:19|20)\d{2}|0?[1-9]|1[0-2]|present|hiện tại|nay)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static string? BuildSkillsBasedSummary(IReadOnlyList<ResumeSkill> skills)
        {
            if (skills.Count == 0) return null;
            var skillNames = skills.Take(8).Select(skill => skill.Name);
            return $"CV có đề cập đến các kỹ năng: {string.Join(", ", skillNames)}.";
        }

        private static bool TryGetHeadingRemainder(string line, IEnumerable<string> headings, out string remainder)
        {
            remainder = string.Empty;
            var trimmed = line.Trim().TrimStart('#', '•', '-', '*', '·').Trim();
            foreach (var separator in new[] { ':', '：', '|', '-', '—', '–' })
            {
                var separatorIndex = trimmed.IndexOf(separator);
                if (separatorIndex < 0) continue;
                var prefix = NormalizeForMatching(trimmed[..separatorIndex]);
                if (headings.Any(heading => prefix == NormalizeForMatching(heading)))
                {
                    remainder = trimmed[(separatorIndex + 1)..].Trim();
                    return true;
                }
            }

            var normalizedLine = NormalizeForMatching(trimmed);
            foreach (var heading in headings.OrderByDescending(value => value.Length))
            {
                var normalizedHeading = NormalizeForMatching(heading);
                if (normalizedLine == normalizedHeading) return true;
                if (normalizedLine.StartsWith(normalizedHeading + " ", StringComparison.Ordinal))
                {
                    remainder = trimmed[Math.Min(heading.Length, trimmed.Length)..].Trim(' ', ':', '-', '—', '–');
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetSectionHeadingRemainder(
            string line,
            IEnumerable<string> headings,
            out string remainder)
        {
            remainder = string.Empty;
            var trimmed = line.Trim().TrimStart('#', '•', '-', '*', '·').Trim();
            var normalizedLine = NormalizeForMatching(trimmed);
            if (headings.Any(heading => normalizedLine == NormalizeForMatching(heading))) return true;

            // Resume templates often append a link or date to a section title,
            // for example: "Selected Academic Projects (GitHub: ...)".
            foreach (var heading in headings.OrderByDescending(value => value.Length))
            {
                var normalizedHeading = NormalizeForMatching(heading);
                if (!normalizedLine.StartsWith(normalizedHeading + " ", StringComparison.Ordinal)) continue;
                var rawRemainder = trimmed[Math.Min(heading.Length, trimmed.Length)..].Trim();
                if (!rawRemainder.StartsWith("(", StringComparison.Ordinal)
                    && !rawRemainder.StartsWith("[", StringComparison.Ordinal)) continue;
                remainder = rawRemainder;
                return true;
            }

            foreach (var separator in new[] { ':', '：', '|', '-', '—', '–' })
            {
                var separatorIndex = trimmed.IndexOf(separator);
                if (separatorIndex < 0) continue;
                var prefix = NormalizeForMatching(trimmed[..separatorIndex]);
                if (!headings.Any(heading => prefix == NormalizeForMatching(heading))) continue;
                remainder = trimmed[(separatorIndex + 1)..].Trim();
                return true;
            }

            return false;
        }

        private static bool IsSectionHeading(string value) =>
            TryGetSectionHeadingRemainder(value, SectionHeadings, out _);

        private static bool IsContactLine(string value) => ContactPattern.IsMatch(value);

        private static string CleanContentLine(string value) =>
            value.Trim().TrimStart('•', '-', '*', '·', '▪', '◦').Trim();

        private static string NormalizeParagraph(string value)
        {
            var normalized = Regex.Replace(value, @"\s+", " ").Trim();
            return Regex.Replace(normalized, @"\s+([,.;:!?])", "$1");
        }

        private static string NormalizeForMatching(string value)
        {
            var decomposed = value.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);
            foreach (var character in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(character);
                }
            }

            var withoutAccents = builder.ToString()
                .Replace('đ', 'd')
                .Replace('Đ', 'D')
                .Normalize(NormalizationForm.FormC)
                .ToLowerInvariant();
            return Regex.Replace(withoutAccents, @"[^\p{L}\p{N}+#.]+", " ").Trim();
        }

        private static string NormalizeText(string text)
        {
            var normalized = text.Replace('\r', '\n');
            normalized = Regex.Replace(normalized, @"[\t ]+", " ");
            normalized = Regex.Replace(normalized, @"\n\s*\n\s*\n+", "\n\n");
            return normalized.Trim();
        }

        private sealed record SkillDefinition(string Name, string Category, string[] Aliases);
    }
}
