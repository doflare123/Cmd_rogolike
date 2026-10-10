using CmdRoguelike.Core;
using CmdRoguelike.Domain.Campaign;
using CmdRoguelike.Domain.Entities;
using CmdRoguelike.State;
using Godot;

namespace CmdRoguelike.World;

public sealed class BaseMap
{
	private readonly EntityRegistry _entities = new();
	public BaseLayout Layout { get; }
	public PlayerCharacter Player { get; }
	public BaseMap(BaseLayout layout, PlayerCharacter hero, Vector2I? position = null)
	{
		Layout = layout; Player = hero;
		_entities.Add(hero);
		Vector2I target = position ?? BaseLayout.Entrance;
		if (!layout.IsFloor(target) || layout.At(target) is not null) throw new ArgumentException("Invalid base hero position.");
		_entities.Move(hero, target);
	}
	public bool TryMove(CardinalDirection direction)
	{
		Vector2I target = Player.Position + direction.ToOffset();
		if (!Player.IsAlive || !Layout.IsFloor(target) || Layout.At(target) is not null) return false;
		_entities.Move(Player, target);
		return true;
	}
	public BaseObject? NearbyObject() => Layout.At(Player.Position) ?? CardinalDirectionExtensions.All
		.Select(d => Layout.At(Player.Position + d.ToOffset())).FirstOrDefault(o => o is not null);
	internal void DetachPlayer() => _entities.Remove(Player);
}
