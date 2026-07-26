using PeterO.Cbor;

namespace BlockchainEvents.Worker.Services.Minswap;

/// <summary>
/// Minimal Minswap V2 order-datum CBOR parser (SwapExactIn-focused).
/// Adapted from cardano-arbitrage-v2 MinswapV2SwapFromCbor — only fields needed for UI display.
/// </summary>
public static class MinswapV2OrderCbor
{
    public sealed class ParsedOrder
    {
        public string LpTokenPolicyId { get; init; } = "";
        public string LpTokenAssetName { get; init; } = "";
        /// <summary>"0" = A→B, "1" = B→A</summary>
        public string SwapDirection { get; init; } = "0";
        public string SwapInAmount { get; init; } = "0";
        public string MinReceive { get; init; } = "0";
        public string BatcherFee { get; init; } = "0";
        public int OrderStepTag { get; init; } = 121;
        public string OrderTypeName { get; init; } = "SwapExactIn";
    }

    public static ParsedOrder? TryParse(byte[] cborBytes)
    {
        try
        {
            var cbor = CBORObject.DecodeFromBytes(cborBytes);
            if (cbor is null || cbor.Type != CBORType.Array || cbor.Count < 6)
                return null;

            return cbor.Count >= 8 ? ParseStandard(cbor) : ParseSimplified(cbor);
        }
        catch
        {
            return null;
        }
    }

    private static ParsedOrder ParseStandard(CBORObject cbor)
    {
        var orderStep = cbor[6];
        var tag = orderStep?.MostOuterTag.ToInt32Checked() ?? 121;
        if (tag < 121) tag = 121;
        var ctor = NormalizeCtor(tag);
        var (amount, minRecv) = ParseAmounts(orderStep, ctor);

        return new ParsedOrder
        {
            LpTokenPolicyId = GetHex(cbor, 5, 0),
            LpTokenAssetName = GetHex(cbor, 5, 1),
            SwapDirection = GetDirection(orderStep),
            SwapInAmount = amount,
            MinReceive = minRecv,
            BatcherFee = GetNumber(cbor, 7),
            OrderStepTag = tag,
            OrderTypeName = OrderTypeName(ctor)
        };
    }

    private static ParsedOrder ParseSimplified(CBORObject cbor)
    {
        var orderStep = cbor[3];
        var tag = orderStep?.MostOuterTag.ToInt32Checked() ?? 121;
        if (tag < 121) tag = 121;
        var ctor = NormalizeCtor(tag);
        var (amount, minRecv) = ParseAmounts(orderStep, ctor);

        return new ParsedOrder
        {
            SwapDirection = GetDirection(orderStep),
            SwapInAmount = amount,
            MinReceive = minRecv,
            BatcherFee = GetNumber(cbor, 4),
            OrderStepTag = tag,
            OrderTypeName = OrderTypeName(ctor)
        };
    }

    private static (string amount, string minRecv) ParseAmounts(CBORObject? body, int ctor) =>
        ctor switch
        {
            0 => ParseSwapExactIn(body),                 // SwapExactIn
            1 => ParseIndexedOption(body, 3, 4),         // StopLoss
            2 => ParseIndexedOption(body, 5, 6),         // OCO
            3 => ParseSwapExactIn(body),                 // SwapExactOut
            _ => ("0", "0")
        };

    private static (string, string) ParseSwapExactIn(CBORObject? body)
    {
        if (body is null || body.Count < 3) return ("0", "0");
        return (ExtractOptionNumber(body[1]), ExtractNumber(body[2]));
    }

    private static (string, string) ParseIndexedOption(CBORObject? body, int amountIdx, int minIdx)
    {
        if (body is null || body.Count <= minIdx) return ("0", "0");
        return (ExtractOptionNumber(body[amountIdx]), ExtractNumber(body[minIdx]));
    }

    private static string GetDirection(CBORObject? orderStep)
    {
        if (orderStep is null || orderStep.Count == 0) return "0";
        var direction = orderStep[0];
        if (direction is null) return "0";
        if (direction.IsTagged)
        {
            var tag = direction.MostOuterTag.ToInt32Checked();
            if (tag >= 121)
                return (tag - 121) == 0 ? "1" : "0"; // ctor 0 = B→A
        }
        if (direction.IsNumber || direction.Type == CBORType.Integer)
            return direction.AsNumber().ToString();
        return "0";
    }

    private static string ExtractOptionNumber(CBORObject? option)
    {
        if (option is null) return "0";
        if (option.IsTagged)
        {
            var tag = option.MostOuterTag.ToInt32Checked();
            var inner = option.Untag();
            if (tag == 121 && inner?.Type == CBORType.Array && inner.Count > 0)
                return ExtractNumber(inner[0]);
            return "0";
        }
        if (option.Type == CBORType.Array && option.Count > 0)
            return ExtractNumber(option[0]);
        return ExtractNumber(option);
    }

    private static string ExtractNumber(CBORObject? obj)
    {
        if (obj is null) return "0";
        if (obj.IsNumber || obj.Type == CBORType.Integer)
            return obj.AsNumber().ToString();
        if (obj.IsTagged)
        {
            var untagged = obj.Untag();
            if (untagged?.IsNumber == true || untagged?.Type == CBORType.Integer)
                return untagged.AsNumber().ToString();
            if (untagged?.Type == CBORType.Array && untagged.Count > 0)
                return ExtractNumber(untagged[0]);
        }
        if (obj.Type == CBORType.Array && obj.Count > 0)
            return ExtractNumber(obj[0]);
        return "0";
    }

    private static string GetHex(CBORObject cbor, params int[] indices)
    {
        var el = GetElement(cbor, indices);
        return el?.Type == CBORType.ByteString
            ? Convert.ToHexString(el.GetByteString()).ToLowerInvariant()
            : "";
    }

    private static string GetNumber(CBORObject cbor, params int[] indices)
    {
        var el = GetElement(cbor, indices);
        if (el is null) return "0";
        if (el.IsNumber || el.Type == CBORType.Integer)
            return el.AsNumber().ToString();
        return "0";
    }

    private static CBORObject? GetElement(CBORObject cbor, params int[] indices)
    {
        foreach (var index in indices)
        {
            if (cbor.IsTagged) cbor = cbor.Untag();
            if (cbor?.Type != CBORType.Array || cbor.Count <= index) return null;
            cbor = cbor[index];
        }
        return cbor;
    }

    private static int NormalizeCtor(int tag)
    {
        if (tag is >= 121 and <= 127) return tag - 121;
        if (tag >= 1280) return tag - 1280 + 7;
        return -1;
    }

    private static string OrderTypeName(int ctor) => ctor switch
    {
        0 => "SwapExactIn",
        1 => "StopLoss",
        2 => "OCO",
        3 => "SwapExactOut",
        4 => "Deposit",
        5 => "Withdraw",
        6 => "ZapOut",
        7 => "PartialSwap",
        9 => "MultiRouting",
        _ => "Unknown"
    };
}
