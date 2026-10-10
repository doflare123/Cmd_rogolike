using Godot;
using CmdRoguelike.Domain.Combat;

namespace CmdRoguelike.Domain.Entities;

/// <summary>
/// Базовый класс враждебных акторов; стратегия объявляет действия, встреча их исполняет.
/// </summary>
public abstract class Enemy : Actor
{
	public int AttackPower { get; }
	public virtual IEnemyBehavior Behavior => EnemyBehaviors.Attacker;

	protected Enemy(
		Vector2I position,
		string name,
		int maxHealth,
		int attackPower)
		: base(position, name, maxHealth)
	{
		if (attackPower < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(attackPower));
		}

		AttackPower = attackPower;
	}
}
