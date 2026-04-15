using System;
using System.IO;
using CreaturesReborn.Sim.Brain;
using CreaturesReborn.Sim.Formats;
using CreaturesReborn.Sim.Util;
using B = CreaturesReborn.Sim.Brain.Brain;
using Xunit;
using Xunit.Abstractions;

namespace CreaturesReborn.Sim.Tests;

public class LobeListTests
{
    private readonly ITestOutputHelper _out;
    public LobeListTests(ITestOutputHelper output) => _out = output;

    private static readonly string GenomePath =
        Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "..",
            "data", "genomes", "starter.gen");

    [Fact]
    public void PrintAllLobesAndTracts()
    {
        if (!File.Exists(GenomePath)) return;

        var brain = new B();
        brain.ReadFromGenome(GenomeReader.LoadNew(new Rng(1), GenomePath), new Rng(1));

        _out.WriteLine($"Lobes ({brain.LobeCount}):");
        for (int i = 0; i < brain.LobeCount; i++)
        {
            var lobe = brain.GetLobe(i);
            if (lobe == null) continue;
            int tok = lobe.Token;
            string name = TokenStr(tok);
            _out.WriteLine($"  [{i:D2}] '{name}'  neurons={lobe.GetNoOfNeurons()}  updateAt={lobe.UpdateAtTime}");
        }
        _out.WriteLine($"Tracts ({brain.TractCount})");

        // Specifically report on lobes the instinct system needs
        bool hasResp = false, hasSmel = false, hasVerb = false;
        for (int i = 0; i < brain.LobeCount; i++)
        {
            var lobe = brain.GetLobe(i);
            if (lobe == null) continue;
            string n = TokenStr(lobe.Token);
            if (n == "resp") hasResp = true;
            if (n == "smel") hasSmel = true;
            if (n == "verb") hasVerb = true;
        }
        _out.WriteLine($"resp={hasResp}  smel={hasSmel}  verb={hasVerb}");

        Assert.True(brain.LobeCount >= 8);
    }

    private static string TokenStr(int tok)
    {
        char c0 = (char)(tok & 0xFF), c1 = (char)((tok >> 8) & 0xFF),
             c2 = (char)((tok >> 16) & 0xFF), c3 = (char)((tok >> 24) & 0xFF);
        return $"{c0}{c1}{c2}{c3}";
    }
}
