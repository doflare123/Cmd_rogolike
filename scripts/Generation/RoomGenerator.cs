using CmdRoguelike.Core;
using CmdRoguelike.Domain;
using CmdRoguelike.State;
using Godot;

namespace CmdRoguelike.Generation;

internal sealed class RoomGenerator
{
	private readonly IRandomSource _random;
	private readonly DungeonGrid _grid;
	private readonly DoorRegistry _doors;
	private readonly RegionGeometry _geometry;
	private readonly RegionDoorBuilder _doorBuilder;
	private readonly RegionExitPlanner _exitPlanner;

	public RoomGenerator(
		IRandomSource random,
		DungeonGrid grid,
		DoorRegistry doors,
		RegionGeometry geometry,
		RegionDoorBuilder doorBuilder,
		RegionExitPlanner exitPlanner)
	{
		_random = random;
		_grid = grid;
		_doors = doors;
		_geometry = geometry;
		_doorBuilder = doorBuilder;
		_exitPlanner = exitPlanner;
	}

	public void Generate(DungeonRegion room, CardinalDirection? requiredEntrance)
	{
		int mainWidth = NextOdd(9, 21);
		int mainHeight = NextOdd(7, 15);
		Rect2I mainBody = _geometry.CreateCenteredInteriorRectangle(
			room,
			room.Anchor,
			mainWidth,
			mainHeight);
		_geometry.AddRectangle(room, mainBody);

		AddWings(room, mainWidth, mainHeight);
		_geometry.Paint(room);

		foreach (CardinalDirection direction in _exitPlanner.ForRoom(requiredEntrance))
		{
			_doorBuilder.EnsureExternalDoor(room, direction);
		}

		int partitionCount = _random.NextFloat() < 0.7f
			? _random.NextInt(1, 2)
			: 0;
		for (int index = 0; index < partitionCount; index++)
		{
			TryAddPartition(room);
		}
	}

	private void AddWings(DungeonRegion room, int mainWidth, int mainHeight)
	{
		int wingCount = _random.NextInt(0, 3);
		for (int index = 0; index < wingCount; index++)
		{
			int wingWidth = NextOdd(5, 13);
			int wingHeight = NextOdd(5, 11);
			Vector2I wingCenter = room.Anchor + new Vector2I(
				_random.NextInt(-(mainWidth / 2), mainWidth / 2),
				_random.NextInt(-(mainHeight / 2), mainHeight / 2));
			Rect2I wing = _geometry.CreateCenteredInteriorRectangle(
				room,
				wingCenter,
				wingWidth,
				wingHeight);
			_geometry.AddRectangle(room, wing);
		}
	}

	private void TryAddPartition(DungeonRegion room)
	{
		for (int attempt = 0; attempt < 12; attempt++)
		{
			bool vertical = _random.NextInt(0, 1) == 0;
			List<Vector2I> span = FindPartitionSpan(room, vertical);
			if (span.Count < 5)
			{
				continue;
			}

			Vector2I passageStep = vertical ? Vector2I.Right : Vector2I.Down;
			List<Vector2I> possibleDoors = FindDoorPositions(room, span, passageStep);
			if (possibleDoors.Count == 0 || WouldBlockExistingDoor(room, span))
			{
				continue;
			}

			Shuffle(possibleDoors);
			if (!TryFindConnectedDoorPosition(room, span, possibleDoors, out Vector2I selectedDoor))
			{
				continue;
			}

			PlacePartition(room, span, selectedDoor, vertical);
			return;
		}
	}

	private bool TryFindConnectedDoorPosition(
		DungeonRegion room,
		IEnumerable<Vector2I> proposedWall,
		IEnumerable<Vector2I> candidates,
		out Vector2I selectedDoor)
	{
		foreach (Vector2I candidate in candidates)
		{
			if (!WouldDisconnectRoom(room, proposedWall, candidate))
			{
				selectedDoor = candidate;
				return true;
			}
		}

		selectedDoor = default;
		return false;
	}

	private List<Vector2I> FindPartitionSpan(DungeonRegion room, bool vertical)
	{
		return vertical
			? _geometry.FindLongestVerticalFloorSpan(
				room,
				_random.NextInt(room.Bounds.Position.X + 4, room.Bounds.End.X - 5))
			: _geometry.FindLongestHorizontalFloorSpan(
				room,
				_random.NextInt(room.Bounds.Position.Y + 4, room.Bounds.End.Y - 5));
	}

	private void PlacePartition(
		DungeonRegion room,
		IEnumerable<Vector2I> span,
		Vector2I doorPosition,
		bool vertical)
	{
		foreach (Vector2I position in span)
		{
			room.Floors.Remove(position);
			_grid.SetWall(position);
		}

		CardinalDirection passageDirection = vertical
			? CardinalDirection.Right
			: CardinalDirection.Down;
		_grid.SetDoor(doorPosition, DungeonTile.ClosedDoor, passageDirection);
		_doors.RegisterInternal(
			doorPosition,
			new DungeonDoor(true, room.Sector, passageDirection));
	}

	/// <summary>
	/// Считает все двери проходимыми и отклоняет перегородку, если какая-либо
	/// существующая часть пола станет островом. Так защищаются полные маршруты
	/// к дверям, а не только соседняя с внешней дверью клетка.
	/// </summary>
	private bool WouldDisconnectRoom(
		DungeonRegion room,
		IEnumerable<Vector2I> proposedWall,
		Vector2I proposedDoor)
	{
		HashSet<Vector2I> blocked = new(proposedWall);
		blocked.Remove(proposedDoor);
		HashSet<Vector2I> traversable = room.Floors
			.Where(position => !blocked.Contains(position))
			.ToHashSet();
		traversable.Add(proposedDoor);

		foreach ((Vector2I position, DungeonDoor door) in _doors.Entries)
		{
			if (door.IsInternal && door.RegionSector == room.Sector)
			{
				traversable.Add(position);
			}
		}

		if (traversable.Count == 0)
		{
			return true;
		}

		HashSet<Vector2I> reached = new();
		Queue<Vector2I> frontier = new();
		Vector2I start = traversable.Contains(room.Anchor)
			? room.Anchor
			: traversable.First();
		reached.Add(start);
		frontier.Enqueue(start);

		while (frontier.Count > 0)
		{
			Vector2I current = frontier.Dequeue();
			foreach (CardinalDirection direction in CardinalDirectionExtensions.All)
			{
				Vector2I next = current + direction.ToOffset();
				if (traversable.Contains(next) && reached.Add(next))
				{
					frontier.Enqueue(next);
				}
			}
		}

		return reached.Count != traversable.Count;
	}

	private static List<Vector2I> FindDoorPositions(
		DungeonRegion room,
		IReadOnlyList<Vector2I> span,
		Vector2I passageStep)
	{
		List<Vector2I> result = new();
		for (int index = 1; index < span.Count - 1; index++)
		{
			Vector2I candidate = span[index];
			if (room.Floors.Contains(candidate - passageStep)
				&& room.Floors.Contains(candidate + passageStep))
			{
				result.Add(candidate);
			}
		}

		return result;
	}

	private bool WouldBlockExistingDoor(
		DungeonRegion room,
		IEnumerable<Vector2I> proposedWall)
	{
		HashSet<Vector2I> wallCells = new(proposedWall);

		foreach ((CardinalDirection direction, Vector2I doorPosition) in room.ExternalDoors)
		{
			Vector2I insideApproach = doorPosition - direction.ToOffset();
			if (wallCells.Contains(insideApproach))
			{
				return true;
			}
		}

		foreach ((Vector2I doorPosition, DungeonDoor door) in _doors.Entries)
		{
			if (!door.IsInternal || door.RegionSector != room.Sector)
			{
				continue;
			}

			Vector2I passageStep = door.Direction.ToOffset();
			if (wallCells.Contains(doorPosition - passageStep)
				|| wallCells.Contains(doorPosition + passageStep))
			{
				return true;
			}
		}

		return false;
	}

	private int NextOdd(int minimum, int maximum)
	{
		int optionCount = ((maximum - minimum) / 2) + 1;
		return minimum + (_random.NextInt(0, optionCount - 1) * 2);
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
