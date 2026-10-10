using CmdRoguelike.Domain.Entities;
using Godot;

namespace CmdRoguelike.Generation;

/// <summary>
/// Взвешенный выбор типов отдельным RNG, без изменения геометрии и размещения.
/// </summary>
internal sealed class EnemyFactory : IEnemyFactory
{
	private readonly IRandomSource _random;
	public EnemyFactory(IRandomSource random)
	{
		ArgumentNullException.ThrowIfNull(random);
		_random = random;
	}
	public Enemy Create(Vector2I position)
	{
		return _random.NextInt(0, 9) switch
		{
			< 5 => new BasicEnemy(position),
			< 8 => new GuardianEnemy(position),
			_ => new BruteEnemy(position),
		};
	}
}
