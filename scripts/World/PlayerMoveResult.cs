using CmdRoguelike.Core;
using CmdRoguelike.Domain;
using CmdRoguelike.Domain.Entities;
using Godot;

namespace CmdRoguelike.World;

public enum PlayerMoveOutcome
{
	Moved,
	OpenedDoor,
	BlockedByTerrain,
	BlockedByEntity,
	PlayerIsDead,
}

/// <summary>
/// Полный результат одной команды движения. Фабричные методы не позволяют
/// создать результат с противоречивыми данными о препятствии или двери.
/// </summary>
public sealed class PlayerMoveResult
{
	public PlayerMoveOutcome Outcome { get; }
	public Vector2I Origin { get; }
	public Vector2I Destination { get; }
	public DungeonTile? BlockingTile { get; }
	public DungeonEntity? BlockingEntity { get; }
	public DoorExpansion? DoorExpansion { get; }

	private PlayerMoveResult(
		PlayerMoveOutcome outcome,
		Vector2I origin,
		Vector2I destination,
		DungeonTile? blockingTile = null,
		DungeonEntity? blockingEntity = null,
		DoorExpansion? doorExpansion = null)
	{
		Outcome = outcome;
		Origin = origin;
		Destination = destination;
		BlockingTile = blockingTile;
		BlockingEntity = blockingEntity;
		DoorExpansion = doorExpansion;
	}

	internal static PlayerMoveResult Moved(Vector2I origin, Vector2I destination)
	{
		return new PlayerMoveResult(PlayerMoveOutcome.Moved, origin, destination);
	}

	internal static PlayerMoveResult OpenedDoor(
		Vector2I origin,
		Vector2I destination,
		DoorExpansion expansion)
	{
		ArgumentNullException.ThrowIfNull(expansion);
		return new PlayerMoveResult(
			PlayerMoveOutcome.OpenedDoor,
			origin,
			destination,
			doorExpansion: expansion);
	}

	internal static PlayerMoveResult BlockedByTerrain(
		Vector2I origin,
		Vector2I destination,
		DungeonTile tile)
	{
		return new PlayerMoveResult(
			PlayerMoveOutcome.BlockedByTerrain,
			origin,
			destination,
			blockingTile: tile);
	}

	internal static PlayerMoveResult BlockedByEntity(
		Vector2I origin,
		Vector2I destination,
		DungeonEntity entity)
	{
		ArgumentNullException.ThrowIfNull(entity);
		return new PlayerMoveResult(
			PlayerMoveOutcome.BlockedByEntity,
			origin,
			destination,
			blockingEntity: entity);
	}

	internal static PlayerMoveResult PlayerIsDead(Vector2I position)
	{
		return new PlayerMoveResult(
			PlayerMoveOutcome.PlayerIsDead,
			position,
			position);
	}
}
