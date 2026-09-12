namespace CmdRoguelike.Domain.Stats;

/// <summary>
/// Детерминированный реестр базовых значений и модификаторов. Процентные этапы
/// округляются от середины от нуля; More применяется в стабильном порядке источников.
/// </summary>
public class StatCollection<TStat> where TStat : struct, Enum
{
	private const int BasisPointsPerWhole = 10_000;
	private readonly Dictionary<TStat, int> _baseValues;
	private readonly Dictionary<TStat, int> _permanentGrowth = new();
	private readonly Dictionary<TStat, List<StatModifier>> _modifiers = new();
	private Dictionary<TStat, List<StatModifier>> _equipment = new();

	public event Action<TStat, int, int>? ValueChanged;

	protected StatCollection(IReadOnlyDictionary<TStat, int> baseValues)
	{
		ArgumentNullException.ThrowIfNull(baseValues);
		_baseValues = new Dictionary<TStat, int>(baseValues);
	}

	public int GetBaseValue(TStat stat)
	{
		return _baseValues.GetValueOrDefault(stat);
	}

	public int GetPermanentGrowth(TStat stat)
	{
		return _permanentGrowth.GetValueOrDefault(stat);
	}

	public int GetValue(TStat stat)
		=> Calculate(stat, _equipment.GetValueOrDefault(stat) ?? new());

	internal int PreviewEquipment(TStat stat, IEnumerable<StatModifier> modifiers)
		=> Calculate(stat, modifiers);

	internal void SetEquipment(Dictionary<TStat, List<StatModifier>> equipment)
	{
		var oldValues = Enum.GetValues<TStat>().ToDictionary(stat => stat, GetValue);
		foreach (TStat stat in oldValues.Keys)
			_ = Calculate(stat, equipment.GetValueOrDefault(stat) ?? new());
		_equipment = equipment;
		foreach ((TStat stat, int oldValue) in oldValues)
			NotifyIfChanged(stat, oldValue);
	}

	private int Calculate(TStat stat, IEnumerable<StatModifier> equipment)
	{
		int baseWithGrowth = checked(GetBaseValue(stat) + GetPermanentGrowth(stat));
		List<StatModifier> modifiers = (_modifiers.GetValueOrDefault(stat) ?? new()).Concat(equipment).ToList();
		if (modifiers.Count == 0)
		{
			ValidateValue(stat, baseWithGrowth);
			return baseWithGrowth;
		}

		int flat = modifiers
			.Where(modifier => modifier.Operation == StatModifierOperation.Flat)
			.Sum(modifier => modifier.Value);
		int value = checked(baseWithGrowth + flat);

		int increased = modifiers
			.Where(modifier => modifier.Operation == StatModifierOperation.Increased)
			.Sum(modifier => modifier.Value);
		value = ScaleAndRound(value, checked(BasisPointsPerWhole + increased));

		foreach (StatModifier modifier in modifiers
			.Where(modifier => modifier.Operation == StatModifierOperation.More)
			.OrderBy(modifier => modifier.SourceId, StringComparer.Ordinal)
			.ThenBy(modifier => modifier.Value))
		{
			value = ScaleAndRound(value, checked(BasisPointsPerWhole + modifier.Value));
		}

		StatModifier? overrideModifier = modifiers.SingleOrDefault(
			modifier => modifier.Operation == StatModifierOperation.Override);
		if (overrideModifier is not null)
		{
			value = overrideModifier.Value;
		}

		int? minimum = modifiers
			.Where(modifier => modifier.Operation == StatModifierOperation.Minimum)
			.Select(modifier => (int?)modifier.Value)
			.Max();
		int? maximum = modifiers
			.Where(modifier => modifier.Operation == StatModifierOperation.Maximum)
			.Select(modifier => (int?)modifier.Value)
			.Min();
		if (minimum > maximum)
		{
			throw new InvalidOperationException($"Conflicting bounds for stat {stat}.");
		}

		if (minimum.HasValue)
		{
			value = Math.Max(value, minimum.Value);
		}

		if (maximum.HasValue)
		{
			value = Math.Min(value, maximum.Value);
		}

		ValidateValue(stat, value);
		return value;
	}

	public void IncreasePermanent(TStat stat, int amount)
	{
		if (amount < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(amount));
		}

		int oldValue = GetValue(stat);
		_permanentGrowth[stat] = checked(GetPermanentGrowth(stat) + amount);
		NotifyIfChanged(stat, oldValue);
	}

	public void AddModifier(TStat stat, StatModifier modifier)
	{
		ArgumentNullException.ThrowIfNull(modifier);
		int oldValue = GetValue(stat);
		List<StatModifier> modifiers = GetOrCreateModifiers(stat);
		if (modifier.Operation == StatModifierOperation.Override
			&& modifiers.Any(existing => existing.Operation == StatModifierOperation.Override))
		{
			throw new InvalidOperationException($"Stat {stat} already has an override modifier.");
		}

		modifiers.Add(modifier);
		try
		{
			NotifyIfChanged(stat, oldValue);
		}
		catch
		{
			modifiers.Remove(modifier);
			if (modifiers.Count == 0)
			{
				_modifiers.Remove(stat);
			}

			throw;
		}
	}

	public int RemoveModifiersFromSource(string sourceId)
	{
		if (string.IsNullOrWhiteSpace(sourceId))
		{
			throw new ArgumentException("Modifier source cannot be empty.", nameof(sourceId));
		}

		Dictionary<TStat, (int OldValue, List<StatModifier> Removed)> changes = new();
		foreach ((TStat stat, List<StatModifier> modifiers) in _modifiers)
		{
			List<StatModifier> matching = modifiers
				.Where(modifier => modifier.SourceId == sourceId)
				.ToList();
			if (matching.Count > 0)
			{
				changes.Add(stat, (GetValue(stat), matching));
			}
		}

		foreach ((TStat stat, (_, List<StatModifier> matching)) in changes)
		{
			_modifiers[stat].RemoveAll(modifier => matching.Contains(modifier));
		}

		try
		{
			foreach (TStat stat in changes.Keys)
			{
				_ = GetValue(stat);
			}
		}
		catch
		{
			foreach ((TStat stat, (_, List<StatModifier> matching)) in changes)
			{
				_modifiers[stat].AddRange(matching);
			}

			throw;
		}

		foreach ((TStat stat, (int oldValue, _)) in changes)
		{
			if (_modifiers[stat].Count == 0)
			{
				_modifiers.Remove(stat);
			}

			NotifyIfChanged(stat, oldValue);
		}

		return changes.Values.Sum(change => change.Removed.Count);
	}

	private List<StatModifier> GetOrCreateModifiers(TStat stat)
	{
		if (!_modifiers.TryGetValue(stat, out List<StatModifier>? modifiers))
		{
			modifiers = new List<StatModifier>();
			_modifiers.Add(stat, modifiers);
		}

		return modifiers;
	}

	private void NotifyIfChanged(TStat stat, int oldValue)
	{
		int newValue = GetValue(stat);
		if (newValue != oldValue)
		{
			ValueChanged?.Invoke(stat, oldValue, newValue);
		}
	}

	private static int ScaleAndRound(int value, int factorBasisPoints)
	{
		decimal scaled = (decimal)value * factorBasisPoints / BasisPointsPerWhole;
		return checked((int)decimal.Round(scaled, 0, MidpointRounding.AwayFromZero));
	}

	protected virtual void ValidateValue(TStat stat, int value)
	{
		_ = stat;
		_ = value;
	}
}
