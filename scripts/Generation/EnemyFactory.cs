using CmdRoguelike.Domain.Entities;
using Godot;

namespace CmdRoguelike.Generation;

/// <summary>
/// Центральная точка создания типов врагов. Когда в проекте появятся новые
/// подклассы Enemy, здесь можно будет добавить их взвешенный случайный выбор.
/// </summary>
internal sealed class EnemyFactory : IEnemyFactory
{
	public Enemy Create(Vector2I position)
	{
		return new BasicEnemy(position);
	}
}
