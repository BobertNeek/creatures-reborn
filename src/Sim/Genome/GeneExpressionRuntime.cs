using System;
using System.Collections.Generic;
using CreaturesReborn.Sim.Biochemistry;

namespace CreaturesReborn.Sim.Genome;

public sealed record GeneExpressionDelta(
    GeneIdentity Gene,
    GenePayloadKind PayloadKind,
    string Action,
    int? ChemicalId = null,
    float? Before = null,
    float? After = null);

public sealed record AgeTransitionResult(
    int FromAge,
    int ToAge,
    GeneExpressionTrace Trace,
    IReadOnlyList<GeneExpressionDelta> Deltas);

public static class GeneExpressionRuntime
{
    public static IReadOnlyList<GeneExpressionDelta> Apply(
        IReadOnlyList<GeneRecord> expressedGenes,
        Biochemistry.Biochemistry biochemistry,
        BiochemistryTrace? trace = null)
    {
        var deltas = new List<GeneExpressionDelta>();
        foreach (GeneRecord record in expressedGenes)
        {
            switch (record.Payload.Kind)
            {
                case GenePayloadKind.BiochemistryInject:
                    ApplyInject(record, biochemistry, trace, deltas);
                    break;
                case GenePayloadKind.BiochemistryHalfLife:
                    ApplyHalfLife(record, biochemistry, deltas);
                    break;
                case GenePayloadKind.BiochemistryReaction:
                case GenePayloadKind.BiochemistryReceptor:
                case GenePayloadKind.BiochemistryEmitter:
                case GenePayloadKind.BiochemistryNeuroEmitter:
                case GenePayloadKind.Organ:
                case GenePayloadKind.BrainOrgan:
                    break;
            }
        }

        return deltas;
    }

    private static void ApplyInject(
        GeneRecord record,
        Biochemistry.Biochemistry biochemistry,
        BiochemistryTrace? trace,
        List<GeneExpressionDelta> deltas)
    {
        ReadOnlySpan<byte> payload = record.Payload.Bytes;
        if (payload.Length < 2)
            return;

        int chemicalId = payload[0];
        if (chemicalId == ChemID.None)
            return;

        float before = biochemistry.GetChemical(chemicalId);
        float after = payload[1] / 255f;
        biochemistry.SetChemical(
            chemicalId,
            after,
            ChemicalDeltaSource.CreatureInjection,
            $"gene-expression:{record.Identity}",
            trace);

        deltas.Add(new GeneExpressionDelta(
            record.Identity,
            record.Payload.Kind,
            "set-initial-concentration",
            chemicalId,
            before,
            biochemistry.GetChemical(chemicalId)));
    }

    private static void ApplyHalfLife(
        GeneRecord record,
        Biochemistry.Biochemistry biochemistry,
        List<GeneExpressionDelta> deltas)
    {
        ReadOnlySpan<byte> payload = record.Payload.Bytes;
        if (payload.Length < BiochemConst.NUMCHEM)
            return;

        biochemistry.ApplyHalfLifeGene(payload);
        deltas.Add(new GeneExpressionDelta(
            record.Identity,
            record.Payload.Kind,
            "replace-half-life-table"));
    }
}
