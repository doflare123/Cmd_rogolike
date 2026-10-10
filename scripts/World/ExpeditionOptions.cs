namespace CmdRoguelike.World;

public sealed record ExpeditionOptions
{
	public int WorldTier { get; }
	public int MinimumBossRooms { get; }
	public int GuaranteedBossRoom { get; }
	public ExpeditionOptions(int worldTier = 0, int minimumBossRooms = 4, int guaranteedBossRoom = 8)
	{
		if (worldTier is < 0 or > 1000 || minimumBossRooms < 2 || guaranteedBossRoom < minimumBossRooms)
			throw new ArgumentOutOfRangeException(nameof(worldTier));
		WorldTier = worldTier; MinimumBossRooms = minimumBossRooms; GuaranteedBossRoom = guaranteedBossRoom;
	}
}
