using System;
using System.Collections.Generic;

namespace CreaturesReborn.Sim.Genome;

public enum GeneValidationSeverity
{
    Info = 0,
    Warning = 1,
    Error = 2
}

public enum GeneValidationCode
{
    BadMarker,
    TruncatedMarker,
    TruncatedHeader,
    MissingEndMarker,
    UnknownGeneType,
    UnknownSubtype,
    ImpossibleLength,
    InvalidVariant,
    ConflictingSexLink
}

public sealed record GeneValidationIssue(
    GeneValidationSeverity Severity,
    GeneValidationCode Code,
    int Offset,
    string Message);

public static class GeneValidator
{
    public static IReadOnlyList<GeneValidationIssue> Validate(Genome genome)
        => ValidateRaw(genome.AsSpan());

    public static IReadOnlyList<GeneValidationIssue> Validate(IEnumerable<GeneRecord> records)
    {
        var issues = new List<GeneValidationIssue>();
        foreach (var record in records)
        {
            ValidateTypeAndSubtype(record.Type, record.Subtype, record.Offset, issues);
            ValidateHeaderValues(record.Header, issues);
        }

        return issues;
    }

    public static IReadOnlyList<GeneValidationIssue> ValidateRaw(ReadOnlySpan<byte> raw)
    {
        var issues = new List<GeneValidationIssue>();
        bool foundEnd = false;
        int offset = 0;

        while (offset < raw.Length)
        {
            if (offset + 4 > raw.Length)
            {
                issues.Add(new(
                    GeneValidationSeverity.Error,
                    GeneValidationCode.TruncatedMarker,
                    offset,
                    "Genome ends in the middle of a four-byte gene marker."));
                break;
            }

            int token = GeneDecoder.TokenAt(raw, offset);
            if (token == GeneConstants.ENDGENOMETOKEN)
            {
                foundEnd = true;
                break;
            }

            if (token != GeneConstants.GENETOKEN)
            {
                issues.Add(new(
                    GeneValidationSeverity.Error,
                    GeneValidationCode.BadMarker,
                    offset,
                    "Expected 'gene' or 'gend' marker."));
                break;
            }

            if (offset + GeneHeaderOffsets.GH_LENGTH > raw.Length)
            {
                issues.Add(new(
                    GeneValidationSeverity.Error,
                    GeneValidationCode.TruncatedHeader,
                    offset,
                    "Gene marker is present but the fixed 12-byte gene header is incomplete."));
                break;
            }

            int nextMarker = GeneDecoder.FindNextMarker(raw, offset + GeneHeaderOffsets.GH_LENGTH);
            if (nextMarker < 0)
            {
                issues.Add(new(
                    GeneValidationSeverity.Error,
                    GeneValidationCode.MissingEndMarker,
                    offset,
                    "Gene data has no following 'gene' or 'gend' marker."));
                break;
            }

            int type = raw[offset + GeneHeaderOffsets.GH_TYPE];
            int subtype = raw[offset + GeneHeaderOffsets.GH_SUB];
            var header = new GeneHeader(
                offset,
                nextMarker - offset,
                type,
                subtype,
                raw[offset + GeneHeaderOffsets.GH_ID],
                raw[offset + GeneHeaderOffsets.GH_GEN],
                raw[offset + GeneHeaderOffsets.GH_SWITCHON],
                raw[offset + GeneHeaderOffsets.GH_FLAGS],
                raw[offset + GeneHeaderOffsets.GH_MUTABILITY],
                raw[offset + GeneHeaderOffsets.GH_VARIANT]);

            if (header.Length < GeneHeaderOffsets.GH_LENGTH)
            {
                issues.Add(new(
                    GeneValidationSeverity.Error,
                    GeneValidationCode.ImpossibleLength,
                    offset,
                    $"Gene length {header.Length} is shorter than the fixed header length."));
            }

            ValidateTypeAndSubtype(type, subtype, offset, issues);
            ValidateHeaderValues(header, issues);

            offset = nextMarker;
        }

        if (!foundEnd)
        {
            issues.Add(new(
                GeneValidationSeverity.Error,
                GeneValidationCode.MissingEndMarker,
                raw.Length,
                "Genome is missing the final 'gend' marker."));
        }

        return issues;
    }

    private static void ValidateHeaderValues(GeneHeader header, List<GeneValidationIssue> issues)
    {
        if (header.Variant < 0 || header.Variant > GeneConstants.NUM_BEHAVIOUR_VARIANTS)
        {
            issues.Add(new(
                GeneValidationSeverity.Error,
                GeneValidationCode.InvalidVariant,
                header.Offset + GeneHeaderOffsets.GH_VARIANT,
                $"Gene variant {header.Variant} is outside 0..{GeneConstants.NUM_BEHAVIOUR_VARIANTS}."));
        }

        if (header.MaleLinked && header.FemaleLinked)
        {
            issues.Add(new(
                GeneValidationSeverity.Warning,
                GeneValidationCode.ConflictingSexLink,
                header.Offset + GeneHeaderOffsets.GH_FLAGS,
                "Gene is marked as both male-linked and female-linked."));
        }
    }

    private static void ValidateTypeAndSubtype(
        int type,
        int subtype,
        int offset,
        List<GeneValidationIssue> issues)
    {
        if (type < 0 || type >= GeneTypeInfo.NUMGENETYPES)
        {
            issues.Add(new(
                GeneValidationSeverity.Error,
                GeneValidationCode.UnknownGeneType,
                offset + GeneHeaderOffsets.GH_TYPE,
                $"Unknown gene type {type}."));
            return;
        }

        int subtypeCount = type switch
        {
            (int)GeneType.BRAINGENE => BrainSubtypeInfo.NUMBRAINSUBTYPES,
            (int)GeneType.BIOCHEMISTRYGENE => BiochemSubtypeInfo.NUMBIOCHEMSUBTYPES,
            (int)GeneType.CREATUREGENE => CreatureSubtypeInfo.NUMCREATURESUBTYPES,
            (int)GeneType.ORGANGENE => OrganSubtypeInfo.NUMORGANSUBTYPES,
            _ => 0
        };

        if (subtype < 0 || subtype >= subtypeCount)
        {
            issues.Add(new(
                GeneValidationSeverity.Error,
                GeneValidationCode.UnknownSubtype,
                offset + GeneHeaderOffsets.GH_SUB,
                $"Unknown subtype {subtype} for gene type {type}."));
        }
    }
}
