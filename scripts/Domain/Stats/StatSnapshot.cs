namespace CmdRoguelike.Domain.Stats;

public sealed record StatSnapshot<T>(T Id, int Base, int Growth, IReadOnlyList<StatModifier> Modifiers) where T : struct, Enum;
