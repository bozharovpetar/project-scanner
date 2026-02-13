using System.Threading.Channels;
using CodeScanner.Api.BackgroundServices;
using CodeScanner.Api.Data;
using CodeScanner.Api.Endpoints;
using CodeScanner.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=codescanner.db"));

// HttpClient for Ollama
builder.Services.AddHttpClient<IOllamaClient, OllamaClient>(client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration.GetValue<string>("Ollama:BaseUrl") ?? "http://localhost:11434");
    client.Timeout = TimeSpan.FromMinutes(5);
});

// Services
builder.Services.AddScoped<IScanService, ScanService>();
builder.Services.AddScoped<IFileDiscoveryService, FileDiscoveryService>();
builder.Services.AddScoped<IFileAnalyzer, FileAnalyzer>();

// Background processing
builder.Services.AddSingleton(Channel.CreateUnbounded<int>());
builder.Services.AddSingleton<ScanProgressBroadcaster>();
builder.Services.AddHostedService<ScanProcessorService>();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Code Scanner API", Version = "v1" });
});

var app = builder.Build();

// Ensure DB exists
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.UseSwagger();
app.UseSwaggerUI();

// Map endpoints
app.MapScanEndpoints();
app.MapReportEndpoints();

app.Run();
