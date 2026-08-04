namespace BlockchainEvents.Worker.Services.Extractors;

public readonly record struct GovernanceFlags(
    bool HasProposals,
    bool HasVotes,
    bool HasTreasuryWithdrawal);

public sealed class GovernanceExtractor : ITransactionExtractor<GovernanceFlags>
{
    public static readonly GovernanceExtractor Instance = new();

    public GovernanceFlags Extract(Transaction tx)
    {
        var hasProposals = tx.Proposals.IsNotNullOrUndefined() && tx.Proposals.Any();
        var hasVotes = tx.Votes.IsNotNullOrUndefined() && tx.Votes.Any();
        var hasTreasuryWithdrawal = hasProposals && tx.Proposals.Any(proposal =>
            proposal.Action.IsNotNullOrUndefined() &&
            proposal.Action.IsGovernanceActionTreasuryWithdrawals);

        return new GovernanceFlags(hasProposals, hasVotes, hasTreasuryWithdrawal);
    }
}
