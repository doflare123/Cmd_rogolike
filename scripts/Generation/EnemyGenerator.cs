using CmdRoguelike.Core;
using CmdRoguelike.Domain;
using CmdRoguelike.Domain.Entities;
using CmdRoguelike.State;
using Godot;

namespace CmdRoguelike.Generation;

/// <summary>
/// Заселяет врагами часть готовых комнат. Не владеет сущностями
/// и ничего не знает об отрисовке или вводе игрока.
/// </summary>
internal sealed class EnemyGenerator
{
	private const int MinimumDistanceFromDoor = 3;

	private readonly DungeonGenerationOptions _options;
	private readonly IRandomSource _random;
	private readonly DungeonGrid _grid;
	private readonly DoorRegistry _doors;
	private readonly EntityRegistry _entities;
	private readonly IEnemyFactory _enemyFactory;

	public EnemyGenerator(
		DungeonGenerationOptions options,
		IRandomSource random,
		DungeonGrid grid,
		DoorRegistry doors,
		EntityRegistry entities,
		IEnemyFactory enemyFactory)
	{
		_options = options;
		_random = random;
		_grid = grid;
		_doors = doors;
		_entities = entities;
		_enemyFactory = enemyFactory;
	}

	public int Populate(DungeonRegion region, bool isSafeRegion)
	{
		if (isSafeRegion
			|| region.Kind != DungeonRegionKind.Room
			|| _random.NextFloat() >= _options.EnemyRoomChance)
		{
			return 0;
		}

		List<Vector2I> candidates = FindSpawnCandidates(region);
		Shuffle(candidates);
		int desiredCount = _random.NextInt(
			_options.MinimumEnemiesPerRoom,
			_options.MaximumEnemiesPerRoom);
		int spawnCount = Math.Min(desiredCount, candidates.Count);

		for (int index = 0; index < spawnCount; index++)
		{
			_entities.Add(_enemyFactory.Create(candidates[index]));
		}

		return spawnCount;
	}

	private List<Vector2I> FindSpawnCandidates(DungeonRegion region)
	{
		List<Vector2I> doorPositions = _doors.Entries
			.Where(entry => entry.Value.RegionSector == region.Sector)
			.Select(entry => entry.Key)
			.ToList();

		return region.Floors
			.Where(position => _grid[position] == DungeonTile.Floor)
			.Where(position => !_entities.IsOccupied(position))
			.Where(position => position.DistanceTo(region.Anchor) >= 2.0f)
			.Where(position => doorPositions.All(door => ManhattanDistance(position, door) >= MinimumDistanceFromDoor))
			.ToList();
	}

	private static int ManhattanDistance(Vector2I left, Vector2I right)
	{
		return Math.Abs(left.X - right.X) + Math.Abs(left.Y - right.Y);
	}

	private void Shuffle<T>(IList<T> values)
	{
		for (int index = values.Count - 1; index > 0; index--)
		{
			int other = _random.NextInt(0, index);
			(values[index], values[other]) = (values[other], values[index]);
		}
	}
}
