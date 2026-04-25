using System;
using CreaturesReborn.Sim.Genome;
using G = CreaturesReborn.Sim.Genome.Genome;

namespace CreaturesReborn.Sim.Creature;

public enum CreatureSex
{
    Male = GeneConstants.MALE,
    Female = GeneConstants.FEMALE,
}

public readonly record struct AppearanceColor(float R, float G, float B)
{
    public static AppearanceColor FromBytes(byte r, byte g, byte b)
        => new(r / 255f, g / 255f, b / 255f);

    public static AppearanceColor Lerp(AppearanceColor a, AppearanceColor b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new AppearanceColor(
            a.R + (b.R - a.R) * t,
            a.G + (b.G - a.G) * t,
            a.B + (b.B - a.B) * t);
    }

    public AppearanceColor WithBrightness(float multiplier)
        => new(
            Math.Clamp(R * multiplier, 0f, 1f),
            Math.Clamp(G * multiplier, 0f, 1f),
            Math.Clamp(B * multiplier, 0f, 1f));
}

/// <summary>
/// Compact, renderer-agnostic appearance phenotype derived from expressed DNA.
/// Godot consumes this to tint and scale the current 3D Norn model, while tests
/// can verify inheritance and mutation behavior without loading Godot.
/// </summary>
public readonly record struct CreatureAppearance(
    CreatureSex Sex,
    CreatureAgeStage AgeStage,
    uint Signature,
    AppearanceColor FurColor,
    AppearanceColor SkinColor,
    AppearanceColor MarkingColor,
    AppearanceColor EyeColor,
    AppearanceColor HairColor,
    float BodyWidthScale,
    float BodyHeightScale,
    float HeadScale,
    float LimbScale,
    float EarScale,
    float TailScale,
    float HairScale,
    float StageScale,
    float MarkingStrength)
{
    public static CreatureAppearance FromGenome(G genome)
    {
        var genes = AppearanceGeneInfluence.Read(genome);
        uint signature = Mix(HashGenome(genome), genes.Signature);
        signature = Mix(signature, (uint)genome.Sex);
        signature = Mix(signature, (uint)genome.Variant);
        signature = Mix(signature, genome.Age);

        var rng = new StableRng(signature);
        bool female = genome.Sex == GeneConstants.FEMALE;
        CreatureAgeStage ageStage = CreatureAge.StageFromAge(genome.Age);

        float warmth = Clamp01(0.50f + (genes.PigmentWarmth - 0.5f) * 0.35f + rng.Range(-0.12f, 0.12f));
        float cream = Clamp01(0.45f + (genes.Body - 0.5f) * 0.20f + rng.Range(-0.10f, 0.12f));
        float teal = Clamp01(0.55f + (genes.PigmentBleed - 0.5f) * 0.30f + rng.Range(-0.08f, 0.10f));

        var amberDark = new AppearanceColor(0.58f, 0.32f, 0.11f);
        var amberLight = new AppearanceColor(0.86f, 0.58f, 0.24f);
        AppearanceColor furBase = AppearanceColor.Lerp(amberDark, amberLight, warmth);
        furBase = AppearanceColor.Lerp(
            furBase,
            new AppearanceColor(0.50f, 0.56f, 0.34f),
            genes.PigmentBleed * 0.08f);

        var fur = furBase.WithBrightness(female ? 1.08f : 1.02f);
        var skin = AppearanceColor.Lerp(
            new AppearanceColor(0.88f, 0.72f, 0.58f),
            new AppearanceColor(1.00f, 0.86f, 0.68f),
            cream);
        var markings = AppearanceColor.Lerp(
            new AppearanceColor(0.23f, 0.14f, 0.08f),
            new AppearanceColor(0.12f, 0.30f, 0.29f),
            teal);
        var eyes = AppearanceColor.Lerp(
            new AppearanceColor(0.18f, 0.82f, 0.84f),
            new AppearanceColor(0.55f, 0.90f, 0.35f),
            rng.Next01());
        var hair = AppearanceColor.Lerp(markings, fur.WithBrightness(0.55f), 0.35f);

        float bodyWidth = 1.0f + genes.Body * 0.08f + rng.Range(-0.025f, 0.025f);
        float bodyHeight = 1.0f + rng.Range(-0.025f, 0.030f);
        float head = 1.0f + genes.Head * 0.07f + rng.Range(-0.020f, 0.025f);
        float limb = 1.0f + genes.Limbs * 0.07f + rng.Range(-0.025f, 0.025f);
        float ear = 1.0f + genes.Head * 0.10f + rng.Range(-0.030f, 0.030f);
        float tail = 1.0f + genes.Tail * 0.12f + rng.Range(-0.030f, 0.035f);
        float hairScale = 1.0f + genes.Hair * 0.14f + rng.Range(-0.035f, 0.035f);

        if (female)
        {
            bodyWidth -= 0.04f;
            head += 0.045f;
            ear += 0.035f;
            tail -= 0.030f;
            hairScale += 0.035f;
        }
        else
        {
            bodyWidth += 0.065f;
            bodyHeight += 0.020f;
            head -= 0.010f;
            limb += 0.030f;
            tail += 0.055f;
        }

        float markingsStrength = Clamp01(0.28f + genes.PigmentBleed * 0.40f + rng.Range(-0.08f, 0.10f));
        ApplyAgeStage(ageStage, ref bodyWidth, ref bodyHeight, ref head, ref limb, ref ear, ref tail, ref hairScale);
        float stageScale = StageScaleFor(ageStage);

        return new CreatureAppearance(
            female ? CreatureSex.Female : CreatureSex.Male,
            ageStage,
            signature,
            fur,
            skin,
            markings,
            eyes,
            hair,
            Clamp(bodyWidth, 0.65f, 1.14f),
            Clamp(bodyHeight, 0.72f, 1.09f),
            Clamp(head, 0.90f, 1.28f),
            Clamp(limb, 0.65f, 1.11f),
            Clamp(ear, 0.84f, 1.24f),
            Clamp(tail, 0.55f, 1.18f),
            Clamp(hairScale, 0.60f, 1.20f),
            stageScale,
            markingsStrength);
    }

    private static void ApplyAgeStage(
        CreatureAgeStage stage,
        ref float bodyWidth,
        ref float bodyHeight,
        ref float head,
        ref float limb,
        ref float ear,
        ref float tail,
        ref float hairScale)
    {
        switch (stage)
        {
            case CreatureAgeStage.Baby:
                bodyWidth *= 0.78f;
                bodyHeight *= 0.82f;
                head *= 1.18f;
                limb *= 0.78f;
                ear *= 1.10f;
                tail *= 0.68f;
                hairScale *= 0.72f;
                break;
            case CreatureAgeStage.Child:
                bodyWidth *= 0.88f;
                bodyHeight *= 0.90f;
                head *= 1.10f;
                limb *= 0.88f;
                ear *= 1.06f;
                tail *= 0.82f;
                hairScale *= 0.88f;
                break;
            case CreatureAgeStage.Adolescent:
                bodyWidth *= 0.96f;
                bodyHeight *= 0.97f;
                head *= 1.04f;
                limb *= 0.97f;
                tail *= 0.95f;
                break;
            case CreatureAgeStage.Senior:
                bodyWidth *= 0.98f;
                bodyHeight *= 0.96f;
                head *= 1.02f;
                limb *= 0.96f;
                ear *= 1.02f;
                tail *= 0.96f;
                hairScale *= 0.92f;
                break;
        }
    }

    private static float StageScaleFor(CreatureAgeStage stage) => stage switch
    {
        CreatureAgeStage.Baby => 0.64f,
        CreatureAgeStage.Child => 0.78f,
        CreatureAgeStage.Adolescent => 0.92f,
        CreatureAgeStage.Senior => 0.96f,
        _ => 1.0f,
    };

    private static uint HashGenome(G genome)
    {
        const uint offset = 2166136261u;
        const uint prime = 16777619u;
        uint hash = offset;

        foreach (byte b in genome.AsSpan())
        {
            hash ^= b;
            hash *= prime;
        }

        return hash;
    }

    private static uint Mix(uint a, uint b)
    {
        uint h = a ^ (b + 0x9E3779B9u + (a << 6) + (a >> 2));
        h ^= h >> 16;
        h *= 0x7FEB352Du;
        h ^= h >> 15;
        h *= 0x846CA68Bu;
        h ^= h >> 16;
        return h;
    }

    private static float Clamp(float value, float min, float max)
        => Math.Clamp(value, min, max);

    private static float Clamp01(float value)
        => Math.Clamp(value, 0f, 1f);

    private readonly struct AppearanceGeneInfluence
    {
        public readonly uint Signature;
        public readonly float Head;
        public readonly float Body;
        public readonly float Limbs;
        public readonly float Tail;
        public readonly float Hair;
        public readonly float PigmentWarmth;
        public readonly float PigmentBleed;

        private AppearanceGeneInfluence(
            uint signature,
            float head,
            float body,
            float limbs,
            float tail,
            float hair,
            float pigmentWarmth,
            float pigmentBleed)
        {
            Signature = signature;
            Head = head;
            Body = body;
            Limbs = limbs;
            Tail = tail;
            Hair = hair;
            PigmentWarmth = pigmentWarmth;
            PigmentBleed = pigmentBleed;
        }

        public static AppearanceGeneInfluence Read(G genome)
        {
            uint sig = 0xA45F131Du;
            int[] regionTotals = new int[BodyRegionInfo.NUMREGIONS];
            int[] regionCounts = new int[BodyRegionInfo.NUMREGIONS];
            int pigmentTotal = 0;
            int pigmentCount = 0;
            int bleedTotal = 0;
            int bleedCount = 0;

            genome.Store();
            try
            {
                genome.Reset();
                while (genome.GetGeneType(
                    (int)GeneType.CREATUREGENE,
                    (int)CreatureSubtype.G_APPEARANCE,
                    CreatureSubtypeInfo.NUMCREATURESUBTYPES,
                    GeneSwitchOverride.SWITCH_UPTOAGE))
                {
                    int part = genome.GetCodon(0, BodyRegionInfo.NUMREGIONS - 1);
                    int variant = genome.GetByte();
                    int species = genome.GetByte();
                    int value = (variant * 37 + species * 19 + part * 53) & 0xFF;
                    regionTotals[part] += value;
                    regionCounts[part]++;
                    sig = Mix(sig, (uint)(part << 16 | variant << 8 | species));
                }

                genome.Reset();
                while (genome.GetGeneType(
                    (int)GeneType.CREATUREGENE,
                    (int)CreatureSubtype.G_PIGMENT,
                    CreatureSubtypeInfo.NUMCREATURESUBTYPES,
                    GeneSwitchOverride.SWITCH_UPTOAGE))
                {
                    int pigment = genome.GetByte();
                    int amount = genome.GetByte();
                    pigmentTotal += (pigment * 29 + amount * 11) & 0xFF;
                    pigmentCount++;
                    sig = Mix(sig, (uint)(0x500000 | pigment << 8 | amount));
                }

                genome.Reset();
                while (genome.GetGeneType(
                    (int)GeneType.CREATUREGENE,
                    (int)CreatureSubtype.G_PIGMENTBLEED,
                    CreatureSubtypeInfo.NUMCREATURESUBTYPES,
                    GeneSwitchOverride.SWITCH_UPTOAGE))
                {
                    int rotation = genome.GetByte();
                    int swap = genome.GetByte();
                    bleedTotal += (rotation * 17 + swap * 31) & 0xFF;
                    bleedCount++;
                    sig = Mix(sig, (uint)(0x700000 | rotation << 8 | swap));
                }
            }
            finally
            {
                genome.Restore();
            }

            float Region(BodyRegion region)
            {
                int idx = (int)region;
                return regionCounts[idx] == 0
                    ? 0.5f
                    : Math.Clamp(regionTotals[idx] / (regionCounts[idx] * 255f), 0f, 1f);
            }

            return new AppearanceGeneInfluence(
                sig,
                Region(BodyRegion.REGION_HEAD),
                Region(BodyRegion.REGION_BODY),
                (Region(BodyRegion.REGION_LEGS) + Region(BodyRegion.REGION_ARMS)) * 0.5f,
                Region(BodyRegion.REGION_TAIL),
                Region(BodyRegion.REGION_HAIR),
                pigmentCount == 0 ? 0.5f : Math.Clamp(pigmentTotal / (pigmentCount * 255f), 0f, 1f),
                bleedCount == 0 ? 0.5f : Math.Clamp(bleedTotal / (bleedCount * 255f), 0f, 1f));
        }
    }

    private struct StableRng
    {
        private uint _state;

        public StableRng(uint seed)
        {
            _state = seed == 0 ? 0x6D2B79F5u : seed;
        }

        public float Next01()
        {
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            return (_state & 0x00FFFFFFu) / 16777215f;
        }

        public float Range(float min, float max)
            => min + (max - min) * Next01();
    }
}
