using CmdRoguelike.Core;
using Godot;

namespace CmdRoguelike.Domain;

internal sealed class DungeonRegion
{
	public Vector2I Sector { get; }
	public Rect2I Bounds { get; }
	public Vector2I Anchor { get; }
	public DungeonRegionKind Kind { get; }
	public HashSet<Vector2I> Floors { get; } = new();
	public Dictionary<CardinalDirection, Vector2I> ExternalDoors { get; } = new();

	public DungeonRegion(
		Vector2I sector,
		Rect2I bounds,
		Vector2I anchor,
		DungeonRegionKind kind)
	{
		Sector = sector;
		Bounds = bounds;
		Anchor = anchor;
		Kind = kind;
	}
}
