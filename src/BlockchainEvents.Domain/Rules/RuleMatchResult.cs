namespace BlockchainEvents.Domain.Rules;

public sealed record RuleMatchResult(
    string RuleId,
    string RuleName,
    IReadOnlyDictionary<string, object> MatchedCriteria
)
{
    public static RuleMatchResult Create(
        ITransactionRule rule,
        IReadOnlyDictionary<string, object> matchedCriteria)
    {
        return new RuleMatchResult(
            rule.Id,
            rule.Name,
            matchedCriteria
        );
    }

    public static RuleMatchResult Create(
        ITransactionRule rule,
        TransactionData transaction,
        IReadOnlyDictionary<string, string> matchedCriteria)
    {
        var criteria = matchedCriteria.ToDictionary(
            kvp => kvp.Key,
            kvp => (object)kvp.Value);
        return new RuleMatchResult(
            rule.Id,
            rule.Name,
            criteria
        );
    }
}
