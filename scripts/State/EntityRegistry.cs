using CmdRoguelike.Domain.Entities;
using Godot;

namespace CmdRoguelike.State;

/// <summary>
/// Пространственное хранилище сущностей. Клетки описывают рельеф, а этот реестр
/// хранит динамические объекты, расположенные поверх него.
/// </summary>
internal sealed class EntityRegistry
{
	private readonly Dictionary<Guid, DungeonEntity> _byId = new();
	private readonly Dictionary<Vector2I, DungeonEntity> _byPosition = new();

	public int Count => _byId.Count;
	public IEnumerable<DungeonEntity> All => _byId.Values;

	public bool TryGetAt(Vector2I position, out DungeonEntity? entity)
	{
		return _byPosition.TryGetValue(position, out entity);
	}

	public bool IsBlocked(Vector2I position)
	{
		return TryGetAt(position, out DungeonEntity? entity)
			&& entity?.BlocksMovement == true;
	}

	public void Add(DungeonEntity entity)
	{
		ArgumentNullException.ThrowIfNull(entity);

		if (_byId.ContainsKey(entity.Id))
		{
			throw new InvalidOperationException($"Entity {entity.Id} is already registered.");
		}

		if (_byPosition.ContainsKey(entity.Position))
		{
			throw new InvalidOperationException($"Cell {entity.Position} is already occupied.");
		}

		_byId.Add(entity.Id, entity);
		_byPosition.Add(entity.Position, entity);
	}
}
