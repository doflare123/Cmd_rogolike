using CmdRoguelike.Core;

namespace CmdRoguelike.Generation;

/// <summary>
/// Выбирает направления внешних дверей независимо от геометрии области.
/// </summary>
internal sealed class RegionExitPlanner
{
	private readonly DungeonGenerationOptions _options;
	private readonly IRandomSource _random;

	public RegionExitPlanner(DungeonGenerationOptions options, IRandomSource random)
	{
		_options = options;
		_random = random;
	}

	public IReadOnlyList<CardinalDirection> ForRoom(CardinalDirection? requiredEntrance)
	{
		List<CardinalDirection> shuffled = new(CardinalDirectionExtensions.All);
		Shuffle(shuffled);
		List<CardinalDirection> result = new();
		int count = _random.NextInt(_options.MinimumDoorsPerRoom, 4);

		if (requiredEntrance is CardinalDirection entrance)
		{
			result.Add(entrance);
			shuffled.Remove(entrance);
		}

		foreach (CardinalDirection direction in shuffled)
		{
			if (result.Count >= count)
			{
				break;
			}

			result.Add(direction);
		}

		return result;
	}

	public IReadOnlyList<CardinalDirection> ForCorridor(CardinalDirection? requiredEntrance)
	{
		CardinalDirection entrance = requiredEntrance
			?? CardinalDirectionExtensions.All[_random.NextInt(0, 3)];
		List<CardinalDirection> result = new() { entrance };
		int roll = _random.NextInt(0, 99);

		if (roll < 30)
		{
			result.Add(entrance.Opposite());
		}
		else if (roll < 55)
		{
			CardinalDirection[] perpendicular = entrance is CardinalDirection.Up or CardinalDirection.Down
				? new[] { CardinalDirection.Left, CardinalDirection.Right }
				: new[] { CardinalDirection.Up, CardinalDirection.Down };
			result.Add(perpendicular[_random.NextInt(0, 1)]);
		}
		else if (roll < 82)
		{
			List<CardinalDirection> candidates = new(CardinalDirectionExtensions.All);
			candidates.Remove(entrance);
			Shuffle(candidates);
			result.Add(candidates[0]);
			result.Add(candidates[1]);
		}
		else
		{
			result = new List<CardinalDirection>(CardinalDirectionExtensions.All);
		}

		return result;
	}

	private void Shuffle<T>(IList<T> values)
	{
		for (int index = values.Count - 1; index > 0; index--)
		{
			int other = _random.NextInt(0, index);
			(values[index], values[other]) = (values[other], values[index]);
		}
	}
}
