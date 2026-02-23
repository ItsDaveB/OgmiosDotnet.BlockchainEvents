namespace BlockchainEvents.Domain.Rules;

public sealed record RuleMatchResult(
    string RuleId,
    string RuleName,
    IReadOnlyDictionary<string, object> MatchedCriteria
);
