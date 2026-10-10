using Godot;

namespace CmdRoguelike.Domain.Entities;

/// <summary>
/// Базовый тип любого объекта, занимающего клетку мира: акторов, предметов,
/// декораций и будущих интерактивных объектов.
/// </summary>
public abstract class DungeonEntity
{
	public Guid Id { get; }
	public Vector2I Position { get; internal set; }
	public virtual bool BlocksMovement => false;

	protected DungeonEntity(Vector2I position, Guid? id = null)
	{
		Id = id ?? Guid.NewGuid();
		if (Id == Guid.Empty) throw new ArgumentException("Entity identity cannot be empty.", nameof(id));
		Position = position;
	}
}
