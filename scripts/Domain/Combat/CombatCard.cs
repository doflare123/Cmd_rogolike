namespace CmdRoguelike.Domain.Combat;

public enum CombatCardKind { Attack, Defense }

/// <summary>Immutable action definition, independent of input and presentation.</summary>
public sealed record CombatCard
{
	public string Id { get; }
	public string Name { get; }
	public CombatCardKind Kind { get; }
	public int ActionPointCost { get; }
	public int Power { get; }

	public CombatCard(string id, string name, CombatCardKind kind, int actionPointCost, int power)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id);
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		if (!Enum.IsDefined(kind) || actionPointCost < 0 || power < 0)
			throw new ArgumentException("Invalid combat card parameters.");
		Id = id;
		Name = name;
		Kind = kind;
		ActionPointCost = actionPointCost;
		Power = power;
	}
}

/// <summary>Prototype balance; defense power is a percentage of computed armor.</summary>
public sealed record CombatOptions
{
	public int HandSize { get; }
	public int AttackCards { get; }
	public int DefenseCards { get; }
	public int EnemiesBeforePlayerPercent { get; }
	public CombatCard Attack { get; }
	public CombatCard Defense { get; }

	public CombatOptions(int handSize = 5, int attackCards = 6, int defenseCards = 4,
		int enemiesBeforePlayerPercent = 30, int attackDamage = 1, int defenseArmorPercent = 100)
	{
		if (handSize < 1 || attackCards < 1 || defenseCards < 0
			|| (long)attackCards + defenseCards > 1000 || handSize > (long)attackCards + defenseCards
			|| enemiesBeforePlayerPercent is < 0 or > 100 || attackDamage < 1 || defenseArmorPercent < 0)
			throw new ArgumentException("Invalid combat options.");
		HandSize = handSize;
		AttackCards = attackCards;
		DefenseCards = defenseCards;
		EnemiesBeforePlayerPercent = enemiesBeforePlayerPercent;
		Attack = new CombatCard("attack", "Атака", CombatCardKind.Attack, 1, attackDamage);
		Defense = new CombatCard("defense", "Защита", CombatCardKind.Defense, 1, defenseArmorPercent);
	}
}
