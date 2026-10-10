using Godot;
using CmdRoguelike.Domain.Combat;
using CmdRoguelike.Domain.Rewards;

namespace CmdRoguelike.Domain.Entities;

/// <summary>
/// Базовый класс враждебных акторов; стратегия объявляет действия, встреча их исполняет.
/// </summary>
public abstract class Enemy : Actor
{
	public int AttackPower { get; }
	public virtual bool IsBoss => false;
	public virtual IEnemyBehavior Behavior => EnemyBehaviors.Attacker;
	public virtual EnemyRewardRole RewardRole => EnemyRewardRole.Attacker;
	public virtual int RewardDifficulty => checked(MaxHealth + AttackPower * 2);

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
