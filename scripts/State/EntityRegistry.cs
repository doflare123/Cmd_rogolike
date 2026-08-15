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

	public bool IsOccupied(Vector2I position)
	{
		return _byPosition.ContainsKey(position);
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

	/// <summary>
	/// Атомарно обновляет позиционный индекс и саму сущность. Все нарушения
	/// проверяются до изменения состояния, поэтому неудачный ход не оставляет
	/// реестр в частично обновлённом состоянии.
	/// </summary>
	public void Move(DungeonEntity entity, Vector2I destination)
	{
		ArgumentNullException.ThrowIfNull(entity);

		if (!_byId.TryGetValue(entity.Id, out DungeonEntity? registered)
			|| !ReferenceEquals(registered, entity))
		{
			throw new InvalidOperationException($"Entity {entity.Id} is not registered.");
		}

		Vector2I origin = entity.Position;
		if (origin == destination)
		{
			return;
		}

		if (!_byPosition.TryGetValue(origin, out DungeonEntity? indexed)
			|| !ReferenceEquals(indexed, entity))
		{
			throw new InvalidOperationException(
				$"Entity {entity.Id} position index is inconsistent at {origin}.");
		}

		if (_byPosition.ContainsKey(destination))
		{
			throw new InvalidOperationException($"Cell {destination} is already occupied.");
		}

		_byPosition.Add(destination, entity);
		if (!_byPosition.Remove(origin))
		{
			_byPosition.Remove(destination);
			throw new InvalidOperationException(
				$"Entity {entity.Id} could not be removed from its previous cell {origin}.");
		}

		entity.Position = destination;
	}
}
