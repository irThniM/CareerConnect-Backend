using CareerConnect.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Thêm Controllers và Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "CareerConnect API", Version = "v1" });

    // Cấu hình xịn: Loại Http, Scheme "bearer" -> Tự động thêm chữ Bearer vào Header
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "Chỉ cần dán thẳng chuỗi Token của bạn vào đây (Hệ thống sẽ tự động thêm chữ Bearer phía trước).",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Gọi hàm mở rộng cấu hình Database & Auth Services ở đây
builder.Services.AddDatabaseConfiguration(builder.Configuration);

var app = builder.Build();

// Cấu hình Middleware HTTP pipeline...
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowReactApp");
app.UseAuthentication();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

await app.SeedDatabaseAsync();

app.Run();