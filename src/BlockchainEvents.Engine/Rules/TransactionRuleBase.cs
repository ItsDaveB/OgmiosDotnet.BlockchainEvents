namespace BlockchainEvents.Engine.Rules;

public abstract class TransactionRuleBase : ITransactionRule
{
    public abstract string Id { get; }

    public abstract string Name { get; }

    public abstract string Description { get; }

    public virtual bool IsEnabled { get; set; } = true;

    public abstract bool IsMatch(TransactionData transaction, RuleContext context);

    public abstract RuleMatchResult Evaluate(TransactionData transaction, RuleContext context);

    protected RuleMatchResult CreateMatchResult(
        TransactionData transaction,
        Dictionary<string, string> matchedCriteria)
    {
        return RuleMatchResult.Create(this, transaction, matchedCriteria);
    }
}
