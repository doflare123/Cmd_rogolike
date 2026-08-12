using Godot;

namespace CmdRoguelike.Core;

public enum CardinalDirection
{
	Up,
	Right,
	Down,
	Left,
}

public static class CardinalDirectionExtensions
{
	public static IReadOnlyList<CardinalDirection> All { get; } = Array.AsReadOnly(
		new[]
		{
			CardinalDirection.Up,
			CardinalDirection.Right,
			CardinalDirection.Down,
			CardinalDirection.Left,
		});

	public static Vector2I ToOffset(this CardinalDirection direction)
	{
		return direction switch
		{
			CardinalDirection.Up => Vector2I.Up,
			CardinalDirection.Right => Vector2I.Right,
			CardinalDirection.Down => Vector2I.Down,
			CardinalDirection.Left => Vector2I.Left,
			_ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null),
		};
	}

	public static CardinalDirection Opposite(this CardinalDirection direction)
	{
		return direction switch
		{
			CardinalDirection.Up => CardinalDirection.Down,
			CardinalDirection.Right => CardinalDirection.Left,
			CardinalDirection.Down => CardinalDirection.Up,
			CardinalDirection.Left => CardinalDirection.Right,
			_ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null),
		};
	}
}
