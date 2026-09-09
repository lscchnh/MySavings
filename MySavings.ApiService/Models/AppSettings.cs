namespace MySavings.ApiService.Models;

/// <summary>
/// Singleton row (Id always 1) holding global application settings.
/// </summary>
public class AppSettings
{
    public int Id { get; set; }

    /// <summary>
    /// Amount kept on the current account, not counted as savings, when computing
    /// monthly expenses from the "end of month before salary" balance.
    /// </summary>
    public decimal SecurityBuffer { get; set; } = 500m;
}
