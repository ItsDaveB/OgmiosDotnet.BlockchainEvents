namespace BlockchainEvents.Worker.Services.Extractors;

public readonly record struct CertificateFlags(
    bool HasGovernance,
    bool HasStakeDelegation,
    bool HasStakeRegistration);

public sealed class CertificateExtractor : ITransactionExtractor<CertificateFlags>
{
    public static readonly CertificateExtractor Instance = new();

    public CertificateFlags Extract(Transaction tx)
    {
        if (!tx.Certificates.IsNotNullOrUndefined())
            return default;

        bool governance = false, delegation = false, registration = false;

        foreach (var cert in tx.Certificates)
        {
            if (cert.IsStakeDelegation || cert.IsConstitutionalCommitteeDelegation)
                delegation = true;
            if (cert.IsStakeCredentialRegistration || cert.IsStakeCredentialDeregistration)
                registration = true;
            if (cert.IsDelegateRepresentativeRegistration || cert.IsDelegateRepresentativeRetirement)
                governance = true;
        }

        return new CertificateFlags(governance, delegation, registration);
    }
}
