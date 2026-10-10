using CmdRoguelike.Domain.Combat;
using CmdRoguelike.Domain.Stats;

namespace CmdRoguelike.Presentation;

/// <summary>Display-only state. Domain results are replayed, never recalculated or applied twice.</summary>
internal sealed class CombatVisualState
{
	public Dictionary<Guid, int> Health { get; } = new();
	public Dictionary<Guid, int> EnemyBlock { get; } = new();
	public List<CombatCard> Hand { get; } = new();
	public CombatPhase Phase { get; set; }
	public int Round { get; set; }
	public int ActionPoints { get; set; }
	public int MaxActionPoints { get; set; }
	public int Block { get; set; }
	public int DrawCount { get; set; }
	public int DiscardCount { get; set; }
	public bool CanReplace { get; set; }
	public int ReplacementInTurns { get; set; }
	public IReadOnlyList<EnemyIntent> Intents { get; set; } = Array.Empty<EnemyIntent>();

	public static CombatVisualState Capture(CombatEncounter battle)
	{
		var view = new CombatVisualState
		{
			Phase = battle.Phase, Round = battle.Round, ActionPoints = battle.ActionPoints,
			MaxActionPoints = battle.Phase == CombatPhase.PlayerTurn ? battle.TurnMaxActionPoints
				: battle.Player.DerivedStats.GetValue(DerivedStatId.MaxActionPoints),
			Block = battle.Block, DrawCount = battle.DrawCount, DiscardCount = battle.DiscardCount,
			CanReplace = battle.CanReplaceCard, ReplacementInTurns = battle.ReplacementInTurns,
			Intents = battle.Intents.ToArray(),
		};
		view.Health.Add(battle.Player.Id, battle.Player.Health);
		foreach (var enemy in battle.Enemies)
		{
			view.Health.Add(enemy.Id, enemy.Health);
			view.EnemyBlock.Add(enemy.Id, battle.GetEnemyBlock(enemy.Id));
		}
		view.Hand.AddRange(battle.Hand);
		return view;
	}
}

internal enum CombatAnimationKind { PlayCard, DiscardCard, DrawCard, Attack, Defense, Charge, BlockExpired, Deal, Finish }
internal enum CombatVisualCommand { BeginRound, PlayCard, ReplaceCard, EndTurn }
internal sealed record CombatAnimationStep(CombatAnimationKind Kind, double Duration, CombatEvent? Event = null,
	CombatCard? Card = null, int CardIndex = 0, bool FromHand = false);

/// <summary>One sequential presentation timeline for both player and enemy actions.</summary>
internal sealed class CombatAnimation
{
	private readonly Queue<CombatAnimationStep> _steps = new();
	private readonly Guid _playerId;
	private CombatVisualState? _final;
	private double _elapsed;
	private bool _applied;
	public CombatVisualState View { get; private set; }
	public CombatAnimationStep? Current => _steps.TryPeek(out var step) ? step : null;
	public bool IsBusy => _steps.Count > 0;
	public float Progress => Current is null ? 0 : (float)Math.Clamp(_elapsed / Current.Duration, 0, 1);
	public event Action<CombatEvent>? EventShown;

	public CombatAnimation(CombatEncounter battle)
	{
		_playerId = battle.Player.Id;
		View = CombatVisualState.Capture(battle);
	}

	public void Start(CombatVisualState before, CombatEncounter battle, CombatVisualCommand command, int index)
	{
		if (IsBusy) throw new InvalidOperationException("Combat animations cannot overlap.");
		View = before;
		_final = CombatVisualState.Capture(battle);
		_elapsed = 0;
		_applied = false;
		CombatCard? card = before.Hand.ElementAtOrDefault(index);
		if (command is CombatVisualCommand.PlayCard or CombatVisualCommand.ReplaceCard && card is not null)
		{
			View.Hand.RemoveAt(index);
			if (command == CombatVisualCommand.PlayCard)
			{
				View.ActionPoints = Math.Max(0, before.ActionPoints - card.ActionPointCost);
				_steps.Enqueue(new(CombatAnimationKind.PlayCard, 0.28, Card: card, CardIndex: index));
			}
			else
			{
				_steps.Enqueue(new(CombatAnimationKind.DiscardCard, 0.24, Card: card, CardIndex: index, FromHand: true));
				_steps.Enqueue(new(CombatAnimationKind.DrawCard, 0.28, Card: battle.Hand.ElementAtOrDefault(index), CardIndex: index));
			}
		}
		if (command == CombatVisualCommand.EndTurn) View.ActionPoints = 0;
		foreach (CombatEvent entry in battle.Events)
		{
			CombatAnimationKind kind = entry.Kind switch
			{
				CombatEventKind.Attack => CombatAnimationKind.Attack,
				CombatEventKind.Block => CombatAnimationKind.Defense,
				CombatEventKind.Charge => CombatAnimationKind.Charge,
				CombatEventKind.BlockExpired => CombatAnimationKind.BlockExpired,
				CombatEventKind.PlayerTurn => CombatAnimationKind.Deal,
				_ => CombatAnimationKind.Finish,
			};
			_steps.Enqueue(new(kind, kind is CombatAnimationKind.Attack or CombatAnimationKind.Defense ? 0.52 : 0.3, entry));
		}
		if (command == CombatVisualCommand.PlayCard && card is not null)
			_steps.Enqueue(new(CombatAnimationKind.DiscardCard, 0.22, Card: card, CardIndex: index));
		if (_steps.Count == 0) Complete();
	}

	public void Tick(double delta)
	{
		if (delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
		while (delta > 0 && Current is CombatAnimationStep step)
		{
			double consumed = Math.Min(delta, step.Duration - _elapsed);
			_elapsed += consumed;
			delta -= consumed;
			if (!_applied && Progress >= 0.5f)
			{
				Apply(step);
				_applied = true;
			}
			if (_elapsed + 0.000001 < step.Duration) break;
			_steps.Dequeue();
			_elapsed = 0;
			_applied = false;
			if (_steps.Count == 0) Complete();
		}
	}

	private void Apply(CombatAnimationStep step)
	{
		if (step.Kind == CombatAnimationKind.DrawCard && _final is not null)
		{
			View.Hand.Clear(); View.Hand.AddRange(_final.Hand);
		}
		if (step.Event is not CombatEvent entry) return;
		switch (entry.Kind)
		{
			case CombatEventKind.Attack:
				if (entry.TargetId is Guid target) View.Health[target] = Math.Max(0, View.Health[target] - entry.Damage);
				if (entry.TargetId == _playerId) View.Block = Math.Max(0, View.Block - entry.Blocked);
				else if (entry.TargetId is Guid enemyId)
					View.EnemyBlock[enemyId] = Math.Max(0, View.EnemyBlock[enemyId] - entry.Blocked);
				break;
			case CombatEventKind.Block:
				if (entry.ActorId == _playerId) View.Block += entry.Blocked;
				else View.EnemyBlock[entry.ActorId] += entry.Blocked;
				break;
			case CombatEventKind.BlockExpired: View.EnemyBlock[entry.ActorId] = 0; break;
			case CombatEventKind.PlayerTurn:
				if (_final is not null)
				{
					View.Block = 0; View.ActionPoints = _final.ActionPoints; View.MaxActionPoints = _final.MaxActionPoints;
					View.Phase = CombatPhase.PlayerTurn;
					View.Hand.Clear(); View.Hand.AddRange(_final.Hand);
					View.DrawCount = _final.DrawCount; View.DiscardCount = _final.DiscardCount;
				}
				break;
			case CombatEventKind.Victory: View.Phase = CombatPhase.Victory; break;
			case CombatEventKind.Defeat: View.Phase = CombatPhase.Defeat; break;
		}
		EventShown?.Invoke(entry);
	}
	private void Complete()
	{
		if (_final is not null) View = _final;
		_final = null;
	}
}
