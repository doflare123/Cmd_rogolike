using CmdRoguelike.Domain;
using Godot;

namespace CmdRoguelike.World;

public enum PlayerDoorInteractionOutcome
{
	OpenedDoor,
	NoAdjacentDoor,
	PlayerIsDead,
}

/// <summary>
/// Результат явной команды взаимодействия с соседней дверью. Данные расширения
/// присутствуют только при успешном открытии.
/// </summary>
public sealed class PlayerDoorInteractionResult
{
	public PlayerDoorInteractionOutcome Outcome { get; }
	public Vector2I PlayerPosition { get; }
	public Vector2I? DoorPosition { get; }
	public DoorExpansion? DoorExpansion { get; }

	private PlayerDoorInteractionResult(
		PlayerDoorInteractionOutcome outcome,
		Vector2I playerPosition,
		Vector2I? doorPosition = null,
		DoorExpansion? doorExpansion = null)
	{
		Outcome = outcome;
		PlayerPosition = playerPosition;
		DoorPosition = doorPosition;
		DoorExpansion = doorExpansion;
	}

	internal static PlayerDoorInteractionResult OpenedDoor(
		Vector2I playerPosition,
		Vector2I doorPosition,
		DoorExpansion expansion)
	{
		return new PlayerDoorInteractionResult(
			PlayerDoorInteractionOutcome.OpenedDoor,
			playerPosition,
			doorPosition,
			expansion);
	}

	internal static PlayerDoorInteractionResult NoAdjacentDoor(Vector2I playerPosition)
	{
		return new PlayerDoorInteractionResult(
			PlayerDoorInteractionOutcome.NoAdjacentDoor,
			playerPosition);
	}

	internal static PlayerDoorInteractionResult PlayerIsDead(Vector2I playerPosition)
	{
		return new PlayerDoorInteractionResult(
			PlayerDoorInteractionOutcome.PlayerIsDead,
			playerPosition);
	}
}
