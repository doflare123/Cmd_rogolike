using CmdRoguelike.Core;
using CmdRoguelike.Domain;
using CmdRoguelike.Domain.Entities;
using CmdRoguelike.Generation;
using CmdRoguelike.State;
using Godot;

namespace CmdRoguelike.World;

/// <summary>
/// Публичный фасад раскрытой части подземелья. Владеет состоянием мира
/// и координирует ленивое расширение, а решения о геометрии принимает RegionGenerator.
/// </summary>
public sealed class DungeonMap
{
	private readonly DungeonGrid _grid = new();
	private readonly DoorRegistry _doors = new();
	private readonly EntityRegistry _entities = new();
	private readonly Dictionary<Vector2I, DungeonRegion> _regions = new();
	private readonly RegionGenerator _regionGenerator;
	private readonly EnemyGenerator _enemyGenerator;

	public int Seed { get; }
	public int RegionCount => _regions.Count;
	public int RoomCount => _regions.Values.Count(region => region.Kind == DungeonRegionKind.Room);
	public int OpenedDoorCount { get; private set; }
	public int EnemyCount => _entities.All.Count(entity => entity is Enemy);
	public int PopulatedRoomCount { get; private set; }
	public Vector2I PlayerStart { get; }
	public PlayerCharacter Player { get; }

	public DungeonMap(int seed, int minimumDoorsPerRoom = 3)
		: this(seed, new DungeonGenerationOptions(minimumDoorsPerRoom))
	{
	}

	public DungeonMap(int seed, DungeonGenerationOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		Seed = seed;
		IRandomSource random = new GodotRandomSource(seed);
		_regionGenerator = new RegionGenerator(options, random, _grid, _doors);
		_enemyGenerator = new EnemyGenerator(
			options,
			random,
			_grid,
			_doors,
			_entities,
			new EnemyFactory());

		DungeonRegion firstRegion = _regionGenerator.Generate(
			Vector2I.Zero,
			requiredEntrance: null,
			forceRoom: true);
		_regions.Add(firstRegion.Sector, firstRegion);
		PlayerStart = _regionGenerator.PickFloorCell(firstRegion);
		Player = new PlayerCharacter(PlayerStart);
		_entities.Add(Player);
		_enemyGenerator.Populate(firstRegion, isSafeRegion: true);
	}

	public DungeonTile GetTile(Vector2I position)
	{
		return _grid[position];
	}

	public bool IsWalkable(Vector2I position)
	{
		return _grid.IsWalkable(position);
	}

	public bool IsPotentiallyTraversable(Vector2I position)
	{
		return GetTile(position) is
			DungeonTile.Floor or DungeonTile.OpenDoor or DungeonTile.ClosedDoor;
	}

	public bool CanEnter(Vector2I position)
	{
		return _grid.IsWalkable(position) && !_entities.IsOccupied(position);
	}

	/// <summary>
	/// Выполняет одну команду перемещения игрока. Проверка рельефа, столкновений,
	/// открытие двери при упоре и атомарное изменение позиции остаются в World.
	/// </summary>
	public PlayerMoveResult TryMovePlayer(CardinalDirection direction)
	{
		Vector2I origin = Player.Position;
		Vector2I destination = origin + direction.ToOffset();

		if (!Player.IsAlive)
		{
			return PlayerMoveResult.PlayerIsDead(origin);
		}

		DungeonTile tile = GetTile(destination);
		if (tile == DungeonTile.ClosedDoor)
		{
			DoorExpansion expansion = OpenDoor(destination)
				?? throw new InvalidOperationException(
					$"Closed door at {destination} is missing from the door registry.");
			return PlayerMoveResult.OpenedDoor(origin, destination, expansion);
		}

		if (!_grid.IsWalkable(destination))
		{
			return PlayerMoveResult.BlockedByTerrain(origin, destination, tile);
		}

		if (_entities.TryGetAt(destination, out DungeonEntity? blockingEntity))
		{
			return PlayerMoveResult.BlockedByEntity(origin, destination, blockingEntity!);
		}

		_entities.Move(Player, destination);
		return PlayerMoveResult.Moved(origin, destination);
	}

	public PlayerDoorInteractionResult TryOpenAdjacentDoor()
	{
		Vector2I playerPosition = Player.Position;
		if (!Player.IsAlive)
		{
			return PlayerDoorInteractionResult.PlayerIsDead(playerPosition);
		}

		foreach (CardinalDirection direction in CardinalDirectionExtensions.All)
		{
			Vector2I position = playerPosition + direction.ToOffset();
			if (GetTile(position) == DungeonTile.ClosedDoor)
			{
				DoorExpansion expansion = OpenDoor(position)
					?? throw new InvalidOperationException(
						$"Closed door at {position} is missing from the door registry.");
				return PlayerDoorInteractionResult.OpenedDoor(
					playerPosition,
					position,
					expansion);
			}
		}

		return PlayerDoorInteractionResult.NoAdjacentDoor(playerPosition);
	}

	public DungeonEntity? GetEntityAt(Vector2I position)
	{
		return _entities.TryGetAt(position, out DungeonEntity? entity)
			? entity
			: null;
	}

	public IReadOnlyCollection<DungeonEntity> GetEntities()
	{
		return _entities.All.ToArray();
	}

	public IReadOnlyCollection<Vector2I> GetKnownTilePositions()
	{
		return _grid.KnownPositions.ToArray();
	}

	public IReadOnlyList<Vector2I> GetClosedDoorPositions()
	{
		return _doors.FindClosedDoors(_grid);
	}

	public bool HasPassageOnBothSides(Vector2I position)
	{
		if (!_doors.TryGet(position, out DungeonDoor door))
		{
			return false;
		}

		Vector2I step = door.Direction.ToOffset();
		return IsWalkable(position - step) && IsWalkable(position + step);
	}

	public bool HasWallFrameAcrossDoor(Vector2I position)
	{
		if (!_doors.TryGet(position, out DungeonDoor door))
		{
			return false;
		}

		Vector2I axis = door.Direction.ToOffset();
		Vector2I perpendicular = new(-axis.Y, axis.X);
		return GetTile(position - perpendicular) == DungeonTile.Wall
			&& GetTile(position + perpendicular) == DungeonTile.Wall;
	}

	public string DescribeDoorNeighborhood(Vector2I position)
	{
		if (!_doors.TryGet(position, out DungeonDoor door))
		{
			return "unregistered door";
		}

		Vector2I axis = door.Direction.ToOffset();
		Vector2I perpendicular = new(-axis.Y, axis.X);
		return $"internal={door.IsInternal}, direction={door.Direction}, "
			+ $"back={GetTile(position - axis)}, front={GetTile(position + axis)}, "
			+ $"sideA={GetTile(position - perpendicular)}, sideB={GetTile(position + perpendicular)}";
	}

	/// <summary>
	/// Открывает внутреннюю дверь либо создаёт/соединяет область за внешней дверью.
	/// Возвращает null, если указанная клетка не является закрытой дверью.
	/// </summary>
	public DoorExpansion? OpenDoor(Vector2I position)
	{
		if (_grid[position] != DungeonTile.ClosedDoor
			|| !_doors.TryGet(position, out DungeonDoor door))
		{
			return null;
		}

		if (door.IsInternal)
		{
			_grid.SetDoorState(position, DungeonTile.OpenDoor);
			OpenedDoorCount++;
			return new DoorExpansion(false, true, null);
		}

		DungeonRegion sourceRegion = _regions[door.RegionSector];
		Vector2I targetSector = door.RegionSector + door.Direction.ToOffset();
		bool createdRegion = !_regions.TryGetValue(targetSector, out DungeonRegion? targetRegion);

		if (createdRegion)
		{
			targetRegion = _regionGenerator.Generate(
				targetSector,
				door.Direction.Opposite());
			_regions.Add(targetSector, targetRegion);
		}
		else
		{
			_regionGenerator.EnsureExternalDoor(
				targetRegion!,
				door.Direction.Opposite());
		}

		_grid.SetDoorState(position, DungeonTile.OpenDoor);

		if (createdRegion)
		{
			int spawnedEnemies = _enemyGenerator.Populate(targetRegion!, isSafeRegion: false);
			if (spawnedEnemies > 0)
			{
				PopulatedRoomCount++;
			}
		}

		OpenedDoorCount++;

		return new DoorExpansion(createdRegion, false, targetRegion!.Kind);
	}
}
