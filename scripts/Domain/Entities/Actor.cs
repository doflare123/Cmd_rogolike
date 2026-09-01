using CmdRoguelike.Domain.Stats;
using Godot;

namespace CmdRoguelike.Domain.Entities;

/// <summary>
/// Живая сущность со здоровьем. Этот слой можно использовать для игрока и NPC.
/// </summary>
public abstract class Actor : DungeonEntity
{
	public string Name { get; }
	public AttributeSet Attributes { get; }
	public DerivedStatSet DerivedStats { get; }
	public ActorResources Resources { get; }
	public int MaxHealth => Resources.MaxHealth;
	public int Health => Resources.Health;
	public bool IsAlive => Health > 0;
	public override bool BlocksMovement => IsAlive;

	protected Actor(
		Vector2I position,
		string name,
		int maxHealth,
		int maxMana = 0,
		IReadOnlyDictionary<AttributeId, int>? baseAttributes = null)
		: base(position)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			throw new ArgumentException("Actor name cannot be empty.", nameof(name));
		}

		Name = name;
		Attributes = new AttributeSet(baseAttributes);
		DerivedStats = new DerivedStatSet(maxHealth, maxMana);
		Resources = new ActorResources(maxHealth, maxMana);
		DerivedStats.ValueChanged += OnDerivedStatChanged;
	}

	public void TakeDamage(int amount)
	{
		if (amount < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(amount));
		}

		Resources.TakeDamage(amount);
	}

	public void RestoreHealth(int amount)
	{
		Resources.RestoreHealth(amount);
	}

	private void OnDerivedStatChanged(DerivedStatId stat, int oldValue, int newValue)
	{
		_ = oldValue;
		_ = newValue;
		if (stat is DerivedStatId.MaxHealth or DerivedStatId.MaxMana)
		{
			Resources.SynchronizeMaximums(
				DerivedStats.GetValue(DerivedStatId.MaxHealth),
				DerivedStats.GetValue(DerivedStatId.MaxMana));
		}
	}
}
