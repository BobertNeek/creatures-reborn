using System;
using Godot;
using CreaturesReborn.Sim.Biochemistry;
using CreaturesReborn.Sim.Formats;
using CreaturesReborn.Sim.Creature;
using CreaturesReborn.Sim.Genome;
using CreaturesReborn.Sim.Save;
using CreaturesReborn.Sim.Util;
using C = CreaturesReborn.Sim.Creature.Creature;

namespace CreaturesReborn.Godot;

/// <summary>
/// Godot Node3D that owns and ticks a <see cref="C"/> (Sim.Creature) at 20 Hz.
///
/// Tick order (per world tick):
///   1. FeedDriveInputs (already handled inside Creature.Tick)
///   2. Creature.Tick() — Biochemistry → Brain → Motor
///   3. ExecuteDecision() — translate WTA verb into movement / biochem side-effects
/// </summary>
[GlobalClass]
public partial class CreatureNode : Node3D
{
    [Export] public string GenomePath = "res://data/genomes/starter.gen";
    [Export] public float  WalkSpeed  = 1.0f;
    [Export] public int    Sex        = GeneConstants.MALE;
    [Export(PropertyHint.Range, "0,255,1")] public int Age = 128;
    [Export] public int    Variant    = 0;
    [Export] public string Moniker    = "";

    // -------------------------------------------------------------------------
    // State
    // -------------------------------------------------------------------------
    private C? _creature;
    private C? _pendingHatchedCreature;
    private float _tickAccumulator;
    private const float TickInterval = 1.0f / 20.0f;   // 20 Hz

    private FoodNode?           _heldFood;
    private NornBillboardSprite? _sprite;
    private float _verticalVelocity;
    private bool _heldByHand;
    private SavedCreatureState? _pendingSavedState;

    // Seconds to suppress re-laying egg after one was just laid
    private float _layEggCooldown;

    // Wall-bounce state: track position so we can detect being stuck at a wall
    private float _prevX     = float.NaN;
    private float _stuckTime = 0.0f;
    private const float StuckThreshold = 1.5f;   // seconds before forced bounce

    // -------------------------------------------------------------------------
    // Public accessors
    // -------------------------------------------------------------------------
    public C? Creature => _creature;
    public SavedCreatureState? PendingSavedState
    {
        get => _pendingSavedState;
        set => _pendingSavedState = value;
    }

    public void SetHeldByHand(bool held)
    {
        _heldByHand = held;
        if (held)
            _verticalVelocity = 0;
    }

    public void InitializeFromHatch(C creature, string genomePath, int sex, byte age, int variant, string moniker)
    {
        _pendingHatchedCreature = creature;
        GenomePath = genomePath;
        Sex = sex;
        Age = age;
        Variant = variant;
        Moniker = moniker;
    }

    // -------------------------------------------------------------------------
    // Godot lifecycle
    // -------------------------------------------------------------------------
    public override void _Ready()
    {
        if (_pendingSavedState != null)
        {
            RestoreFromSavedState(_pendingSavedState);
        }
        else if (_pendingHatchedCreature != null)
        {
            _creature = _pendingHatchedCreature;
            _pendingHatchedCreature = null;
        }
        else
        {
            string absPath = ProjectSettings.GlobalizePath(GenomePath);
            if (!System.IO.File.Exists(absPath))
            {
                GD.PrintErr($"[CreatureNode] Genome not found: {absPath}");
                return;
            }

            var rng = new StatefulRng((int)GD.Randi());
            string moniker = string.IsNullOrWhiteSpace(Moniker)
                ? Name.ToString()
                : Moniker;
            byte age = (byte)Math.Clamp(Age, 0, 255);
            _creature = C.LoadFromFile(absPath, rng, Sex, age, Variant, moniker);
            _creature.SetChemical(ChemID.ATP, 1.0f);
        }

        if (_creature == null)
            return;

        _sprite = GetNodeOrNull<NornBillboardSprite>("Sprite");
        _sprite?.UpdateVisuals(_creature);

        // Give the sprite a room-bounds clamper so it respects walls
        // Supports old MetaroomNode, ColonyMetaroomNode, and TreehouseMetaroomNode
        var mm       = GetParent()?.GetNodeOrNull<MetaroomNode>("Metaroom");
        var colony   = GetParent()?.GetNodeOrNull<ColonyMetaroomNode>("Metaroom");
        var treehouse = GetParent()?.GetNodeOrNull<TreehouseMetaroomNode>("Metaroom");
        if (GetParent() is WorldNode worldNode && _sprite != null)
            _sprite.SetWalkSurface(worldNode.ProjectWalkStep);
        else if (mm != null && _sprite != null)
            _sprite.SetClampX(x => mm.Sim.ClampX(x));
        else if (colony != null && _sprite != null)
            _sprite.SetClampX(x => colony.MetaRoom.ClampX(x));
        else if (treehouse != null && _sprite != null)
            _sprite.SetClampX(x => treehouse.MetaRoom.ClampX(x));

        GD.Print($"[CreatureNode] Loaded creature. Lobes={_creature.Brain.LobeCount}, Tracts={_creature.Brain.TractCount}");
    }

    public SavedCreatureState CreateSavedState()
    {
        if (_creature == null)
            throw new InvalidOperationException("Cannot save an unloaded creature.");

        SavedCreatureState state = _creature.CreateSnapshot();
        state.GenomePath = GenomePath;
        state.X = Position.X;
        state.Y = Position.Y;
        state.Z = Position.Z;
        state.WalkSpeed = WalkSpeed;
        state.VerticalVelocity = _verticalVelocity;
        return state;
    }

    public void ReplaceCreatureGenome(byte[] genomeFileBytes, int? sex = null, byte? age = null, int? variant = null, string? moniker = null)
    {
        if (genomeFileBytes.Length < 4)
        {
            GD.PrintErr("[CreatureNode] Live apply refused: genome byte array is too short.");
            return;
        }

        int resolvedSex = sex ?? _creature?.Genome.Sex ?? Sex;
        byte resolvedAge = age ?? _creature?.Genome.Age ?? (byte)Math.Clamp(Age, 0, 255);
        int resolvedVariant = variant ?? _creature?.Genome.Variant ?? Variant;
        string resolvedMoniker = string.IsNullOrWhiteSpace(moniker)
            ? (_creature?.Genome.Moniker ?? Moniker)
            : moniker!;

        var rng = new StatefulRng((int)GD.Randi());
        var genome = new CreaturesReborn.Sim.Genome.Genome(rng);
        try
        {
            GenomeReader.Load(genome, genomeFileBytes, resolvedSex, age: 0, resolvedVariant, resolvedMoniker);
            _creature = C.CreateFromGenome(genome, rng);
            _creature.Genome.Age = resolvedAge;
            Sex = resolvedSex;
            Age = resolvedAge;
            Variant = resolvedVariant;
            Moniker = resolvedMoniker;
            _sprite?.UpdateVisuals(_creature);
            GD.Print($"[CreatureNode] Live-applied genome to {Name}.");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[CreatureNode] Live apply refused: {ex.Message}");
        }
    }

    private void RestoreFromSavedState(SavedCreatureState state)
    {
        _creature = C.RestoreSnapshot(state);
        GenomePath = state.GenomePath;
        Sex = state.Sex;
        Age = state.Age;
        Variant = state.Variant;
        Moniker = state.Moniker;
        WalkSpeed = state.WalkSpeed;
        _verticalVelocity = state.VerticalVelocity;
        Position = new Vector3(state.X, state.Y, state.Z);
    }

    public override void _Process(double delta)
    {
        if (_creature == null) return;

        _tickAccumulator += (float)delta;
        while (_tickAccumulator >= TickInterval)
        {
            _tickAccumulator -= TickInterval;
            FeedContextualDrives();   // must come BEFORE Tick() so drives arrive in same batch
            _creature.Tick();
            ExecuteDecision();
        }

        // Update visual every render frame (smooth interpolation)
        ApplyGravity((float)delta);
        _sprite?.UpdateVisuals(_creature);

        // Keep held food above our hand
        if (_heldFood is { IsConsumed: false })
            _heldFood.Position = Position + new Vector3(0.4f, 0.5f, 0);

        if (_layEggCooldown > 0)
            _layEggCooldown -= (float)delta;
    }

    // -------------------------------------------------------------------------
    // Contextual drive supplements (wall-aversion, social proximity)
    // Called BEFORE Creature.Tick() so AddDriveInput values are mixed in by FeedDriveInputs.
    // -------------------------------------------------------------------------
    private void FeedContextualDrives()
    {
        if (_creature == null) return;
        CreatureContextDrives.Apply(this, _creature);
    }

    // -------------------------------------------------------------------------
    // Decision execution
    // -------------------------------------------------------------------------
    private void ExecuteDecision()
    {
        if (_creature == null) return;
        int verb = _creature.Motor.CurrentVerb;

        // Determine walk direction for the sprite (-1/0/+1)
        int walkDir = 0;
        NornActionPose pose = NornActionPose.Idle;

        switch (verb)
        {
            case VerbId.TravelEast:
                walkDir = 1;
                break;
            case VerbId.TravelWest:
                walkDir = -1;
                break;
            case VerbId.Rest:
                pose = NornActionPose.Rest;
                DoSleep();
                break;
            case VerbId.Approach:
                pose = NornActionPose.Approach;
                walkDir = ApproachDirection();
                break;
            case VerbId.Get:
                pose = NornActionPose.Get;
                if (!TryPickUp())
                    walkDir = ApproachDirection();
                break;
            case VerbId.Eat:
                pose = NornActionPose.Eat;
                if (!TryEat())
                    walkDir = ApproachDirection();
                break;
            case VerbId.Activate1:
                pose = NornActionPose.Activate;
                if (!TryEat())
                    walkDir = ApproachDirection();
                break;
            case VerbId.Retreat:
                pose = NornActionPose.Retreat;
                walkDir = RetreatDirection();
                break;
            case VerbId.Drop:
                pose = NornActionPose.Drop;
                Drop();
                break;
        }

        // Delegate walking to sprite (step-driven animation)
        _sprite?.SetWalkDirection(walkDir);
        _sprite?.SetActionPose(walkDir != 0 ? NornActionPose.Walk : pose);

        // ── Physical wall-bounce guard ──────────────────────────────────────
        // If the norn hasn't moved horizontally for StuckThreshold seconds while
        // trying to travel (verb East or West), assume it's pinned against a wall
        // and force it away.  This simulates the "Walked into a wall" stimulus
        // (gene 021) that the real c2e engine fires.
        bool travellingHoriz = walkDir != 0;
        if (travellingHoriz && !float.IsNaN(_prevX) && MathF.Abs(Position.X - _prevX) < 0.02f)
        {
            _stuckTime += TickInterval;
            if (_stuckTime >= StuckThreshold)
            {
                StimulusTable.Apply(_creature!, StimulusId.WallBump);
                // Bounce: reverse walk direction briefly
                var wallBounds = GetRoomBounds();
                if (wallBounds.HasValue)
                {
                    var (wlb, wrb) = wallBounds.Value;
                    float cx = (wlb + wrb) * 0.5f;
                    _sprite?.SetWalkDirection(cx > Position.X ? 1 : -1);
                }
                _stuckTime = 0;
            }
        }
        else
        {
            _stuckTime = 0;
        }
        _prevX = Position.X;

        // Biochem-driven egg laying: triggered by Progesterone, not a decision lobe verb
        if (_layEggCooldown <= 0 && _creature.GetChemical(ChemID.Progesterone) > 0.7f)
            TryLayEgg();
    }

    // -------------------------------------------------------------------------
    // Actions
    // -------------------------------------------------------------------------
    private void MoveX(float dx)
    {
        float newX = Position.X + dx;

        // Clamp to room bounds (works with either metaroom type)
        var b = GetRoomBounds();
        if (b.HasValue)
        {
            var (l, r) = b.Value;
            newX = Math.Clamp(newX, l, r);
        }

        Position = new Vector3(newX, Position.Y, Position.Z);
    }

    private void ApplyGravity(float delta)
    {
        if (_heldByHand || GetParent() is not WorldNode world)
            return;

        Position = world.ApplyGravityStep(Position, delta, ref _verticalVelocity);
    }

    private void DoSleep()
    {
        // Metabolise sleepiness and tiredness while resting
        _creature!.InjectChemical(ChemID.Sleepiness, -0.005f);
        _creature!.InjectChemical(ChemID.Tiredness,  -0.003f);
    }

    private int RetreatDirection()
    {
        var rb = GetRoomBounds();
        float cx = 0f;
        if (rb.HasValue)
        {
            var (rl, rr) = rb.Value;
            cx = (rl + rr) * 0.5f;
        }
        float dir = cx - Position.X;
        if (MathF.Abs(dir) < 0.1f) return 0;
        return dir > 0 ? 1 : -1;
    }

    // Returns true if pick-up succeeded (food was in range)
    private bool TryPickUp()
    {
        if (_heldFood != null) return true;
        var food = FindNearestFood(1.5f);
        if (food == null) return false;
        _heldFood = food;
        food.PickUp(this);
        StimulusTable.Apply(_creature!, StimulusId.GotFood);
        return true;
    }

    // Returns true if eat succeeded (food was in range)
    private bool TryEat()
    {
        FoodNode? target = _heldFood ?? FindNearestFood(0.8f);
        if (target == null) return false;

        StimulusTable.Apply(_creature!, target.EatStimulusId);
        _creature!.InjectChemical(ChemID.ATP, target.ResolvedATPAmount);

        if (_heldFood == target)
            _heldFood = null;

        target.Consume();
        return true;
    }

    private int ApproachDirection()
    {
        var food = FindNearestFood(20.0f);
        if (food == null) return 0;
        return CreaturePerception.DirectionToward(GetParent(), Position, food.Position);
    }

    private void Drop()
    {
        if (_heldFood == null) return;
        _heldFood.Drop(Position);
        _heldFood = null;
    }

    private void TryLayEgg()
    {
        if (_creature == null || _creature.Genome.Sex != GeneConstants.FEMALE)
            return;

        // Find the nearest mate within 3m whose progesterone is also elevated.
        // Proximity gate: without it, a norn across the map qualifies and the
        // egg spawns at the midpoint out in empty space.
        const float MateRadius = 3.0f;

        CreatureNode? mate = null;
        float nearest = MateRadius;
        foreach (Node n in GetParent()!.GetChildren())
        {
            if (n is CreatureNode other && other != this
                && other.Creature != null
                && other.Creature.GetChemical(ChemID.Progesterone) > 0.5f)
            {
                float d = Position.DistanceTo(other.Position);
                if (d < nearest
                    && NornReproductionRules.CanLayEgg(_creature, other.Creature, d, _layEggCooldown, MateRadius))
                {
                    nearest = d;
                    mate = other;
                }
            }
        }

        if (mate == null) return;
        LayEggWith(mate);
    }

    /// <summary>
    /// Force-lay an egg with an explicit mate, bypassing progesterone / proximity
    /// gates. Used by the GUI Breed button, where the player has already said
    /// "these two should breed now." Runs Genome.Cross, writes the child .gen
    /// to user://, instantiates a new CreatureNode at the midpoint, and applies
    /// the normal cooldown + progesterone burn-off.
    /// </summary>
    public void LayEggWith(CreatureNode mate)
    {
        if (_creature == null || mate.Creature == null) return;

        CreatureNode? motherNode = _creature.Genome.Sex == GeneConstants.FEMALE ? this
            : mate.Creature.Genome.Sex == GeneConstants.FEMALE ? mate
            : null;
        CreatureNode? fatherNode = ReferenceEquals(motherNode, this) ? mate
            : ReferenceEquals(motherNode, mate) ? this
            : null;

        if (motherNode == null || fatherNode?.Creature == null)
        {
            GD.Print("[CreatureNode] Breed refused — needs a female/male pair.");
            return;
        }

        if (!NornReproductionRules.CanLayEgg(
                motherNode.Creature!,
                fatherNode.Creature,
                distance: 0,
                cooldownSeconds: motherNode._layEggCooldown,
                mateRadius: float.MaxValue))
        {
            GD.Print("[CreatureNode] Breed refused — parents are not currently fertile.");
            return;
        }

        motherNode.SpawnEggWithFather(fatherNode);
    }

    private void SpawnEggWithFather(CreatureNode fatherNode)
    {
        if (_creature == null || fatherNode.Creature == null) return;

        // Cross genomes (real c2e CrossLoop with LINKAGE=50, MUTATIONRATE=4800)
        var childGenome = new CreaturesReborn.Sim.Genome.Genome(new Rng((int)GD.Randi()));
        childGenome.Cross("child", _creature.Genome, fatherNode.Creature.Genome, 4, 4, 4, 4);

        // Serialize child genome to user:// so the new CreatureNode can load it
        string tmpPath = System.IO.Path.Combine(
            ProjectSettings.GlobalizePath("user://"),
            $"egg_{(ulong)Time.GetTicksMsec()}.gen");
        byte[] bytes = CreaturesReborn.Sim.Formats.GenomeWriter.Serialize(childGenome);
        System.IO.File.WriteAllBytes(tmpPath, bytes);

        // Spawn an EggNode that will warm + hatch into a Norn after HatchTime
        var egg = new Agents.EggNode
        {
            GenomePath = tmpPath,
            Sex = childGenome.Sex,
            Variant = childGenome.Variant,
            HatchTime  = 12.0f,
            Position   = (Position + fatherNode.Position) * 0.5f,
        };
        GetParent()!.AddChild(egg);
        GD.Print($"[CreatureNode] Egg laid at {egg.Position}, will hatch in {egg.HatchTime}s.");

        // Consume progesterone on both parents + 30 s cooldown on mother
        StimulusTable.Apply(_creature, StimulusId.LaidEgg);
        _creature.InjectChemical(ChemID.Progesterone, -0.3f);
        fatherNode.Creature.InjectChemical(ChemID.Progesterone, -0.4f);
        _layEggCooldown = 30.0f;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>Get room bounds from whichever metaroom type is in the scene.</summary>
    internal (float left, float right)? GetRoomBounds()
    {
        var mm = GetParent()?.GetNodeOrNull<MetaroomNode>("Metaroom");
        if (mm != null) return (mm.Sim.LeftBound, mm.Sim.RightBound);
        var colony = GetParent()?.GetNodeOrNull<ColonyMetaroomNode>("Metaroom");
        if (colony != null) return (colony.MetaRoom.LeftBound, colony.MetaRoom.RightBound);
        var treehouse = GetParent()?.GetNodeOrNull<TreehouseMetaroomNode>("Metaroom");
        if (treehouse != null) return (treehouse.MetaRoom.LeftBound, treehouse.MetaRoom.RightBound);
        return null;
    }

    private FoodNode? FindNearestFood(float maxDist)
    {
        return CreaturePerception.FindNearestReachableFood(GetParent(), Position, maxDist);
    }
}
