using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using CreaturesReborn.Sim.Creature;

namespace CreaturesReborn.Godot;

/// <summary>
/// Geneforge norn model with stride-driven movement.
/// </summary>
[GlobalClass]
public partial class NornBillboardSprite : Node3D
{
    const float TurnSpeed    = 12.0f;
    const float WalkCycleHz  = 1.8f;    // full strides per second
    const float ModelScale   = 0.0033f;
    const float FootOffset   = 0.92f;
    const float LegSwing     = 0.28f;
    const float KneeBend     = 0.30f;
    const float ArmSwing     = 0.18f;
    const float ForearmBend  = 0.10f;
    const float TailSwing    = 0.18f;
    const float StrideBob    = 0.025f;  // body bob amplitude
    const float StrideLength = 0.12f;   // world units per foot-plant

    [Export] public bool UseProceduralModel = false;

    private Node3D? _model;
    private Node3D? _body, _head, _earL, _earR;
    private Node3D? _thighL, _thighR, _shinL, _shinR;
    private Node3D? _footL, _footR;
    private Node3D? _humerusL, _humerusR, _radiusL, _radiusR;
    private Node3D? _tail, _tailTip;
    private Vector3 _basePos;
    private Vector3 _baseModelScale = Vector3.One;
    private IReadOnlyDictionary<string, Vector3> _baseScales = new Dictionary<string, Vector3>();
    private uint? _appliedAppearanceSignature;

    private float _phase;        // walk cycle phase (radians, continuous)
    private float _targetY, _curY;
    private int   _walkDir;      // -1, 0, +1  set by CreatureNode
    private NornActionPose _actionPose = NornActionPose.Idle;
    private Func<float, float>? _clampX;  // room bounds clamper
    private Func<Vector3, float, Vector3>? _walkSurface;

    public void SetWalkDirection(int dir) => _walkDir = dir;
    public void SetActionPose(NornActionPose pose) => _actionPose = pose;
    public void SetClampX(Func<float, float> clamp) => _clampX = clamp;
    public void SetWalkSurface(Func<Vector3, float, Vector3> projectWalkStep) => _walkSurface = projectWalkStep;

    public override void _Ready()
    {
        _model = ShouldUseProceduralModel() ? NornModelFactory.Create() : LoadLegacyGlbModel();
        if (_model == null) return;
        if (_model.GetParent() == null)
            AddChild(_model);
        _basePos = _model.Position;
        _baseModelScale = _model.Scale;

        // Grab all limb nodes and save original model-space positions
        var orig = new Dictionary<string, Vector3>();
        _body     = Grab("Body4", orig);
        _head     = Grab("Head1_normal", orig);
        _earL     = Grab("ear_4L_chichi", orig);
        _earR     = Grab("ear_4R_chichi", orig);
        _thighL   = Grab("Thigh_L", orig);    _thighR   = Grab("Thigh_R", orig);
        _shinL    = Grab("Shin_L", orig);      _shinR    = Grab("Shin_R", orig);
        _footL    = Grab("Foot_4L", orig);     _footR    = Grab("Foot_4R", orig);
        _humerusL = Grab("Humerous_L", orig);  _humerusR = Grab("Humerous_R", orig);
        _radiusL  = Grab("radius_L", orig);    _radiusR  = Grab("radius_R", orig);
        _tail     = Grab("tail", orig);        _tailTip  = Grab("tailtip_f", orig);

        // Build FK chains using original positions for correct offsets
        Chain(_shinL, _thighL, orig);   Chain(_footL, _shinL, orig);
        Chain(_shinR, _thighR, orig);   Chain(_footR, _shinR, orig);
        Chain(_radiusL, _humerusL, orig);
        Chain(_radiusR, _humerusR, orig);
        Chain(_tailTip, _tail, orig);

        _baseScales = NornAppearanceApplier.CaptureBaseScales(_model);
        ApplyDefaultTextures();
    }

    private bool ShouldUseProceduralModel()
        => UseProceduralModel || IsHeadlessDisplay() || !HasImportedLegacyModel();

    private static bool IsHeadlessDisplay()
        => string.Equals(DisplayServer.GetName(), "headless", StringComparison.OrdinalIgnoreCase);

    private static bool HasImportedLegacyModel()
    {
        string importDirectory = ProjectSettings.GlobalizePath("res://.godot/imported");
        return Directory.Exists(importDirectory)
            && Directory.GetFiles(importDirectory, "norn.glb-*.scn").Length > 0;
    }

    private Node3D? LoadLegacyGlbModel()
    {
        var scene = ResourceLoader.Load<PackedScene>("res://assets/models/norn.glb");
        if (scene == null) { GD.PrintErr("norn.glb not found"); return null; }

        var model = scene.Instantiate<Node3D>();
        model.Scale = new Vector3(ModelScale, ModelScale, ModelScale);
        model.RotationDegrees = new Vector3(0, 90, 0);
        model.Position = new Vector3(0, FootOffset, 0);
        AddChild(model);
        _basePos = model.Position;
        return model;
    }

    private Node3D? Grab(string name, Dictionary<string, Vector3> orig)
    {
        var n = _model?.FindChild(name, true, false) as Node3D;
        if (n != null) orig[name] = n.Position;
        return n;
    }

    private void Chain(Node3D? child, Node3D? newParent, Dictionary<string, Vector3> orig)
    {
        if (child == null || newParent == null) return;
        var oldParent = child.GetParent();
        if (oldParent == null) return;
        if (!orig.TryGetValue(child.Name, out var cPos)) return;
        if (!orig.TryGetValue(newParent.Name, out var pPos)) return;
        oldParent.RemoveChild(child);
        newParent.AddChild(child);
        child.Position = cPos - pPos;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        var parent = GetParent<Node3D>();
        if (parent == null) return;

        if (_walkDir != 0)
        {
            // Advance walk cycle
            _phase += WalkCycleHz * MathF.Tau * dt;

            // Smooth continuous movement (2 foot-plants per full cycle)
            float moveSpeed = WalkCycleHz * 2f * StrideLength;
            float dx = _walkDir * moveSpeed * dt;
            float newX = parent.Position.X + dx;
            if (_walkSurface != null)
            {
                parent.Position = _walkSurface(parent.Position, newX);
            }
            else
            {
                if (_clampX != null) newX = _clampX(newX);
                parent.Position = new Vector3(newX, parent.Position.Y, parent.Position.Z);
            }

            _targetY = _walkDir > 0 ? 0f : 180f;
        }
        else
        {
            // Not walking: ease phase to nearest rest pose (multiple of π)
            float restPhase = MathF.Round(_phase / MathF.PI) * MathF.PI;
            _phase = Lerp(_phase, restPhase, dt * 6f);
        }

        Animate(_phase);

        _curY = Lerp(_curY, _targetY, dt * TurnSpeed);
        RotationDegrees = new Vector3(0, _curY, 0);
    }

    public void UpdateVisuals(Creature creature)
    {
        if (_model == null) return;

        CreatureAppearance appearance = CreatureAppearance.FromGenome(creature.Genome);
        if (_appliedAppearanceSignature == appearance.Signature)
            return;

        _model.Scale = _baseModelScale * appearance.StageScale;
        NornAppearanceApplier.Apply(_model, appearance, _baseScales, LoadNornTexture);
        _appliedAppearanceSignature = appearance.Signature;
    }

    private void Animate(float ph)
    {
        float s = MathF.Sin(ph);

        // Body bob: peaks at each foot-plant (twice per stride)
        float bob = (1f - MathF.Abs(MathF.Cos(ph))) * StrideBob;
        if (_model != null)
            _model.Position = _basePos + new Vector3(0, bob, 0);

        // ── Legs ──
        float thL = s * LegSwing;
        float thR = -s * LegSwing;
        float knL = MathF.Max(0, s) * KneeBend;
        float knR = MathF.Max(0, -s) * KneeBend;
        if (_thighL != null) _thighL.Rotation = new Vector3(thL, 0, 0);
        if (_thighR != null) _thighR.Rotation = new Vector3(thR, 0, 0);
        if (_shinL != null) _shinL.Rotation = new Vector3(knL, 0, 0);
        if (_shinR != null) _shinR.Rotation = new Vector3(knR, 0, 0);
        // Counter-rotate feet to stay level
        if (_footL != null) _footL.Rotation = new Vector3(-(thL + knL), 0, 0);
        if (_footR != null) _footR.Rotation = new Vector3(-(thR + knR), 0, 0);

        // ── Arms (counter-swing) ──
        if (_humerusL != null) _humerusL.Rotation = new Vector3(-s * ArmSwing, 0, 0);
        if (_humerusR != null) _humerusR.Rotation = new Vector3(s * ArmSwing, 0, 0);
        if (_radiusL != null) _radiusL.Rotation = new Vector3(-MathF.Abs(s) * ForearmBend, 0, 0);
        if (_radiusR != null) _radiusR.Rotation = new Vector3(-MathF.Abs(s) * ForearmBend, 0, 0);

        // ── Tail ──
        if (_tail != null) _tail.Rotation = new Vector3(s * TailSwing, 0, 0);
        if (_tailTip != null) _tailTip.Rotation = new Vector3(s * TailSwing * 0.5f, 0, 0);

        var rig = new NornPoseRig(
            _body,
            _head,
            _humerusL,
            _humerusR,
            _radiusL,
            _radiusR,
            _tail,
            _tailTip,
            _earL,
            _earR);
        NornPoseAnimator.Apply(_walkDir != 0 ? NornActionPose.Walk : _actionPose, ph, rig);
    }

    private void ApplyDefaultTextures()
    {
        if (_model == null) return;

        // Map mesh node names → texture files
        var map = new Dictionary<string, string>
        {
            { "Body4",           "Body_F.png" },
            { "Head1_normal",    "Head_F.png" },
            { "Bald Patch",      "Head_F.png" },
            { "ear_4L_chichi",   "Ear_F.png" },
            { "ear_4R_chichi",   "Ear_F.png" },
            { "Thigh_L",         "Thigh_F.png" },
            { "Thigh_R",         "Thigh_F.png" },
            { "Shin_L",          "Shin_F.png" },
            { "Shin_R",          "Shin_F.png" },
            { "Foot_4L",         "Feet_F.png" },
            { "Foot_4R",         "Feet_F.png" },
            { "Humerous_L",      "Humerus_F.png" },
            { "Humerous_R",      "Humerus_F.png" },
            { "radius_L",        "Radius_F.png" },
            { "radius_R",        "Radius_F.png" },
            { "tail",            "Tail_Base_F.png" },
            { "tailtip_f",       "Tail_Tip_F.png" },
            { "Lid_L",           "Head_F.png" },
            { "Lid_R",           "Head_F.png" },
            { "Hair_m",          "Hair.png" },
            { "Hair_m_civet",    "Hair.png" },
        };

        foreach (var (nodeName, texFile) in map)
        {
            var mesh = _model.FindChild(nodeName, true, false) as MeshInstance3D;
            if (mesh == null) continue;
            var tex = LoadNornTexture(texFile);
            if (tex == null) continue;
            mesh.MaterialOverride = new StandardMaterial3D
            {
                AlbedoTexture = tex,
                Roughness = 0.9f,
            };
        }

        // Eyes get a glossy material
        var eyeTex = LoadNornTexture("Eye.png");
        if (eyeTex == null) return;
        foreach (string name in new[] { "Eye_L", "Eye_R" })
        {
            var eye = _model.FindChild(name, true, false) as MeshInstance3D;
            if (eye != null)
                eye.MaterialOverride = new StandardMaterial3D
                    { AlbedoTexture = eyeTex, Roughness = 0.05f, MetallicSpecular = 0.3f };
        }
    }

    private static Texture2D? LoadNornTexture(string texFile)
    {
        string resPath = $"res://assets/textures/norn/{texFile}";
        string absPath = ProjectSettings.GlobalizePath(resPath);
        if (System.IO.File.Exists(absPath))
        {
            var img = Image.LoadFromFile(absPath);
            if (img != null)
                return ImageTexture.CreateFromImage(img);
        }

        return ResourceLoader.Load<Texture2D>(resPath);
    }

    private static float Lerp(float a, float b, float t) =>
        a + (b - a) * Math.Clamp(t, 0f, 1f);
}
