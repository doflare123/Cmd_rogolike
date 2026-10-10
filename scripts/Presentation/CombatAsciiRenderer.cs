using CmdRoguelike.Domain.Combat;
using CmdRoguelike.Domain.Entities;
using CmdRoguelike.Domain.Stats;
using Godot;

namespace CmdRoguelike.Presentation;

/// <summary>Painter's ASCII buffer: even spaces overwrite scenery behind a card or figure.</summary>
internal sealed class CombatAsciiFrame
{
	public const int Columns = 112, Rows = 50;
	public readonly record struct Panel(int X, int Y, int Width, int Height, Color Color);
	public List<Panel> Panels { get; } = new();
	private readonly char[,] _characters = new char[Rows, Columns];
	private readonly Color[,] _colors = new Color[Rows, Columns];
	public void Write(int x, int y, string text, Color color, int width = Columns)
	{
		if (y < 0 || y >= Rows) return;
		for (int i = 0; i < Math.Min(text.Length, width); i++)
			if (x + i >= 0 && x + i < Columns)
			{
				_characters[y, x + i] = text[i];
				_colors[y, x + i] = color;
			}
	}
	public void Clear(int x, int y, int width, int height)
	{
		for (int row = 0; row < height; row++) Write(x, y + row, new string(' ', width), Colors.Transparent);
	}
	public IEnumerable<(int X, int Y, char Character, Color Color)> Glyphs()
	{
		for (int y = 0; y < Rows; y++)
			for (int x = 0; x < Columns; x++)
				if (_characters[y, x] is not '\0' and not ' ') yield return (x, y, _characters[y, x], _colors[y, x]);
	}
	public string Text => string.Join('\n', Enumerable.Range(0, Rows).Select(y =>
		new string(Enumerable.Range(0, Columns).Select(x => _characters[y, x] == '\0' ? ' ' : _characters[y, x]).ToArray())));
}

/// <summary>Decorative perspective stage, independent of procedural world geometry and rules.</summary>
internal static class CombatAsciiRenderer
{
	internal static readonly Rect2 JournalButton = new(84, 44, 26, 3);
	internal const int JournalPageSize = 24;
	private static readonly Color Ink = new("cdd8d4"), Muted = new("647a78"), Accent = new("a4e8bf"),
		Amber = new("e8bc78"), Danger = new("ec8f89"), Stone = new("435658"), Floor = new("263437"), Shadow = new("344748");
	private static readonly string[] HeroArt = { "    .-.    ", "    (@)  / ", "   /|#|\\/  ", "  / |#|    ", "   /   \\   " };
	private static readonly string[] EnemyArt = { "    .---.    ", "   / o o \\   ", "   |  v  |   ", "  /|/---\\|\\  ", "   /     \\   " };
	private static readonly string[] GuardianArt = { "    .---.    ", "    |o o|    ", " .--|---|--. ", " | /\\ | /|  ", " | \\/ |/ \\  " };
	private static readonly string[] BruteArt = { "   .-----.   ", "  / >   < \\  ", " /|  ===  |\\ ", "[ |=======| ]", "  /|     |\\  " };
	private static readonly string[] SwordArt = { "        / ", "       /  ", "      /   ", "   --+--  ", "    /     " };
	private static readonly string[] ShieldArt = { "  .----.  ", "  | /\\ |  ", "  | \\/ |  ", "   \\  /   ", "    \\/    " };

	internal static CombatAsciiFrame Compose(CombatEncounter battle, CombatAnimation animation, int cardIndex,
		int targetIndex, string status, IReadOnlyList<string> log, bool journalOpen = false, int journalOffset = 0)
	{
		var frame = new CombatAsciiFrame();
		var view = animation.View;
		var hero = battle.Player;
		void Put(int x, int y, string text, Color color, int width = CombatAsciiFrame.Columns) => frame.Write(x, y, text, color, width);
		Put(2, 0, "C M D   R O G U E L I K E", Accent);
		Put(74, 0, $"КАРТОЧНЫЙ БОЙ / РАУНД {view.Round}", Muted);
		Stats(frame, battle, animation);
		string phase = animation.IsBusy ? "ДЕЙСТВИЕ / " + AnimationLabel(animation.Current!.Kind) : view.Phase switch
		{
			CombatPhase.RoundPreview => "НАМЕРЕНИЯ / ENTER НАЧАТЬ РАУНД",
			CombatPhase.PlayerTurn => "ХОД ГЕРОЯ / ВЫБЕРИТЕ КАРТУ",
			CombatPhase.Victory => "ПОБЕДА / ENTER К КАРТЕ",
			_ => "ГЕРОЙ ПОГИБ / R НОВАЯ ПОДГОТОВКА",
		};
		Put(2, 9, phase, view.Phase == CombatPhase.Defeat ? Danger : Amber);
		Room(frame);
		var step = animation.Current;
		float progress = animation.Progress;
		float lunge = step?.Kind == CombatAnimationKind.Attack ? MathF.Sin(progress * MathF.PI) : 0;
		int heroOffset = step?.Event?.ActorId == hero.Id ? (int)Math.Round(lunge * 10) : 0;
		int recoil = step?.Kind == CombatAnimationKind.Attack && step.Event?.TargetId == hero.Id && progress > 0.45f
			? (int)Math.Round(MathF.Sin(progress * MathF.PI * 5)) : 0;
		Sprite(frame, 24 + heroOffset + recoil, 17, HeroArt, view.Health[hero.Id] > 0 ? Accent : Muted);
		Put(25, 22, " '-------' ", Shadow);
		Put(25, 23, "ГЕРОЙ / @", Accent);

		var selected = battle.Enemies.Where(enemy => view.Health[enemy.Id] > 0).ElementAtOrDefault(targetIndex);
		int selectedIndex = selected is null ? 0 : battle.Enemies.ToList().IndexOf(selected);
		Guid? activeId = step?.Event is CombatEvent entry
			? entry.ActorId == hero.Id ? entry.TargetId : entry.ActorId : null;
		if (activeId is Guid active)
			selectedIndex = Math.Max(0, battle.Enemies.ToList().FindIndex(enemy => enemy.Id == active));
		int firstEnemy = selectedIndex / 3 * 3;
		Put(3, 26, $"[W/S] ЦЕЛЬ   {firstEnemy + 1}-{Math.Min(firstEnemy + 3, battle.Enemies.Count)}/{battle.Enemies.Count}", Muted, 40);
		for (int i = firstEnemy; i < Math.Min(battle.Enemies.Count, firstEnemy + 3); i++)
		{
			var enemy = battle.Enemies[i];
			var position = EnemyPosition(i - firstEnemy);
			int x = position.X, y = position.Y;
			bool alive = view.Health[enemy.Id] > 0;
			var intent = view.Intents.FirstOrDefault(intent => intent.EnemyId == enemy.Id);
			Color color = !alive ? Muted : enemy == selected || enemy.Id == activeId ? Accent : Danger;
			Put(x - 1, y - 2, enemy.Name, color, 18);
			Put(x - 1, y - 1, $"{(enemy == selected ? '>' : ' ')} e{i + 1} HP {view.Health[enemy.Id]}/{enemy.MaxHealth}".PadRight(18), color, 18);
			int offset = step?.Event?.ActorId == enemy.Id ? -(int)Math.Round(lunge * 8) : 0;
			int hit = step?.Kind == CombatAnimationKind.Attack && step.Event?.TargetId == enemy.Id && progress > 0.45f
				? (int)Math.Round(MathF.Sin(progress * MathF.PI * 5)) : 0;
			if (alive) Sprite(frame, x + offset + hit, y,
				enemy is GuardianEnemy ? GuardianArt : enemy is BruteEnemy ? BruteArt : EnemyArt, color);
			else
			{
				frame.Clear(x, y, 13, 5);
				Put(x + 3, y + 3, "_x_x_", Muted);
			}
			Put(x, y + 5, " '-------' ", Shadow);
			string intention = intent?.Action switch
			{
				EnemyActionKind.Guard => $" ! +{intent.Block} блока",
				EnemyActionKind.Charge => " ! Готовит удар",
				_ => intent?.Damage > 1 ? $" ! Удар {intent.Damage} -> @" : $" ! {intent?.Damage} -> @",
			};
			Put(x - 1, y + 6, alive ? intention : " ПОВЕРЖЕН ", color, 18);
			Put(x - 1, y + 7, alive ? intent?.BeforePlayer == true ? " ДО ГЕРОЯ " : " ПОСЛЕ ГЕРОЯ " : "", Muted, 17);
			if (alive && view.EnemyBlock[enemy.Id] > 0) Put(x - 1, y + 8, $" БЛОК {view.EnemyBlock[enemy.Id]}", Amber, 18);
		}
		Effects(frame, battle, animation, firstEnemy);
		string EnemyLabel(Guid id) => "e" + (battle.Enemies.ToList().FindIndex(enemy => enemy.Id == id) + 1);
		bool Alive(EnemyIntent intent) => view.Health[intent.EnemyId] > 0;
		Put(2, 27, "ОЧЕРЕДЬ  " + string.Join(" > ", view.Intents.Where(intent => intent.BeforePlayer && Alive(intent))
			.Select(intent => EnemyLabel(intent.EnemyId)).Concat(new[] { "@" })
			.Concat(view.Intents.Where(intent => !intent.BeforePlayer && Alive(intent)).Select(intent => EnemyLabel(intent.EnemyId)))), Muted, 108);
		Put(21, 28, $"РУКА {view.Hand.Count}   " + (animation.IsBusy ? "ДЕЙСТВИЕ ВЫПОЛНЯЕТСЯ" : view.CanReplace
			? "[Q] БЕСПЛАТНАЯ ЗАМЕНА" : $"Замена через {view.ReplacementInTurns} ход(а)"), Amber, 90);
		Pile(frame, 2, 30, "КОЛОДА", view.DrawCount, true);
		Pile(frame, 2, 38, "СБРОС", view.DiscardCount, false);
		int firstCard = cardIndex / 5 * 5;
		for (int i = firstCard; i < Math.Min(view.Hand.Count, firstCard + 5); i++)
		{
			CombatCard card = view.Hand[i];
			int x = 21 + (i - firstCard) * 18, y = i == cardIndex ? 30 : 31;
			Color color = i == cardIndex ? Accent : card.Kind == CombatCardKind.Attack ? Danger : Amber;
			Card(frame, x, y, 16, 11, card.Name.ToUpperInvariant(), color);
			Put(x + 2, y + 1, $"Цена: {card.ActionPointCost} AP", Ink, 12);
			Sprite(frame, x + 3, y + 2, card.Kind == CombatCardKind.Attack ? SwordArt : ShieldArt, color);
			Put(x + 2, y + 7, card.Kind == CombatCardKind.Attack ? $"{card.Power} урона" : $"{battle.DefenseBlock(card)} блока", Ink, 12);
			Put(x + 2, y + 8, card.SourceName, Muted, 12);
			if (!battle.IsCardAvailable(card)) Put(x + 2, y + 9, "DORMANT", Danger, 12);
			else if (i == cardIndex && view.Phase == CombatPhase.PlayerTurn && !animation.IsBusy) Put(x + 2, y + 9, "[ENTER]", Accent, 12);
		}
		if (view.Hand.Count == 0) Put(35, 35, "Рука появится в начале хода героя.", Muted);
		FlyingCard(frame, animation);
		if (log.Count > 0) Put(21, 44, log[^1], Ink, 61);
		Put(2, 45, "> " + status, Amber, 79);
		frame.Panels.Add(new(84, 44, 26, 3, new Color("193b2b")));
		Box(frame, 84, 44, 26, 3, "", Accent);
		Put(86, 45, "[L] ЖУРНАЛ БОЯ", Accent, 22);
		Put(2, 48, "[A/D] карта  [W/S] цель  [ENTER/E] играть  [Q] заменить  [SPACE] конец хода", Accent, 108);
		Put(2, 49, "[I] инвентарь  [R] сброс героя  [ESC] выход   Двери доступны после победы.", Muted, 108);
		if (journalOpen) Journal(frame, log, journalOffset);
		return frame;
	}

	private static void Stats(CombatAsciiFrame frame, CombatEncounter battle, CombatAnimation animation)
	{
		var view = animation.View;
		frame.Panels.Add(new(2, 2, 76, 7, new Color("102025")));
		frame.Panels.Add(new(81, 2, 29, 7, new Color("193b2b")));
		Box(frame, 2, 2, 76, 7, "ГЕРОЙ / ХАРАКТЕРИСТИКИ", Accent);
		Box(frame, 81, 2, 29, 7, "ОЧКИ ДЕЙСТВИЯ", Accent);
		frame.Write(5, 4, $"ЗДОРОВЬЕ  {view.Health[battle.Player.Id]}/{battle.Player.MaxHealth}", Ink, 28);
		int filled = (int)((long)view.Health[battle.Player.Id] * 22 / battle.Player.MaxHealth);
		frame.Write(5, 6, "[" + new string('#', filled) + new string('-', 22 - filled) + "]", Danger);
		frame.Write(35, 4, $"БЛОК   {view.Block}", Amber, 18);
		frame.Write(35, 6, $"БРОНЯ  {battle.Player.DerivedStats.GetValue(DerivedStatId.Armor)}", Ink, 18);
		frame.Write(56, 4, $"ВОЛЯ {battle.Player.Attributes.GetValue(AttributeId.Willpower)}", Ink, 19);
		frame.Write(56, 6, $"MP {battle.Player.Resources.Mana}/{battle.Player.Resources.MaxMana}", Ink, 19);
		string current = view.ActionPoints.ToString();
		if (current.Length <= 3)
			for (int i = 0; i < current.Length; i++) Sprite(frame, 84 + i * 4, 3, Digit(current[i]), Accent);
		else frame.Write(84, 4, current, Accent, 12);
		frame.Write(98, 4, "/ " + view.MaxActionPoints, Ink, 10);
		frame.Write(95, 6, $"AP {view.ActionPoints}/{view.MaxActionPoints}", Accent, 13);
	}
	private static string[] Digit(char digit) => digit switch
	{
		'0' => new[] { "###", "# #", "# #", "# #", "###" },
		'1' => new[] { " # ", "## ", " # ", " # ", "###" },
		'2' => new[] { "###", "  #", "###", "#  ", "###" },
		'3' => new[] { "###", "  #", "###", "  #", "###" },
		'4' => new[] { "# #", "# #", "###", "  #", "  #" },
		'5' => new[] { "###", "#  ", "###", "  #", "###" },
		'6' => new[] { "###", "#  ", "###", "# #", "###" },
		'7' => new[] { "###", "  #", "  #", " # ", " # " },
		'8' => new[] { "###", "# #", "###", "# #", "###" },
		_ => new[] { "###", "# #", "###", "  #", "###" },
	};
	private static string AnimationLabel(CombatAnimationKind kind) => kind switch
	{
		CombatAnimationKind.Attack => "УДАР", CombatAnimationKind.Defense => "ЗАЩИТА",
		CombatAnimationKind.Charge => "ПОДГОТОВКА УДАРА",
		CombatAnimationKind.BlockExpired => "БЛОК ИСЧЕЗАЕТ",
		CombatAnimationKind.PlayCard => "РОЗЫГРЫШ КАРТЫ", CombatAnimationKind.DiscardCard => "КАРТА В СБРОС",
		CombatAnimationKind.DrawCard or CombatAnimationKind.Deal => "ДОБОР КАРТ", _ => "ЗАВЕРШЕНИЕ",
	};
	private static Vector2I EnemyPosition(int local) => local switch
	{
		0 => new(49, 16), 1 => new(69, 16), _ => new(88, 16),
	};
	private static void Effects(CombatAsciiFrame frame, CombatEncounter battle, CombatAnimation animation, int firstEnemy)
	{
		var step = animation.Current;
		if (step is null) return;
		float p = animation.Progress;
		var target = step.Event?.TargetId == battle.Player.Id ? new Vector2I(24, 17)
			: EnemyPosition(Math.Max(0, battle.Enemies.ToList().FindIndex(enemy => enemy.Id == step.Event?.TargetId) - firstEnemy));
		if (step.Kind == CombatAnimationKind.Charge)
		{
			Box(frame, target.X - 1, target.Y - 1, 15, 7, "", Danger);
			frame.Write(target.X - 1, target.Y - 2, "ГОТОВИТ УДАР", Amber, 18);
		}
		if (step.Kind == CombatAnimationKind.Defense || step.Kind == CombatAnimationKind.Attack && step.Event?.Blocked > 0 && p > 0.4f)
		{
			int width = 13 + (int)Math.Round(MathF.Sin(p * MathF.PI) * 4);
			Box(frame, target.X - 2, target.Y - 1, width, 7, "", Amber);
			frame.Write(target.X, target.Y - 2, step.Kind == CombatAnimationKind.Defense ? $"+{step.Event?.Blocked} БЛОКА" : "БЛОК!", Amber, 17);
		}
		if (step.Kind != CombatAnimationKind.Attack || p < 0.42f || step.Event is not CombatEvent entry) return;
		frame.Write(target.X + 1, target.Y + 2, p < 0.72f ? "  / * / " : " *     * ", entry.Damage > 0 ? Danger : Amber);
		frame.Write(target.X, target.Y - 2, entry.Damage > 0 ? $" -{entry.Damage} HP " : " ПОГЛОЩЕНО ", entry.Damage > 0 ? Danger : Amber, 16);
	}
	private static void FlyingCard(CombatAsciiFrame frame, CombatAnimation animation)
	{
		var step = animation.Current;
		if (step is null) return;
		CombatCard? card = step.Card;
		Vector2 from, to;
		var hand = new Vector2(21 + (step.CardIndex % 5) * 18, 30);
		switch (step.Kind)
		{
			case CombatAnimationKind.PlayCard: from = hand; to = new(48, 18); break;
			case CombatAnimationKind.DiscardCard: from = step.FromHand ? hand : new(48, 18); to = new(3, 38); break;
			case CombatAnimationKind.DrawCard: from = new(3, 30); to = hand; break;
			case CombatAnimationKind.Deal: from = new(3, 30); to = new(57, 30); card = animation.View.Hand.FirstOrDefault(); break;
			default: return;
		}
		if (card is null) return;
		float p = animation.Progress;
		float ease = p * p * (3 - 2 * p);
		Vector2 point = from.Lerp(to, ease) - new Vector2(0, MathF.Sin(p * MathF.PI) * 5);
		int x = (int)Math.Round(point.X), y = (int)Math.Round(point.Y);
		Color color = card.Kind == CombatCardKind.Attack ? Danger : Amber;
		Card(frame, x, y, 12, 7, card.Name.ToUpperInvariant(), color);
		frame.Write(x + 2, y + 2, card.Kind == CombatCardKind.Attack ? "   /" : "  /\\", color, 8);
		frame.Write(x + 2, y + 3, card.Kind == CombatCardKind.Attack ? "--+--" : "  \\/", color, 8);
		frame.Write(x + 2, y + 5, "В ДЕЙСТВИИ", Ink, 8);
	}
	private static void Box(CombatAsciiFrame frame, int x, int y, int width, int height, string title, Color color)
	{
		frame.Write(x, y, "." + ("-- " + title + " ").PadRight(width - 2, '-')[..(width - 2)] + ".", color);
		for (int row = 1; row < height - 1; row++)
		{
			frame.Write(x, y + row, "|", color);
			frame.Write(x + width - 1, y + row, "|", color);
		}
		frame.Write(x, y + height - 1, "'" + new string('-', width - 2) + "'", color);
	}
	private static void Room(CombatAsciiFrame frame)
	{
		// Frontal room perspective: back wall, two side walls, trapezoid floor.
		Line(frame, 24, 12, 88, 12, '_', Stone);
		Line(frame, 24, 17, 88, 17, '_', Stone);
		foreach (int x in new[] { 24, 88 }) Line(frame, x, 12, x, 17, '|', Stone);
		Line(frame, 4, 11, 24, 12, '\\', Stone);
		Line(frame, 88, 12, 108, 11, '/', Stone);
		Line(frame, 4, 25, 24, 17, '/', Stone);
		Line(frame, 88, 17, 108, 25, '\\', Stone);
		foreach (int x in new[] { 4, 108 }) Line(frame, x, 11, x, 25, '|', Stone);
		Line(frame, 4, 25, 108, 25, '_', Stone);
		foreach (int y in new[] { 19, 22 }) Line(frame, 24 - (y - 17) * 2, y, 88 + (y - 17) * 2, y, '-', Floor);
		foreach (int x in new[] { 17, 39, 73, 95 })
			Line(frame, 56 + (x - 56) / 2, 18, x, 24, x < 56 ? '/' : '\\', Floor);
	}

	private static void Journal(CombatAsciiFrame frame, IReadOnlyList<string> log, int offset)
	{
		frame.Clear(10, 11, 92, 32);
		frame.Panels.Add(new(10, 11, 92, 32, new Color("102025")));
		Box(frame, 10, 11, 92, 32, "ЖУРНАЛ БОЯ", Accent);
		int start = Math.Clamp(offset, 0, Math.Max(0, log.Count - JournalPageSize));
		frame.Write(13, 12, $"События {Math.Min(start + 1, log.Count)}-{Math.Min(start + JournalPageSize, log.Count)} / {log.Count}", Muted, 86);
		for (int i = 0; i < Math.Min(JournalPageSize, log.Count - start); i++)
			frame.Write(13, 14 + i, log[start + i], Ink, 86);
		if (log.Count == 0) frame.Write(13, 15, "Действий пока нет. Подтвердите начало раунда.", Muted, 86);
		frame.Write(13, 40, "W/S, стрелки, колесо: прокрутка   PgUp/PgDn: страница", Muted, 86);
		frame.Write(13, 41, "L / ESC: закрыть   Бой приостановлен", Accent, 86);
	}


	private static void Sprite(CombatAsciiFrame frame, int x, int y, IReadOnlyList<string> art, Color color)
	{
		for (int i = 0; i < art.Count; i++) frame.Write(x, y + i, art[i], color);
	}
	private static void Card(CombatAsciiFrame frame, int x, int y, int width, int height, string title, Color color)
	{
		frame.Clear(x, y, width + 2, height + 1);
		frame.Write(x + 2, y + 1, "+" + new string('-', width - 2) + "+", Shadow);
		for (int row = 2; row <= height; row++) frame.Write(x + width + 1, y + row, "|", Shadow);
		frame.Write(x + 2, y + height, "+" + new string('=', width - 2) + "+", Shadow);
		frame.Write(x, y, "." + (" " + title + " ").PadRight(width - 2, '-')[..(width - 2)] + ".", color);
		for (int row = 1; row < height - 1; row++)
		{
			frame.Write(x, y + row, "|", color);
			frame.Write(x + width - 1, y + row, "|", color);
		}
		frame.Write(x, y + height - 1, "+" + new string('-', width - 2) + "+", color);
		frame.Write(x + width, y + 1, "\\", Shadow);
		frame.Write(x + width, y + height - 1, "\\", Shadow);
	}
	private static void Pile(CombatAsciiFrame frame, int x, int y, string title, int count, bool draw)
	{
		Color color = count > 0 ? Muted : Shadow;
		if (draw)
		{
			frame.Write(x + 3, y, "+---------+", Shadow);
			frame.Write(x + 2, y + 1, "+---------+|", Shadow);
			frame.Write(x + 1, y + 2, "+---------+||", color);
			frame.Write(x + 1, y + 3, "| /\\ /\\  |||", color);
			frame.Write(x + 1, y + 4, "| \\/ \\/  ||/", color);
			frame.Write(x + 1, y + 5, "+---------+/", color);
			frame.Write(x + 2, y + 6, $"{title} {count}", Ink);
		}
		else
		{
			frame.Write(x + 2, y, "+---------+", Shadow);
			frame.Write(x + 1, y + 1, "+---------+|", color);
			frame.Write(x + 1, y + 2, "|  /  /   |/", color);
			frame.Write(x + 1, y + 3, "+---------+", color);
			frame.Write(x + 2, y + 4, $"{title} {count}", Ink);
		}
	}
	private static void Line(CombatAsciiFrame frame, int x1, int y1, int x2, int y2, char symbol, Color color)
	{
		int steps = Math.Max(Math.Abs(x2 - x1), Math.Abs(y2 - y1));
		for (int i = 0; i <= steps; i++)
		{
			float amount = steps == 0 ? 0 : (float)i / steps;
			int x = (int)Math.Round(x1 + (x2 - x1) * amount);
			int y = (int)Math.Round(y1 + (y2 - y1) * amount);
			frame.Write(x, y, symbol.ToString(), color);
		}
	}
}
