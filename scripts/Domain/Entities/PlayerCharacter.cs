using CmdRoguelike.Domain.Stats;
using Godot;

namespace CmdRoguelike.Domain.Entities;

/// <summary>
/// Управляемый игроком актор. Пространственное положение персонажа изменяется
/// только через реестр сущностей мира, а не напрямую из Presentation.
/// </summary>
public sealed class PlayerCharacter : Actor
{
	public const string DefaultName = "Hero";
	public const int DefaultMaxHealth = 10;
	public const int DefaultMaxMana = 0;

	public PlayerCharacter(
		Vector2I position,
		IReadOnlyDictionary<AttributeId, int>? baseAttributes = null)
		: base(position, DefaultName, DefaultMaxHealth, DefaultMaxMana, baseAttributes)
	{
	}
}
