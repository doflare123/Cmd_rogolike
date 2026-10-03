using CmdRoguelike.Core;
using Godot;

namespace CmdRoguelike.State;

/// <summary>Discovered terrain and current floor section; doors stop disclosure even when open.</summary>
internal sealed class DungeonKnowledge
{
	private readonly HashSet<Vector2I> _revealed = new();
	private readonly HashSet<Vector2I> _section = new();
	private static readonly Vector2I[] Corners = { new(-1, -1), new(1, -1), new(-1, 1), new(1, 1) };
	public bool IsRevealed(Vector2I position) => _revealed.Contains(position);
	public bool IsInCurrentSection(Vector2I position) => _section.Contains(position);

	public void Enter(Vector2I position, Func<Vector2I, DungeonTile> getTile, bool refresh = false)
	{
		_revealed.Add(position);
		if (getTile(position) != DungeonTile.Floor) return;
		if (!refresh && _section.Contains(position)) return;
		_section.Clear();
		_section.Add(position);
		Queue<Vector2I> pending = new();
		pending.Enqueue(position);
		while (pending.TryDequeue(out Vector2I cell))
		{
			_revealed.Add(cell);
			foreach (CardinalDirection direction in CardinalDirectionExtensions.All)
			{
				Vector2I next = cell + direction.ToOffset();
				DungeonTile tile = getTile(next);
				if (tile != DungeonTile.Empty) _revealed.Add(next);
				if (tile == DungeonTile.Floor && _section.Add(next)) pending.Enqueue(next);
			}
			// Wall corners are visible; diagonal floors and entities stay hidden.
			foreach (Vector2I offset in Corners)
				if (getTile(cell + offset) == DungeonTile.Wall) _revealed.Add(cell + offset);
		}
	}
}
