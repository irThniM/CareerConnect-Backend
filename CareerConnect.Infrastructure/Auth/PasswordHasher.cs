namespace CareerConnect.Infrastructure.Auth
{
    public class PasswordHasher
    {
        // Hàm băm mật khẩu (Dùng khi Đăng ký)
        public string HashPassword(string password)
        {
            // Tự động sinh ra salt và băm mật khẩu
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        // Hàm kiểm tra mật khẩu (Dùng khi Đăng nhập)
        public bool VerifyPassword(string password, string hashedPassword)
        {
            // So sánh mật khẩu người dùng nhập với chuỗi băm trong Database
            return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
        }
    }
}
