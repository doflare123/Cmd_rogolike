using CmdRoguelike.Domain.Combat;
using Godot;

namespace CmdRoguelike.Domain.Entities;

public sealed class BruteEnemy : Enemy
{
	public override IEnemyBehavior Behavior => EnemyBehaviors.Brute;
	public BruteEnemy(Vector2I position) : base(position, "Громила", 5, 3) { }
}
