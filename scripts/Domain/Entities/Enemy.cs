using Godot;

namespace CmdRoguelike.Domain.Entities;

/// <summary>
/// Базовый класс всех враждебных акторов. После появления соответствующих систем
/// сюда или в отдельные стратегии будет добавлено поведение ИИ и боя.
/// </summary>
public abstract class Enemy : Actor
{
	public int AttackPower { get; }

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
