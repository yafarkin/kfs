using System.Reflection;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Добавляем контроллеры, Swagger и статические файлы
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Подтягиваем XML-докстринги (GenerateDocumentationFile в .csproj) в схему —
    // из неё tools/generate-api-types.mjs генерирует wwwroot/api-types.d.ts с теми же
    // комментариями, чтобы фронт не «читал C#-класс и угадывал» формат JSON.
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

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

// Точка входа сделана видимой для интеграционных тестов (WebApplicationFactory<Program>).
public partial class Program
{
}