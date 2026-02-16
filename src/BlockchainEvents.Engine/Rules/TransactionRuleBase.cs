namespace BlockchainEvents.Engine.Rules;

public abstract class TransactionRuleBase : ITransactionRule
{
    /// <inheritdoc />
    public abstract string Id { get; }

    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public abstract string Description { get; }

    /// <inheritdoc />
    public virtual bool IsEnabled { get; set; } = true;

    /// <inheritdoc />
    public abstract bool IsMatch(TransactionData transaction, RuleContext context);

    /// <inheritdoc />
    public abstract RuleMatchResult Evaluate(TransactionData transaction, RuleContext context);

    protected RuleMatchResult CreateMatchResult(
        TransactionData transaction,
        Dictionary<string, string> matchedCriteria)
    {
        return RuleMatchResult.Create(this, transaction, matchedCriteria);
    }
}
