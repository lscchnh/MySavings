using Microsoft.EntityFrameworkCore;
using MySavings.ApiService.Data;
using MySavings.ApiService.Models;

namespace MySavings.ApiService.Endpoints;

public static class AppSettingsEndpoints
{
    /// <summary>
    /// Fixed primary key for the single AppSettings row (seeded by the AddAppSettings migration).
    /// Targeting this id explicitly, instead of "first row found", means concurrent first-time
    /// saves collide on the primary key rather than silently creating duplicate rows.
    /// </summary>
    private const int SingletonId = 1;

    public static void MapAppSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/app-settings")
            .WithTags("App Settings");

        // GET the singleton app settings row
        group.MapGet("/", async (MySavingsDbContext db) =>
        {
            var settings = await db.AppSettings.FindAsync(SingletonId) ?? new AppSettings();
            return Results.Ok(new AppSettingsDto { SecurityBuffer = settings.SecurityBuffer });
        })
        .WithName("GetAppSettings");

        // PUT update the singleton app settings row
        group.MapPut("/", async (UpdateAppSettingsRequest request, MySavingsDbContext db) =>
        {
            if (request.SecurityBuffer < 0)
                return Results.BadRequest(new { error = "Le solde de sécurité ne peut pas être négatif." });

            var settings = await db.AppSettings.FindAsync(SingletonId);
            if (settings is null)
            {
                settings = new AppSettings { Id = SingletonId, SecurityBuffer = request.SecurityBuffer };
                db.AppSettings.Add(settings);
            }
            else
            {
                settings.SecurityBuffer = request.SecurityBuffer;
            }

            await db.SaveChangesAsync();

            return Results.Ok(new AppSettingsDto { SecurityBuffer = settings.SecurityBuffer });
        })
        .WithName("UpdateAppSettings");
    }
}

public record AppSettingsDto
{
    public decimal SecurityBuffer { get; set; }
}

public record UpdateAppSettingsRequest
{
    public decimal SecurityBuffer { get; set; }
}
