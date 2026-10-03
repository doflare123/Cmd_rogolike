using CmdRoguelike.Domain.Combat;
using CmdRoguelike.Domain.Entities;
using CmdRoguelike.World;
using Godot;

namespace CmdRoguelike.Presentation;

/// <summary>Keyboard controller for the perspective ASCII combat view. Commands delegate to World.</summary>
internal sealed partial class CombatPanel : CanvasLayer
{
	private const int Columns = CombatAsciiFrame.Columns, Rows = CombatAsciiFrame.Rows;
	private readonly DungeonMap _map;
	private readonly Action _close;
	private readonly Node2D _canvas = new();
	private readonly SystemFont _font = new() { FontNames = new[] { "Consolas", "DejaVu Sans Mono", "Liberation Mono", "Courier New" } };
	private readonly List<string> _log = new();
	private int _cardIndex, _targetIndex;
	private bool _journalOpen;
	private int _journalOffset;
	private string _playedCardName = "Атака";
	private Vector2 _renderOrigin;
	private Vector2 _renderCell;
	private readonly CombatAnimation _animation;
	private double _fadeIn = 0.35;
	private string _status = "Проверьте намерения. ENTER начинает раунд.";
	private CombatEncounter Battle => _map.Combat ?? throw new InvalidOperationException("Combat panel has no encounter.");
	internal string ScreenText => Compose().Text;
	internal bool IsAnimating => _animation.IsBusy || _fadeIn > 0;
	internal CombatAnimation Animation => _animation;
	internal bool IsJournalOpen => _journalOpen;
	internal IReadOnlyList<string> JournalEntries => _log;
	internal Rect2 JournalButtonBounds => new(_renderOrigin + CombatAsciiRenderer.JournalButton.Position * _renderCell,
		CombatAsciiRenderer.JournalButton.Size * _renderCell);

	public CombatPanel(DungeonMap map, Action close)
	{
		_map = map;
		_close = close;
		_animation = new CombatAnimation(map.Combat ?? throw new ArgumentException("Missing encounter.", nameof(map)));
		_animation.EventShown += AddEventToLog;
		Layer = 1;
	}
	public override void _Ready()
	{
		AddChild(_canvas);
		_canvas.Draw += Render;
		GetViewport().SizeChanged += Refresh;
		Refresh();
	}
	public override void _ExitTree() => GetViewport().SizeChanged -= Refresh;
	public override void _Process(double delta)
	{
		if (!IsAnimating || _journalOpen) return;
		_fadeIn = Math.Max(0, _fadeIn - delta);
		_animation.Tick(delta);
		Refresh();
	}
	private Enemy[] Targets() => Battle.Enemies.Where(enemy => enemy.IsAlive).ToArray();
	private void Refresh()
	{
		_cardIndex = Math.Clamp(_cardIndex, 0, Math.Max(0, Battle.Hand.Count - 1));
		_targetIndex = Math.Clamp(_targetIndex, 0, Math.Max(0, Targets().Length - 1));
		_canvas.QueueRedraw();
	}
	public void HandleKey(InputEventKey key)
	{
		if (!key.Pressed) return;
		if (key.Keycode == Key.L && !key.Echo && _fadeIn <= 0)
		{
			ToggleJournal();
			return;
		}
		if (_journalOpen)
		{
			switch (key.Keycode)
			{
				case Key.Escape: ToggleJournal(); break;
				case Key.W: case Key.Up: ScrollJournal(-1); break;
				case Key.S: case Key.Down: ScrollJournal(1); break;
				case Key.Pageup: ScrollJournal(-CombatAsciiRenderer.JournalPageSize); break;
				case Key.Pagedown: ScrollJournal(CombatAsciiRenderer.JournalPageSize); break;
				case Key.Home: ScrollJournal(-_log.Count); break;
				case Key.End: ScrollJournal(_log.Count); break;
			}
			return;
		}
		if (IsAnimating) return;
		if (key.Keycode is Key.Left or Key.A) _cardIndex--;
		else if (key.Keycode is Key.Right or Key.D) _cardIndex++;
		else if (key.Keycode is Key.Up or Key.W) _targetIndex--;
		else if (key.Keycode is Key.Down or Key.S) _targetIndex++;
		else if (!key.Echo)
		{
			if (Battle.Phase == CombatPhase.Victory && key.Keycode is Key.Enter or Key.F)
			{
				if (_map.LeaveVictoriousCombat()) _close();
				return;
			}
			CombatCommandResult? result = null;
			bool replacement = false;
			CombatVisualCommand command = CombatVisualCommand.PlayCard;
			CombatVisualState before = CombatVisualState.Capture(Battle);
			CombatCard? selectedCard = before.Hand.ElementAtOrDefault(_cardIndex);
			switch (key.Keycode)
			{
				case Key.Enter: case Key.E:
					command = Battle.Phase == CombatPhase.RoundPreview ? CombatVisualCommand.BeginRound : CombatVisualCommand.PlayCard;
					result = Battle.Phase == CombatPhase.RoundPreview ? _map.BeginCombatRound()
						: _map.PlayCombatCard(_cardIndex, Targets().ElementAtOrDefault(_targetIndex)?.Id);
					break;
				case Key.Q:
					command = CombatVisualCommand.ReplaceCard;
					result = _map.ReplaceCombatCard(_cardIndex);
					replacement = true;
					break;
				case Key.Space: command = CombatVisualCommand.EndTurn; result = _map.EndCombatTurn(); break;
			}
			if (result is not null)
			{
				ShowResult(result.Value, replacement);
				if (result == CombatCommandResult.Success)
				{
					if (command == CombatVisualCommand.PlayCard) _playedCardName = selectedCard?.Name ?? "Атака";
					if (command == CombatVisualCommand.ReplaceCard)
						AppendLog(before.Round, $"Герой: замена {selectedCard?.Name} -> {Battle.Hand.ElementAtOrDefault(_cardIndex)?.Name}, 0 AP.");
					if (command == CombatVisualCommand.EndTurn) AppendLog(before.Round, "Герой завершил ход.");
					_animation.Start(before, Battle, command, _cardIndex);
				}
			}
		}
		Refresh();
	}
	internal bool HandleMouse(InputEventMouseButton mouse)
	{
		if (!mouse.Pressed || _fadeIn > 0) return false;
		if (mouse.ButtonIndex == MouseButton.Left && JournalButtonBounds.HasPoint(mouse.Position))
		{
			ToggleJournal();
			return true;
		}
		if (!_journalOpen) return false;
		if (mouse.ButtonIndex == MouseButton.WheelUp) ScrollJournal(-3);
		if (mouse.ButtonIndex == MouseButton.WheelDown) ScrollJournal(3);
		return true;
	}
	private void ToggleJournal()
	{
		_journalOpen = !_journalOpen;
		if (_journalOpen) _journalOffset = Math.Max(0, _log.Count - CombatAsciiRenderer.JournalPageSize);
		Refresh();
	}
	private void ScrollJournal(int amount)
	{
		_journalOffset = Math.Clamp(_journalOffset + amount, 0, Math.Max(0, _log.Count - CombatAsciiRenderer.JournalPageSize));
		Refresh();
	}

	private void ShowResult(CombatCommandResult result, bool replacement)
	{
		_status = result switch
		{
			CombatCommandResult.InvalidCard => "Выберите карту в руке.",
			CombatCommandResult.InvalidTarget => "Выберите живую цель клавишами W/S.",
			CombatCommandResult.NotEnoughActionPoints => "Недостаточно AP. SPACE завершает ход.",
			CombatCommandResult.ReplacementUnavailable => "Замена доступна раз в два собственных хода.",
			CombatCommandResult.WrongPhase => "Сейчас действие недоступно.",
			_ => replacement ? "Карта заменена без расхода AP." : "",
		};
	}

	private void AddEventToLog(CombatEvent entry)
	{
		string actor = entry.ActorId == Battle.Player.Id ? "Герой" : EnemyLabel(entry.ActorId);
		string target = entry.TargetId == Battle.Player.Id ? "герой" : EnemyLabel(entry.TargetId);
		string action = entry.ActorId == Battle.Player.Id ? _playedCardName : "Атака";
		string message = entry.Kind switch
		{
			CombatEventKind.Attack => $"{actor}: {action} -> {target}: {entry.Damage} урона, {entry.Blocked} поглощено.",
			CombatEventKind.Block => $"Герой: {_playedCardName}, +{entry.Blocked} блока от брони.",
			CombatEventKind.PlayerTurn => $"Ход героя {Battle.PlayerTurn}: AP восстановлены, новая рука.",
			CombatEventKind.Victory => "Победа. Можно продолжить исследование.",
			_ => "Герой погиб. R создаёт нового героя без прежних вещей.",
		};
		AppendLog(_animation.View.Round, message);
	}
	private void AppendLog(int round, string message) => _log.Add($"Раунд {round} | {message}");
	private string EnemyLabel(Guid? id)
	{
		int index = Battle.Enemies.ToList().FindIndex(enemy => enemy.Id == id);
		return index < 0 ? "-" : $"e{index + 1}";
	}

	private CombatAsciiFrame Compose()
		=> CombatAsciiRenderer.Compose(Battle, _animation, _cardIndex, _targetIndex, _status, _log, _journalOpen, _journalOffset);

	private void Render()
	{
		Vector2 viewport = GetViewport().GetVisibleRect().Size;
		_canvas.DrawRect(new Rect2(Vector2.Zero, viewport), new Color("080b0f"));
		float cell = _font.GetStringSize("M", fontSize: 16).X;
		const float rowHeight = 16;
		float scale = Math.Min(1.25f, Math.Min(viewport.X / ((Columns + 2) * cell), viewport.Y / ((Rows + 2) * rowHeight)));
		Vector2 origin = (viewport - new Vector2(Columns * cell, Rows * rowHeight) * scale) / 2;
		_renderOrigin = origin;
		_renderCell = new Vector2(cell, rowHeight) * scale;
		_canvas.DrawSetTransform(origin, 0, Vector2.One * scale);
		CombatAsciiFrame frame = Compose();
		foreach (var panel in frame.Panels)
			_canvas.DrawRect(new Rect2(panel.X * cell, panel.Y * rowHeight, panel.Width * cell, panel.Height * rowHeight), panel.Color);
		foreach (var glyph in frame.Glyphs())
			_canvas.DrawString(_font, new Vector2(glyph.X * cell, glyph.Y * rowHeight + 14), glyph.Character.ToString(),
				HorizontalAlignment.Left, -1, 16, glyph.Color);
		_canvas.DrawSetTransform(Vector2.Zero);
		if (_fadeIn > 0) _canvas.DrawRect(new Rect2(Vector2.Zero, viewport), new Color(0, 0, 0, (float)(_fadeIn / 0.35)));
	}
}
