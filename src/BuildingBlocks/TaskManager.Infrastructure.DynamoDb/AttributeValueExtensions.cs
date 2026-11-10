using System.Globalization;
using Amazon.DynamoDBv2.Model;

namespace TaskManager.Infrastructure.DynamoDb;

public static class AttributeValueExtensions
{
    public static AttributeValue ToAttr(this string value) => new() { S = value };

    public static AttributeValue ToAttr(this Guid value) => new() { S = value.ToString("D") };

    public static AttributeValue ToAttr(this DateTimeOffset value) =>
        new() { S = value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) };

    public static string GetString(this IReadOnlyDictionary<string, AttributeValue> item, string name) =>
        item.TryGetValue(name, out var v) && v.S is not null
            ? v.S
            : throw new InvalidOperationException($"Attribute '{name}' is missing or not a string.");

    public static string? GetStringOrNull(this IReadOnlyDictionary<string, AttributeValue> item, string name) =>
        item.TryGetValue(name, out var v) ? v.S : null;

    public static Guid GetGuid(this IReadOnlyDictionary<string, AttributeValue> item, string name) =>
        Guid.Parse(item.GetString(name));

    public static DateTimeOffset GetDateTimeOffset(this IReadOnlyDictionary<string, AttributeValue> item, string name) =>
        DateTimeOffset.Parse(item.GetString(name), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
