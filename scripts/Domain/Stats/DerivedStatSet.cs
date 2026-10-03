namespace CmdRoguelike.Domain.Stats;

public sealed class DerivedStatSet : StatCollection<DerivedStatId>
{
	public DerivedStatSet(int maxHealth, int maxMana)
		: base(new Dictionary<DerivedStatId, int>
		{
			[DerivedStatId.MaxHealth] = maxHealth,
			[DerivedStatId.MaxMana] = maxMana,
			[DerivedStatId.Armor] = 0,
			[DerivedStatId.MaxActionPoints] = 3,
		})
	{
		if (maxHealth <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(maxHealth));
		}

		if (maxMana < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(maxMana));
		}
	}

	protected override void ValidateValue(DerivedStatId stat, int value)
	{
		if (stat == DerivedStatId.MaxHealth && value <= 0)
		{
			throw new InvalidOperationException("Maximum health must remain positive.");
		}

		if (stat != DerivedStatId.MaxHealth && value < 0)
		{
			throw new InvalidOperationException($"{stat} cannot be negative.");
		}
	}
}
