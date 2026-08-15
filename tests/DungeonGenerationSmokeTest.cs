using CmdRoguelike.Core;
using CmdRoguelike.Domain;
using CmdRoguelike.Domain.Entities;
using CmdRoguelike.World;
using Godot;

namespace CmdRoguelike.Tests;

public partial class DungeonGenerationSmokeTest : Node
{
	private const int SeedCount = 100;
	private const int RegionsPerSeed = 200;

	public override void _Ready()
	{
		try
		{
			int generatedEnemies = 0;
			for (int seed = 1; seed <= SeedCount; seed++)
			{
				generatedEnemies += CheckSeed(seed);
			}

			if (generatedEnemies == 0)
			{
				throw new InvalidOperationException("Enemy generation did not create any enemies.");
			}

			GD.Print(
				$"Dungeon smoke test passed: {SeedCount} seeds, "
				+ $"{RegionsPerSeed} regions each, {generatedEnemies} enemies.");
			GetTree().Quit(0);
		}
		catch (Exception exception)
		{
			GD.PushError(exception.ToString());
			GetTree().Quit(1);
		}
	}

	private static int CheckSeed(int seed)
	{
		DungeonMap map = new(seed, 3);
		PlayerCharacter player = map.Player;
		if (map.GetTile(map.PlayerStart) != DungeonTile.Floor)
		{
			throw new InvalidOperationException($"Seed {seed}: player did not spawn on a floor cell.");
		}

		if (player.Position != map.PlayerStart
			|| !ReferenceEquals(map.GetEntityAt(player.Position), player))
		{
			throw new InvalidOperationException($"Seed {seed}: player is not registered at the spawn point.");
		}

		if (map.EnemyCount != 0)
		{
			throw new InvalidOperationException($"Seed {seed}: starting room is not safe.");
		}

		int attempts = 0;
		while (map.RegionCount < RegionsPerSeed && attempts < RegionsPerSeed * 20)
		{
			IReadOnlyList<Vector2I> doors = map.GetClosedDoorPositions();
			if (doors.Count == 0)
			{
				throw new InvalidOperationException($"Seed {seed}: generation ran out of doors.");
			}

			Vector2I door = doors[attempts % doors.Count];
			DoorExpansion? expansion = map.OpenDoor(door);
			if (expansion is null || map.GetTile(door) != DungeonTile.OpenDoor)
			{
				throw new InvalidOperationException($"Seed {seed}: door {door} did not open.");
			}

			if (!map.HasPassageOnBothSides(door))
			{
				throw new InvalidOperationException(
					$"Seed {seed}: door {door} leads into a wall; {map.DescribeDoorNeighborhood(door)}.");
			}

			if (!map.HasWallFrameAcrossDoor(door))
			{
				throw new InvalidOperationException(
					$"Seed {seed}: door {door} is not embedded in a wall; {map.DescribeDoorNeighborhood(door)}.");
			}

			attempts++;
		}

		if (map.RegionCount < RegionsPerSeed)
		{
			throw new InvalidOperationException(
				$"Seed {seed}: created only {map.RegionCount} regions after {attempts} door openings.");
		}

		if (map.PopulatedRoomCount >= map.RoomCount)
		{
			throw new InvalidOperationException($"Seed {seed}: every room was populated.");
		}

		AssertFloorHasNoHoles(map, seed);
		AssertAllWalkableTilesAreConnected(map, seed);

		HashSet<Vector2I> occupiedCells = new();
		int playerCount = 0;
		foreach (DungeonEntity entity in map.GetEntities())
		{
			if (!occupiedCells.Add(entity.Position))
			{
				throw new InvalidOperationException($"Seed {seed}: duplicate entity cell {entity.Position}.");
			}

			switch (entity)
			{
				case PlayerCharacter registeredPlayer:
					playerCount++;
					if (!ReferenceEquals(registeredPlayer, player)
						|| registeredPlayer.Position != map.PlayerStart)
					{
						throw new InvalidOperationException($"Seed {seed}: unexpected player registration.");
					}
					break;
				case BasicEnemy:
					break;
				default:
					throw new InvalidOperationException(
						$"Seed {seed}: unexpected entity type {entity.GetType().Name}.");
			}

			if (map.GetTile(entity.Position) != DungeonTile.Floor || map.CanEnter(entity.Position))
			{
				throw new InvalidOperationException($"Seed {seed}: invalid entity cell {entity.Position}.");
			}
		}

		if (playerCount != 1)
		{
			throw new InvalidOperationException($"Seed {seed}: expected one player, found {playerCount}.");
		}

		return map.EnemyCount;
	}

	private static void AssertAllWalkableTilesAreConnected(DungeonMap map, int seed)
	{
		HashSet<Vector2I> walkable = map.GetKnownTilePositions()
			.Where(map.IsPotentiallyTraversable)
			.ToHashSet();
		HashSet<Vector2I> reached = new() { map.PlayerStart };
		Queue<Vector2I> frontier = new();
		frontier.Enqueue(map.PlayerStart);

		while (frontier.Count > 0)
		{
			Vector2I current = frontier.Dequeue();
			foreach (Vector2I offset in new[] { Vector2I.Up, Vector2I.Right, Vector2I.Down, Vector2I.Left })
			{
				Vector2I next = current + offset;
				if (walkable.Contains(next) && reached.Add(next))
				{
					frontier.Enqueue(next);
				}
			}
		}

		if (reached.Count != walkable.Count)
		{
			Vector2I unreachable = walkable.First(position => !reached.Contains(position));
			throw new InvalidOperationException(
				$"Seed {seed}: walkable area at {unreachable} is disconnected from player start; "
				+ $"reached {reached.Count} of {walkable.Count} tiles.");
		}
	}

	private static void AssertFloorHasNoHoles(DungeonMap map, int seed)
	{
		Vector2I[] offsets =
		{
			Vector2I.Up,
			Vector2I.Right,
			Vector2I.Down,
			Vector2I.Left,
		};

		foreach (Vector2I position in map.GetKnownTilePositions())
		{
			if (map.GetTile(position) != DungeonTile.Floor)
			{
				continue;
			}

			foreach (Vector2I offset in offsets)
			{
				if (map.GetTile(position + offset) == DungeonTile.Empty)
				{
					throw new InvalidOperationException(
						$"Seed {seed}: floor {position} has a hole at {position + offset}.");
				}
			}
		}
	}
}
