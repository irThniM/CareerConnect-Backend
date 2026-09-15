using CareerConnect.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Thêm Controllers và Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();