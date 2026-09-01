namespace CmdRoguelike.Domain.Stats;

/// <summary>
/// Восемь базовых характеристик актора. Нулевые значения по умолчанию означают,
/// что стартовый баланс ещё не распределён расой или архетипом.
/// </summary>
public sealed class AttributeSet : StatCollection<AttributeId>
{
	public AttributeSet(IReadOnlyDictionary<AttributeId, int>? baseValues = null)
		: base(CreateBaseValues(baseValues))
	{
	}

	private static IReadOnlyDictionary<AttributeId, int> CreateBaseValues(
		IReadOnlyDictionary<AttributeId, int>? configuredValues)
	{
		Dictionary<AttributeId, int> values = Enum
			.GetValues<AttributeId>()
			.ToDictionary(attribute => attribute, _ => 0);
		if (configuredValues is not null)
		{
			foreach ((AttributeId attribute, int value) in configuredValues)
			{
				values[attribute] = value;
			}
		}

		return values;
	}
}
