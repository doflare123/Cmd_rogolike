using CmdRoguelike.Core;
using CmdRoguelike.Domain;
using CmdRoguelike.Domain.Entities;
using CmdRoguelike.Generation;
using CmdRoguelike.World;
using Godot;

namespace CmdRoguelike.Presentation;

/// <summary>
/// Корень композиции Godot: преобразует ввод в действия над миром
/// и просит отрисовщик показать текущее состояние.
/// </summary>
public partial class DungeonGame : Node2D
{
	[Export(PropertyHint.Range, "1,4,1")]
	public int MinimumDoorsPerRoom { get; set; } = 3;

	[Export(PropertyHint.Range, "0,1,0.05")]
	public float EnemyRoomChance { get; set; } = 0.38f;

	[Export(PropertyHint.Range, "1,8,1")]
	public int MaximumEnemiesPerRoom { get; set; } = 3;

	[Export]
	public int WorldSeed { get; set; }

	[Export(PropertyHint.Range, "12,32,1")]
	public int FontSize { get; set; } = 20;

	[Export(PropertyHint.Range, "10,28,1")]
	public int CellWidth { get; set; } = 17;

	[Export(PropertyHint.Range, "14,36,1")]
	public int CellHeight { get; set; } = 22;

	private readonly AsciiDungeonRenderer _renderer = new();
	private DungeonMap _map = null!;
	private PreparationSession _session = null!;
	private InventoryPanel _inventoryPanel = null!;
	private CombatPanel? _combatPanel;
	private double _combatEntrance = -1;
	internal bool IsEnteringCombat => _combatEntrance >= 0;
	internal DungeonMap? CurrentMap => _session.Map;
	private int _requestedSeed;
	private string _status = string.Empty;

	public override void _Ready()
	{
		StartNewWorld(WorldSeed);
		GetViewport().SizeChanged += QueueRedraw;
	}

	public override void _UnhandledKeyInput(InputEvent @event)
	{
		if (@event is not InputEventKey { Pressed: true } key)
		{
			return;
		}

		if (_inventoryPanel.Visible)
		{
			_inventoryPanel.HandleKey(key);
			GetViewport().SetInputAsHandled();
			return;
		}
		if (IsEnteringCombat && key.Keycode is not Key.R and not Key.Escape)
		{
			GetViewport().SetInputAsHandled();
			return;
		}

		if (_combatPanel?.IsJournalOpen == true && key.Keycode != Key.R)
		{
			_combatPanel.HandleKey(key);
			GetViewport().SetInputAsHandled();
			return;
		}
		if (key.Keycode == Key.I && !key.Echo)
		{
			_inventoryPanel.Refresh();
			_inventoryPanel.Show();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (key.Keycode == Key.R && !key.Echo)
		{
			StartNewWorld(0);
			GetViewport().SetInputAsHandled();
			return;
		}
		if (key.Keycode == Key.Escape)
		{
			GetTree().Quit();
			return;
		}
		if (_combatPanel is not null)
		{
			_combatPanel.HandleKey(key);
			GetViewport().SetInputAsHandled();
			return;
		}

		if (GetMovement(key.Keycode) is CardinalDirection movement)
		{
			TryMove(movement);
			GetViewport().SetInputAsHandled();
			return;
		}

		switch (key.Keycode)
		{
			case Key.E:
			case Key.Space:
				TryOpenAdjacentDoor();
				break;
			default:
				return;
		}

		GetViewport().SetInputAsHandled();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!_inventoryPanel.Visible && @event is InputEventMouseButton mouse && _combatPanel?.HandleMouse(mouse) == true)
			GetViewport().SetInputAsHandled();
	}

	public override void _Draw()
	{
		if (_session.Map is null) return;
		_renderer.Draw(
			this,
			_map,
			_status,
			GetViewportRect().Size,
			new AsciiRenderOptions(FontSize, CellWidth, CellHeight),
			IsEnteringCombat ? (float)(0.5 + Math.Sin(_combatEntrance * 12) * 0.5) : 0);
		if (_combatEntrance > 0.85)
			DrawRect(GetViewportRect(), new Color(0, 0, 0, (float)Math.Clamp((_combatEntrance - 0.85) / 0.3, 0, 1)));
	}

	public override void _Process(double delta)
	{
		if (!IsEnteringCombat) return;
		_combatEntrance += delta;
		if (_combatEntrance >= 1.15)
		{
			_combatEntrance = -1;
			CreateCombatPanel();
		}
		QueueRedraw();
	}

	private void StartNewWorld(int requestedSeed)
	{
		_requestedSeed = requestedSeed;
		_combatEntrance = -1;
		if (_combatPanel is not null) { RemoveChild(_combatPanel); _combatPanel.QueueFree(); _combatPanel = null; }
		_session = new PreparationSession();
		if (_inventoryPanel is not null) { RemoveChild(_inventoryPanel); _inventoryPanel.QueueFree(); }
		_inventoryPanel = new InventoryPanel(_session.Hero, EnterExpedition, CloseInventory);
		_inventoryPanel.Layer = 2;
		AddChild(_inventoryPanel);
		QueueRedraw();
	}

	private void CloseInventory() => _inventoryPanel.Hide();

	private void EnterExpedition()
	{
		int seed = _requestedSeed != 0
			? _requestedSeed
			: Random.Shared.Next(1, int.MaxValue);
		DungeonGenerationOptions options = new(
			minimumDoorsPerRoom: MinimumDoorsPerRoom,
			enemyRoomChance: EnemyRoomChance,
			minimumEnemiesPerRoom: 1,
			maximumEnemiesPerRoom: MaximumEnemiesPerRoom);

		_map = _session.StartExpedition(seed, options);
		CloseInventory();
		_status = "Новый мир. Упритесь в + или нажмите E рядом с дверью.";
		QueueRedraw();
	}

	private void TryMove(CardinalDirection direction)
	{
		PlayerMoveResult result = _map.TryMovePlayer(direction);
		_status = result.Outcome switch
		{
			PlayerMoveOutcome.Moved => string.Empty,
			PlayerMoveOutcome.OpenedDoor => DescribeDoorExpansion(
				result.DoorExpansion
					?? throw new InvalidOperationException("Door movement result has no expansion data.")),
			PlayerMoveOutcome.BlockedByEntity when result.BlockingEntity is Enemy enemy
				=> $"{enemy.Name} преграждает путь.",
			PlayerMoveOutcome.BlockedByEntity => "Клетка занята.",
			PlayerMoveOutcome.BlockedByTerrain when result.BlockingTile == DungeonTile.Wall
				=> "Здесь стена (#).",
			PlayerMoveOutcome.BlockedByTerrain => "За пределами открытой карты — пустота.",
			PlayerMoveOutcome.PlayerIsDead => "Мёртвый персонаж не может двигаться.",
			PlayerMoveOutcome.InCombat => "Сначала завершите бой.",
			_ => throw new ArgumentOutOfRangeException(nameof(result.Outcome), result.Outcome, null),
		};

		ShowCombatIfNeeded();
		QueueRedraw();
	}

	private void ShowCombatIfNeeded()
	{
		if (_map.Combat is null || _combatPanel is not null || IsEnteringCombat) return;
		_combatEntrance = 0;
		_status = $"Вы вошли в комнату. Обнаружены противники: {_map.Combat.Enemies.Count}. Начинается бой...";
	}

	private void CreateCombatPanel()
	{
		if (_map.Combat is null || _combatPanel is not null) return;
		_combatPanel = new CombatPanel(_map, () =>
		{
			if (_combatPanel is not null) { RemoveChild(_combatPanel); _combatPanel.QueueFree(); _combatPanel = null; }
			_status = "Победа. Двери доступны, исследование продолжается.";
			QueueRedraw();
		});
		AddChild(_combatPanel);
	}

	private void TryOpenAdjacentDoor()
	{
		PlayerDoorInteractionResult result = _map.TryOpenAdjacentDoor();
		_status = result.Outcome switch
		{
			PlayerDoorInteractionOutcome.OpenedDoor => DescribeDoorExpansion(
				result.DoorExpansion
					?? throw new InvalidOperationException("Door interaction result has no expansion data.")),
			PlayerDoorInteractionOutcome.NoAdjacentDoor => "Рядом нет закрытой двери (+).",
			PlayerDoorInteractionOutcome.PlayerIsDead => "Мёртвый персонаж не может открывать двери.",
			PlayerDoorInteractionOutcome.InCombat => "Двери доступны после завершения боя.",
			_ => throw new ArgumentOutOfRangeException(nameof(result.Outcome), result.Outcome, null),
		};
		QueueRedraw();
	}

	private static string DescribeDoorExpansion(DoorExpansion expansion)
	{
		return expansion switch
		{
			{ OpenedInternalDoor: true } => "Открыта дверь между частями комнаты.",
			{ CreatedRegion: true, RegionKind: DungeonRegionKind.Room } => "За дверью открылась новая комната.",
			{ CreatedRegion: true } => "За дверью открылся новый коридор.",
			_ => "Дверь соединила две уже известные области.",
		};
	}

	private static CardinalDirection? GetMovement(Key key)
	{
		return key switch
		{
			Key.W or Key.Up => CardinalDirection.Up,
			Key.D or Key.Right => CardinalDirection.Right,
			Key.S or Key.Down => CardinalDirection.Down,
			Key.A or Key.Left => CardinalDirection.Left,
			_ => null,
		};
	}
}
