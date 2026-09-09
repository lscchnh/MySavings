using MySavings.Web.ApiClients;

namespace MySavings.Web.Helpers;

/// <summary>
/// Shared owner id -> display name lookup, used by pages that show an account's owner name.
/// </summary>
public static class OwnerNameLookup
{
    public static Dictionary<int, string> BuildMap(IEnumerable<PersonSettingsDto> owners) =>
        owners.ToDictionary(o => o.Id, o => o.Name);

    public static string GetName(Dictionary<int, string> map, int? ownerId) =>
        ownerId.HasValue && map.TryGetValue(ownerId.Value, out var name) ? name : "";
}
