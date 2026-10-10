using Godot;
using CmdRoguelike.Core;

namespace CmdRoguelike.Domain.Campaign;

public enum BaseObjectKind { Storage, Campfire, ExpeditionGate, Workbench, Healer }
public sealed record BaseObject(Guid Id, BaseObjectKind Kind, Vector2I Position);

/// <summary>Persistent base layout, independent of expedition terrain and presentation.</summary>
public sealed class BaseLayout
{
	public const int Width = 64, Height = 25;
	public static Vector2I Entrance => new(32, 19);
	private readonly List<BaseObject> _objects = new();
	public IReadOnlyList<BaseObject> Objects => _objects.AsReadOnly();
	public BaseLayout(bool populate = true)
	{
		if (!populate) return;
		_objects.Add(new(Guid.NewGuid(), BaseObjectKind.Storage, new(12, 12)));
		_objects.Add(new(Guid.NewGuid(), BaseObjectKind.Campfire, new(32, 12)));
		_objects.Add(new(Guid.NewGuid(), BaseObjectKind.ExpeditionGate, new(52, 12)));
	}
	public bool IsFloor(Vector2I position) => position.X > 0 && position.X < Width - 1
		&& position.Y > 0 && position.Y < Height - 1;
	public BaseObject? At(Vector2I position) => _objects.FirstOrDefault(o => o.Position == position);
	public bool TryPlace(BaseObjectKind kind, Vector2I position, Vector2I hero, Guid? moving = null)
	{
		if (!Enum.IsDefined(kind) || !IsFloor(position) || position == hero || position == Entrance
			|| _objects.Count >= 80 && moving is null || At(position) is not null) return false;
		var existing = moving is Guid id ? _objects.FirstOrDefault(o => o.Id == id) : null;
		if (moving is not null && existing is null) return false;
		if (kind == BaseObjectKind.ExpeditionGate && existing?.Kind != BaseObjectKind.ExpeditionGate) return false;
		if (existing is not null) _objects.Remove(existing);
		var placed = new BaseObject(existing?.Id ?? Guid.NewGuid(), existing?.Kind ?? kind, position);
		_objects.Add(placed);
		if (!IsConnected())
		{
			_objects.Remove(placed); if (existing is not null) _objects.Add(existing); return false;
		}
		return true;
	}
	public bool TryRemove(Vector2I position)
	{
		var item = At(position);
		if (item is null || item.Kind == BaseObjectKind.ExpeditionGate
			|| item.Kind == BaseObjectKind.Storage && _objects.Count(o => o.Kind == BaseObjectKind.Storage) == 1) return false;
		_objects.Remove(item); return true;
	}
	private bool IsConnected()
	{
		var blocked = _objects.Select(o => o.Position).ToHashSet();
		var seen = new HashSet<Vector2I> { Entrance };
		var queue = new Queue<Vector2I>(); queue.Enqueue(Entrance);
		while (queue.TryDequeue(out var cell)) foreach (var direction in CardinalDirectionExtensions.All)
		{
			var next = cell + direction.ToOffset();
			if (IsFloor(next) && !blocked.Contains(next) && seen.Add(next)) queue.Enqueue(next);
		}
		return seen.Count == (Width - 2) * (Height - 2) - blocked.Count;
	}
	internal void Restore(IEnumerable<BaseObject> objects)
	{
		var values = objects.ToArray();
		if (values.Length > 80 || values.Select(o => o.Id).Distinct().Count() != values.Length
			|| values.Select(o => o.Position).Distinct().Count() != values.Length
			|| values.Any(o => o.Id == Guid.Empty || !Enum.IsDefined(o.Kind) || !IsFloor(o.Position) || o.Position == Entrance)
			|| values.Count(o => o.Kind == BaseObjectKind.ExpeditionGate) != 1)
			throw new ArgumentException("Invalid base layout.");
		_objects.Clear(); _objects.AddRange(values);
		if (!IsConnected()) throw new ArgumentException("Disconnected base layout.");
	}
}
