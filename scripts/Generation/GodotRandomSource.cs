using Godot;

namespace CmdRoguelike.Generation;

internal sealed class GodotRandomSource : IRandomSource
{
	private readonly RandomNumberGenerator _random = new();

	public GodotRandomSource(int seed)
	{
		_random.Seed = unchecked((ulong)(uint)seed);
	}

	public int NextInt(int minimum, int maximum)
	{
		return _random.RandiRange(minimum, maximum);
	}

	public float NextFloat()
	{
		return _random.Randf();
	}
}
