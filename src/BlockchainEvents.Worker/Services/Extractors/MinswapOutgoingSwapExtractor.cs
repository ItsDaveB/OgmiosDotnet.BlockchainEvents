using System.Globalization;
using System.Text;
using BlockchainEvents.Worker.Services.Minswap;

namespace BlockchainEvents.Worker.Services.Extractors;

/// <summary>
/// Extracts Minswap V2 outgoing swap display fields from order UTxOs.
/// Address detection uses the known Minswap V2 order bech32 prefixes (same as AddressMatchRule demo).
/// Token legs are inferred from the order UTxO when no pool registry is available.
/// </summary>
public sealed class MinswapOutgoingSwapExtractor
{
    public static readonly MinswapOutgoingSwapExtractor Instance = new();

    public const string OrderAddressMarker = "8p79rpkcdz8x9d6tft0x0dx5mwuzac2sa4gm8cvkw5hc";

    private static readonly string[] OrderPrefixes =
    [
        "addr1z8p79rpkcdz8x9d6tft0x0dx5mwuzac2sa4gm8cvkw5hc",
        "addr1x8p79rpkcdz8x9d6tft0x0dx5mwuzac2sa4gm8cvkw5hc",
        "addr1w8p79rpkcdz8x9d6tft0x0dx5mwuzac2sa4gm8cvkw5hc"
    ];

    public MinswapSwapDetails? Extract(Transaction tx)
    {
        if (!tx.Outputs.IsNotNullOrUndefined()) return null;

        foreach (var output in tx.Outputs)
        {
            if (!output.Address.IsNotNullOrUndefined()) continue;
            var address = (string)output.Address.AsString;
            if (!IsMinswapV2OrderAddress(address)) continue;

            var (datumHex, source) = ResolveDatumHex(output, tx);
            if (string.IsNullOrEmpty(datumHex)) continue;

            byte[] bytes;
            try { bytes = Convert.FromHexString(datumHex); }
            catch { continue; }

            var order = MinswapV2OrderCbor.TryParse(bytes);
            if (order is null) continue;

            var assets = ReadOutputAssets(output);
            return BuildDetails(order, assets, source);
        }

        return null;
    }

    public static bool IsMinswapV2OrderAddress(string address) =>
        OrderPrefixes.Any(p => address.StartsWith(p, StringComparison.OrdinalIgnoreCase))
        || address.Contains(OrderAddressMarker, StringComparison.OrdinalIgnoreCase);

    private static (string? Hex, string Source) ResolveDatumHex(TransactionOutput output, Transaction tx)
    {
        if (output.Datum.IsNotNullOrUndefined())
            return ((string)output.Datum, "inline");

        if (output.DatumHash.IsNotNullOrUndefined() && tx.Datums.IsNotNullOrUndefined())
        {
            var hash = (string)output.DatumHash;
            try
            {
                var found = tx.Datums.FirstOrDefault(x => x.Key == hash).Value;
                if (!found.IsNullOrUndefined())
                    return ((string)found, "witness-set");
            }
            catch { /* ignore */ }
        }

        return (null, "unknown");
    }

    private static List<(string PolicyId, string AssetName, string Amount)> ReadOutputAssets(TransactionOutput output)
    {
        var items = new List<(string, string, string)>();
        try
        {
            var lovelace = output.Value.Ada.Lovelace.AsNumber.ToString();
            if (!string.IsNullOrEmpty(lovelace))
                items.Add(("lovelace", "", lovelace));
        }
        catch { /* no ADA */ }

        try
        {
            foreach (var property in output.Value.EnumerateObject())
            {
                var policyId = (string)property.Name;
                if (policyId == "ada") continue;
                try
                {
                    foreach (var asset in property.Value.AsObject.EnumerateObject())
                    {
                        items.Add((policyId, (string)asset.Name, asset.Value.AsNumber.ToString()));
                    }
                }
                catch { /* skip */ }
            }
        }
        catch { /* skip */ }

        return items;
    }

    private static MinswapSwapDetails BuildDetails(
        MinswapV2OrderCbor.ParsedOrder order,
        List<(string PolicyId, string AssetName, string Amount)> assets,
        string datumSource)
    {
        var ada = assets.FirstOrDefault(a => a.PolicyId == "lovelace");
        var nonAda = assets.FirstOrDefault(a =>
            a.PolicyId != "lovelace"
            && !string.Equals(a.PolicyId, order.LpTokenPolicyId, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrEmpty(a.Amount));

        var totalAda = long.TryParse(ada.Amount, out var adaVal) ? adaVal : 0L;
        var batcherFee = long.TryParse(order.BatcherFee, out var bf) ? bf : 2_000_000L;
        const long deposit = 2_000_000L;

        string inSubject, outSubject, inTicker, outTicker;
        int inDecimals, outDecimals;
        string amountIn = order.SwapInAmount;

        var adaBuy = string.IsNullOrEmpty(nonAda.PolicyId) && totalAda > 6_000_000L;

        if (adaBuy || (order.SwapDirection == "0" && string.IsNullOrEmpty(nonAda.PolicyId)))
        {
            // ADA → ???
            inSubject = "";
            inTicker = "ADA";
            inDecimals = 6;
            outSubject = "unknown";
            outTicker = "TOKEN";
            outDecimals = 6;
            if (string.IsNullOrEmpty(amountIn) || amountIn == "0")
                amountIn = Math.Max(totalAda - batcherFee - deposit, 0).ToString();
        }
        else
        {
            // Token → ??? (often ADA, but may be token↔token)
            inSubject = nonAda.PolicyId + nonAda.AssetName;
            inTicker = DecodeTicker(nonAda.AssetName, fallback: "TOKEN");
            inDecimals = GuessDecimals(inTicker);
            outSubject = "";
            outTicker = "ADA";
            outDecimals = 6;
            if (string.IsNullOrEmpty(amountIn) || amountIn == "0")
                amountIn = nonAda.Amount ?? "0";
        }

        // If we have a non-ADA asset AND direction A→B with significant ADA, prefer ADA-in
        if (order.SwapDirection == "0" && totalAda > 6_000_000L)
        {
            inSubject = "";
            inTicker = "ADA";
            inDecimals = 6;
            if (!string.IsNullOrEmpty(nonAda.PolicyId))
            {
                // UTxO may still hold only fees — out token unknown without pool registry
                outSubject = "unknown";
                outTicker = "TOKEN";
                outDecimals = 6;
            }
            if (string.IsNullOrEmpty(order.SwapInAmount) || order.SwapInAmount == "0")
                amountIn = Math.Max(totalAda - batcherFee - deposit, 0).ToString();
            else
                amountIn = order.SwapInAmount;
        }

        var direction = DetermineDirection(inTicker, outTicker);
        var lpSubject = string.IsNullOrEmpty(order.LpTokenPolicyId)
            ? null
            : order.LpTokenPolicyId + order.LpTokenAssetName;

        return new MinswapSwapDetails
        {
            Dex = "Minswap V2",
            Direction = direction,
            OrderType = order.OrderTypeName,
            SwapInTicker = inTicker,
            SwapOutTicker = outTicker,
            SwapInSubject = inSubject,
            SwapOutSubject = outSubject,
            AmountInRaw = amountIn,
            MinReceiveRaw = order.MinReceive,
            AmountInDisplay = FormatAmount(amountIn, inDecimals),
            MinReceiveDisplay = FormatAmount(order.MinReceive, outDecimals),
            SwapInDecimals = inDecimals,
            SwapOutDecimals = outDecimals,
            BatcherFeeAda = (batcherFee / 1_000_000m).ToString("0.###", CultureInfo.InvariantCulture),
            LpTokenSubject = lpSubject,
            DatumSource = datumSource
        };
    }

    private static string DetermineDirection(string inTicker, string outTicker)
    {
        if (inTicker.Equals("ADA", StringComparison.OrdinalIgnoreCase)) return "BUY";
        if (outTicker.Equals("ADA", StringComparison.OrdinalIgnoreCase)) return "SELL";
        return "SWAP";
    }

    private static string DecodeTicker(string hexAssetName, string fallback)
    {
        if (string.IsNullOrEmpty(hexAssetName)) return fallback;
        try
        {
            var bytes = Convert.FromHexString(hexAssetName);
            // CIP-67: skip 4-byte label prefix when present
            var start = bytes.Length > 4 && bytes[0] == 0x00 ? 4 : 0;
            var ascii = Encoding.ASCII.GetString(bytes, start, bytes.Length - start);
            if (ascii.Length > 0 && ascii.All(c => c is >= (char)32 and < (char)127))
                return ascii;
        }
        catch { /* ignore */ }

        return hexAssetName.Length <= 8 ? hexAssetName.ToUpperInvariant() : hexAssetName[..8].ToUpperInvariant();
    }

    private static int GuessDecimals(string ticker) =>
        ticker.Equals("ADA", StringComparison.OrdinalIgnoreCase) ? 6 :
        ticker.Equals("USDC", StringComparison.OrdinalIgnoreCase) || ticker.Contains("USD", StringComparison.OrdinalIgnoreCase) ? 6 :
        6;

    private static string FormatAmount(string raw, int decimals)
    {
        if (!decimal.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            return raw;
        var scaled = value / (decimal)Math.Pow(10, decimals);
        return scaled switch
        {
            >= 1_000_000 => scaled.ToString("0.##M", CultureInfo.InvariantCulture),
            >= 1_000 => scaled.ToString("0.##K", CultureInfo.InvariantCulture),
            _ => scaled.ToString("0.####", CultureInfo.InvariantCulture)
        };
    }
}
