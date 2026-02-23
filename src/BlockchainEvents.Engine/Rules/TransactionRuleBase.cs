namespace BlockchainEvents.Engine.Rules;

public abstract class TransactionRuleBase : ITransactionRule
{
    public abstract string Id { get; }

    public abstract string Name { get; }

    public abstract string Description { get; }

    public virtual bool IsEnabled => true;

    public abstract bool IsMatch(TransactionData transaction, RuleContext context);

    public abstract RuleMatchResult Evaluate(TransactionData transaction, RuleContext context);
}
