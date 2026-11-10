using System.Text.Json;
using Amazon.DynamoDBv2.Model;
using TaskManager.Tasks.Application.Ports;

namespace TaskManager.Tasks.Infrastructure.Persistence;

/// <summary>Opaque paging cursor: base64url (no padding) of the JSON of a DynamoDB last-evaluated key.</summary>
internal static class TaskCursor
{
    private const int MaxLength = 1024;
    private const string UserIdKey = "userId";
    private const string IdKey = "_id";
    private static readonly string[] Keys = [UserIdKey, IdKey, TaskItemMapper.CreatedAtIdAttribute];

    public static string Encode(IReadOnlyDictionary<string, AttributeValue> lastKey)
    {
        var values = Keys.ToDictionary(k => k, k => lastKey[k].S);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(values);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static Dictionary<string, AttributeValue> Decode(string cursor, Guid userId)
    {
        if (cursor.Length > MaxLength)
        {
            throw new InvalidCursorException("Cursor is too long.");
        }

        Dictionary<string, string>? values;
        try
        {
            var base64 = cursor.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + ((4 - (base64.Length % 4)) % 4), '=');
            values = JsonSerializer.Deserialize<Dictionary<string, string>>(Convert.FromBase64String(base64));
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
        {
            throw new InvalidCursorException("Cursor is malformed.");
        }

        if (values is null
            || values.Count != Keys.Length
            || !Keys.All(values.ContainsKey)
            || values[UserIdKey] != userId.ToString("D"))
        {
            throw new InvalidCursorException("Cursor is invalid.");
        }

        return Keys.ToDictionary(k => k, k => new AttributeValue { S = values[k] });
    }
}
