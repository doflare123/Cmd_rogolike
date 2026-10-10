namespace CmdRoguelike.Generation;

/// <summary>Independent seeded rolls on new rooms, including a bound on unlucky exploration.</summary>
internal sealed class BossPlacementPolicy
{
	private readonly IRandomSource _random;
	public int MinimumRooms { get; }
	public int GuaranteedRoom { get; }
	public BossPlacementPolicy(IRandomSource random, int minimumRooms = 4, int guaranteedRoom = 8)
	{
		if (minimumRooms < 2 || guaranteedRoom < minimumRooms) throw new ArgumentOutOfRangeException(nameof(minimumRooms));
		_random = random; MinimumRooms = minimumRooms; GuaranteedRoom = guaranteedRoom;
	}
	public bool ShouldPlace(int roomCount) => roomCount >= GuaranteedRoom
		|| roomCount >= MinimumRooms && _random.NextFloat() < (float)(roomCount - MinimumRooms + 1) / (GuaranteedRoom - MinimumRooms + 1);
}
