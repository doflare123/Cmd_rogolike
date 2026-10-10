using CmdRoguelike.Domain.Combat;
using CmdRoguelike.Domain.Rewards;
using Godot;

namespace CmdRoguelike.Domain.Entities;

public sealed class BruteEnemy : Enemy
{
	public override IEnemyBehavior Behavior => EnemyBehaviors.Brute;
	public override EnemyRewardRole RewardRole => EnemyRewardRole.Heavy;
	public BruteEnemy(Vector2I position) : base(position, "Громила", 5, 3) { }
}
