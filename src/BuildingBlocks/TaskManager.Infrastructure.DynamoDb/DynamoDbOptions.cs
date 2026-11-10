namespace TaskManager.Infrastructure.DynamoDb;

public sealed class DynamoDbOptions
{
    public const string SectionName = "Dynamo";

    public string? ServiceUrl { get; set; }
    public string Region { get; set; } = "us-east-1";
    public string TableName { get; set; } = "";
    public bool CreateTablesIfMissing { get; set; }
}
