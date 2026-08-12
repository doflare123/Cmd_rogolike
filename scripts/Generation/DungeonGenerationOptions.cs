namespace CmdRoguelike.Generation;

public sealed record DungeonGenerationOptions
{
	public int SectorWidth { get; }
	public int SectorHeight { get; }
	public int MinimumDoorsPerRoom { get; }
	public float RoomChance { get; }
	public float EnemyRoomChance { get; }
	public int MinimumEnemiesPerRoom { get; }
	public int MaximumEnemiesPerRoom { get; }

	public DungeonGenerationOptions(
		int minimumDoorsPerRoom = 3,
		int sectorWidth = 32,
		int sectorHeight = 20,
		float roomChance = 0.68f,
		float enemyRoomChance = 0.38f,
		int minimumEnemiesPerRoom = 1,
		int maximumEnemiesPerRoom = 3)
	{
		if (minimumDoorsPerRoom is < 1 or > 4)
		{
			throw new ArgumentOutOfRangeException(nameof(minimumDoorsPerRoom), "Expected a value from 1 to 4.");
		}

		if (sectorWidth < 24)
		{
			throw new ArgumentOutOfRangeException(nameof(sectorWidth), "Expected at least 24 cells.");
		}

		if (sectorHeight < 16)
		{
			throw new ArgumentOutOfRangeException(nameof(sectorHeight), "Expected at least 16 cells.");
		}

		if (roomChance is < 0.0f or > 1.0f)
		{
			throw new ArgumentOutOfRangeException(nameof(roomChance), "Expected a probability from 0 to 1.");
		}

		if (enemyRoomChance is < 0.0f or > 1.0f)
		{
			throw new ArgumentOutOfRangeException(nameof(enemyRoomChance), "Expected a probability from 0 to 1.");
		}

		if (minimumEnemiesPerRoom < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(minimumEnemiesPerRoom));
		}

		if (maximumEnemiesPerRoom < minimumEnemiesPerRoom)
		{
			throw new ArgumentOutOfRangeException(
				nameof(maximumEnemiesPerRoom),
				"Maximum enemy count cannot be lower than the minimum.");
		}

		MinimumDoorsPerRoom = minimumDoorsPerRoom;
		SectorWidth = sectorWidth;
		SectorHeight = sectorHeight;
		RoomChance = roomChance;
		EnemyRoomChance = enemyRoomChance;
		MinimumEnemiesPerRoom = minimumEnemiesPerRoom;
		MaximumEnemiesPerRoom = maximumEnemiesPerRoom;
	}
}
