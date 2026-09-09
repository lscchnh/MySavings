using Microsoft.EntityFrameworkCore;
using MySavings.ApiService.Data;
using MySavings.ApiService.Endpoints;
using MySavings.ApiService.Services;
using OfficeOpenXml;
using Scalar.AspNetCore;
using System.Text.Json.Serialization;

ExcelPackage.License.SetNonCommercialPersonal("MySavings");

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

// Ignore JSON cycles (MonthlyEntry ↔ SavingsAllocation navigation properties)
// and serialize enums (e.g. LiquidityLevel) as their string name rather than a number.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// Configure SQLite database
builder.Services.AddDbContext<MySavingsDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("MySavingsDb") ?? "Data Source=mysavings.db"));

// Register services
builder.Services.AddScoped<ExcelImportService>();
builder.Services.AddScoped<AllocationCalculationService>();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Ensure database is created and migrations are applied
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MySavingsDbContext>();
    db.Database.Migrate();
}

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// Map API endpoints
app.MapMonthlyEntryEndpoints();
app.MapAllocationRuleEndpoints();
app.MapDashboardEndpoints();
app.MapImportEndpoints();
app.MapDatabaseEndpoints();
app.MapPersonSettingsEndpoints();
app.MapSavingsAllocationEndpoints();
app.MapTransferGroupEndpoints();
app.MapAppSettingsEndpoints();

app.MapDefaultEndpoints();

app.Run();

public partial class Program { }

