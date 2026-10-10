namespace CmdRoguelike.Domain.Stats;

/// <summary>
/// Текущие ресурсы актора. Максимумы приходят из DerivedStatSet; их рост не
/// восстанавливает ресурс, а уменьшение только ограничивает текущее значение.
/// </summary>
public sealed class ActorResources
{
	public int Health { get; private set; }
	public int Mana { get; private set; }
	public int MaxHealth { get; private set; }
	public int MaxMana { get; private set; }

	public ActorResources(int maxHealth, int maxMana)
	{
		if (maxHealth <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(maxHealth));
		}

		if (maxMana < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(maxMana));
		}

		MaxHealth = maxHealth;
		MaxMana = maxMana;
		Health = maxHealth;
		Mana = maxMana;
	}

	public void TakeDamage(int amount)
	{
		if (amount < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(amount));
		}

		Health = Math.Max(0, Health - amount);
	}

	public void RestoreHealth(int amount)
	{
		if (amount < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(amount));
		}

		Health = (int)Math.Min(MaxHealth, (long)Health + amount);
	}

	public bool TrySpendMana(int amount)
	{
		if (amount < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(amount));
		}

		if (Mana < amount)
		{
			return false;
		}

		Mana -= amount;
		return true;
	}

	public void RestoreMana(int amount)
	{
		if (amount < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(amount));
		}

		Mana = (int)Math.Min(MaxMana, (long)Mana + amount);
	}

	internal void SynchronizeMaximums(int maxHealth, int maxMana)
	{
		if (maxHealth <= 0 || maxMana < 0)
		{
			throw new InvalidOperationException("Derived resource maximums are outside valid bounds.");
		}

		MaxHealth = maxHealth;
		MaxMana = maxMana;
		Health = Math.Min(Health, MaxHealth);
		Mana = Math.Min(Mana, MaxMana);
	}

	internal void Restore(int health, int mana)
	{
		if (health < 0 || health > MaxHealth || mana < 0 || mana > MaxMana) throw new ArgumentException("Invalid saved resources.");
		Health = health;
		Mana = mana;
	}
}
