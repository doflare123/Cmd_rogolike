namespace CmdRoguelike.Domain;

public readonly record struct DoorExpansion(
	bool CreatedRegion,
	bool OpenedInternalDoor,
	DungeonRegionKind? RegionKind);
