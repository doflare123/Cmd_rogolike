using Godot;

namespace CmdRoguelike.Domain.Entities;

/// <summary>
/// Временный враг для прототипа. Новые типы врагов должны наследовать Enemy,
/// а не добавлять специфичные для типа поля в DungeonMap.
/// </summary>
public sealed class BasicEnemy : Enemy
{
	public BasicEnemy(Vector2I position)
		: base(position, name: "Enemy", maxHealth: 3, attackPower: 1)
	{
	}
}
