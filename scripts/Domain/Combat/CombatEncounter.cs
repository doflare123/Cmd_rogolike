using CmdRoguelike.Domain.Entities;
using CmdRoguelike.Domain.Stats;
using CmdRoguelike.Domain.Rewards;

namespace CmdRoguelike.Domain.Combat;

public enum CombatPhase { RoundPreview, PlayerTurn, Victory, Defeat }
public enum CombatCommandResult { Success, WrongPhase, InvalidCard, InvalidTarget, NotEnoughActionPoints, ReplacementUnavailable, SourceUnavailable }
public enum CombatEventKind { Attack, Block, PlayerTurn, Victory, Defeat, Charge, BlockExpired }
public sealed record CombatEvent(CombatEventKind Kind, Guid ActorId, Guid? TargetId, int Damage = 0, int Blocked = 0,
	string? ActionName = null, int Mitigated = 0, DamageAspect Aspect = DamageAspect.Physical);
public sealed record EnemyIntent(Guid EnemyId, Guid TargetId, int Damage, bool BeforePlayer,
	EnemyActionKind Action = EnemyActionKind.Attack, int Block = 0, string ActionName = "Атака");

/// <summary>JRPG card encounter. No world movement, rendering, or generation dependencies.</summary>
public sealed class CombatEncounter
{
	private readonly CombatOptions _options;
	private readonly CombatDeck _deck;
	private readonly IReadOnlyList<Enemy> _enemies;
	private readonly List<Enemy> _before = new(), _after = new();
	private IReadOnlyList<EnemyIntent> _intents = Array.Empty<EnemyIntent>();
	private readonly List<CombatEvent> _events = new();
	private readonly Dictionary<Guid, int> _enemyBlock = new();
	private int _nextReplacementTurn = 1;
	private readonly RewardEnemy[] _rewardEnemies;
	public CombatVictorySummary? VictorySummary { get; private set; }
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
	public int GetEnemyBlock(Guid id) => _enemyBlock.GetValueOrDefault(id);
	public bool IsCardAvailable(CombatCard card) => CombatDeckBuilder.IsAvailable(Player, card);
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
		_rewardEnemies = participants.Select(enemy => new RewardEnemy(enemy.RewardDifficulty, enemy.Health, enemy.RewardRole)).ToArray();
		_options = options ?? new CombatOptions();
		_deck = new CombatDeck(CombatDeckBuilder.Build(player, _options), seed);
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
			ExecuteEnemyIntent(enemy);
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
		=> CombatRules.DefenseBlock(Player, card);

	public CombatCommandResult PlayCard(int index, Guid? targetId = null)
	{
		if (Phase != CombatPhase.PlayerTurn || !Player.IsAlive) return CombatCommandResult.WrongPhase;
		if (index < 0 || index >= Hand.Count) return CombatCommandResult.InvalidCard;
		CombatCard card = Hand[index];
		if (!IsCardAvailable(card)) return CombatCommandResult.SourceUnavailable;
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
			DealAttack(Player, target, card.Power, card.Name, card.DamageAspect);
		}
		else
		{
			int gained = newBlock - Block;
			Block = newBlock;
			_events.Add(new CombatEvent(CombatEventKind.Block, Player.Id, Player.Id, Blocked: gained, ActionName: card.Name));
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
			ExecuteEnemyIntent(enemy);
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
		_intents = Array.AsReadOnly(_before.Concat(_after).Select(enemy =>
		{
			EnemyAction action = enemy.Behavior.Plan(enemy, Round);
			return new EnemyIntent(enemy.Id, action.Kind == EnemyActionKind.Attack ? Player.Id : enemy.Id,
				action.Kind == EnemyActionKind.Attack ? action.Power : 0, _before.Contains(enemy), action.Kind,
				action.Kind == EnemyActionKind.Guard ? action.Power : 0, action.Name);
		}).ToArray());
		Phase = CombatPhase.RoundPreview;
	}

	private void ExecuteEnemyIntent(Enemy enemy)
	{
		if (!enemy.IsAlive || !Player.IsAlive) return;
		EnemyIntent intent = _intents.Single(intent => intent.EnemyId == enemy.Id);
		int remaining = GetEnemyBlock(enemy.Id);
		if (remaining > 0)
			_events.Add(new CombatEvent(CombatEventKind.BlockExpired, enemy.Id, enemy.Id, Blocked: remaining));
		_enemyBlock[enemy.Id] = 0;
		switch (intent.Action)
		{
			case EnemyActionKind.Attack: DealAttack(enemy, Player, intent.Damage, intent.ActionName); break;
			case EnemyActionKind.Guard:
				_enemyBlock[enemy.Id] = intent.Block;
				_events.Add(new CombatEvent(CombatEventKind.Block, enemy.Id, enemy.Id, Blocked: intent.Block, ActionName: intent.ActionName));
				break;
			case EnemyActionKind.Charge:
				_events.Add(new CombatEvent(CombatEventKind.Charge, enemy.Id, enemy.Id, ActionName: intent.ActionName));
				break;
		}
	}

	/// <summary>Shared damage/block rules for both sides; actions never bypass protection.</summary>
	private void DealAttack(Actor attacker, Actor target, int power, string actionName, DamageAspect aspect = DamageAspect.Physical)
	{
		int adjusted = target.DamageProfile.Apply(power, aspect);
		int block = target == Player ? Block : GetEnemyBlock(target.Id);
		int blocked = Math.Min(block, adjusted);
		if (target == Player) Block -= blocked;
		else _enemyBlock[target.Id] = block - blocked;
		int damage = Math.Min(target.Health, adjusted - blocked);
		target.TakeDamage(adjusted - blocked);
		_events.Add(new CombatEvent(CombatEventKind.Attack, attacker.Id, target.Id, damage, blocked, actionName,
			Math.Max(0, power - adjusted), aspect));
	}

	private bool CheckFinished()
	{
		CombatPhase? finish = !Player.IsAlive ? CombatPhase.Defeat
			: _enemies.All(enemy => !enemy.IsAlive) ? CombatPhase.Victory : null;
		if (finish is null) return false;
		Phase = finish.Value;
		if (Phase == CombatPhase.Victory)
			VictorySummary ??= new CombatVictorySummary(_rewardEnemies, Math.Max(1, PlayerTurn),
				Player.Health, Player.MaxHealth, Player.Resources.Mana, Player.Resources.MaxMana);
		ActionPoints = 0;
		Block = 0;
		_enemyBlock.Clear();
		_events.Add(new CombatEvent(Phase == CombatPhase.Victory ? CombatEventKind.Victory : CombatEventKind.Defeat, Player.Id, null));
		return true;
	}
}
