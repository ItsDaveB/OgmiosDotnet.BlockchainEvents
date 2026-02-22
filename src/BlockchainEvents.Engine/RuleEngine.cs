namespace BlockchainEvents.Engine;

public sealed record TransactionMatchResult(TransactionData Transaction, RuleMatchResult MatchResult);

public interface IRuleEngine
{
    int RuleCount { get; }
    int EnabledRuleCount { get; }
    IEnumerable<string> EnabledRuleNames { get; }
    IEnumerable<RuleMatchResult> MatchTransaction(TransactionData transaction, RuleContext context);
    IEnumerable<TransactionMatchResult> MatchTransactions(IEnumerable<TransactionData> transactions, RuleContext context);
}

public sealed class RuleEngine(IEnumerable<ITransactionRule> rules, ILogger<RuleEngine> logger) : IRuleEngine
{
    private readonly List<ITransactionRule> _rules = rules.ToList();

    public int RuleCount => _rules.Count;
    public int EnabledRuleCount => _rules.Count(r => r.IsEnabled);
    public IEnumerable<string> EnabledRuleNames => _rules.Where(r => r.IsEnabled).Select(r => r.Name);

    public IEnumerable<RuleMatchResult> MatchTransaction(TransactionData transaction, RuleContext context)
    {
        foreach (var rule in _rules.Where(r => r.IsEnabled))
        {
            RuleMatchResult? result = null;
            try
            {
                if (rule.IsMatch(transaction, context))
                    result = rule.Evaluate(transaction, context);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error evaluating rule {RuleName} for transaction {TransactionId}",
                    rule.Name, transaction.Id);
            }

            if (result is not null)
            {
                logger.LogDebug("Transaction {TransactionId} matched rule {RuleName}",
                    transaction.Id, rule.Name);
                yield return result;
            }
        }
    }

    public IEnumerable<TransactionMatchResult> MatchTransactions(
        IEnumerable<TransactionData> transactions,
        RuleContext context)
    {
        return transactions.SelectMany(tx =>
            MatchTransaction(tx, context).Select(result => new TransactionMatchResult(tx, result)));
    }
}
