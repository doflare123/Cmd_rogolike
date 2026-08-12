using CmdRoguelike.Core;
using CmdRoguelike.Domain;
using CmdRoguelike.State;
using Godot;

namespace CmdRoguelike.Generation;

/// <summary>
/// Создаёт общие двери на границах секторов и гарантирует маршрут
/// от двери к полу принадлежащей ей области.
/// </summary>
internal sealed class RegionDoorBuilder
{
	private readonly DungeonGenerationOptions _options;
	private readonly IRandomSource _random;
	private readonly DungeonGrid _grid;
	private readonly DoorRegistry _doors;
	private readonly RegionGeometry _geometry;

	public RegionDoorBuilder(
		DungeonGenerationOptions options,
		IRandomSource random,
		DungeonGrid grid,
		DoorRegistry doors,
		RegionGeometry geometry)
	{
		_options = options;
		_random = random;
		_grid = grid;
		_doors = doors;
		_geometry = geometry;
	}

	public Vector2I EnsureExternalDoor(
		DungeonRegion region,
		CardinalDirection direction)
	{
		if (region.ExternalDoors.TryGetValue(direction, out Vector2I existingDoor))
		{
			return existingDoor;
		}

		Vector2I position = GetPortalPosition(region, direction);
		ConnectPortalToRegion(region, direction);
		_geometry.Paint(region);
		region.ExternalDoors.Add(direction, position);
		_doors.RegisterExternalIfMissing(
			position,
			new DungeonDoor(false, region.Sector, direction));

		if (_grid[position] != DungeonTile.OpenDoor)
		{
			_grid.SetDoor(position, DungeonTile.ClosedDoor, direction);
		}

		return position;
	}

	private void ConnectPortalToRegion(
		DungeonRegion region,
		CardinalDirection direction)
	{
		Vector2I portal = GetPortalPosition(region, direction);
		Vector2I firstInside = portal - direction.ToOffset();

		if (region.Floors.Count > 0)
		{
			CarveToNearestFloor(region, firstInside);
			return;
		}

		int radius = region.Kind == DungeonRegionKind.Room
			? _random.NextInt(1, 2)
			: (_random.NextFloat() < 0.22f ? 1 : 0);
		bool horizontalFirst = _random.NextInt(0, 1) == 0;

		_geometry.CarveOrthogonalPath(
			region,
			firstInside,
			region.Anchor,
			radius,
			horizontalFirst);
		_geometry.AddRectangle(
			region,
			new Rect2I(
				firstInside - new Vector2I(radius, radius),
				new Vector2I((radius * 2) + 1, (radius * 2) + 1)));
	}

	private void CarveToNearestFloor(DungeonRegion region, Vector2I start)
	{
		if (region.Floors.Contains(start))
		{
			return;
		}

		Queue<Vector2I> frontier = new();
		Dictionary<Vector2I, Vector2I> previous = new();
		HashSet<Vector2I> visited = new() { start };
		frontier.Enqueue(start);
		Vector2I? target = null;

		while (frontier.Count > 0)
		{
			Vector2I current = frontier.Dequeue();
			if (region.Floors.Contains(current))
			{
				target = current;
				break;
			}

			foreach (CardinalDirection direction in CardinalDirectionExtensions.All)
			{
				Vector2I next = current + direction.ToOffset();
				if (!IsInterior(region, next)
					|| !_grid.CanCarveFloor(next)
					|| !visited.Add(next))
				{
					continue;
				}

				previous[next] = current;
				frontier.Enqueue(next);
			}
		}

		if (target is not Vector2I destination)
		{
			throw new InvalidOperationException(
				$"Cannot connect door approach {start} to region {region.Sector}.");
		}

		Vector2I pathCell = destination;
		while (pathCell != start)
		{
			_geometry.AddFloor(region, pathCell);
			pathCell = previous[pathCell];
		}

		_geometry.AddFloor(region, start);
	}

	private static bool IsInterior(DungeonRegion region, Vector2I position)
	{
		return position.X > region.Bounds.Position.X
			&& position.X < region.Bounds.End.X - 1
			&& position.Y > region.Bounds.Position.Y
			&& position.Y < region.Bounds.End.Y - 1;
	}

	private Vector2I GetPortalPosition(
		DungeonRegion region,
		CardinalDirection direction)
	{
		Vector2I origin = region.Bounds.Position;
		return direction switch
		{
			CardinalDirection.Up => origin + new Vector2I(_options.SectorWidth / 2, 0),
			CardinalDirection.Right => origin + new Vector2I(_options.SectorWidth, _options.SectorHeight / 2),
			CardinalDirection.Down => origin + new Vector2I(_options.SectorWidth / 2, _options.SectorHeight),
			CardinalDirection.Left => origin + new Vector2I(0, _options.SectorHeight / 2),
			_ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null),
		};
	}
}
