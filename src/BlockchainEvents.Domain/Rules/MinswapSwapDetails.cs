namespace BlockchainEvents.Domain.Rules;

/// <summary>
/// Minimal Minswap V2 outgoing swap display model (CBOR datum + UTxO inference).
/// </summary>
public sealed class MinswapSwapDetails
{
    public required string Dex { get; init; }
    public required string Direction { get; init; } // BUY | SELL | SWAP
    public required string OrderType { get; init; } // SwapExactIn, etc.
    public required string SwapInTicker { get; init; }
    public required string SwapOutTicker { get; init; }
    public required string SwapInSubject { get; init; }
    public required string SwapOutSubject { get; init; }
    public required string AmountInRaw { get; init; }
    public required string MinReceiveRaw { get; init; }
    public required string AmountInDisplay { get; init; }
    public required string MinReceiveDisplay { get; init; }
    public int SwapInDecimals { get; init; }
    public int SwapOutDecimals { get; init; }
    public string? BatcherFeeAda { get; init; }
    public string? LpTokenSubject { get; init; }
    public string DatumSource { get; init; } = "unknown";
}
