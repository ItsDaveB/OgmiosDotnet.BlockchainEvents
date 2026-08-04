using BlockchainEvents.Worker.Services.Extractors;
using Generated;

namespace BlockchainEvents.Tests.Services;

public class GovernanceExtractorTests
{
    [Fact]
    public void Extract_WithTreasuryWithdrawalsProposal_SetsHasTreasuryWithdrawal()
    {
        var tx = Transaction.Parse("""
            {
              "id": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
              "spends": "inputs",
              "inputs": [],
              "outputs": [],
              "signatories": [],
              "proposals": [
                {
                  "deposit": { "ada": { "lovelace": 100000000000 } },
                  "returnAccount": "stake1u9xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
                  "action": {
                    "type": "treasuryWithdrawals",
                    "withdrawals": {},
                    "guardrails": null
                  }
                }
              ]
            }
            """);

        var flags = GovernanceExtractor.Instance.Extract(tx);

        flags.HasProposals.Should().BeTrue();
        flags.HasTreasuryWithdrawal.Should().BeTrue();
        flags.HasVotes.Should().BeFalse();
    }

    [Fact]
    public void Extract_WithInfoProposal_DoesNotSetHasTreasuryWithdrawal()
    {
        var tx = Transaction.Parse("""
            {
              "id": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
              "spends": "inputs",
              "inputs": [],
              "outputs": [],
              "signatories": [],
              "proposals": [
                {
                  "deposit": { "ada": { "lovelace": 100000000000 } },
                  "returnAccount": "stake1u9xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
                  "action": {
                    "type": "information"
                  }
                }
              ]
            }
            """);

        var flags = GovernanceExtractor.Instance.Extract(tx);

        flags.HasProposals.Should().BeTrue();
        flags.HasTreasuryWithdrawal.Should().BeFalse();
    }

    [Fact]
    public void Extract_WithoutProposals_ReturnsFalseFlags()
    {
        var tx = Transaction.Parse("""
            {
              "id": "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
              "spends": "inputs",
              "inputs": [],
              "outputs": [],
              "signatories": []
            }
            """);

        var flags = GovernanceExtractor.Instance.Extract(tx);

        flags.HasProposals.Should().BeFalse();
        flags.HasTreasuryWithdrawal.Should().BeFalse();
        flags.HasVotes.Should().BeFalse();
    }
}
