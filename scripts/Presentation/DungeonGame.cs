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
			case Key.R when !key.Echo:
				StartNewWorld(0);
				break;
			case Key.Escape:
				GetTree().Quit();
				break;
			default:
				return;
		}

		GetViewport().SetInputAsHandled();
	}

	public override void _Draw()
	{
		_renderer.Draw(
			this,
			_map,
			_status,
			GetViewportRect().Size,
			new AsciiRenderOptions(FontSize, CellWidth, CellHeight));
	}

	private void StartNewWorld(int requestedSeed)
	{
		int seed = requestedSeed != 0
			? requestedSeed
			: Random.Shared.Next(1, int.MaxValue);
		DungeonGenerationOptions options = new(
			minimumDoorsPerRoom: MinimumDoorsPerRoom,
			enemyRoomChance: EnemyRoomChance,
			minimumEnemiesPerRoom: 1,
			maximumEnemiesPerRoom: MaximumEnemiesPerRoom);

		_map = new DungeonMap(seed, options);
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
				=> $"{enemy.Name} преграждает путь. Бой пока не реализован.",
			PlayerMoveOutcome.BlockedByEntity => "Клетка занята.",
			PlayerMoveOutcome.BlockedByTerrain when result.BlockingTile == DungeonTile.Wall
				=> "Здесь стена (#).",
			PlayerMoveOutcome.BlockedByTerrain => "За пределами открытой карты — пустота.",
			PlayerMoveOutcome.PlayerIsDead => "Мёртвый персонаж не может двигаться.",
			_ => throw new ArgumentOutOfRangeException(nameof(result.Outcome), result.Outcome, null),
		};

		QueueRedraw();
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
