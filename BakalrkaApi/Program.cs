using API.Services;
using BakalrkaApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddOptions<ApiSettings>()
    .Bind(builder.Configuration.GetSection("Api"))
    .ValidateDataAnnotations()
    .Validate(config => !string.IsNullOrEmpty(config.Key), "API Key is required")
    .Validate(config => !string.IsNullOrEmpty(config.Url), "Url is required");

builder.Services.AddScoped<SearchService>();
builder.Services.AddScoped<OcrService>();
builder.Services.AddScoped<FileService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();