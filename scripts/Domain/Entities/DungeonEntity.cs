using Godot;

namespace CmdRoguelike.Domain.Entities;

/// <summary>
/// Базовый тип любого объекта, занимающего клетку мира: акторов, предметов,
/// декораций и будущих интерактивных объектов.
/// </summary>
public abstract class DungeonEntity
{
	public Guid Id { get; } = Guid.NewGuid();
	public Vector2I Position { get; internal set; }
	public virtual bool BlocksMovement => false;

	protected DungeonEntity(Vector2I position)
	{
		Position = position;
	}
}
