using System.Text.Json;
using CmdRoguelike.Domain.Campaign;
using CmdRoguelike.Domain.Entities;
using CmdRoguelike.Domain.Items;
using CmdRoguelike.Domain.Stats;
using Godot;

namespace CmdRoguelike.World;

/// <summary>Three versioned base checkpoints. No expedition terrain or battle is serialized.</summary>
public sealed class CampaignSaveStore
{
	private readonly string _directory;
	private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
	public CampaignSaveStore(string directory) => _directory = Path.GetFullPath(directory);
	private string PathFor(int slot)
	{
		if (slot is < 1 or > 3) throw new ArgumentOutOfRangeException(nameof(slot));
		return Path.Combine(_directory, $"campaign-{slot}.json");
	}
	public bool Exists(int slot) => File.Exists(PathFor(slot));
	public CampaignSaveData? Read(int slot)
	{
		string path = PathFor(slot);
		if (!File.Exists(path)) return null;
		if (new FileInfo(path).Length > 4_000_000) throw new InvalidDataException("Сохранение слишком большое.");
		var data = JsonSerializer.Deserialize<CampaignSaveData>(File.ReadAllText(path), JsonOptions)
			?? throw new InvalidDataException("Пустое сохранение.");
		if (data.Version != 1 || data.Slot != slot) throw new InvalidDataException("Неподдерживаемая версия сохранения.");
		return data;
	}
	public CampaignSession Load(int slot)
	{
		var data = Read(slot) ?? throw new InvalidDataException("Слот пуст.");
		if (data.Storage is null || data.BaseObjects is null || data.KnownItems is null || data.Storage.Count > data.StorageCapacity
			|| data.KnownItems.Distinct(StringComparer.Ordinal).Count() != data.KnownItems.Count
			|| data.WorldTier is < 0 or > 1000 || data.OakVictories < 0 || data.FallenHeroes < 0
			|| data.Hero is null) throw new InvalidDataException("Повреждено состояние прохождения.");
		var layout = new BaseLayout(false);
		layout.Restore(data.BaseObjects.Select(o => new BaseObject(o.Id, o.Kind, new(o.X, o.Y))));
		var storage = new ItemStorage(data.StorageCapacity, data.StorageId);
		foreach (var item in data.Storage)
		{
			if (item.OwnerId != storage.Id || item.Location != ItemLocation.Storage) throw new InvalidDataException("Неверный владелец хранилища.");
			storage.Attach(RestoreItem(item));
		}
		bool lostHero = HasCombatMarker(slot, data.Hero!.Id);
		PlayerCharacter hero;
		if (lostHero) hero = CampaignSession.FreshHero();
		else
		{
			var saved = data.Hero!;
			if (saved.Items is null || saved.Attributes is null || saved.Stats is null) throw new InvalidDataException("Нет состояния героя.");
			hero = new(Vector2I.Zero, id: saved.Id);
			hero.Attributes.Restore(saved.Attributes);
			hero.DerivedStats.Restore(saved.Stats);
			hero.Resources.SynchronizeMaximums(hero.DerivedStats.GetValue(DerivedStatId.MaxHealth), hero.DerivedStats.GetValue(DerivedStatId.MaxMana));
			hero.Inventory.RestoreItems(saved.Items.Select(RestoreItem));
			hero.Resources.Restore(saved.Health, saved.Mana);
			if (!hero.IsAlive) throw new InvalidDataException("На базе сохранён погибший герой.");
		}
		if (storage.Items.Select(i => i.Id).Concat(hero.Inventory.Items.Select(i => i.Id)).Distinct().Count()
			!= storage.Items.Count + hero.Inventory.Items.Count || storage.Id == hero.Id)
			throw new InvalidDataException("Повторяющиеся владельцы или предметы.");
		return new(slot, layout, storage, hero, lostHero ? BaseLayout.Entrance : new(data.HeroX, data.HeroY),
			lostHero ? 0 : data.WorldTier, data.OakVictories, checked(data.FallenHeroes + (lostHero ? 1 : 0)), data.KnownItems);
	}
	public void Save(CampaignSession campaign)
	{
		if (!campaign.IsOnBase) throw new InvalidOperationException("Сохранение доступно только на базе.");
		var hero = campaign.Hero;
		var data = new CampaignSaveData
		{
			Slot = campaign.Slot, SavedAtUtc = DateTimeOffset.UtcNow,
			WorldTier = campaign.WorldTier, OakVictories = campaign.OakVictories, FallenHeroes = campaign.FallenHeroes,
			HeroX = hero.Position.X, HeroY = hero.Position.Y,
			StorageId = campaign.Storage.Id, StorageCapacity = campaign.Storage.Capacity,
			Storage = campaign.Storage.Items.Select(CaptureItem).ToList(),
			KnownItems = campaign.KnownItems.ToList(),
			BaseObjects = campaign.Layout.Objects.Select(o => new SavedBaseObject(o.Id, o.Kind, o.Position.X, o.Position.Y)).ToList(),
			Hero = new SavedHero(hero.Id, hero.Health, hero.Resources.Mana, hero.Attributes.Snapshot(),
				hero.DerivedStats.Snapshot(), hero.Inventory.Items.Select(CaptureItem).ToArray()),
		};
		Directory.CreateDirectory(_directory);
		string path = PathFor(campaign.Slot), temp = path + ".tmp";
		string json = JsonSerializer.Serialize(data, JsonOptions);
		using (var stream = new FileStream(temp, FileMode.Create, System.IO.FileAccess.Write, FileShare.None))
		{
			using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false), leaveOpen: true);
			writer.Write(json); writer.Flush(); stream.Flush(flushToDisk: true);
		}
		File.Move(temp, path, overwrite: true);
		File.Delete(path + ".combat");
	}

	/// <summary>Only an encounter identity marker, never a game/world save. Prevents undoing a lethal battle by quitting.</summary>
	public void MarkCombat(CampaignSession campaign, bool dangerous)
	{
		string path = PathFor(campaign.Slot) + ".combat";
		if (!dangerous) { File.Delete(path); return; }
		if (campaign.Expedition is null) throw new InvalidOperationException("No expedition encounter.");
		Directory.CreateDirectory(_directory);
		File.WriteAllText(path + ".tmp", campaign.Hero.Id.ToString());
		File.Move(path + ".tmp", path, overwrite: true);
	}
	public bool HasCombatMarker(int slot, Guid heroId)
	{
		string path = PathFor(slot) + ".combat";
		if (!File.Exists(path)) return false;
		if (new FileInfo(path).Length > 100 || !Guid.TryParse(File.ReadAllText(path), out Guid marked))
			throw new InvalidDataException("Повреждена отметка боя.");
		return marked == heroId;
	}
	private static SavedItem CaptureItem(ItemInstance item) => new(item.Id, item.OwnerId, item.Definition.Id,
		item.Rarity, item.ItemLevel, item.Quantity, item.Location, item.Slot, item.State,
		item.Affixes.Select(a => new SavedAffix(a.Definition.Id, a.Tier.Tier, a.Value)).ToArray());
	private static ItemInstance RestoreItem(SavedItem saved)
	{
		if (!Enum.IsDefined(saved.Location) || !Enum.IsDefined(saved.State) || saved.Affixes is null || saved.Affixes.Length > 6)
			throw new InvalidDataException("Неверное состояние предмета.");
		var affixes = saved.Affixes.Select(a =>
		{
			var definition = PrototypeAffixes.All.FirstOrDefault(d => d.Id == a.Id)
				?? throw new InvalidDataException("Неизвестный аффикс.");
			var tier = definition.Tiers.FirstOrDefault(t => t.Tier == a.Tier) ?? throw new InvalidDataException("Неизвестный тир.");
			return new ItemAffix(definition, tier, a.Value);
		});
		var roll = new ItemRoll(ContentCatalog.Find(saved.DefinitionId), saved.Rarity, saved.Level, affixes,
			new ItemRarityRules(3, 3, 3));
		return new(saved.OwnerId, roll, saved.Quantity, saved.Id) { Location = saved.Location, Slot = saved.Slot, State = saved.State };
	}
}

public sealed class CampaignSaveData
{
	public int Version { get; set; } = 1;
	public int Slot { get; set; }
	public DateTimeOffset SavedAtUtc { get; set; }
	public int WorldTier { get; set; }
	public int OakVictories { get; set; }
	public int FallenHeroes { get; set; }
	public int HeroX { get; set; }
	public int HeroY { get; set; }
	public Guid StorageId { get; set; }
	public int StorageCapacity { get; set; }
	public List<SavedItem> Storage { get; set; } = new();
	public List<SavedBaseObject> BaseObjects { get; set; } = new();
	public List<string> KnownItems { get; set; } = new();
	public SavedHero? Hero { get; set; }
}
public sealed record SavedBaseObject(Guid Id, BaseObjectKind Kind, int X, int Y);
public sealed record SavedHero(Guid Id, int Health, int Mana, StatSnapshot<AttributeId>[] Attributes,
	StatSnapshot<DerivedStatId>[] Stats, SavedItem[] Items);
public sealed record SavedAffix(string Id, int Tier, int Value);
public sealed record SavedItem(Guid Id, Guid OwnerId, string DefinitionId, ItemRarity Rarity, int Level, int Quantity,
	ItemLocation Location, EquipmentSlot? Slot, EquipmentState State, SavedAffix[] Affixes);
