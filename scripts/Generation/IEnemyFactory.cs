using CmdRoguelike.Domain.Entities;
using Godot;

namespace CmdRoguelike.Generation;

internal interface IEnemyFactory
{
	Enemy Create(Vector2I position);
}
