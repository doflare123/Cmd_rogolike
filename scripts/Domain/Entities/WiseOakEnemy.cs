using CmdRoguelike.Domain.Combat;
using CmdRoguelike.Domain.Rewards;
using Godot;

namespace CmdRoguelike.Domain.Entities;

public sealed class WiseOakEnemy : Enemy
{
	public override bool IsBoss => true;
	public override DamageProfile DamageProfile { get; } = new(physicalArmor: 20, fireMultiplierPercent: 200);
	public override IEnemyBehavior Behavior => OakBehavior.Instance;
	public override EnemyRewardRole RewardRole => EnemyRewardRole.Defender;
	public override int RewardDifficulty => 24;
	public WiseOakEnemy(Vector2I position, int worldTier = 0)
		: base(position, "Мудрый дуб", checked(24 + worldTier * 6), checked(8 + worldTier * 2))
	{
		if (worldTier is < 0 or > 1000) throw new ArgumentOutOfRangeException(nameof(worldTier));
	}
	private sealed class OakBehavior : IEnemyBehavior
	{
		public static OakBehavior Instance { get; } = new();
		public EnemyAction Plan(Enemy enemy, int round) => (round % 3) switch
		{
			1 => new(EnemyActionKind.Guard, 2),
			2 => new(EnemyActionKind.Charge),
			_ => new(EnemyActionKind.Attack, enemy.AttackPower),
		};
	}
}
