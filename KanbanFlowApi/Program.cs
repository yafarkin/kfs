using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Добавляем контроллеры, Swagger и статические файлы
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Включаем Swagger UI и статические файлы
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Раздача статических файлов (wwwroot)
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();

app.Run();