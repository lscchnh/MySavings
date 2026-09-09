namespace MySavings.Web.ApiClients;

public class ImportApiClient(HttpClient httpClient)
{
    private readonly HttpClient _httpClient = httpClient;

    public async Task<ImportResultDto?> ImportExcelAsync(Stream fileStream, string fileName)
    {
        using var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(streamContent, "file", fileName);

        var response = await _httpClient.PostAsync("/api/import/excel", content);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<ImportResultDto>();
        }

        return null;
    }

    public async Task<(byte[] Content, string FileName)?> ExportExcelAsync()
    {
        var response = await _httpClient.GetAsync("/api/import/excel/export");
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var content = await response.Content.ReadAsByteArrayAsync();
        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName
            ?? "mysavings-export.xlsx";

        return (content, fileName.Trim('"'));
    }
}

public class ImportResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int Person1Entries { get; set; }
    public int Person2Entries { get; set; }
    public int AllocationRulesImported { get; set; }
}
