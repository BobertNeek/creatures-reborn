using CreaturesReborn.Sim.Agent;

namespace CreaturesReborn.Sim.World;

/// <summary>
/// The top-level simulation world. Owns the <see cref="GameMap"/>,
/// <see cref="AgentManager"/>, <see cref="GameTime"/>, and simulation
/// parameters. This is the equivalent of openc2e's <c>World</c> class.
///
/// The Godot layer holds a reference to this and calls <see cref="Tick"/>
/// at 20 Hz. All game systems are pure C# with no Godot dependency.
/// </summary>
public sealed class GameWorld
{
    // ── Subsystems ──────────────────────────────────────────────────────────
    public GameMap      Map    { get; } = new GameMap();
    public AgentManager Agents { get; } = new AgentManager();
    public GameTime     Time   { get; } = new GameTime();

    // ── Population limits (from DS !DS_game variables.cos) ──────────────────
    public int BreedingLimit      { get; set; } = 6;
    public int TotalPopulationMax { get; set; } = 16;
    public int ExtraEggsAllowed   { get; set; } = 4;

    // ── Creature physics defaults ───────────────────────────────────────────
    public float CreatureAccG { get; set; } = 5.0f;
    public int   CreatureBhvr { get; set; } = 15;   // push+pull+stop+hit
    public int   CreatureAttr { get; set; } = 198;   // mouseable+activateable+suffercollisions+sufferphysics
    public int   CreaturePerm { get; set; } = 100;

    // ── Tick ────────────────────────────────────────────────────────────────
    /// <summary>
    /// One simulation step at 20 Hz. Ticks time, CA, then all agents.
    /// </summary>
    public void Tick()
    {
        Time.Tick();
        Map.Tick();        // CA diffusion
        Agents.Tick(Map);  // all agents
    }
}
