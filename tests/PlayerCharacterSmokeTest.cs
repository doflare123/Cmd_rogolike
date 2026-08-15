using CmdRoguelike.Core;
using CmdRoguelike.Domain.Entities;
using CmdRoguelike.State;
using CmdRoguelike.World;
using Godot;

namespace CmdRoguelike.Tests;

public partial class PlayerCharacterSmokeTest : Node
{
	public override void _Ready()
	{
		try
		{
			AssertPlayerIsRegisteredInWorld();
			AssertWorldMovementUpdatesEntityIndex();
			AssertRegistryMoveIsAtomic();
			GD.Print("Player character smoke test passed.");
			GetTree().Quit(0);
		}
		catch (Exception exception)
		{
			GD.PushError(exception.ToString());
			GetTree().Quit(1);
		}
	}

	private static void AssertPlayerIsRegisteredInWorld()
	{
		DungeonMap map = new(seed: 1701, minimumDoorsPerRoom: 3);
		PlayerCharacter player = map.Player;

		if (player.Position != map.PlayerStart)
		{
			throw new InvalidOperationException("Player position differs from the world spawn point.");
		}

		if (map.GetTile(player.Position) != DungeonTile.Floor)
		{
			throw new InvalidOperationException("Player was not registered on a floor cell.");
		}

		if (!ReferenceEquals(map.GetEntityAt(player.Position), player))
		{
			throw new InvalidOperationException("Player is missing from the positional entity index.");
		}

		if (map.GetEntities().Count(entity => entity is PlayerCharacter) != 1)
		{
			throw new InvalidOperationException("World must contain exactly one player character.");
		}

		if (map.CanEnter(player.Position))
		{
			throw new InvalidOperationException("A living player must block their occupied cell.");
		}
	}

	private static void AssertWorldMovementUpdatesEntityIndex()
	{
		DungeonMap map = new(seed: 1701, minimumDoorsPerRoom: 3);
		PlayerCharacter player = map.Player;
		Vector2I origin = player.Position;
		CardinalDirection? direction = null;
		foreach (CardinalDirection candidate in CardinalDirectionExtensions.All)
		{
			Vector2I candidateDestination = origin + candidate.ToOffset();
			if (map.GetTile(candidateDestination) == DungeonTile.Floor
				&& map.GetEntityAt(candidateDestination) is null)
			{
				direction = candidate;
				break;
			}
		}

		if (direction is null)
		{
			throw new InvalidOperationException("Test seed has no free floor next to the player spawn.");
		}

		Vector2I destination = origin + direction.Value.ToOffset();
		PlayerMoveResult result = map.TryMovePlayer(direction.Value);

		if (result.Outcome != PlayerMoveOutcome.Moved
			|| result.Origin != origin
			|| result.Destination != destination)
		{
			throw new InvalidOperationException("World did not report the expected player movement.");
		}

		if (player.Position != destination
			|| map.GetEntityAt(origin) is not null
			|| !ReferenceEquals(map.GetEntityAt(destination), player))
		{
			throw new InvalidOperationException("Player movement did not atomically update the positional index.");
		}

		player.TakeDamage(player.MaxHealth);
		PlayerMoveResult deadMove = map.TryMovePlayer(direction.Value);
		if (deadMove.Outcome != PlayerMoveOutcome.PlayerIsDead
			|| player.Position != destination
			|| !ReferenceEquals(map.GetEntityAt(destination), player))
		{
			throw new InvalidOperationException("A dead player moved or corrupted the positional index.");
		}

		PlayerDoorInteractionResult deadDoorInteraction = map.TryOpenAdjacentDoor();
		if (deadDoorInteraction.Outcome != PlayerDoorInteractionOutcome.PlayerIsDead)
		{
			throw new InvalidOperationException("A dead player was allowed to issue a door interaction.");
		}
	}

	private static void AssertRegistryMoveIsAtomic()
	{
		EntityRegistry registry = new();
		PlayerCharacter player = new(Vector2I.Zero);
		BasicEnemy blocker = new(Vector2I.Right);
		registry.Add(player);
		registry.Add(blocker);

		bool rejected = false;
		try
		{
			registry.Move(player, blocker.Position);
		}
		catch (InvalidOperationException)
		{
			rejected = true;
		}

		if (!rejected)
		{
			throw new InvalidOperationException("Moving into an occupied cell unexpectedly succeeded.");
		}

		if (player.Position != Vector2I.Zero
			|| !ReferenceEquals(GetRequiredEntity(registry, Vector2I.Zero), player)
			|| !ReferenceEquals(GetRequiredEntity(registry, Vector2I.Right), blocker))
		{
			throw new InvalidOperationException("Rejected movement partially changed the entity registry.");
		}

		registry.Move(player, Vector2I.Down);
		if (registry.TryGetAt(Vector2I.Zero, out _)
			|| !ReferenceEquals(GetRequiredEntity(registry, Vector2I.Down), player))
		{
			throw new InvalidOperationException("Successful movement left a stale positional index entry.");
		}
	}

	private static DungeonEntity GetRequiredEntity(EntityRegistry registry, Vector2I position)
	{
		return registry.TryGetAt(position, out DungeonEntity? entity)
			? entity!
			: throw new InvalidOperationException($"Expected an entity at {position}.");
	}
}
