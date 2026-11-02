# Cấu hình thanh toán VNPay Sandbox

## 1. Tạo bảng trên Neon

Chạy trong Visual Studio Package Manager Console:

```powershell
Update-Database -Migration AddCompanyBilling -Project CareerConnect.Infrastructure -StartupProject CareerConnect.Api
```

Migration tạo `packages`, `company_subscriptions` và `payment_transactions`. Migration này chỉ tạo cấu trúc; chưa tự áp dụng lên Neon.

## 2. Thêm một gói để thử

Sau migration, chạy SQL này trong Neon SQL Editor. Giá và hạn mức bên dưới chỉ là dữ liệu minh họa cho Sandbox; sửa trước khi chạy nếu muốn dùng giá khác.

```sql
INSERT INTO public.packages
    ("Name", "Description", "Price", "DurationDays", "MemberLimit", "JobPostLimit", "IsActive", "CreatedAt")
VALUES
    ('Gói thử VNPay', 'Gói minh họa để kiểm tra thanh toán Sandbox.', 10000, 30, 1, 3, TRUE, NOW());
```

## 3. Cấu hình máy local bằng User Secrets

Chạy tại thư mục backend, thay các giá trị trong ngoặc nhọn bằng thông tin trong thư VNPAY và domain ứng dụng của em:

```powershell
dotnet user-secrets set "Vnpay:TmnCode" "<Terminal ID>" --project CareerConnect.Api
dotnet user-secrets set "Vnpay:HashSecret" "<Secret Key>" --project CareerConnect.Api
dotnet user-secrets set "Vnpay:ReturnUrl" "https://<frontend-domain>/employer/dashboard/billing" --project CareerConnect.Api
dotnet user-secrets set "Cors:AllowedOrigins:0" "https://<frontend-domain>" --project CareerConnect.Api
```

`PaymentUrl` mặc định đã trỏ tới Sandbox của VNPay. Không đưa `HashSecret` vào Git hoặc biến `VITE_*` của frontend.

## 4. Cấu hình khi deploy

- Trên backend host, đặt biến môi trường `Vnpay__TmnCode`, `Vnpay__HashSecret`, `Vnpay__ReturnUrl` và `Cors__AllowedOrigins__0` bằng các giá trị tương ứng.
- Trên Vercel, đặt `VITE_API_BASE_URL` thành địa chỉ API công khai, kết thúc bằng `/api`, rồi redeploy frontend.
- Đăng ký với VNPay IPN URL công khai của backend: `https://<backend-domain>/api/company/billing/vnpay/ipn`. IPN phải trỏ tới backend; URL frontend không thể thay thế endpoint này.
- Dùng domain backend ổn định. Quick Tunnel của Cloudflare có thể đổi URL sau khi chạy lại.

Luồng sau khi cấu hình: doanh nghiệp chọn gói → backend tạo giao dịch `PENDING` → trình duyệt chuyển sang VNPay → IPN hợp lệ mới kích hoạt gói và cấp lượt đăng. Trang quay lại chỉ đọc trạng thái giao dịch, không tự xác nhận thanh toán.
