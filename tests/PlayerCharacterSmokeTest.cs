using CmdRoguelike.Core;
using CmdRoguelike.Domain.Entities;
using CmdRoguelike.Domain.Stats;
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
			AssertHeroStatsAreCalculatedFromSourcedModifiers();
			AssertResourceMaximumChangesDoNotHeal();
			GD.Print("Player character smoke test passed.");
			GetTree().Quit(0);
		}
		catch (Exception exception)
		{
			GD.PushError(exception.ToString());
			GetTree().Quit(1);
		}
	}

	private static void AssertHeroStatsAreCalculatedFromSourcedModifiers()
	{
		PlayerCharacter player = new(Vector2I.Zero);
		if (Enum.GetValues<AttributeId>().Any(attribute => player.Attributes.GetValue(attribute) != 0))
		{
			throw new InvalidOperationException("Unconfigured hero attributes must remain neutral.");
		}

		player.Attributes.IncreasePermanent(AttributeId.Strength, 2);
		player.Attributes.AddModifier(
			AttributeId.Strength,
			new StatModifier("training-ring", StatModifierOperation.Flat, 3));
		player.Attributes.AddModifier(
			AttributeId.Strength,
			new StatModifier("blessing", StatModifierOperation.Increased, 5_000));
		player.Attributes.AddModifier(
			AttributeId.Strength,
			new StatModifier("battle-stance", StatModifierOperation.More, 20_000));

		if (player.Attributes.GetValue(AttributeId.Strength) != 24)
		{
			throw new InvalidOperationException("Attribute modifier stages or rounding are incorrect.");
		}

		if (player.Attributes.RemoveModifiersFromSource("training-ring") != 1
			|| player.Attributes.GetValue(AttributeId.Strength) != 9)
		{
			throw new InvalidOperationException("Removing a modifier source produced an invalid attribute value.");
		}
	}

	private static void AssertResourceMaximumChangesDoNotHeal()
	{
		PlayerCharacter player = new(Vector2I.Zero);
		player.TakeDamage(4);
		player.DerivedStats.AddModifier(
			DerivedStatId.MaxHealth,
			new StatModifier("test-amulet", StatModifierOperation.Flat, 5));

		if (player.MaxHealth != 15 || player.Health != 6)
		{
			throw new InvalidOperationException("Increasing maximum health unexpectedly healed the hero.");
		}

		player.RestoreHealth(100);
		player.DerivedStats.RemoveModifiersFromSource("test-amulet");
		if (player.MaxHealth != 10 || player.Health != 10)
		{
			throw new InvalidOperationException("Health was not clamped after maximum health decreased.");
		}

		bool rejectedInvalidMaximum = false;
		try
		{
			player.DerivedStats.AddModifier(
				DerivedStatId.MaxHealth,
				new StatModifier("invalid-curse", StatModifierOperation.Override, 0));
		}
		catch (InvalidOperationException)
		{
			rejectedInvalidMaximum = true;
		}

		if (!rejectedInvalidMaximum || player.MaxHealth != 10 || player.Health != 10)
		{
			throw new InvalidOperationException("An invalid maximum left the hero resources corrupted.");
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
