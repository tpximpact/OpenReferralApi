using System.Text.Json;

namespace OpenReferralApi.Core.Extensions;

internal static class JsonElementExtensions
{
  internal static string? TryGetPathString(this JsonElement root, string path)
  {
    var current = root;
    foreach (var segment in path.Split('.'))
    {
      if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
      {
        return null;
      }
    }

    return current.ValueKind switch
    {
      JsonValueKind.String => current.GetString(),
      JsonValueKind.Number => current.GetRawText(),
      JsonValueKind.True => bool.TrueString,
      JsonValueKind.False => bool.FalseString,
      _ => null
    };
  }
}