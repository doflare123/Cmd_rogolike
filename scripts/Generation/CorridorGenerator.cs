using CmdRoguelike.Core;
using CmdRoguelike.Domain;

namespace CmdRoguelike.Generation;

internal sealed class CorridorGenerator
{
	private readonly RegionDoorBuilder _doorBuilder;
	private readonly RegionExitPlanner _exitPlanner;

	public CorridorGenerator(
		RegionDoorBuilder doorBuilder,
		RegionExitPlanner exitPlanner)
	{
		_doorBuilder = doorBuilder;
		_exitPlanner = exitPlanner;
	}

	public void Generate(DungeonRegion corridor, CardinalDirection? requiredEntrance)
	{
		foreach (CardinalDirection direction in _exitPlanner.ForCorridor(requiredEntrance))
		{
			_doorBuilder.EnsureExternalDoor(corridor, direction);
		}
	}
}
