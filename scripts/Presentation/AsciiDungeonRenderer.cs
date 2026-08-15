using CmdRoguelike.Core;
using CmdRoguelike.Domain.Entities;
using CmdRoguelike.World;
using Godot;

namespace CmdRoguelike.Presentation;

/// <summary>
/// Рисует проекцию подземелья размером с область просмотра.
/// Отрисовка не влияет на генерацию или состояние игры.
/// </summary>
internal sealed class AsciiDungeonRenderer
{
	private const float Padding = 14.0f;
	private const float HeaderHeight = 50.0f;
	private const float FooterHeight = 30.0f;

	private static readonly Color BackgroundColor = new("080b0f");
	private static readonly Color PlayerColor = new("78e08f");
	private static readonly Color WallColor = new("9aa6b2");
	private static readonly Color ClosedDoorColor = new("f6c85f");
	private static readonly Color OpenDoorColor = new("d98b4e");
	private static readonly Color EnemyColor = new("e05252");

	public void Draw(
		Node2D canvas,
		DungeonMap map,
		string status,
		Vector2 viewportSize,
		AsciiRenderOptions options)
	{
		canvas.DrawRect(new Rect2(Vector2.Zero, viewportSize), BackgroundColor);
		Font font = ThemeDB.FallbackFont;
		DrawHeader(canvas, font, map);
		DrawMap(canvas, font, map, viewportSize, options);
		canvas.DrawString(
			font,
			new Vector2(Padding, viewportSize.Y - 9),
			status,
			HorizontalAlignment.Left,
			viewportSize.X - (Padding * 2),
			15,
			new Color("d5dce3"));
	}

	private static void DrawHeader(Node2D canvas, Font font, DungeonMap map)
	{
		PlayerCharacter player = map.Player;
		string info = $"{player.Name} HP {player.Health}/{player.MaxHealth}   SEED {map.Seed}   "
			+ $"AREAS {map.RegionCount}   ENEMIES {map.EnemyCount}   OPENED {map.OpenedDoorCount}";
		canvas.DrawString(
			font,
			new Vector2(Padding, 21),
			info,
			HorizontalAlignment.Left,
			-1,
			15,
			new Color("8aa0b5"));
		canvas.DrawString(
			font,
			new Vector2(Padding, 42),
			"WASD/стрелки — ход   E/Space — открыть   R — новый мир   Esc — выход",
			HorizontalAlignment.Left,
			-1,
			15,
			new Color("718394"));
	}

	private static void DrawMap(
		Node2D canvas,
		Font font,
		DungeonMap map,
		Vector2 viewportSize,
		AsciiRenderOptions options)
	{
		int columns = Math.Max(1, (int)((viewportSize.X - (Padding * 2)) / options.CellWidth));
		int rows = Math.Max(1, (int)((viewportSize.Y - HeaderHeight - FooterHeight) / options.CellHeight));
		Vector2I firstTile = map.Player.Position - new Vector2I(columns / 2, rows / 2);

		for (int screenY = 0; screenY < rows; screenY++)
		{
			for (int screenX = 0; screenX < columns; screenX++)
			{
				Vector2I worldPosition = firstTile + new Vector2I(screenX, screenY);
				(string symbol, Color color) = GetAppearance(map, worldPosition);

				if (symbol.Length == 0)
				{
					continue;
				}

				Vector2 drawPosition = new(
					Padding + (screenX * options.CellWidth) + 1,
					HeaderHeight + (screenY * options.CellHeight) + options.FontSize);
				canvas.DrawString(
					font,
					drawPosition,
					symbol,
					HorizontalAlignment.Left,
					-1,
					options.FontSize,
					color);
			}
		}
	}

	private static (string Symbol, Color Color) GetAppearance(
		DungeonMap map,
		Vector2I position)
	{
		DungeonEntity? entity = map.GetEntityAt(position);
		if (entity is PlayerCharacter)
		{
			return ("@", PlayerColor);
		}

		if (entity is Enemy)
		{
			return ("e", EnemyColor);
		}

		return GetTileAppearance(map.GetTile(position));
	}

	private static (string Symbol, Color Color) GetTileAppearance(DungeonTile tile)
	{
		return tile switch
		{
			DungeonTile.Wall => ("#", WallColor),
			DungeonTile.ClosedDoor => ("+", ClosedDoorColor),
			DungeonTile.OpenDoor => ("/", OpenDoorColor),
			_ => (string.Empty, Colors.Transparent),
		};
	}
}
