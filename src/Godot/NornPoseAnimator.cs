using System;
using Godot;

namespace CreaturesReborn.Godot;

internal readonly record struct NornPoseRig(
    Node3D? Body,
    Node3D? Head,
    Node3D? HumerusL,
    Node3D? HumerusR,
    Node3D? RadiusL,
    Node3D? RadiusR,
    Node3D? Tail,
    Node3D? TailTip,
    Node3D? EarL,
    Node3D? EarR);

internal static class NornPoseAnimator
{
    public static void Apply(NornActionPose pose, float phase, in NornPoseRig rig)
    {
        float s = MathF.Sin(phase);
        float breathe = MathF.Sin(phase * 0.35f) * 0.025f;

        if (rig.Body != null)
            rig.Body.Rotation = Vector3.Zero;
        if (rig.Head != null)
            rig.Head.Rotation = new Vector3(breathe, 0, 0);
        if (rig.EarL != null)
            rig.EarL.Rotation = new Vector3(0, 0, 0.04f + breathe);
        if (rig.EarR != null)
            rig.EarR.Rotation = new Vector3(0, 0, -0.04f - breathe);

        switch (pose)
        {
            case NornActionPose.Rest:
                if (rig.Body != null) rig.Body.Rotation = new Vector3(-0.10f, 0, 0.08f);
                if (rig.Head != null) rig.Head.Rotation = new Vector3(0.22f, 0, 0.10f);
                if (rig.HumerusL != null) rig.HumerusL.Rotation = new Vector3(0.32f, 0, -0.18f);
                if (rig.HumerusR != null) rig.HumerusR.Rotation = new Vector3(0.32f, 0, 0.18f);
                if (rig.Tail != null) rig.Tail.Rotation = new Vector3(-0.18f, 0, 0.10f);
                break;

            case NornActionPose.Eat:
                float chew = MathF.Sin(phase * 2.4f) * 0.10f;
                if (rig.Head != null) rig.Head.Rotation = new Vector3(0.24f + chew, 0, 0);
                if (rig.HumerusL != null) rig.HumerusL.Rotation = new Vector3(-0.52f, 0, -0.12f);
                if (rig.HumerusR != null) rig.HumerusR.Rotation = new Vector3(-0.52f, 0, 0.12f);
                if (rig.RadiusL != null) rig.RadiusL.Rotation = new Vector3(-0.24f, 0, 0);
                if (rig.RadiusR != null) rig.RadiusR.Rotation = new Vector3(-0.24f, 0, 0);
                break;

            case NornActionPose.Get:
            case NornActionPose.Activate:
                float reach = pose == NornActionPose.Activate ? -0.72f : -0.58f;
                if (rig.Head != null) rig.Head.Rotation = new Vector3(0.10f, 0, 0);
                if (rig.HumerusL != null) rig.HumerusL.Rotation = new Vector3(reach, 0, -0.10f);
                if (rig.HumerusR != null) rig.HumerusR.Rotation = new Vector3(reach * 0.85f, 0, 0.10f);
                if (rig.RadiusL != null) rig.RadiusL.Rotation = new Vector3(-0.10f, 0, 0);
                if (rig.RadiusR != null) rig.RadiusR.Rotation = new Vector3(-0.10f, 0, 0);
                break;

            case NornActionPose.Drop:
                if (rig.Head != null) rig.Head.Rotation = new Vector3(0.12f, 0, 0);
                if (rig.HumerusL != null) rig.HumerusL.Rotation = new Vector3(0.24f, 0, -0.08f);
                if (rig.HumerusR != null) rig.HumerusR.Rotation = new Vector3(0.24f, 0, 0.08f);
                break;

            case NornActionPose.Retreat:
                if (rig.Head != null) rig.Head.Rotation = new Vector3(-0.08f, 0, -0.10f);
                if (rig.Tail != null) rig.Tail.Rotation = new Vector3(s * 0.24f, 0, 0);
                if (rig.TailTip != null) rig.TailTip.Rotation = new Vector3(s * 0.16f, 0, 0);
                break;
        }
    }
}
