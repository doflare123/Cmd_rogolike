namespace CmdRoguelike.Domain.Stats;

public enum StatModifierOperation
{
	Flat,
	Increased,
	More,
	Minimum,
	Maximum,
	Override,
}

/// <summary>
/// Один вклад в характеристику. Для процентов Value хранится в basis points:
/// 10000 означает 100%.
/// </summary>
public sealed record StatModifier
{
	public string SourceId { get; }
	public StatModifierOperation Operation { get; }
	public int Value { get; }

	public StatModifier(string sourceId, StatModifierOperation operation, int value)
	{
		if (string.IsNullOrWhiteSpace(sourceId))
		{
			throw new ArgumentException("A stat modifier must identify its source.", nameof(sourceId));
		}

		SourceId = sourceId;
		Operation = operation;
		Value = value;
	}
}
