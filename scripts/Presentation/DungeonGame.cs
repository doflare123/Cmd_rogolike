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
	private Vector2I _player;
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

		if (GetMovement(key.Keycode) is Vector2I movement)
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
			_player,
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
		_player = _map.PlayerStart;
		_status = "Новый мир. Упритесь в + или нажмите E рядом с дверью.";
		QueueRedraw();
	}

	private void TryMove(Vector2I direction)
	{
		Vector2I destination = _player + direction;
		DungeonTile tile = _map.GetTile(destination);

		if (tile == DungeonTile.ClosedDoor)
		{
			OpenDoor(destination);
			return;
		}

		if (_map.GetEntityAt(destination) is Enemy enemy)
		{
			_status = $"{enemy.Name} преграждает путь. Бой пока не реализован.";
		}
		else if (_map.CanEnter(destination))
		{
			_player = destination;
			_status = string.Empty;
		}
		else
		{
			_status = tile == DungeonTile.Wall
				? "Здесь стена (#)."
				: "За пределами открытой карты — пустота.";
		}

		QueueRedraw();
	}

	private void TryOpenAdjacentDoor()
	{
		foreach (CardinalDirection direction in CardinalDirectionExtensions.All)
		{
			Vector2I position = _player + direction.ToOffset();
			if (_map.GetTile(position) == DungeonTile.ClosedDoor)
			{
				OpenDoor(position);
				return;
			}
		}

		_status = "Рядом нет закрытой двери (+).";
		QueueRedraw();
	}

	private void OpenDoor(Vector2I position)
	{
		DoorExpansion? expansion = _map.OpenDoor(position);
		_status = expansion switch
		{
			null => "Эта дверь не открывается.",
			{ OpenedInternalDoor: true } => "Открыта дверь между частями комнаты.",
			{ CreatedRegion: true, RegionKind: DungeonRegionKind.Room } => "За дверью открылась новая комната.",
			{ CreatedRegion: true } => "За дверью открылся новый коридор.",
			_ => "Дверь соединила две уже известные области.",
		};
		QueueRedraw();
	}

	private static Vector2I? GetMovement(Key key)
	{
		return key switch
		{
			Key.W or Key.Up => Vector2I.Up,
			Key.D or Key.Right => Vector2I.Right,
			Key.S or Key.Down => Vector2I.Down,
			Key.A or Key.Left => Vector2I.Left,
			_ => null,
		};
	}
}
