using Godot;
using CreaturesReborn.Sim.Agent;

namespace CreaturesReborn.Godot;

public static class AgentSpriteFactory
{
    public static Sprite3D? Create(AgentArchetype archetype, float size = 1.0f)
    {
        if (string.IsNullOrWhiteSpace(archetype.SpriteToken))
            return null;

        string path = $"res://art/agents/generated/{archetype.SpriteToken}.svg";
        if (!ResourceLoader.Exists(path))
            return null;

        Texture2D? texture = ResourceLoader.Load<Texture2D>(path);
        if (texture == null)
            return null;

        return new Sprite3D
        {
            Name = $"{archetype.SpriteToken}_Sprite",
            Texture = texture,
            PixelSize = 0.0055f * size,
            NoDepthTest = false,
            Shaded = false,
            Position = new Vector3(0, 0.58f * size, -0.03f),
        };
    }
}
