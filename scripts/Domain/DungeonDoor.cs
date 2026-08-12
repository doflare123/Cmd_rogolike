using CmdRoguelike.Core;
using Godot;

namespace CmdRoguelike.Domain;

internal readonly record struct DungeonDoor(
	bool IsInternal,
	Vector2I RegionSector,
	CardinalDirection Direction);
