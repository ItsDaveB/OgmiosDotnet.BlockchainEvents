namespace BlockchainEvents.Domain.Configuration;

public sealed class OgmiosConfiguration
{
    public const string SectionName = "Ogmios";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1337;
    public bool Tls { get; set; } = false;
    public List<StartingPoint>? StartingPoints { get; set; }
}

public sealed class StartingPoint
{
    public string Id { get; set; } = string.Empty;
    public long Slot { get; set; }
}
