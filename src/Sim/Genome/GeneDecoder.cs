using System;
using System.Collections.Generic;

namespace CreaturesReborn.Sim.Genome;

public static class GeneDecoder
{
    public static IReadOnlyList<GeneRecord> Decode(Genome genome)
        => DecodeRaw(genome.AsSpan());

    public static IReadOnlyList<GeneRecord> DecodeRaw(ReadOnlySpan<byte> raw)
    {
        var records = new List<GeneRecord>();
        int offset = 0;

        while (offset + 4 <= raw.Length)
        {
            int token = TokenAt(raw, offset);
            if (token == GeneConstants.ENDGENOMETOKEN)
                break;

            if (token != GeneConstants.GENETOKEN)
                break;

            if (offset + GeneHeaderOffsets.GH_LENGTH > raw.Length)
                break;

            int nextMarker = FindNextMarker(raw, offset + GeneHeaderOffsets.GH_LENGTH);
            if (nextMarker < 0)
                break;

            int length = nextMarker - offset;
            int payloadOffset = offset + GeneHeaderOffsets.GH_LENGTH;
            int payloadLength = Math.Max(0, nextMarker - payloadOffset);
            int type = raw[offset + GeneHeaderOffsets.GH_TYPE];
            int subtype = raw[offset + GeneHeaderOffsets.GH_SUB];

            var header = new GeneHeader(
                Offset: offset,
                Length: length,
                Type: type,
                Subtype: subtype,
                Id: raw[offset + GeneHeaderOffsets.GH_ID],
                Generation: raw[offset + GeneHeaderOffsets.GH_GEN],
                SwitchOnAge: raw[offset + GeneHeaderOffsets.GH_SWITCHON],
                Flags: raw[offset + GeneHeaderOffsets.GH_FLAGS],
                Mutability: raw[offset + GeneHeaderOffsets.GH_MUTABILITY],
                Variant: raw[offset + GeneHeaderOffsets.GH_VARIANT]);

            var payload = new GenePayload(
                Offset: payloadOffset,
                Length: payloadLength,
                Kind: GeneNames.PayloadKind(type, subtype),
                Bytes: raw.Slice(payloadOffset, payloadLength).ToArray());

            records.Add(new GeneRecord(
                header,
                payload,
                raw.Slice(offset, length).ToArray()));

            offset = nextMarker;
        }

        return records;
    }

    internal static int TokenAt(ReadOnlySpan<byte> raw, int offset)
    {
        if (offset < 0 || offset + 4 > raw.Length)
            return 0;

        return raw[offset]
            | (raw[offset + 1] << 8)
            | (raw[offset + 2] << 16)
            | (raw[offset + 3] << 24);
    }

    internal static int FindNextMarker(ReadOnlySpan<byte> raw, int start)
    {
        for (int i = start; i + 4 <= raw.Length; i++)
        {
            int token = TokenAt(raw, i);
            if (token == GeneConstants.GENETOKEN || token == GeneConstants.ENDGENOMETOKEN)
                return i;
        }

        return -1;
    }
}
