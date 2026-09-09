using System.Net;
using System.Net.Http.Json;
using MySavings.ApiService.Services;
using MySavings.Tests.Helpers;

namespace MySavings.Tests.Endpoints;

public class ImportEndpointsTests : IDisposable
{
    private readonly ApiWebApplicationFactory _factory = new();
    private readonly HttpClient _client;

    public ImportEndpointsTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose() => _factory.Dispose();

    private static MultipartFormDataContent BuildFileContent(Stream stream, string fileName)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "file", fileName);
        return content;
    }

    [Fact]
    public async Task ImportExcel_WrongExtension_ReturnsBadRequest()
    {
        using var stream = new MemoryStream([1, 2, 3]);
        using var content = BuildFileContent(stream, "data.txt");

        var response = await _client.PostAsync("/api/import/excel", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Excel file", body);
    }

    [Fact]
    public async Task ImportExcel_ValidWorkbook_ReturnsSuccessAndPersistsEntry()
    {
        using var package = ExcelTestHelper.CreateImportWorkbook(new DateTime(2024, 1, 1));
        using var stream = ExcelTestHelper.ToStream(package);
        using var content = BuildFileContent(stream, "import.xlsx");

        var response = await _client.PostAsync("/api/import/excel", content);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ImportResult>();
        Assert.NotNull(result);
        Assert.True(result!.Success);
        Assert.Equal(1, result.Person1Entries);
        Assert.Equal(1, result.Person2Entries);

        await using var verifyCtx = _factory.CreateDbContext();
        var entry = Assert.Single(verifyCtx.MonthlyEntries);
        Assert.Equal(new DateTime(2024, 1, 1), entry.Month);
    }

    [Fact]
    public async Task ExportExcel_ReturnsXlsxFile()
    {
        var response = await _client.GetAsync("/api/import/excel/export");
        response.EnsureSuccessStatusCode();

        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", response.Content.Headers.ContentType?.MediaType);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.NotEmpty(bytes);
    }
}
