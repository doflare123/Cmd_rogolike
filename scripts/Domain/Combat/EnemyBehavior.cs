using CmdRoguelike.Domain.Entities;

namespace CmdRoguelike.Domain.Combat;

public enum EnemyActionKind { Attack, Guard, Charge }

public sealed record EnemyAction
{
	public EnemyActionKind Kind { get; }
	public int Power { get; }
	public string Name => Kind switch
	{
		EnemyActionKind.Guard => "Защита", EnemyActionKind.Charge => "Подготовка",
		_ => Power > 1 ? "Сильный удар" : "Атака",
	};
	public EnemyAction(EnemyActionKind kind, int power = 0)
	{
		if (!Enum.IsDefined(kind) || power < 0 || (kind == EnemyActionKind.Charge && power != 0))
			throw new ArgumentException("Invalid enemy action.");
		Kind = kind;
		Power = power;
	}
}

/// <summary>Only public actor data and round number; no access to the player's hand or world.</summary>
public interface IEnemyBehavior
{
	EnemyAction Plan(Enemy enemy, int round);
}

public static class EnemyBehaviors
{
	public static IEnemyBehavior Attacker { get; } = new AttackBehavior();
	public static IEnemyBehavior Guardian { get; } = new AlternatingBehavior(EnemyActionKind.Guard, 2);
	public static IEnemyBehavior Brute { get; } = new AlternatingBehavior(EnemyActionKind.Charge, 0);
	private sealed class AttackBehavior : IEnemyBehavior
	{
		public EnemyAction Plan(Enemy enemy, int round) => new(EnemyActionKind.Attack, enemy.AttackPower);
	}
	private sealed class AlternatingBehavior(EnemyActionKind setup, int block) : IEnemyBehavior
	{
		public EnemyAction Plan(Enemy enemy, int round)
			=> round % 2 == 1 ? new(setup, block) : new(EnemyActionKind.Attack, enemy.AttackPower);
	}
}
