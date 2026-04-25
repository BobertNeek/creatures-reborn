namespace CreaturesReborn.Sim.Creature;

public enum CreatureAgeStage
{
    Baby,
    Child,
    Adolescent,
    Adult,
    Senior,
}

public static class CreatureAge
{
    public static CreatureAgeStage StageFromAge(byte age)
    {
        if (age < 32) return CreatureAgeStage.Baby;
        if (age < 80) return CreatureAgeStage.Child;
        if (age < 128) return CreatureAgeStage.Adolescent;
        if (age < 210) return CreatureAgeStage.Adult;
        return CreatureAgeStage.Senior;
    }

    public static bool IsReproductive(byte age)
        => StageFromAge(age) is CreatureAgeStage.Adolescent or CreatureAgeStage.Adult or CreatureAgeStage.Senior;
}
