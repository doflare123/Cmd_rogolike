namespace CmdRoguelike.Presentation;

/// <summary>Only reveal progress; never generates, awards or modifies items.</summary>
internal sealed class RewardReveal
{
	internal const double Duration = 1.4;
	private double _elapsed;
	private readonly int _count;
	public int RevealedCount { get; private set; }
	public bool IsComplete => RevealedCount == _count;
	public double Progress => _elapsed / Duration;
	public RewardReveal(int count, bool animate)
	{
		if (count is < 0 or > 6) throw new ArgumentOutOfRangeException(nameof(count));
		_count = count; if (!animate) Skip();
	}
	public void Skip() { RevealedCount = _count; _elapsed = 0; }
	public void Tick(double delta)
	{
		if (delta < 0 || !double.IsFinite(delta)) throw new ArgumentOutOfRangeException(nameof(delta));
		if (IsComplete) return;
		_elapsed += delta;
		while (_elapsed >= Duration && !IsComplete) { _elapsed -= Duration; RevealedCount++; }
		if (IsComplete) _elapsed = 0;
	}
}
