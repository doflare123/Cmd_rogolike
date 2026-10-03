namespace CmdRoguelike.Domain.Combat;

/// <summary>Owns all card locations. Seeded separately from world geometry.</summary>
internal sealed class CombatDeck
{
	private readonly Random _random;
	private readonly List<CombatCard> _draw = new(), _discard = new(), _hand = new();
	public IReadOnlyList<CombatCard> Hand => _hand.AsReadOnly();
	public int DrawCount => _draw.Count;
	public int DiscardCount => _discard.Count;

	public CombatDeck(CombatOptions options, int seed)
	{
		_random = new Random(seed);
		_draw.AddRange(Enumerable.Repeat(options.Attack, options.AttackCards));
		_draw.AddRange(Enumerable.Repeat(options.Defense, options.DefenseCards));
		Shuffle();
	}

	public void NewHand(int count)
	{
		_discard.AddRange(_hand);
		_hand.Clear();
		for (int i = 0; i < count; i++) Draw();
	}

	public void Discard(int index)
	{
		_discard.Add(_hand[index]);
		_hand.RemoveAt(index);
	}

	public void Replace(int index)
	{
		Discard(index);
		Draw();
		CombatCard replacement = _hand[^1];
		_hand.RemoveAt(_hand.Count - 1);
		_hand.Insert(index, replacement);
	}

	private void Draw()
	{
		if (_draw.Count == 0)
		{
			_draw.AddRange(_discard);
			_discard.Clear();
			Shuffle();
		}
		if (_draw.Count == 0) return;
		_hand.Add(_draw[^1]);
		_draw.RemoveAt(_draw.Count - 1);
	}

	private void Shuffle()
	{
		for (int i = _draw.Count - 1; i > 0; i--)
		{
			int j = _random.Next(i + 1);
			(_draw[i], _draw[j]) = (_draw[j], _draw[i]);
		}
	}
}
