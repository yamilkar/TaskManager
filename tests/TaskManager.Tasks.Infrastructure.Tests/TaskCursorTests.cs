using System.Text;
using Amazon.DynamoDBv2.Model;
using TaskManager.Tasks.Application.Ports;
using TaskManager.Tasks.Infrastructure.Persistence;

namespace TaskManager.Tasks.Infrastructure.Tests;

public sealed class TaskCursorTests
{
    private static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static string Base64Url(string json) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static Dictionary<string, AttributeValue> Key(Guid userId) => new()
    {
        ["userId"] = new AttributeValue { S = userId.ToString("D") },
        ["_id"] = new AttributeValue { S = "id-1" },
        ["createdAtId"] = new AttributeValue { S = "2026-11-10T08:00:00.0000000Z#id-1" },
    };

    [Fact]
    public void EncodeDecode_validKey_roundTrips()
    {
        var cursor = TaskCursor.Encode(Key(UserId));

        cursor.ShouldNotContain("=");
        cursor.ShouldNotContain("+");
        cursor.ShouldNotContain("/");
        var decoded = TaskCursor.Decode(cursor, UserId);
        decoded.Keys.OrderBy(k => k).ShouldBe(["_id", "createdAtId", "userId"]);
        decoded["userId"].S.ShouldBe(UserId.ToString("D"));
        decoded["_id"].S.ShouldBe("id-1");
        decoded["createdAtId"].S.ShouldBe("2026-11-10T08:00:00.0000000Z#id-1");
    }

    [Fact]
    public void Decode_otherUsersCursor_throwsInvalidCursor()
    {
        var cursor = TaskCursor.Encode(Key(Guid.NewGuid()));

        Should.Throw<InvalidCursorException>(() => TaskCursor.Decode(cursor, UserId));
    }

    [Fact]
    public void Decode_notBase64_throwsInvalidCursor()
    {
        Should.Throw<InvalidCursorException>(() => TaskCursor.Decode("%%%", UserId));
    }

    [Theory]
    [InlineData("""{"userId":1,"_id":"x","createdAtId":"y"}""")]
    [InlineData("""{"userId":"22222222-2222-2222-2222-222222222222","_id":"x"}""")]
    [InlineData("""{"userId":"22222222-2222-2222-2222-222222222222","_id":"x","createdAtId":"y","extra":"z"}""")]
    public void Decode_wrongJsonShape_throwsInvalidCursor(string json)
    {
        Should.Throw<InvalidCursorException>(() => TaskCursor.Decode(Base64Url(json), UserId));
    }

    [Fact]
    public void Decode_over1024Chars_throwsInvalidCursorWithoutDecoding()
    {
        var ex = Should.Throw<InvalidCursorException>(() => TaskCursor.Decode(new string('A', 1025), UserId));

        ex.InnerException.ShouldBeNull();
    }
}
