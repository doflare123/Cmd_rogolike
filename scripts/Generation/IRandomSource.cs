namespace CmdRoguelike.Generation;

internal interface IRandomSource
{
	int NextInt(int minimum, int maximum);
	float NextFloat();
}
