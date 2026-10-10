using CmdRoguelike.Domain.Combat;
using CmdRoguelike.Domain.Rewards;
using Godot;

namespace CmdRoguelike.Domain.Entities;

public sealed class GuardianEnemy : Enemy
{
	public override IEnemyBehavior Behavior => EnemyBehaviors.Guardian;
	public override EnemyRewardRole RewardRole => EnemyRewardRole.Defender;
	public override int RewardDifficulty => checked(base.RewardDifficulty + 2);
	public GuardianEnemy(Vector2I position) : base(position, "Защитник", 4, 1) { }
}
