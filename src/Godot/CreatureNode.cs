using System;
using Godot;
using CreaturesReborn.Sim.Biochemistry;
using CreaturesReborn.Sim.Creature;
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

    // -------------------------------------------------------------------------
    // State
    // -------------------------------------------------------------------------
    private C? _creature;
    private float _tickAccumulator;
    private const float TickInterval = 1.0f / 20.0f;   // 20 Hz

    private FoodNode?           _heldFood;
    private NornBillboardSprite? _sprite;

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

    // -------------------------------------------------------------------------
    // Godot lifecycle
    // -------------------------------------------------------------------------
    public override void _Ready()
    {
        string absPath = ProjectSettings.GlobalizePath(GenomePath);
        if (!System.IO.File.Exists(absPath))
        {
            GD.PrintErr($"[CreatureNode] Genome not found: {absPath}");
            return;
        }

        var rng = new Rng((int)GD.Randi());
        _creature = C.LoadFromFile(absPath, rng);
        _creature.SetChemical(ChemID.ATP, 1.0f);

        _sprite = GetNodeOrNull<NornBillboardSprite>("Sprite");

        // Give the sprite a room-bounds clamper so it respects walls
        // Supports old MetaroomNode, ColonyMetaroomNode, and TreehouseMetaroomNode
        var mm       = GetParent()?.GetNodeOrNull<MetaroomNode>("Metaroom");
        var colony   = GetParent()?.GetNodeOrNull<ColonyMetaroomNode>("Metaroom");
        var treehouse = GetParent()?.GetNodeOrNull<TreehouseMetaroomNode>("Metaroom");
        if (mm != null && _sprite != null)
            _sprite.SetClampX(x => mm.Sim.ClampX(x));
        else if (colony != null && _sprite != null)
            _sprite.SetClampX(x => colony.MetaRoom.ClampX(x));
        else if (treehouse != null && _sprite != null)
            _sprite.SetClampX(x => treehouse.MetaRoom.ClampX(x));

        GD.Print($"[CreatureNode] Loaded creature. Lobes={_creature.Brain.LobeCount}, Tracts={_creature.Brain.TractCount}");
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
        if (_creature == null || GetParent() == null) return;

        // ── Wall aversion ───────────────────────────────────────────────────
        // Inject Fear when the norn is within 1.5 units of either boundary.
        var bounds = GetRoomBounds();
        if (bounds is var (lb, rb))
        {
            float distL  = Position.X - lb;
            float distR  = rb - Position.X;
            float nearest = MathF.Min(distL, distR);
            if (nearest < 1.5f)
            {
                float fear = (1.0f - nearest / 1.5f) * 0.6f;
                _creature.AddDriveInput(DriveId.Fear, fear);
            }
        }

        // ── Social proximity ────────────────────────────────────────────────
        // Reduce Loneliness when near another creature; let it rise naturally when alone.
        bool foundFriend = false;
        foreach (Node n in GetParent().GetChildren())
        {
            if (n is not CreatureNode other || other == this || other.Creature == null) continue;
            float dist = Position.DistanceTo(other.Position);
            if (dist < 4.0f)
            {
                float closeness = 1.0f - dist / 4.0f;
                _creature.AddDriveInput(DriveId.Loneliness, -closeness * 0.4f);
                foundFriend = true;
            }
        }
        // When completely alone, nudge loneliness up slightly so the brain seeks company
        if (!foundFriend)
            _creature.AddDriveInput(DriveId.Loneliness, 0.05f);
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

        switch (verb)
        {
            case VerbId.TravelEast:
                walkDir = 1;
                break;
            case VerbId.TravelWest:
                walkDir = -1;
                break;
            case VerbId.Rest:
                DoSleep();
                break;
            case VerbId.Approach:
                walkDir = ApproachDirection();
                break;
            case VerbId.Get:
                if (!TryPickUp())
                    walkDir = ApproachDirection();
                break;
            case VerbId.Eat:
            case VerbId.Activate1:
                if (!TryEat())
                    walkDir = ApproachDirection();
                break;
            case VerbId.Retreat:
                walkDir = RetreatDirection();
                break;
            case VerbId.Drop:
                Drop();
                break;
        }

        // Delegate walking to sprite (step-driven animation)
        _sprite?.SetWalkDirection(walkDir);

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
                _creature!.AddDriveInput(DriveId.Fear, 1.0f);
                _creature!.AddDriveInput(DriveId.Pain, 0.3f);
                // Bounce: reverse walk direction briefly
                var wallBounds = GetRoomBounds();
                if (wallBounds is var (wlb, wrb))
                {
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
        if (b is var (l, r))
            newX = Math.Clamp(newX, l, r);

        Position = new Vector3(newX, Position.Y, Position.Z);
    }

    private void DoSleep()
    {
        // Metabolise sleepiness and tiredness while resting
        _creature!.InjectChemical(ChemID.Sleepiness, -0.005f);
        _creature!.InjectChemical(ChemID.Tiredness,  -0.003f);
    }

    private int RetreatDirection()
    {
        var rb  = GetRoomBounds();
        float cx = rb is var (rl, rr)
            ? (rl + rr) * 0.5f
            : 0f;
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
        return true;
    }

    // Returns true if eat succeeded (food was in range)
    private bool TryEat()
    {
        FoodNode? target = _heldFood ?? FindNearestFood(0.8f);
        if (target == null) return false;

        // Inject nutrition
        _creature!.InjectChemical(ChemID.Glycogen, target.GlycogenAmount);
        _creature!.InjectChemical(ChemID.ATP,      target.ATPAmount);

        if (_heldFood == target)
            _heldFood = null;

        target.Consume();
        return true;
    }

    private int ApproachDirection()
    {
        var food = FindNearestFood(20.0f);
        if (food == null) return 0;
        float dx = food.Position.X - Position.X;
        if (MathF.Abs(dx) < 0.1f) return 0;
        return dx > 0 ? 1 : -1;
    }

    private void Drop()
    {
        if (_heldFood == null) return;
        _heldFood.Drop(Position);
        _heldFood = null;
    }

    private void TryLayEgg()
    {
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
                if (d < nearest) { nearest = d; mate = other; }
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
        if (_layEggCooldown > 0)
        {
            GD.Print("[CreatureNode] Breed refused — still on cooldown.");
            return;
        }

        // Cross genomes (real c2e CrossLoop with LINKAGE=50, MUTATIONRATE=4800)
        var childGenome = new CreaturesReborn.Sim.Genome.Genome(new Rng((int)GD.Randi()));
        childGenome.Cross("child", _creature.Genome, mate.Creature.Genome, 4, 4, 4, 4);

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
            HatchTime  = 12.0f,
            Position   = (Position + mate.Position) * 0.5f,
        };
        GetParent()!.AddChild(egg);
        GD.Print($"[CreatureNode] Egg laid at {egg.Position}, will hatch in {egg.HatchTime}s.");

        // Consume progesterone on both parents + 30 s cooldown on mother
        _creature.InjectChemical(ChemID.Progesterone, -0.8f);
        mate.Creature.InjectChemical(ChemID.Progesterone, -0.4f);
        _layEggCooldown = 30.0f;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>Get room bounds from whichever metaroom type is in the scene.</summary>
    private (float left, float right)? GetRoomBounds()
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
        if (GetParent() == null) return null;

        // Two-tier search: prefer food on the current floor, but fall back
        // to food on *any* floor so norns will go hunt for stairs when their
        // own floor is empty. Stairs (StairsNode) carry the norn across Y
        // automatically as they walk horizontally, so "walk toward food X"
        // is sufficient AI — no stair-specific pathfinding needed in the
        // vertical slice.
        //
        // Without this fallback, a bottom-floor norn with food only on the
        // top deck would idle forever. With it, the norn walks toward the
        // food, stumbles onto a stair, and ascends.
        const float SameFloorTolerance = 1.5f;

        FoodNode? same = null;
        float     sameDist = maxDist;
        FoodNode? any  = null;
        float     anyDist = maxDist;

        foreach (Node n in GetParent()!.GetChildren())
        {
            if (n is not FoodNode food || food.IsConsumed) continue;

            float dx = MathF.Abs(food.Position.X - Position.X);
            if (dx < anyDist) { any = food; anyDist = dx; }

            if (MathF.Abs(food.Position.Y - Position.Y) <= SameFloorTolerance
                && dx < sameDist)
            {
                same = food; sameDist = dx;
            }
        }
        return same ?? any;
    }
}
