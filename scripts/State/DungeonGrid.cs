using CmdRoguelike.Core;
using Godot;

namespace CmdRoguelike.State;

/// <summary>
/// Хранит явный рельеф (пол, перегородки и двери) и вычисляет вокруг него единый
/// глобальный контур стен. Сгенерированные стены не принадлежат отдельным комнатам,
/// поэтому раскрытие новой области не может оставить устаревший контур.
/// </summary>
internal sealed class DungeonGrid
{
	private static readonly Vector2I[] CardinalOffsets =
	{
		Vector2I.Up,
		Vector2I.Right,
		Vector2I.Down,
		Vector2I.Left,
	};

	private static readonly Vector2I[] DiagonalOffsets =
	{
		new(-1, -1),
		new(1, -1),
		new(-1, 1),
		new(1, 1),
	};

	private readonly Dictionary<Vector2I, DungeonTile> _terrain = new();
	private readonly Dictionary<Vector2I, CardinalDirection> _doorDirections = new();
	private readonly HashSet<Vector2I> _generatedWalls = new();

	public DungeonTile this[Vector2I position]
	{
		get
		{
			if (_terrain.TryGetValue(position, out DungeonTile tile))
			{
				return tile;
			}

			return _generatedWalls.Contains(position)
				? DungeonTile.Wall
				: DungeonTile.Empty;
		}
	}

	public IEnumerable<Vector2I> KnownPositions => _terrain.Keys.Concat(_generatedWalls).Distinct();

	public bool IsWalkable(Vector2I position)
	{
		return this[position] is DungeonTile.Floor or DungeonTile.OpenDoor;
	}

	public void SetFloor(Vector2I position)
	{
		SetTerrain(position, DungeonTile.Floor);
	}

	public void SetWall(Vector2I position)
	{
		SetTerrain(position, DungeonTile.Wall);
	}

	public void SetDoor(
		Vector2I position,
		DungeonTile state,
		CardinalDirection direction)
	{
		if (state is not DungeonTile.ClosedDoor and not DungeonTile.OpenDoor)
		{
			throw new ArgumentOutOfRangeException(nameof(state), "Expected a door tile.");
		}

		_doorDirections[position] = direction;
		SetTerrain(position, state);
	}

	public void SetDoorState(Vector2I position, DungeonTile state)
	{
		if (state is not DungeonTile.ClosedDoor and not DungeonTile.OpenDoor)
		{
			throw new ArgumentOutOfRangeException(nameof(state), "Expected a door tile.");
		}

		if (!_doorDirections.ContainsKey(position))
		{
			throw new InvalidOperationException($"Cell {position} is not a registered door.");
		}

		_terrain[position] = state;
	}

	public bool IsGeneratedWall(Vector2I position)
	{
		return _generatedWalls.Contains(position) && !_terrain.ContainsKey(position);
	}

	public bool CanCarveFloor(Vector2I position)
	{
		DungeonTile explicitTile = _terrain.GetValueOrDefault(position, DungeonTile.Empty);
		return explicitTile is DungeonTile.Empty or DungeonTile.Floor;
	}

	private void SetTerrain(Vector2I position, DungeonTile tile)
	{
		DungeonTile previous = _terrain.GetValueOrDefault(position, DungeonTile.Empty);
		if (previous == tile)
		{
			return;
		}

		if (previous is DungeonTile.ClosedDoor or DungeonTile.OpenDoor
			&& tile is not DungeonTile.ClosedDoor and not DungeonTile.OpenDoor)
		{
			_doorDirections.Remove(position);
		}

		_terrain[position] = tile;
		RefreshOutlineNear(position);
	}

	private void RefreshOutlineNear(Vector2I changedPosition)
	{
		for (int y = -1; y <= 1; y++)
		{
			for (int x = -1; x <= 1; x++)
			{
				RefreshGeneratedWall(changedPosition + new Vector2I(x, y));
			}
		}
	}

	private void RefreshGeneratedWall(Vector2I position)
	{
		if (_terrain.ContainsKey(position) || !ShouldBeGeneratedWall(position))
		{
			_generatedWalls.Remove(position);
			return;
		}

		_generatedWalls.Add(position);
	}

	private bool ShouldBeGeneratedWall(Vector2I position)
	{
		foreach (Vector2I offset in CardinalOffsets)
		{
			Vector2I neighbor = position + offset;
			if (IsFloor(neighbor) || IsPerpendicularToDoor(position, neighbor))
			{
				return true;
			}
		}

		foreach (Vector2I diagonal in DiagonalOffsets)
		{
			Vector2I diagonalNeighbor = position + diagonal;
			if (!IsFloor(diagonalNeighbor))
			{
				continue;
			}

			Vector2I horizontalBridge = position + new Vector2I(diagonal.X, 0);
			Vector2I verticalBridge = position + new Vector2I(0, diagonal.Y);
			if (!IsSurface(horizontalBridge) && !IsSurface(verticalBridge))
			{
				return true;
			}
		}

		return false;
	}

	private bool IsFloor(Vector2I position)
	{
		return _terrain.GetValueOrDefault(position) == DungeonTile.Floor;
	}

	private bool IsSurface(Vector2I position)
	{
		return _terrain.GetValueOrDefault(position) is
			DungeonTile.Floor or DungeonTile.ClosedDoor or DungeonTile.OpenDoor;
	}

	private bool IsPerpendicularToDoor(Vector2I wallPosition, Vector2I doorPosition)
	{
		if (!_doorDirections.TryGetValue(doorPosition, out CardinalDirection direction))
		{
			return false;
		}

		Vector2I doorAxis = direction.ToOffset();
		Vector2I offset = wallPosition - doorPosition;
		return (doorAxis.X == 0 && offset.Y == 0)
			|| (doorAxis.Y == 0 && offset.X == 0);
	}
}
