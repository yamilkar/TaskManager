using Amazon.CDK;
using Amazon.CDK.AWS.DynamoDB;
using Constructs;
using Attribute = Amazon.CDK.AWS.DynamoDB.Attribute;

namespace TaskManager.Infra;

/// <summary>
/// The two DynamoDB tables, with the keys the repositories expect (UsersTableDefinition, TasksTableDefinition).
/// Provisioned at 5 RCU / 5 WCU each: 10 + 10 stays inside DynamoDB's always-free 25 / 25.
/// </summary>
public sealed class DataConstruct : Construct
{
    private const int FreeTierCapacityPerTable = 5;

    public DataConstruct(Construct scope, string id) : base(scope, id)
    {
        UsersTable = NewTable("Users", "TaskManager-Users", new Attribute { Name = "pk", Type = AttributeType.STRING });
        TasksTable = NewTable(
            "Tasks",
            "TaskManager-Tasks",
            new Attribute { Name = "userId", Type = AttributeType.STRING },
            new Attribute { Name = "_id", Type = AttributeType.STRING });
    }

    public ITable UsersTable { get; }

    public ITable TasksTable { get; }

    private Table NewTable(string id, string name, Attribute partitionKey, Attribute? sortKey = null) =>
        new(this, id, new TableProps
        {
            TableName = name,
            PartitionKey = partitionKey,
            SortKey = sortKey,
            BillingMode = BillingMode.PROVISIONED,
            ReadCapacity = FreeTierCapacityPerTable,
            WriteCapacity = FreeTierCapacityPerTable,
            RemovalPolicy = RemovalPolicy.DESTROY,
        });
}
