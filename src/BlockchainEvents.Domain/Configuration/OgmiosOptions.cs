using Ogmios.Domain;

namespace BlockchainEvents.Domain.Configuration;

public sealed class OgmiosOptions
{
    public const string SectionName = "Ogmios";

    public ConnectionConfig Connection { get; set; } = new();
    public List<StartingPointConfiguration> StartingPoints { get; set; } = [];
}
