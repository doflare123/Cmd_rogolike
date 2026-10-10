using CmdRoguelike.Domain.Combat;
using Godot;

namespace CmdRoguelike.Domain.Entities;

public sealed class GuardianEnemy : Enemy
{
	public override IEnemyBehavior Behavior => EnemyBehaviors.Guardian;
	public GuardianEnemy(Vector2I position) : base(position, "Защитник", 4, 1) { }
}
