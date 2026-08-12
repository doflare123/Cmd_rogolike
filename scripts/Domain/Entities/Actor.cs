using Godot;

namespace CmdRoguelike.Domain.Entities;

/// <summary>
/// Живая сущность со здоровьем. Этот слой можно использовать для игрока и NPC.
/// </summary>
public abstract class Actor : DungeonEntity
{
	public string Name { get; }
	public int MaxHealth { get; }
	public int Health { get; private set; }
	public bool IsAlive => Health > 0;
	public override bool BlocksMovement => IsAlive;

	protected Actor(Vector2I position, string name, int maxHealth)
		: base(position)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			throw new ArgumentException("Actor name cannot be empty.", nameof(name));
		}

		if (maxHealth <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(maxHealth));
		}

		Name = name;
		MaxHealth = maxHealth;
		Health = maxHealth;
	}

	public void TakeDamage(int amount)
	{
		if (amount < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(amount));
		}

		Health = Math.Max(0, Health - amount);
	}
}
