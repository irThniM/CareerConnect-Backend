using System.Text.Json.Serialization;

namespace CareerConnect.Application.Companies.Responses
{
    public class KybAiResultDto
    {
        [JsonPropertyName("MaSoDoanhNghiep")]
        public string MaSoDoanhNghiep { get; set; } = string.Empty;

        [JsonPropertyName("TenCongTy")]
        public string TenCongTy { get; set; } = string.Empty;

        [JsonPropertyName("DiaChi")]
        public string DiaChi { get; set; } = string.Empty;

        [JsonPropertyName("NguoiDaiDien")]
        public string NguoiDaiDien { get; set; } = string.Empty;

        [JsonPropertyName("CoMocDoVaChuKy")]
        public bool CoMocDoVaChuKy { get; set; }
    }
}
