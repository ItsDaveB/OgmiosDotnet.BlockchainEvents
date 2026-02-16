namespace BlockchainEvents.Domain.Rules;

public interface ITransactionRule
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    bool IsEnabled { get; }

    /// <summary>Fast filter check before detailed evaluation.</summary>
    bool IsMatch(TransactionData transaction, RuleContext context);

    /// <summary>Detailed evaluation after IsMatch returns true.</summary>
    RuleMatchResult Evaluate(TransactionData transaction, RuleContext context);
}
