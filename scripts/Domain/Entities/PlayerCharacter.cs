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

	public PlayerCharacter(Vector2I position)
		: base(position, DefaultName, DefaultMaxHealth)
	{
	}
}
