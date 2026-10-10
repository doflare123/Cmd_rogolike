using CmdRoguelike.Core;
using CmdRoguelike.Domain;
using CmdRoguelike.Domain.Entities;
using CmdRoguelike.Domain.Combat;
using CmdRoguelike.Domain.Items;
using CmdRoguelike.Domain.Rewards;
using CmdRoguelike.Generation;
using CmdRoguelike.State;
using Godot;

namespace CmdRoguelike.World;

/// <summary>
/// Публичный фасад раскрытой части подземелья. Владеет состоянием мира
/// и координирует ленивое расширение, а решения о геометрии принимает RegionGenerator.
/// </summary>
public sealed class DungeonMap
{
	private readonly DungeonGrid _grid = new();
	private readonly DoorRegistry _doors = new();
	private readonly EntityRegistry _entities = new();
	private readonly Dictionary<Vector2I, DungeonRegion> _regions = new();
	private readonly RegionGenerator _regionGenerator;
	private readonly EnemyGenerator _enemyGenerator;
	private readonly DungeonKnowledge _knowledge = new();
	private readonly CombatOptions _combatOptions;
	private int _encounterNumber;
	private readonly List<RewardCache> _rewardCaches = new();
	private CombatEncounter? _rewardedBattle;
	private readonly CombatRewardOptions _rewardOptions;
	private readonly BossPlacementPolicy? _bossPlacement;
	private readonly ExpeditionOptions? _expeditionOptions;
	private bool _bossPlaced;
	public Vector2I? ReturnPortal { get; private set; }
	public bool BossDefeated => ReturnPortal is not null;
	public bool CanUseReturnPortal => !IsInCombat && Player.IsAlive && ReturnPortal == Player.Position;
	public bool IsReturnPortalVisible => ReturnPortal is Vector2I p && IsRevealed(p);
	public RewardCache? PendingReward { get; private set; }
	public CombatEncounter? Combat { get; private set; }
	public bool IsInCombat => Combat is not null;

	public int Seed { get; }
	public int RegionCount => _regions.Count;
	public int RoomCount => _regions.Values.Count(region => region.Kind == DungeonRegionKind.Room);
	public int OpenedDoorCount { get; private set; }
	public int EnemyCount => _entities.All.Count(entity => entity is Enemy);
	public int PopulatedRoomCount { get; private set; }
	public Vector2I PlayerStart { get; }
	public PlayerCharacter Player { get; }

	public DungeonMap(int seed, int minimumDoorsPerRoom = 3)
		: this(seed, new DungeonGenerationOptions(minimumDoorsPerRoom))
	{
	}

	public DungeonMap(int seed, DungeonGenerationOptions options, PlayerCharacter? preparedHero = null, CombatOptions? combatOptions = null,
		CombatRewardOptions? rewardOptions = null, ExpeditionOptions? expeditionOptions = null)
	{
		ArgumentNullException.ThrowIfNull(options);
		if (preparedHero is not null && (preparedHero.Inventory.IsLocked || !preparedHero.IsAlive))
			throw new InvalidOperationException("Hero is unavailable for a new expedition.");

		Seed = seed;
		_expeditionOptions = expeditionOptions;
		if (expeditionOptions is not null) _bossPlacement = new(new GodotRandomSource(unchecked(seed * 7919 ^ 0x32ce17)),
			expeditionOptions.MinimumBossRooms, expeditionOptions.GuaranteedBossRoom);
		_combatOptions = combatOptions ?? new CombatOptions();
		_rewardOptions = rewardOptions ?? new CombatRewardOptions();
		IRandomSource random = new GodotRandomSource(seed);
		_regionGenerator = new RegionGenerator(options, random, _grid, _doors);
		_enemyGenerator = new EnemyGenerator(
			options,
			random,
			_grid,
			_doors,
			_entities,
			new EnemyFactory(new GodotRandomSource(unchecked(seed * 887 ^ 0x45f23))));

		DungeonRegion firstRegion = _regionGenerator.Generate(
			Vector2I.Zero,
			requiredEntrance: null,
			forceRoom: true);
		_regions.Add(firstRegion.Sector, firstRegion);
		PlayerStart = _regionGenerator.PickFloorCell(firstRegion);
		Player = preparedHero ?? new PlayerCharacter(PlayerStart);
		_entities.Add(Player);
		_entities.Move(Player, PlayerStart);
		Player.Inventory.LockForExpedition();
		_enemyGenerator.Populate(firstRegion, isSafeRegion: true);
		_knowledge.Enter(PlayerStart, GetTile);
	}

	public DungeonTile GetTile(Vector2I position)
	{
		return _grid[position];
	}

	public bool IsWalkable(Vector2I position)
	{
		return _grid.IsWalkable(position);
	}

	public bool IsPotentiallyTraversable(Vector2I position)
	{
		return GetTile(position) is
			DungeonTile.Floor or DungeonTile.OpenDoor or DungeonTile.ClosedDoor;
	}

	public bool CanEnter(Vector2I position)
	{
		return _grid.IsWalkable(position) && !_entities.IsOccupied(position);
	}

	/// <summary>
	/// Выполняет одну команду перемещения игрока. Проверка рельефа, столкновений,
	/// открытие двери при упоре и атомарное изменение позиции остаются в World.
	/// </summary>
	public PlayerMoveResult TryMovePlayer(CardinalDirection direction)
	{
		Vector2I origin = Player.Position;
		Vector2I destination = origin + direction.ToOffset();

		if (!Player.IsAlive)
		{
			return PlayerMoveResult.PlayerIsDead(origin);
		}
		if (IsInCombat) return PlayerMoveResult.InCombat(origin);

		DungeonTile tile = GetTile(destination);
		if (tile == DungeonTile.ClosedDoor)
		{
			DoorExpansion expansion = OpenDoor(destination)
				?? throw new InvalidOperationException(
					$"Closed door at {destination} is missing from the door registry.");
			return PlayerMoveResult.OpenedDoor(origin, destination, expansion);
		}

		if (!_grid.IsWalkable(destination))
		{
			return PlayerMoveResult.BlockedByTerrain(origin, destination, tile);
		}

		if (_entities.TryGetAt(destination, out DungeonEntity? blockingEntity))
		{
			return PlayerMoveResult.BlockedByEntity(origin, destination, blockingEntity!);
		}

		_entities.Move(Player, destination);
		RevealPlayerSection();
		return PlayerMoveResult.Moved(origin, destination);
	}

	public PlayerDoorInteractionResult TryOpenAdjacentDoor()
	{
		Vector2I playerPosition = Player.Position;
		if (!Player.IsAlive)
		{
			return PlayerDoorInteractionResult.PlayerIsDead(playerPosition);
		}
		if (IsInCombat) return PlayerDoorInteractionResult.InCombat(playerPosition);

		foreach (CardinalDirection direction in CardinalDirectionExtensions.All)
		{
			Vector2I position = playerPosition + direction.ToOffset();
			if (GetTile(position) == DungeonTile.ClosedDoor)
			{
				DoorExpansion expansion = OpenDoor(position)
					?? throw new InvalidOperationException(
						$"Closed door at {position} is missing from the door registry.");
				return PlayerDoorInteractionResult.OpenedDoor(
					playerPosition,
					position,
					expansion);
			}
		}

		return PlayerDoorInteractionResult.NoAdjacentDoor(playerPosition);
	}

	public DungeonEntity? GetEntityAt(Vector2I position)
	{
		return _entities.TryGetAt(position, out DungeonEntity? entity)
			? entity
			: null;
	}

	public bool IsRevealed(Vector2I position) => _knowledge.IsRevealed(position);
	public DungeonTile GetRevealedTile(Vector2I position)
		=> IsRevealed(position) ? GetTile(position) : DungeonTile.Empty;
	public DungeonEntity? GetVisibleEntityAt(Vector2I position)
		=> position == Player.Position || _knowledge.IsInCurrentSection(position)
			? GetEntityAt(position) : null;
	public int VisibleEnemyCount => _entities.All.OfType<Enemy>()
		.Count(enemy => enemy.IsAlive && _knowledge.IsInCurrentSection(enemy.Position));

	private void RevealPlayerSection()
	{
		_knowledge.Enter(Player.Position, GetTile);
		if (GetTile(Player.Position) != DungeonTile.Floor || IsInCombat) return;
		Enemy[] enemies = _entities.All.OfType<Enemy>()
			.Where(enemy => enemy.IsAlive && _knowledge.IsInCurrentSection(enemy.Position))
			.OrderBy(enemy => enemy.Position.Y).ThenBy(enemy => enemy.Position.X).ToArray();
		if (enemies.Length == 0) return;
		// Independent combat RNG: playing or replacing cards cannot change future geometry.
		int combatSeed = unchecked(Seed * 397 ^ ++_encounterNumber);
		Combat = new CombatEncounter(Player, enemies, combatSeed, _combatOptions);
	}

	public CombatCommandResult BeginCombatRound()
		=> ApplyCombatCommand(battle => battle.BeginRound());
	public CombatCommandResult EndCombatTurn()
		=> ApplyCombatCommand(battle => battle.EndPlayerTurn());
	public CombatCommandResult ReplaceCombatCard(int index)
		=> Combat?.ReplaceCard(index) ?? CombatCommandResult.WrongPhase;
	public CombatCommandResult PlayCombatCard(int index, Guid? targetId = null)
		=> ApplyCombatCommand(battle => battle.PlayCard(index, targetId));

	private CombatCommandResult ApplyCombatCommand(Func<CombatEncounter, CombatCommandResult> command)
	{
		CombatCommandResult result = Combat is null ? CombatCommandResult.WrongPhase : command(Combat);
		if (Combat is not null && result == CombatCommandResult.Success)
		{
			foreach (Enemy enemy in Combat.Enemies.Where(enemy => !enemy.IsAlive))
				if (ReferenceEquals(GetEntityAt(enemy.Position), enemy)) _entities.Remove(enemy);
			if (Combat.VictorySummary is not null && !ReferenceEquals(_rewardedBattle, Combat))
			{
				Enemy? boss = Combat.Enemies.FirstOrDefault(e => e.IsBoss);
				if (boss is not null) ReturnPortal = boss.Position;
				int seed = unchecked(Seed * 15485863 ^ _encounterNumber * 32452843 ^ 0x5ac712);
				var reward = new CombatRewardGenerator(new GodotRandomSource(seed), _rewardOptions).Generate(Combat.VictorySummary);
				PendingReward = new RewardCache(Player.Position, reward);
				_rewardCaches.Add(PendingReward);
				_rewardedBattle = Combat;
			}
		}
		return result;
	}

	public RewardCache? GetAvailableReward() => IsInCombat || !Player.IsAlive ? null
		: _rewardCaches.FirstOrDefault(cache => cache.HasRemaining && _knowledge.IsInCurrentSection(cache.Position));
	public RewardCache? GetVisibleRewardAt(Vector2I position) => IsRevealed(position)
		? _rewardCaches.FirstOrDefault(cache => cache.Position == position && cache.HasRemaining) : null;
	public RewardClaimResult TryClaimReward(Guid cacheId, Guid entryId)
	{
		var cache = _rewardCaches.FirstOrDefault(c => c.Id == cacheId);
		if (IsInCombat || !Player.IsAlive || cache is null || !cache.IsRevealed || !_knowledge.IsInCurrentSection(cache.Position))
			return RewardClaimResult.Unavailable;
		return cache.Entries.FirstOrDefault(e => e.Id == entryId)?.Claim(Player.Inventory) ?? RewardClaimResult.Unavailable;
	}
	public void RevealReward(Guid cacheId)
	{
		if (!IsInCombat && Player.IsAlive)
			_rewardCaches.FirstOrDefault(c => c.Id == cacheId && _knowledge.IsInCurrentSection(c.Position))?.Reveal();
	}
	public void FinishRewardPresentation() => PendingReward = null;
	public ConsumableUseResult UseConsumable(Guid id) => IsInCombat || !Player.IsAlive
		? ConsumableUseResult.Unavailable : Player.Inventory.TryUseConsumable(id);

	public bool LeaveVictoriousCombat()
	{
		if (Combat?.Phase != CombatPhase.Victory) return false;
		Combat = null;
		return true;
	}

	internal void ReleasePlayer()
	{
		if (!CanUseReturnPortal) throw new InvalidOperationException("Return requires entering a victorious portal.");
		_entities.Remove(Player);
	}

	public IReadOnlyCollection<DungeonEntity> GetEntities()
	{
		return _entities.All.ToArray();
	}

	public IReadOnlyCollection<Vector2I> GetKnownTilePositions()
	{
		return _grid.KnownPositions.ToArray();
	}

	public IReadOnlyList<Vector2I> GetClosedDoorPositions()
	{
		return _doors.FindClosedDoors(_grid);
	}

	public bool HasPassageOnBothSides(Vector2I position)
	{
		if (!_doors.TryGet(position, out DungeonDoor door))
		{
			return false;
		}

		Vector2I step = door.Direction.ToOffset();
		return IsWalkable(position - step) && IsWalkable(position + step);
	}

	public bool HasWallFrameAcrossDoor(Vector2I position)
	{
		if (!_doors.TryGet(position, out DungeonDoor door))
		{
			return false;
		}

		Vector2I axis = door.Direction.ToOffset();
		Vector2I perpendicular = new(-axis.Y, axis.X);
		return GetTile(position - perpendicular) == DungeonTile.Wall
			&& GetTile(position + perpendicular) == DungeonTile.Wall;
	}

	public string DescribeDoorNeighborhood(Vector2I position)
	{
		if (!_doors.TryGet(position, out DungeonDoor door))
		{
			return "unregistered door";
		}

		Vector2I axis = door.Direction.ToOffset();
		Vector2I perpendicular = new(-axis.Y, axis.X);
		return $"internal={door.IsInternal}, direction={door.Direction}, "
			+ $"back={GetTile(position - axis)}, front={GetTile(position + axis)}, "
			+ $"sideA={GetTile(position - perpendicular)}, sideB={GetTile(position + perpendicular)}";
	}

	/// <summary>
	/// Открывает внутреннюю дверь либо создаёт/соединяет область за внешней дверью.
	/// Возвращает null, если указанная клетка не является закрытой дверью.
	/// </summary>
	public DoorExpansion? OpenDoor(Vector2I position)
	{
		if (IsInCombat || !Player.IsAlive) return null;
		if (_grid[position] != DungeonTile.ClosedDoor
			|| !_doors.TryGet(position, out DungeonDoor door))
		{
			return null;
		}

		if (door.IsInternal)
		{
			_grid.SetDoorState(position, DungeonTile.OpenDoor);
			_knowledge.Enter(Player.Position, GetTile, refresh: true);
			OpenedDoorCount++;
			return new DoorExpansion(false, true, null);
		}

		DungeonRegion sourceRegion = _regions[door.RegionSector];
		Vector2I targetSector = door.RegionSector + door.Direction.ToOffset();
		bool createdRegion = !_regions.TryGetValue(targetSector, out DungeonRegion? targetRegion);

		if (createdRegion)
		{
			targetRegion = _regionGenerator.Generate(
				targetSector,
				door.Direction.Opposite());
			_regions.Add(targetSector, targetRegion);
		}
		else
		{
			_regionGenerator.EnsureExternalDoor(
				targetRegion!,
				door.Direction.Opposite());
		}

		_grid.SetDoorState(position, DungeonTile.OpenDoor);

		if (createdRegion)
		{
			int spawnedEnemies = TryPlaceBoss(targetRegion!) ? 1 : _enemyGenerator.Populate(targetRegion!, isSafeRegion: false);
			if (spawnedEnemies > 0)
			{
				PopulatedRoomCount++;
			}
		}

		OpenedDoorCount++;

		_knowledge.Enter(Player.Position, GetTile, refresh: true);
		return new DoorExpansion(createdRegion, false, targetRegion!.Kind);
	}

	private bool TryPlaceBoss(DungeonRegion region)
	{
		if (_bossPlaced || _bossPlacement is null || region.Kind != DungeonRegionKind.Room || !_bossPlacement.ShouldPlace(RoomCount)) return false;
		Vector2I[] candidates = region.Floors.Where(p => GetTile(p) == DungeonTile.Floor && !_entities.IsOccupied(p))
			.OrderBy(p => p.DistanceSquaredTo(region.Anchor)).ThenBy(p => p.Y).ThenBy(p => p.X).ToArray();
		if (candidates.Length == 0) return false;
		_entities.Add(new WiseOakEnemy(candidates[0], _expeditionOptions!.WorldTier));
		_bossPlaced = true; return true;
	}
}
