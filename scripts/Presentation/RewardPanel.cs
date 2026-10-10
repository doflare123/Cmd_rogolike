using CmdRoguelike.Domain.Combat;
using CmdRoguelike.Domain.Items;
using CmdRoguelike.Domain.Stats;
using CmdRoguelike.World;
using Godot;

namespace CmdRoguelike.Presentation;

internal sealed partial class RewardPanel : TerminalOverlay
{
	private readonly DungeonMap _map;
	private readonly RewardCache _cache;
	private readonly GameSettings _settings;
	private readonly Action _close;
	private readonly string[][] _candidates;
	private int _selection;
	private string _status = "Выберите предмет. Незабранная добыча останется в комнате.";
	internal RewardReveal Reveal { get; }
	internal RewardCache Cache => _cache;
	private RewardEntry[] Available() => _cache.Entries.Where(entry => !entry.IsClaimed).ToArray();
	public RewardPanel(DungeonMap map, RewardCache cache, GameSettings settings, Action close)
	{
		_map = map; _cache = cache; _settings = settings; _close = close; Layer = 2;
		var roll = cache.Reward.Equipment;
		Reveal = new RewardReveal(roll.Affixes.Count, settings.RewardAnimations && !cache.IsRevealed);
		_candidates = roll.Affixes.Select(a => PrototypeAffixes.All
			.Where(candidate => candidate.Kind == a.Definition.Kind && candidate.Categories.Contains(roll.Definition.Category)
				&& candidate.Tiers.Any(t => t.MinimumItemLevel <= roll.ItemLevel))
			.Select(candidate => candidate.NameFor(roll.Definition.NameGender)).ToArray()).ToArray();
		if (Reveal.IsComplete) _map.RevealReward(cache.Id);
	}
	public override void _Process(double delta)
	{
		if (Reveal.IsComplete)
		{
			if (Available().Any(e => e.Item.Roll.Rarity == ItemRarity.Unique)) Refresh();
			return;
		}
		if (!_settings.RewardAnimations) Reveal.Skip(); else Reveal.Tick(delta);
		if (Reveal.IsComplete) _map.RevealReward(_cache.Id);
		Refresh();
	}
	public void HandleKey(InputEventKey key)
	{
		if (!key.Pressed) return;
		if (key.Keycode == Key.Escape && !key.Echo)
		{
			Reveal.Skip(); _map.RevealReward(_cache.Id); Refresh(); return;
		}
		if (!Reveal.IsComplete) return;
		var available = Available();
		_selection = Math.Clamp(_selection, 0, Math.Max(0, available.Length - 1));
		if (key.Keycode is Key.W or Key.Up) _selection = Math.Max(0, _selection - 1);
		else if (key.Keycode is Key.S or Key.Down) _selection = Math.Min(Math.Max(0, available.Length - 1), _selection + 1);
		else if (!key.Echo && key.Keycode is Key.Space or Key.F) { _close(); return; }
		else if (available.Length > 0 && !key.Echo && (key.Keycode is Key.Enter or Key.E))
		{
			_status = _map.TryClaimReward(_cache.Id, available[_selection].Id) switch
			{
				RewardClaimResult.Success => "Предмет получен в рюкзак. [I] инвентарь и расходники.",
				RewardClaimResult.Full => "Рюкзак полон. Добыча сохранена в комнате; вернитесь через G.",
				RewardClaimResult.AlreadyClaimed => "Этот предмет уже забран.",
				_ => "Сейчас получение недоступно.",
			};
			_selection = Math.Clamp(_selection, 0, Math.Max(0, Available().Length - 1));
		}
		Refresh();
	}
	protected override List<Line> Compose()
	{
		var lines = new List<Line>();
		var reward = _cache.Reward; var roll = reward.Equipment; var budget = reward.Budget;
		var color = InventoryPanel.RarityColor(roll.Rarity);
		var available = Available();
		bool hasEquipment = !_cache.Entries[0].IsClaimed;
		void Write(int x, int y, string text, Color c, int width = 103) => Put(lines, x, y, text, c, width);
		Write(2, 0, "CMD ROGUELIKE / ПОБЕДА / НАГРАДА", Accent);
		Write(2, 2, $"HP {_map.Player.Health}/{_map.Player.MaxHealth}   MP {_map.Player.Resources.Mana}/{_map.Player.Resources.MaxMana}   Рюкзак {_map.Player.Inventory.UsedSlots}/{_map.Player.Inventory.Capacity}", Ink);
		Box(lines, 2, 4, 108, 6, "ИТОГ ВСТРЕЧИ");
		Write(4, 5, $"Противники: {budget.EnemyCount} | Ходы героя: {budget.ActualTurns} | Ожидаемый темп: {budget.ExpectedTurns} ходов", Ink);
		Write(4, 6, $"Сложность: {budget.DifficultyBudget} | Бонус за быстрый бой: +{budget.EfficiencyBonus}", Ink);
		Write(4, 7, budget.SupportPercent == 0 ? "Ресурсы в порядке: весь бюджет награды идёт в экипировку."
			: $"{budget.SupportPercent}% бюджета экипировки направлено на помощь: "
				+ string.Join(" и ", new[] { budget.HealthSupport ? "здоровье" : null, budget.ManaSupport ? "мана" : null }.OfType<string>()), Gold);
		if (hasEquipment)
		{
			Box(lines, 2, 10, 108, 15, "ОСНОВНОЙ ПРЕДМЕТ");
			Write(4, 11, $"{(_selection == 0 ? '>' : ' ')} {(Reveal.IsComplete ? roll.DisplayName : roll.Definition.Name)}", color);
			Write(4, 12, $"{InventoryPanel.RarityName(roll.Rarity)} | Уровень {roll.ItemLevel}"
				+ (roll.Affixes.Count > 0 ? $" | Свойств {roll.Affixes.Count} | Раскрыто {Reveal.RevealedCount}" : ""), color);
			Write(4, 13, "Требования: " + OrNone(roll.Definition.Requirements.Select(p => $"{InventoryPanel.AttributeName(p.Key)} {p.Value}")), Ink);
			Write(4, 14, "Бонусы: " + Bonuses(Reveal.IsComplete ? roll.AttributeBonuses : roll.Definition.AttributeBonuses,
				Reveal.IsComplete ? roll.StatBonuses : roll.Definition.StatBonuses), Ink);
			Write(4, 15, "Карты: " + Cards(roll.Definition), Ink);
			for (int i = 0; i < roll.Affixes.Count; i++)
			{
				var affix = roll.Affixes[i];
				string kind = affix.Definition.Kind == AffixKind.Prefix ? "ПРЕФИКС" : "СУФФИКС";
				string text;
				if (i < Reveal.RevealedCount)
				{
					string stat = affix.Definition.Attribute is AttributeId a ? InventoryPanel.AttributeName(a) : InventoryPanel.StatName(affix.Definition.Stat!.Value);
					text = $"[+] {kind}: {affix.Definition.NameFor(roll.Definition.NameGender)} | T{affix.Tier.Tier} | +{affix.Value} {stat}";
				}
				else if (i == Reveal.RevealedCount)
				{
					var candidates = _candidates[i];
					int center = (int)Math.Floor(30 * (1 - Math.Pow(1 - Reveal.Progress, 3))) + i;
					string Choice(int offset) => offset == 0 && Reveal.Progress > 0.9
						? affix.Definition.NameFor(roll.Definition.NameGender) : candidates[(center + offset + candidates.Length) % candidates.Length];
					text = $"[~] {kind}: {Choice(-1),-18} >> {Choice(0),-18} << {Choice(1)}";
				}
				else text = $"[ ] {kind}: ожидает раскрытия";
				Write(4, 17 + i, text, i <= Reveal.RevealedCount ? color : Muted);
			}
			Box(lines, 2, 25, 108, 6, "СРАВНЕНИЕ С ЭКИПИРОВКОЙ");
			var equipped = _map.Player.Inventory.Items.Where(i => i.Location == ItemLocation.Equipment && i.Slot is EquipmentSlot slot && roll.Definition.Slots.Contains(slot)).ToArray();
			for (int i = 0; i < Math.Min(2, equipped.Length); i++)
			{
				Write(4, 26 + i * 2, "Надето: " + equipped[i].DisplayName + (equipped[i].State == EquipmentState.Dormant ? " [DORMANT]" : ""), Ink);
				Write(4, 27 + i * 2, Bonuses(equipped[i].AttributeBonuses, equipped[i].StatBonuses) + " | Карты: " + Cards(equipped[i].Definition), Muted);
			}
			if (equipped.Length == 0) Write(4, 26, "Подходящие слоты свободны.", Muted);
			if (equipped.Length < 2) Write(4, 29, "Новая экипировка надевается на базе. Сейчас предмет попадёт в рюкзак.", Muted);
		}
		int suppliesY = hasEquipment ? 31 : 10;
		var supplies = available.Where(entry => entry.Item.Roll.Definition.Restoration is not null).ToArray();
		if (supplies.Length > 0) Box(lines, 2, suppliesY, 108, 6, "ПОМОЩЬ С РЕСУРСАМИ");
		for (int i = 0; i < supplies.Length; i++)
		{
			var entry = supplies[i]; var effect = entry.Item.Roll.Definition.Restoration!;
			Write(4, suppliesY + 1 + i, $"{(_selection == Array.IndexOf(available, entry) ? '>' : ' ')} {entry.Item.Roll.DisplayName} x{entry.Item.Quantity}: восстановление {effect.Percent}% {(effect.Resource == RestorationResource.Health ? "HP" : "MP")}", InventoryPanel.RarityColor(entry.Item.Roll.Rarity));
		}
		if (available.Length == 0) Write(4, 12, "Добыча собрана. [F / SPACE] вернуться к карте.", Muted);
		Write(2, 38, "> " + _status, Gold);
		Write(2, 39, Reveal.IsComplete ? "[W/S] выбор  [ENTER/E] забрать  [F/SPACE] к карте  [I] инвентарь" : "Раскрытие свойств... [ESC] показать весь результат", Accent);
		Write(2, 41, "[ESC] пропустить рулетки  [F2] настройки  [R] сброс героя", Muted);
		return lines;
	}
	private static string OrNone(IEnumerable<string> parts) => parts.Any() ? string.Join(", ", parts) : "нет";
	private static string Bonuses(IReadOnlyDictionary<AttributeId, int> attributes, IReadOnlyDictionary<DerivedStatId, int> stats)
		=> OrNone(attributes.Select(p => $"+{p.Value} {InventoryPanel.AttributeName(p.Key)}").Concat(stats.Select(p => $"+{p.Value} {InventoryPanel.StatName(p.Key)}")));
	private string Cards(ItemDefinition definition) => OrNone(definition.CombatCards.Select(g =>
		$"{g.Card.Name} x{g.Copies} ({g.Card.ActionPointCost} AP, " + (g.Card.Kind == CombatCardKind.Attack ? $"{g.Card.Power} урона)" : $"{CombatRules.DefenseBlock(_map.Player, g.Card)} блока сейчас)")));
}
