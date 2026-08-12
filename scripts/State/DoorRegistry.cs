using CmdRoguelike.Core;
using CmdRoguelike.Domain;
using Godot;

namespace CmdRoguelike.State;

internal sealed class DoorRegistry
{
	private readonly Dictionary<Vector2I, DungeonDoor> _doors = new();

	public IEnumerable<KeyValuePair<Vector2I, DungeonDoor>> Entries => _doors;

	public bool TryGet(Vector2I position, out DungeonDoor door)
	{
		return _doors.TryGetValue(position, out door);
	}

	public void RegisterExternalIfMissing(Vector2I position, DungeonDoor door)
	{
		_doors.TryAdd(position, door);
	}

	public void RegisterInternal(Vector2I position, DungeonDoor door)
	{
		_doors[position] = door;
	}

	public void RemoveInternalAt(Vector2I position)
	{
		if (_doors.TryGetValue(position, out DungeonDoor door) && door.IsInternal)
		{
			_doors.Remove(position);
		}
	}

	public List<Vector2I> FindClosedDoors(DungeonGrid grid)
	{
		List<Vector2I> result = new();
		foreach (Vector2I position in _doors.Keys)
		{
			if (grid[position] == DungeonTile.ClosedDoor)
			{
				result.Add(position);
			}
		}

		return result;
	}
}
