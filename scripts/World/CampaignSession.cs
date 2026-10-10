using CmdRoguelike.Domain.Campaign;
using CmdRoguelike.Domain.Entities;
using CmdRoguelike.Domain.Items;
using CmdRoguelike.Generation;
using Godot;

namespace CmdRoguelike.World;

/// <summary>One persistent base and one living hero; each expedition owns a disposable map.</summary>
public sealed class CampaignSession
{
	public int Slot { get; }
	public string Name => $"Прохождение {Slot}";
	public BaseLayout Layout { get; }
	public ItemStorage Storage { get; }
	public PlayerCharacter Hero { get; private set; }
	public BaseMap? Base { get; private set; }
	public DungeonMap? Expedition { get; private set; }
	public int WorldTier { get; private set; }
	public int OakVictories { get; private set; }
	public int FallenHeroes { get; private set; }
	private readonly HashSet<string> _knownItems = new(StringComparer.Ordinal);
	public IReadOnlyCollection<string> KnownItems => _knownItems.Order(StringComparer.Ordinal).ToArray();
	public bool IsOnBase => Base is not null && Expedition is null;

	public CampaignSession(int slot)
	{
		if (slot is < 1 or > 3) throw new ArgumentOutOfRangeException(nameof(slot));
		Slot = slot; Layout = new(); Storage = new(); Hero = CreateHero(); Base = new(Layout, Hero);
		Storage.Supply(new(ExpeditionItems.FireTorch));
		Storage.Supply(new(RewardItemCatalog.HealingPotion), 3);
		Observe(Hero.Inventory.Items.Select(i => i.Definition).Concat(Storage.Items.Select(i => i.Definition)));
	}
	internal CampaignSession(int slot, BaseLayout layout, ItemStorage storage, PlayerCharacter hero,
		Vector2I position, int tier, int victories, int deaths, IEnumerable<string> known)
	{
		if (slot is < 1 or > 3 || tier is < 0 or > 1000 || victories < 0 || deaths < 0) throw new ArgumentException("Invalid campaign progress.");
		Slot = slot; Layout = layout; Storage = storage; Hero = hero;
		WorldTier = tier; OakVictories = victories; FallenHeroes = deaths;
		Observe(known.Select(ContentCatalog.Find)); Base = new(layout, hero, position);
	}
	private static PlayerCharacter CreateHero()
	{
		var hero = new PlayerCharacter(Vector2I.Zero);
		foreach (var (definition, quantity) in PrototypeItems.StartingItems.Where(i => i.Definition.Category != ItemCategory.Material))
			if (hero.Inventory.TryAcquire(definition, quantity) != InventoryResult.Success) throw new InvalidOperationException("Starter inventory overflow.");
		return hero;
	}
	public void Observe(IEnumerable<ItemDefinition> definitions)
	{
		foreach (var definition in definitions) _knownItems.Add(definition.Id);
	}
	public InventoryResult Store(Guid id) => IsOnBase ? Hero.Inventory.Store(id, Storage) : InventoryResult.Locked;
	public InventoryResult Withdraw(Guid id) => IsOnBase ? Hero.Inventory.Withdraw(id, Storage) : InventoryResult.Locked;
	public bool Rest()
	{
		if (!IsOnBase || !Hero.IsAlive) return false;
		Hero.RestoreHealth(Hero.MaxHealth); Hero.Resources.RestoreMana(Hero.Resources.MaxMana); return true;
	}
	public DungeonMap StartExpedition(int seed, DungeonGenerationOptions options)
	{
		if (!IsOnBase || !Hero.IsAlive) throw new InvalidOperationException("Expeditions start on the base.");
		ArgumentNullException.ThrowIfNull(options);
		Vector2I original = Hero.Position;
		Base!.DetachPlayer();
		try
		{
			var map = new DungeonMap(seed, options, Hero, expeditionOptions: new(WorldTier));
			Base = null; Expedition = map; return map;
		}
		catch
		{
			Hero.Inventory.UnlockOnBase(); Base = new(Layout, Hero, original); throw;
		}
	}
	public bool EnterReturnPortal()
	{
		if (Expedition is null || !Expedition.CanUseReturnPortal) return false;
		Expedition.ReleasePlayer(); Expedition = null; Hero.Inventory.UnlockOnBase();
		WorldTier = Math.Min(1000, WorldTier + 1); OakVictories++;
		Base = new(Layout, Hero); Observe(Hero.Inventory.Items.Select(i => i.Definition)); return true;
	}
	public bool AcceptDeath(bool abandon = false)
	{
		if (Expedition is null || Hero.IsAlive && !abandon) return false;
		Expedition = null; FallenHeroes++; WorldTier = 0;
		Hero = CreateHero(); Base = new(Layout, Hero);
		Observe(Hero.Inventory.Items.Select(i => i.Definition)); return true;
	}
	internal static PlayerCharacter FreshHero() => CreateHero();
}
