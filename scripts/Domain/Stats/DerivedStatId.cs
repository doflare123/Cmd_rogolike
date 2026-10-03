namespace CmdRoguelike.Domain.Stats;

/// <summary>
/// Вычисляемые значения актора. Новые формулы добавляются отдельно от хранения
/// текущих ресурсов, поэтому изменение максимума само по себе не лечит актора.
/// </summary>
public enum DerivedStatId
{
	MaxHealth,
	MaxMana,
	Armor,
	MaxActionPoints,
}
