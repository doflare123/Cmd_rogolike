using CmdRoguelike.Core;
using CmdRoguelike.Domain;
using CmdRoguelike.State;
using Godot;

namespace CmdRoguelike.Generation;

/// <summary>
/// Низкоуровневые операции с сеткой для генераторов областей.
/// Умеет вырезать формы, но не принимает процедурных решений.
/// </summary>
internal sealed class RegionGeometry
{
	private readonly DungeonGrid _grid;
	private readonly DoorRegistry _doors;

	public RegionGeometry(DungeonGrid grid, DoorRegistry doors)
	{
		_grid = grid;
		_doors = doors;
	}

	public void AddRectangle(DungeonRegion region, Rect2I rectangle)
	{
		for (int y = rectangle.Position.Y; y < rectangle.End.Y; y++)
		{
			for (int x = rectangle.Position.X; x < rectangle.End.X; x++)
			{
				AddFloor(region, new Vector2I(x, y));
			}
		}
	}

	public void AddFloor(DungeonRegion region, Vector2I position)
	{
		if (IsInterior(region, position) && _grid.CanCarveFloor(position))
		{
			region.Floors.Add(position);
		}
	}

	public void CarveOrthogonalPath(
		DungeonRegion region,
		Vector2I from,
		Vector2I to,
		int radius,
		bool horizontalFirst)
	{
		Vector2I bend = horizontalFirst
			? new Vector2I(to.X, from.Y)
			: new Vector2I(from.X, to.Y);

		AddWideLine(region, from, bend, radius);
		AddWideLine(region, bend, to, radius);
	}

	public void Paint(DungeonRegion region)
	{
		foreach (Vector2I floor in region.Floors)
		{
			_doors.RemoveInternalAt(floor);
			_grid.SetFloor(floor);
		}
	}

	public Rect2I CreateCenteredInteriorRectangle(
		DungeonRegion region,
		Vector2I center,
		int width,
		int height)
	{
		int left = Math.Clamp(
			center.X - (width / 2),
			region.Bounds.Position.X + 2,
			region.Bounds.End.X - width - 2);
		int top = Math.Clamp(
			center.Y - (height / 2),
			region.Bounds.Position.Y + 2,
			region.Bounds.End.Y - height - 2);

		return new Rect2I(left, top, width, height);
	}

	public List<Vector2I> FindLongestVerticalFloorSpan(DungeonRegion region, int x)
	{
		List<Vector2I> line = new();
		for (int y = region.Bounds.Position.Y + 1; y < region.Bounds.End.Y - 1; y++)
		{
			line.Add(new Vector2I(x, y));
		}

		return FindLongestFloorSpan(region, line);
	}

	public List<Vector2I> FindLongestHorizontalFloorSpan(DungeonRegion region, int y)
	{
		List<Vector2I> line = new();
		for (int x = region.Bounds.Position.X + 1; x < region.Bounds.End.X - 1; x++)
		{
			line.Add(new Vector2I(x, y));
		}

		return FindLongestFloorSpan(region, line);
	}

	private void AddWideLine(DungeonRegion region, Vector2I from, Vector2I to, int radius)
	{
		Vector2I step = new(Math.Sign(to.X - from.X), Math.Sign(to.Y - from.Y));
		Vector2I current = from;

		while (true)
		{
			for (int offset = -radius; offset <= radius; offset++)
			{
				Vector2I point = from.Y == to.Y
					? current + new Vector2I(0, offset)
					: current + new Vector2I(offset, 0);
				AddFloor(region, point);
			}

			if (current == to)
			{
				break;
			}

			current += step;
		}
	}

	private static bool IsInterior(DungeonRegion region, Vector2I position)
	{
		return position.X > region.Bounds.Position.X
			&& position.X < region.Bounds.End.X - 1
			&& position.Y > region.Bounds.Position.Y
			&& position.Y < region.Bounds.End.Y - 1;
	}

	private static List<Vector2I> FindLongestFloorSpan(
		DungeonRegion region,
		IEnumerable<Vector2I> line)
	{
		List<Vector2I> best = new();
		List<Vector2I> current = new();

		foreach (Vector2I position in line)
		{
			if (region.Floors.Contains(position))
			{
				current.Add(position);
				continue;
			}

			if (current.Count > best.Count)
			{
				best = new List<Vector2I>(current);
			}

			current.Clear();
		}

		return current.Count > best.Count ? current : best;
	}
}
