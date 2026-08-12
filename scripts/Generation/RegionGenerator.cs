using CmdRoguelike.Core;
using CmdRoguelike.Domain;
using CmdRoguelike.State;
using Godot;

namespace CmdRoguelike.Generation;

/// <summary>
/// Создаёт модель области для одного сектора и передаёт наполнение
/// подходящему специализированному генератору.
/// </summary>
internal sealed class RegionGenerator
{
	private readonly DungeonGenerationOptions _options;
	private readonly IRandomSource _random;
	private readonly DungeonGrid _grid;
	private readonly RegionDoorBuilder _doorBuilder;
	private readonly RoomGenerator _roomGenerator;
	private readonly CorridorGenerator _corridorGenerator;

	public RegionGenerator(
		DungeonGenerationOptions options,
		IRandomSource random,
		DungeonGrid grid,
		DoorRegistry doors)
	{
		_options = options;
		_random = random;
		_grid = grid;

		RegionGeometry geometry = new(grid, doors);
		RegionExitPlanner exitPlanner = new(options, random);
		_doorBuilder = new RegionDoorBuilder(options, random, grid, doors, geometry);
		_roomGenerator = new RoomGenerator(
			random,
			grid,
			doors,
			geometry,
			_doorBuilder,
			exitPlanner);
		_corridorGenerator = new CorridorGenerator(_doorBuilder, exitPlanner);
	}

	public DungeonRegion Generate(
		Vector2I sector,
		CardinalDirection? requiredEntrance,
		bool forceRoom = false)
	{
		DungeonRegionKind kind = forceRoom || _random.NextFloat() < _options.RoomChance
			? DungeonRegionKind.Room
			: DungeonRegionKind.Corridor;
		DungeonRegion region = CreateRegionModel(sector, kind);

		if (kind == DungeonRegionKind.Room)
		{
			_roomGenerator.Generate(region, requiredEntrance);
		}
		else
		{
			_corridorGenerator.Generate(region, requiredEntrance);
		}

		return region;
	}

	public Vector2I EnsureExternalDoor(
		DungeonRegion region,
		CardinalDirection direction)
	{
		return _doorBuilder.EnsureExternalDoor(region, direction);
	}

	public Vector2I PickFloorCell(DungeonRegion region)
	{
		List<Vector2I> choices = region.Floors
			.Where(position => _grid[position] == DungeonTile.Floor)
			.ToList();

		return choices.Count > 0
			? choices[_random.NextInt(0, choices.Count - 1)]
			: region.Anchor;
	}

	private DungeonRegion CreateRegionModel(
		Vector2I sector,
		DungeonRegionKind kind)
	{
		Vector2I origin = new(
			sector.X * _options.SectorWidth,
			sector.Y * _options.SectorHeight);
		Rect2I bounds = new(
			origin,
			new Vector2I(_options.SectorWidth + 1, _options.SectorHeight + 1));
		Vector2I center = origin + new Vector2I(
			_options.SectorWidth / 2,
			_options.SectorHeight / 2);
		Vector2I anchor = kind == DungeonRegionKind.Room
			? center + new Vector2I(_random.NextInt(-3, 3), _random.NextInt(-2, 2))
			: center + new Vector2I(_random.NextInt(-6, 6), _random.NextInt(-4, 4));

		return new DungeonRegion(sector, bounds, anchor, kind);
	}
}
