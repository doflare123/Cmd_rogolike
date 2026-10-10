using CmdRoguelike.Domain.Items;

namespace CmdRoguelike.Domain.Rewards;

public enum EnemyRewardRole { Attacker, Defender, Heavy }
public sealed record RewardEnemy(int Difficulty, int StartingHealth, EnemyRewardRole Role);

/// <summary>Snapshot at victory; later healing, inventory changes or animation cannot change it.</summary>
public sealed class CombatVictorySummary
{
	public IReadOnlyList<RewardEnemy> Enemies { get; }
	public int PlayerTurns { get; }
	public int Health { get; }
	public int MaxHealth { get; }
	public int Mana { get; }
	public int MaxMana { get; }
	public CombatVictorySummary(IEnumerable<RewardEnemy> enemies, int playerTurns, int health, int maxHealth, int mana, int maxMana)
	{
		ArgumentNullException.ThrowIfNull(enemies);
		var copy = enemies.ToArray();
		if (copy.Length == 0 || copy.Any(e => e is null || e.Difficulty < 1 || e.StartingHealth < 1 || !Enum.IsDefined(e.Role))
			|| playerTurns < 1 || health < 1 || maxHealth < health || mana < 0 || maxMana < mana)
			throw new ArgumentException("Invalid victory snapshot.");
		Enemies = Array.AsReadOnly(copy); PlayerTurns = playerTurns;
		Health = health; MaxHealth = maxHealth; Mana = mana; MaxMana = maxMana;
	}
}

/// <summary>Prototype balance, separated from geometry options and visual preferences.</summary>
public sealed class CombatRewardOptions
{
	public int GroupBonus { get; }
	public int SynergyBonus { get; }
	public int MaximumEfficiencyPercent { get; }
	public int LowResourcePercent { get; }
	public int SupportCostPercent { get; }
	public CombatRewardOptions(int groupBonus = 2, int synergyBonus = 3, int maximumEfficiencyPercent = 20,
		int lowResourcePercent = 35, int supportCostPercent = 12)
	{
		if (groupBonus < 0 || synergyBonus < 0 || maximumEfficiencyPercent is < 0 or > 30
			|| lowResourcePercent is < 1 or > 99 || supportCostPercent is < 1 or > 12)
			throw new ArgumentOutOfRangeException(nameof(groupBonus), "Invalid reward balance.");
		GroupBonus = groupBonus; SynergyBonus = synergyBonus; MaximumEfficiencyPercent = maximumEfficiencyPercent;
		LowResourcePercent = lowResourcePercent; SupportCostPercent = supportCostPercent;
	}
}

public sealed record RewardBudget(int EnemyCount, int DifficultyBudget, int ExpectedTurns, int ActualTurns,
	int EfficiencyBonus, int TotalBudget, int EquipmentBudget, int SupportPercent, bool HealthSupport, bool ManaSupport);

public static class CombatRewardRules
{
	public static RewardBudget Calculate(CombatVictorySummary summary, CombatRewardOptions? options = null)
	{
		ArgumentNullException.ThrowIfNull(summary);
		options ??= new CombatRewardOptions();
		int count = summary.Enemies.Count;
		int synergy = summary.Enemies.Any(e => e.Role == EnemyRewardRole.Defender)
			&& summary.Enemies.Any(e => e.Role == EnemyRewardRole.Heavy) ? options.SynergyBonus : 0;
		int difficulty = checked((int)(summary.Enemies.Sum(e => (long)e.Difficulty)
			+ (long)(count - 1) * options.GroupBonus + synergy));
		int expected = checked((int)((summary.Enemies.Sum(e => (long)e.StartingHealth) + 2) / 3
			+ summary.Enemies.Count(e => e.Role == EnemyRewardRole.Defender)));
		int percent = (int)((long)Math.Max(0, expected - summary.PlayerTurns) * options.MaximumEfficiencyPercent / expected);
		int bonus = checked((int)((long)difficulty * percent / 100));
		int total = checked(difficulty + bonus);
		bool health = (long)summary.Health * 100 <= (long)summary.MaxHealth * options.LowResourcePercent;
		bool mana = summary.MaxMana > 0 && (long)summary.Mana * 100 <= (long)summary.MaxMana * options.LowResourcePercent;
		int support = ((health ? 1 : 0) + (mana ? 1 : 0)) * options.SupportCostPercent;
		// At most 24% goes to supplies. Remaining quality is reduced, never increased by damage.
		int gear = Math.Max(1, checked((int)(((long)total * (100 - support) + 99) / 100)));
		return new(count, difficulty, expected, summary.PlayerTurns, bonus, total, gear, support, health, mana);
	}
}

public sealed record RewardItem(ItemRoll Roll, int Quantity = 1);
public sealed class CombatReward
{
	public RewardBudget Budget { get; }
	public IReadOnlyList<RewardItem> Items { get; }
	public ItemRoll Equipment => Items[0].Roll;
	public CombatReward(RewardBudget budget, IEnumerable<RewardItem> items)
	{
		ArgumentNullException.ThrowIfNull(budget);
		ArgumentNullException.ThrowIfNull(items);
		var copy = items.ToArray();
		if (copy.Length is < 1 or > 3 || copy.Any(i => i is null || i.Roll is null || i.Quantity < 1)
			|| copy[0].Quantity != 1
			|| copy[0].Roll.Definition.Slots.Count == 0 || copy.Skip(1).Any(i => i.Roll.Definition.Restoration is null))
			throw new ArgumentException("Reward requires one equipment item and optional restorative supplies.");
		Budget = budget; Items = Array.AsReadOnly(copy);
	}
}
