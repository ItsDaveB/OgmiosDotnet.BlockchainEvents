namespace BlockchainEvents.Engine.Rules;

public sealed class AllTransactionsRule : TransactionRuleBase
{
    public override string Id => "all-transactions";
    public override string Name => "All Transactions";
    public override string Description => "Matches all transactions for testing or full capture";

    public override bool IsMatch(TransactionData transaction, RuleContext context) => true;

    public override RuleMatchResult Evaluate(TransactionData transaction, RuleContext context)
    {
        return new RuleMatchResult(Id, Name, new Dictionary<string, object>
        {
            ["transaction_id"] = transaction.Id,
            ["input_count"] = transaction.InputAddresses.Count,
            ["output_count"] = transaction.OutputAddresses.Count
        });
    }
}
