using CmdRoguelike.Domain.Entities;
using CmdRoguelike.Domain.Stats;

namespace CmdRoguelike.Domain.Combat;

public enum CombatPhase { RoundPreview, PlayerTurn, Victory, Defeat }
public enum CombatCommandResult { Success, WrongPhase, InvalidCard, InvalidTarget, NotEnoughActionPoints, ReplacementUnavailable }
public enum CombatEventKind { Attack, Block, PlayerTurn, Victory, Defeat }
public sealed record CombatEvent(CombatEventKind Kind, Guid ActorId, Guid? TargetId, int Damage = 0, int Blocked = 0);
public sealed record EnemyIntent(Guid EnemyId, Guid TargetId, int Damage, bool BeforePlayer);

/// <summary>JRPG card encounter. No world movement, rendering, or generation dependencies.</summary>
public sealed class CombatEncounter
{
	private readonly CombatOptions _options;
	private readonly CombatDeck _deck;
	private readonly IReadOnlyList<Enemy> _enemies;
	private readonly List<Enemy> _before = new(), _after = new();
	private IReadOnlyList<EnemyIntent> _intents = Array.Empty<EnemyIntent>();
	private readonly List<CombatEvent> _events = new();
	private int _nextReplacementTurn = 1;
	public PlayerCharacter Player { get; }
	public IReadOnlyList<Enemy> Enemies => _enemies;
	public IReadOnlyList<CombatCard> Hand => _deck.Hand;
	public IReadOnlyList<EnemyIntent> Intents => _intents;
	public IReadOnlyList<CombatEvent> Events => _events.AsReadOnly();
	public CombatPhase Phase { get; private set; }
	public bool IsFinished => Phase is CombatPhase.Victory or CombatPhase.Defeat;
	public int Round { get; private set; }
	public int PlayerTurn { get; private set; }
	public int ActionPoints { get; private set; }
	public int TurnMaxActionPoints { get; private set; }
	public int Block { get; private set; }
	public int DrawCount => _deck.DrawCount;
	public int DiscardCount => _deck.DiscardCount;
	public bool CanReplaceCard => Phase == CombatPhase.PlayerTurn && PlayerTurn >= _nextReplacementTurn;
	public int ReplacementInTurns => Math.Max(0, _nextReplacementTurn - PlayerTurn);

	public CombatEncounter(PlayerCharacter player, IEnumerable<Enemy> enemies, int seed, CombatOptions? options = null)
	{
		ArgumentNullException.ThrowIfNull(player);
		ArgumentNullException.ThrowIfNull(enemies);
		Player = player;
		var participants = enemies.ToArray();
		if (!player.IsAlive || participants.Length == 0 || participants.Any(enemy => !enemy.IsAlive)
			|| participants.Select(enemy => enemy.Id).Distinct().Count() != participants.Length)
			throw new ArgumentException("Combat requires a living player and distinct living enemies.");
		_enemies = Array.AsReadOnly(participants);
		_options = options ?? new CombatOptions();
		_deck = new CombatDeck(_options, seed);
		PrepareRound();
	}

	/// <summary>Explicit confirmation ensures opening attacks are telegraphed before any damage.</summary>
	public CombatCommandResult BeginRound()
	{
		if (Phase != CombatPhase.RoundPreview) return CombatCommandResult.WrongPhase;
		_events.Clear();
		if (CheckFinished()) return CombatCommandResult.Success;
		foreach (Enemy enemy in _before)
		{
			EnemyAttack(enemy);
			if (CheckFinished()) return CombatCommandResult.Success;
		}
		// Previous block covers both late enemies and the next round's early enemies.
		Block = 0;
		PlayerTurn++;
		TurnMaxActionPoints = Player.DerivedStats.GetValue(DerivedStatId.MaxActionPoints);
		ActionPoints = TurnMaxActionPoints;
		_deck.NewHand(_options.HandSize);
		Phase = CombatPhase.PlayerTurn;
		_events.Add(new CombatEvent(CombatEventKind.PlayerTurn, Player.Id, null));
		return CombatCommandResult.Success;
	}

	public int DefenseBlock(CombatCard card)
		=> card.Kind == CombatCardKind.Defense
			? checked((int)((long)Player.DerivedStats.GetValue(DerivedStatId.Armor) * card.Power / 100)) : 0;

	public CombatCommandResult PlayCard(int index, Guid? targetId = null)
	{
		if (Phase != CombatPhase.PlayerTurn || !Player.IsAlive) return CombatCommandResult.WrongPhase;
		if (index < 0 || index >= Hand.Count) return CombatCommandResult.InvalidCard;
		CombatCard card = Hand[index];
		if (ActionPoints < card.ActionPointCost) return CombatCommandResult.NotEnoughActionPoints;
		Enemy? target = null;
		int newBlock = Block;
		if (card.Kind == CombatCardKind.Attack)
		{
			target = _enemies.FirstOrDefault(enemy => enemy.Id == targetId && enemy.IsAlive);
			if (target is null) return CombatCommandResult.InvalidTarget;
		}
		else newBlock = checked(Block + DefenseBlock(card));

		_events.Clear();
		ActionPoints -= card.ActionPointCost;
		_deck.Discard(index);
		if (target is not null)
		{
			int damage = Math.Min(target.Health, card.Power);
			target.TakeDamage(card.Power);
			_events.Add(new CombatEvent(CombatEventKind.Attack, Player.Id, target.Id, damage));
		}
		else
		{
			int gained = newBlock - Block;
			Block = newBlock;
			_events.Add(new CombatEvent(CombatEventKind.Block, Player.Id, Player.Id, Blocked: gained));
		}
		CheckFinished();
		return CombatCommandResult.Success;
	}

	public CombatCommandResult ReplaceCard(int index)
	{
		if (Phase != CombatPhase.PlayerTurn || !Player.IsAlive) return CombatCommandResult.WrongPhase;
		if (index < 0 || index >= Hand.Count) return CombatCommandResult.InvalidCard;
		if (!CanReplaceCard) return CombatCommandResult.ReplacementUnavailable;
		_deck.Replace(index);
		_nextReplacementTurn = PlayerTurn + 2;
		_events.Clear();
		return CombatCommandResult.Success;
	}

	public CombatCommandResult EndPlayerTurn()
	{
		if (Phase != CombatPhase.PlayerTurn) return CombatCommandResult.WrongPhase;
		_events.Clear();
		ActionPoints = 0;
		if (CheckFinished()) return CombatCommandResult.Success;
		foreach (Enemy enemy in _after)
		{
			EnemyAttack(enemy);
			if (CheckFinished()) return CombatCommandResult.Success;
		}
		PrepareRound();
		return CombatCommandResult.Success;
	}

	private void PrepareRound()
	{
		Round++;
		_before.Clear();
		_after.Clear();
		// Stable encounter order breaks enemy ties, never random Guid values.
		var sorted = _enemies.Where(enemy => enemy.IsAlive)
			.OrderByDescending(enemy => enemy.Attributes.GetValue(AttributeId.Willpower)).ToArray();
		int limit = (int)((long)sorted.Length * _options.EnemiesBeforePlayerPercent / 100);
		int willpower = Player.Attributes.GetValue(AttributeId.Willpower);
		foreach (Enemy enemy in sorted)
		{
			if (_before.Count < limit && enemy.Attributes.GetValue(AttributeId.Willpower) > willpower)
				_before.Add(enemy);
			else _after.Add(enemy);
		}
		_intents = Array.AsReadOnly(_before.Concat(_after)
			.Select(enemy => new EnemyIntent(enemy.Id, Player.Id, enemy.AttackPower, _before.Contains(enemy))).ToArray());
		Phase = CombatPhase.RoundPreview;
	}

	private void EnemyAttack(Enemy enemy)
	{
		if (!enemy.IsAlive || !Player.IsAlive) return;
		int damage = _intents.Single(intent => intent.EnemyId == enemy.Id).Damage;
		int blocked = Math.Min(Block, damage);
		Block -= blocked;
		int actualDamage = Math.Min(Player.Health, damage - blocked);
		Player.TakeDamage(damage - blocked);
		_events.Add(new CombatEvent(CombatEventKind.Attack, enemy.Id, Player.Id, actualDamage, blocked));
	}

	private bool CheckFinished()
	{
		CombatPhase? finish = !Player.IsAlive ? CombatPhase.Defeat
			: _enemies.All(enemy => !enemy.IsAlive) ? CombatPhase.Victory : null;
		if (finish is null) return false;
		Phase = finish.Value;
		ActionPoints = 0;
		Block = 0;
		_events.Add(new CombatEvent(Phase == CombatPhase.Victory ? CombatEventKind.Victory : CombatEventKind.Defeat, Player.Id, null));
		return true;
	}
}
