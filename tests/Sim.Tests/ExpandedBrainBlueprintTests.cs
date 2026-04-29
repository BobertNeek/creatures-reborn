using System.Linq;
using CreaturesReborn.Sim.Biochemistry;
using CreaturesReborn.Sim.Brain;
using CreaturesReborn.Sim.Genome;
using CreaturesReborn.Sim.Util;
using Xunit;
using B = CreaturesReborn.Sim.Brain.Brain;

namespace CreaturesReborn.Sim.Tests;

public sealed class ExpandedBrainBlueprintTests
{
    [Fact]
    public void ClassicProfile_DefinesC3DsCompatibleInterfaceLobes()
    {
        CreatureRebornBrainProfile profile = BrainBlueprintBuilder.ClassicC3DsBrainProfile();

        Assert.Equal(NeuralArchitectureProfile.ClassicC3Ds, profile.Profile);
        Assert.Contains(profile.Blueprint.Lobes, lobe => lobe.Token == "driv");
        Assert.Contains(profile.Blueprint.Lobes, lobe => lobe.Token == "decn");
        Assert.False(BrainBlueprintValidator.Validate(profile.Blueprint).HasHardInvalid);
    }

    [Fact]
    public void RebornExpandedProfile_AddsInspectableMemoryAndControlLobes()
    {
        CreatureRebornBrainProfile classic = BrainBlueprintBuilder.ClassicC3DsBrainProfile();
        CreatureRebornBrainProfile expanded = BrainBlueprintBuilder.RebornExpandedBrainProfile();

        Assert.Equal(NeuralArchitectureProfile.RebornExpanded, expanded.Profile);
        Assert.True(expanded.Blueprint.Lobes.Sum(lobe => lobe.Width * lobe.Height) >
                    classic.Blueprint.Lobes.Sum(lobe => lobe.Width * lobe.Height));
        Assert.Contains(expanded.Blueprint.Lobes, lobe => lobe.Token == "plac");
        Assert.Contains(expanded.Blueprint.Lobes, lobe => lobe.Token == "socl");
        Assert.Contains(expanded.Blueprint.Lobes, lobe => lobe.Token == "dang");
        Assert.Contains(expanded.Blueprint.Lobes, lobe => lobe.Token == "goal");
        Assert.False(BrainBlueprintValidator.Validate(expanded.Blueprint).HasHardInvalid);
    }

    [Fact]
    public void RebornExpandedBlueprint_GeneratesGenomeThatBootsTicksSnapshotsAndRestores()
    {
        BrainBlueprint blueprint = BrainBlueprintBuilder.RebornExpandedBrainProfile().Blueprint;
        var genome = BrainBlueprintBuilder.CreateGenome(blueprint, new Rng(44), "expanded");
        var brain = new B();
        var chemicals = new float[BiochemConst.NUMCHEM];
        chemicals[ChemID.ATP] = 1.0f;

        brain.RegisterBiochemistry(chemicals);
        brain.ReadFromGenome(genome, new Rng(45));
        brain.Update();
        BrainSnapshot snapshot = brain.CreateSnapshot();
        var saveState = brain.CreateSaveState();

        Assert.True(brain.LobeCount >= blueprint.Lobes.Count);
        Assert.True(brain.TractCount >= blueprint.Tracts.Count);
        Assert.Contains(snapshot.Lobes, lobe => B.TokenToString(lobe.Token) == "plac");
        Assert.Contains(snapshot.Lobes, lobe => B.TokenToString(lobe.Token) == "goal");

        var restored = new B();
        restored.RegisterBiochemistry((float[])chemicals.Clone());
        restored.ReadFromGenome(genome, new Rng(45));
        restored.RestoreSaveState(saveState);

        Assert.Equal(brain.CreateSnapshot().Lobes.Count, restored.CreateSnapshot().Lobes.Count);
        Assert.Equal(brain.CreateSnapshot().Tracts.Count, restored.CreateSnapshot().Tracts.Count);
    }

    [Fact]
    public void ExpandedBlueprint_DoesNotAffectImportedC3DsGenomeDefaults()
    {
        byte[] rawGenome = C3DsBiologyParityTests.RawGenome(
            C3DsBiologyParityTests.Lobe("driv", 4),
            C3DsBiologyParityTests.Lobe("decn", 4),
            C3DsBiologyParityTests.Tract("driv", "decn"),
            C3DsBiologyParityTests.Organ(),
            C3DsBiologyParityTests.Reaction(ChemID.ATP, ChemID.ADP),
            C3DsBiologyParityTests.Receptor(ChemID.Injury, 3, 0));

        C3DsGenomeImportResult result = C3DsGenomeImporter.ImportRaw(rawGenome);

        Assert.Equal(BiochemistryCompatibilityMode.C3DS, result.CompatibilityProfile.BiochemistryMode);
        Assert.DoesNotContain(result.Records, record =>
            record.Payload.Kind == GenePayloadKind.BrainLobe &&
            record.Payload.Bytes.Length >= 4 &&
            System.Text.Encoding.ASCII.GetString(record.Payload.Bytes, 0, 4).Trim() == "plac");
    }
}
