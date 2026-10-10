using Godot;

namespace CmdRoguelike.Presentation;

/// <summary>Drawing only; each screen owns its input and reads already calculated values.</summary>
internal abstract partial class TerminalOverlay : CanvasLayer
{
	protected const int Columns = 112, Rows = 42;
	protected static readonly Color Ink = new("cad5cc"), Muted = new("61786d"), Accent = new("93d8a0"), Gold = new("efd17b");
	protected readonly record struct Line(int X, int Y, string Text, Color Color);
	private readonly Node2D _canvas = new();
	private readonly SystemFont _font = new() { FontNames = new[] { "Consolas", "DejaVu Sans Mono", "Liberation Mono", "Courier New" } };
	protected abstract List<Line> Compose();
	internal string ScreenText => string.Join('\n', Compose().Select(l => l.Text));
	public override void _Ready()
	{
		AddChild(_canvas); _canvas.Draw += Render;
		GetViewport().SizeChanged += Refresh; Refresh();
	}
	public override void _ExitTree() => GetViewport().SizeChanged -= Refresh;
	protected void Refresh() => _canvas.QueueRedraw();
	protected static void Put(List<Line> lines, int x, int y, string text, Color color, int width = 108)
	{
		if (text.Length > width) text = text[..Math.Max(0, width - 3)] + "...";
		lines.Add(new(x, y, text, color));
	}
	protected static void Box(List<Line> lines, int x, int y, int width, int height, string title)
	{
		Put(lines, x, y, "+" + ("-- " + title + " ").PadRight(width - 2, '-') + "+", Muted, width);
		for (int row = 1; row < height - 1; row++)
		{
			Put(lines, x, y + row, "|", Muted); Put(lines, x + width - 1, y + row, "|", Muted);
		}
		Put(lines, x, y + height - 1, "+" + new string('-', width - 2) + "+", Muted, width);
	}
	private void Render()
	{
		Vector2 viewport = GetViewport().GetVisibleRect().Size;
		_canvas.DrawRect(new Rect2(Vector2.Zero, viewport), new Color("080b0f"));
		float cell = _font.GetStringSize("M", fontSize: 16).X;
		const float rowHeight = 18;
		float scale = Math.Min(1.25f, Math.Min(viewport.X / ((Columns + 2) * cell), viewport.Y / ((Rows + 2) * rowHeight)));
		var origin = (viewport - new Vector2(Columns * cell, Rows * rowHeight) * scale) / 2;
		_canvas.DrawSetTransform(origin, 0, Vector2.One * scale);
		foreach (var line in Compose())
			for (int i = 0; i < line.Text.Length; i++)
				if (line.Text[i] != ' ') _canvas.DrawString(_font,
					new Vector2((line.X + i) * cell, line.Y * rowHeight + 14), line.Text[i].ToString(),
					HorizontalAlignment.Left, -1, 16, line.Color);
		_canvas.DrawSetTransform(Vector2.Zero);
	}
}
