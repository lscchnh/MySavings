using MySavings.ApiService.Services;

namespace MySavings.ApiService.Endpoints;

public static class ImportEndpoints
{
    public static void MapImportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/import")
            .WithTags("Import");

        // POST upload Excel file for import
        group.MapPost("/excel", async (IFormFile file, ExcelImportService importService) =>
        {
            if (file == null || file.Length == 0)
                return Results.BadRequest("No file provided");

            if (!file.FileName.EndsWith(".xlsx") && !file.FileName.EndsWith(".xls"))
                return Results.BadRequest("File must be an Excel file (.xlsx or .xls)");

            using var stream = file.OpenReadStream();
            var result = await importService.ImportFromExcelAsync(stream);

            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        })
        .WithName("ImportExcel")
        .DisableAntiforgery();

        group.MapGet("/excel/export", async (ExcelImportService importService) =>
        {
            var exportResult = await importService.ExportWorkbookAsync();
            if (!exportResult.IsSuccess || exportResult.Content is null)
                return Results.Problem(exportResult.ErrorMessage ?? "Unable to export Excel file", statusCode: StatusCodes.Status500InternalServerError);

            var fileName = $"mysavings-export-{DateTime.Now:yyyyMMdd-HHmm}.xlsx";
            return Results.File(exportResult.Content,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        })
        .WithName("ExportExcel");
    }
}
