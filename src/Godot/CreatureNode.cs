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
        var mm = GetParent()?.GetNodeOrNull<MetaroomNode>("Metaroom");
        if (mm != null && _sprite != null)
            _sprite.SetClampX(x => mm.Sim.ClampX(x));

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
        var mm = GetParent().GetNodeOrNull<MetaroomNode>("Metaroom");
        if (mm != null)
        {
            float distL  = Position.X - mm.Sim.LeftBound;
            float distR  = mm.Sim.RightBound - Position.X;
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
                var wallMm = GetParent()?.GetNodeOrNull<MetaroomNode>("Metaroom");
                if (wallMm != null)
                {
                    float cx = (wallMm.Sim.LeftBound + wallMm.Sim.RightBound) * 0.5f;
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

        // Clamp to room bounds if we can find the MetaroomNode sibling
        var mm = GetParent()?.GetNodeOrNull<MetaroomNode>("Metaroom");
        if (mm != null)
            newX = mm.Sim.ClampX(newX);

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
        var mm  = GetParent()?.GetNodeOrNull<MetaroomNode>("Metaroom");
        float cx = mm != null
            ? (mm.Sim.LeftBound + mm.Sim.RightBound) * 0.5f
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
        // Find a mate (another CreatureNode in the scene)
        CreatureNode? mate = null;
        foreach (Node n in GetParent()!.GetChildren())
        {
            if (n is CreatureNode other && other != this
                && other.Creature != null
                && other.Creature.GetChemical(ChemID.Progesterone) > 0.5f)
            {
                mate = other;
                break;
            }
        }

        if (mate == null) return;

        // Cross genomes
        var childGenome = new CreaturesReborn.Sim.Genome.Genome(new Rng((int)GD.Randi()));
        childGenome.Cross("child", _creature!.Genome, mate.Creature!.Genome, 4, 4, 4, 4);

        // Serialize child genome to a temp file in user:// and spawn a new norn
        string tmpPath = System.IO.Path.Combine(
            ProjectSettings.GlobalizePath("user://"),
            $"egg_{(ulong)Time.GetTicksMsec()}.gen");
        byte[] bytes = CreaturesReborn.Sim.Formats.GenomeWriter.Serialize(childGenome);
        System.IO.File.WriteAllBytes(tmpPath, bytes);

        // Spawn egg as a new CreatureNode
        var nornScene = GD.Load<PackedScene>("res://scenes/Norn.tscn");
        if (nornScene != null)
        {
            var egg = (CreatureNode)nornScene.Instantiate();
            egg.GenomePath = tmpPath;
            egg.Position   = (Position + mate.Position) * 0.5f;
            GetParent()!.AddChild(egg);
            GD.Print("[CreatureNode] Egg laid!");
        }

        // Consume progesterone
        _creature!.InjectChemical(ChemID.Progesterone, -0.8f);

        _layEggCooldown = 30.0f;   // 30 s before she can lay again
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------
    private FoodNode? FindNearestFood(float maxDist)
    {
        FoodNode? nearest     = null;
        float     nearestDist = maxDist;
        if (GetParent() == null) return null;

        foreach (Node n in GetParent()!.GetChildren())
        {
            if (n is FoodNode food && !food.IsConsumed)
            {
                float d = Position.DistanceTo(food.Position);
                if (d < nearestDist) { nearest = food; nearestDist = d; }
            }
        }
        return nearest;
    }
}
