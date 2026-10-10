using CmdRoguelike.Domain.Combat;
using CmdRoguelike.Domain.Entities;
using CmdRoguelike.Domain.Items;
using CmdRoguelike.Domain.Stats;
using CmdRoguelike.Generation;
using CmdRoguelike.Presentation;
using CmdRoguelike.World;
using Godot;

namespace CmdRoguelike.Tests;

internal static class CombatContentChecks
{
	private static void Check(bool condition, string message) => CombatSmokeTest.Check(condition, message);
	private sealed class DummyEnemy : Enemy
	{
		public DummyEnemy() : base(Vector2I.Right, "Dummy", 100, 0) { }
	}
	public static void Run()
	{
		EquipmentDeck();
		DormantAndAtomicCards();
		EnemyIntents();
		FrozenIntent();
		FactoryDeterminism();
		ShieldCard();
	}
	private static void EquipmentDeck()
	{
		var hero = new PreparationSession().Hero;
		Check(CombatDeckBuilder.Build(hero).Count == 10, "Backpack granted combat cards.");
		var ring = hero.Inventory.Items.Single(item => item.Definition.Id == "strength-ring");
		var sword = hero.Inventory.Items.Single(item => item.Definition.Id == "training-sword");
		var shield = hero.Inventory.Items.Single(item => item.Definition.Id == "training-shield");
		hero.Inventory.TryEquip(ring.Id, EquipmentSlot.RingLeft);
		hero.Inventory.TryEquip(sword.Id, EquipmentSlot.MainHand);
		hero.Inventory.TryEquip(shield.Id, EquipmentSlot.OffHand);
		var deck = CombatDeckBuilder.Build(hero);
		Check(deck.Count == 15 && deck.Count(card => card.SourceItemId == sword.Id) == 3
			&& deck.Count(card => card.SourceItemId == shield.Id) == 2, "Equipment contributions or sources are wrong.");
		Check(deck.SingleOrDefault(card => card.Id == "crush") is null, "Unequipped alternate weapon granted cards.");
		var battle = new CombatEncounter(hero, new[] { new DummyEnemy() }, 73);
		var sameHero = new PreparationSession().Hero;
		foreach (string id in new[] { "strength-ring", "training-sword", "training-shield" })
		{
			var item = sameHero.Inventory.Items.Single(item => item.Definition.Id == id);
			Check(sameHero.Inventory.TryEquip(item.Id, item.Definition.Slots[0]) == InventoryResult.Success, "Comparison fixture failed to equip.");
		}
		var copy = new CombatEncounter(sameHero, new[] { new DummyEnemy() }, 73);
		for (int turn = 0; turn < 20; turn++)
		{
			battle.BeginRound(); copy.BeginRound();
			Check(battle.Hand.Select(card => (card.Id, card.SourceName)).SequenceEqual(copy.Hand.Select(card => (card.Id, card.SourceName))),
				"Equipment GUIDs changed seeded card order.");
			Check(battle.Hand.Count + battle.DrawCount + battle.DiscardCount == 15, "Equipment cards disappeared or duplicated.");
			if (battle.CanReplaceCard) { battle.ReplaceCard(0); copy.ReplaceCard(0); }
			battle.EndPlayerTurn(); copy.EndPlayerTurn();
		}
		hero.Inventory.TryUnequip(sword.Id);
		Check(CombatDeckBuilder.Build(hero).Count == 12, "Unequipped weapon remained in the next deck.");
		// Two copies of the same definition in two slots retain distinct sources.
		var twinHero = new PlayerCharacter(Vector2I.Zero);
		var twin = new ItemDefinition("twin", "Twin", slots: new[] { EquipmentSlot.RingLeft, EquipmentSlot.RingRight },
			combatCards: new[] { new EquipmentCardGrant(new("twin-hit", "Twin hit", CombatCardKind.Attack, 1, 2), 1) });
		twinHero.Inventory.TryAcquire(twin, 2);
		twinHero.Inventory.TryEquip(twinHero.Inventory.Items[0].Id, EquipmentSlot.RingLeft);
		twinHero.Inventory.TryEquip(twinHero.Inventory.Items[1].Id, EquipmentSlot.RingRight);
		Check(CombatDeckBuilder.Build(twinHero).Where(card => card.Id == "twin-hit").Select(card => card.SourceItemId).Distinct().Count() == 2,
			"Identical equipment lost distinct card sources.");
	}
	private static void DormantAndAtomicCards()
	{
		var hero = new PlayerCharacter(Vector2I.Zero);
		var definition = new ItemDefinition("weapon", "Weapon", slots: new[] { EquipmentSlot.MainHand },
			requirements: new Dictionary<AttributeId, int> { [AttributeId.Strength] = 1 },
			combatCards: new[] { new EquipmentCardGrant(new("heavy", "Heavy", CombatCardKind.Attack, 2, 4), 4) });
		hero.Inventory.TryAcquire(definition);
		hero.Attributes.AddModifier(AttributeId.Strength, new("buff", StatModifierOperation.Flat, 1));
		hero.Inventory.TryEquip(hero.Inventory.Items[0].Id, EquipmentSlot.MainHand);
		var enemy = new DummyEnemy();
		var battle = new CombatEncounter(hero, new[] { enemy }, 1, new CombatOptions(handSize: 10));
		battle.BeginRound();
		int index = battle.Hand.ToList().FindIndex(card => card.Id == "heavy");
		Check(index >= 0, "Fixture did not draw equipment cards.");
		hero.Attributes.RemoveModifiersFromSource("buff");
		string dormantScreen = CombatAsciiRenderer.Compose(battle, new CombatAnimation(battle), index, 0, "", Array.Empty<string>()).Text;
		Check(dormantScreen.Contains("DORMANT") && dormantScreen.Contains("Weapon"), "Combat UI omitted unavailable card or equipment source.");
		var hand = battle.Hand.ToArray();
		var events = battle.Events.ToArray();
		Check(CombatDeckBuilder.Build(hero).Count == 10 && battle.PlayCard(index, enemy.Id) == CombatCommandResult.SourceUnavailable
			&& battle.ActionPoints == 3 && battle.Hand.SequenceEqual(hand) && battle.Events.SequenceEqual(events) && enemy.Health == 100,
			"Dormant card changed AP, hand, events, or HP.");
		hero.Attributes.AddModifier(AttributeId.Strength, new("buff", StatModifierOperation.Flat, 1));
		Check(battle.IsCardAvailable(battle.Hand[index]) && battle.PlayCard(index, Guid.NewGuid()) == CombatCommandResult.InvalidTarget
			&& battle.ActionPoints == 3 && enemy.Health == 100, "Reactivation or atomic target rejection failed.");
		Check(battle.PlayCard(index, enemy.Id) == CombatCommandResult.Success && battle.ActionPoints == 1 && enemy.Health == 96,
			"Heavy equipment card ignored cost or damage.");
		index = battle.Hand.ToList().FindIndex(card => card.Id == "heavy");
		Check(index >= 0, "Fixture lost remaining equipment cards.");
		Check(battle.PlayCard(index, enemy.Id) == CombatCommandResult.NotEnoughActionPoints && battle.ActionPoints == 1 && enemy.Health == 96,
			"Expensive card consumed state without enough AP.");
		hero.Attributes.RemoveModifiersFromSource("buff");
		Check(battle.ReplaceCard(index) == CombatCommandResult.Success && battle.Hand.Count == 9, "Dormant card cannot be replaced.");
		Check(battle.Hand.Count + battle.DrawCount + battle.DiscardCount == 14, "Dormant transition destroyed cards.");
	}
	private static void EnemyIntents()
	{
		var hero = new PlayerCharacter(Vector2I.Zero);
		var guard = new GuardianEnemy(Vector2I.Right);
		var brute = new BruteEnemy(new Vector2I(2, 0));
		var battle = new CombatEncounter(hero, new Enemy[] { guard, brute }, 1, new CombatOptions(attackCards: 10, defenseCards: 0));
		string preview = CombatAsciiRenderer.Compose(battle, new CombatAnimation(battle), 0, 0, "", Array.Empty<string>()).Text;
		Check(preview.Contains("Защитник") && preview.Contains("Громила") && preview.Contains("+2 блока") && preview.Contains("Готовит удар"),
			"Combat preview omitted enemy roles or setup actions.");
		Check(battle.Intents[0].Action == EnemyActionKind.Guard && battle.Intents[0].TargetId == guard.Id
			&& battle.Intents[1].Action == EnemyActionKind.Charge && hero.Health == 10 && battle.GetEnemyBlock(guard.Id) == 0,
			"Setup intents have the wrong target or applied before confirmation.");
		battle.BeginRound(); battle.EndPlayerTurn();
		Check(hero.Health == 10 && battle.GetEnemyBlock(guard.Id) == 2 && battle.Events.Any(e => e.Kind == CombatEventKind.Charge),
			"Guard or charge dealt damage or failed to execute.");
		Check(battle.Intents.Single(i => i.EnemyId == brute.Id).Damage == 3, "Strong attack was not telegraphed next round.");
		battle.BeginRound();
		battle.PlayCard(0, guard.Id);
		Check(guard.Health == 4 && battle.GetEnemyBlock(guard.Id) == 1 && battle.Events[0].Blocked == 1,
			"Enemy block failed to absorb player damage.");
		battle.EndPlayerTurn();
		Check(hero.Health == 6 && battle.GetEnemyBlock(guard.Id) == 0
			&& battle.Events.Any(e => e.Kind == CombatEventKind.BlockExpired && e.Blocked == 1), "Enemy block duration or strong attack damage is wrong.");
		var doomed = new BruteEnemy(Vector2I.Right);
		var survivor = new BasicEnemy(new Vector2I(2, 0));
		var won = new CombatEncounter(new PlayerCharacter(Vector2I.Zero), new Enemy[] { doomed, survivor }, 1,
			new CombatOptions(attackCards: 10, defenseCards: 0, attackDamage: 5));
		won.BeginRound(); won.PlayCard(0, doomed.Id); won.EndPlayerTurn();
		Check(won.Events.All(e => e.ActorId != doomed.Id) && won.Player.Health == 9, "Dead enemy executed its announced preparation.");
		// Early enemy defense must survive the player's own block reset.
		var early = new GuardianEnemy(Vector2I.Right);
		early.Attributes.AddModifier(AttributeId.Willpower, new("test", StatModifierOperation.Flat, 10));
		var earlyBattle = new CombatEncounter(new PlayerCharacter(Vector2I.Zero),
			new Enemy[] { early, new DummyEnemy(), new BruteEnemy(new(3, 0)), new BasicEnemy(new(4, 0)) }, 1);
		earlyBattle.BeginRound();
		Check(earlyBattle.GetEnemyBlock(early.Id) == 2 && earlyBattle.Events[0].Kind == CombatEventKind.Block,
			"Early enemy defense vanished at the start of player turn.");
	}
	private sealed class MutableBehavior : IEnemyBehavior
	{
		public int Power { get; set; } = 1;
		public int Calls { get; private set; }
		public EnemyAction Plan(Enemy enemy, int round) { Calls++; return new(EnemyActionKind.Attack, Power); }
	}
	private sealed class PlannedEnemy : Enemy
	{
		public MutableBehavior Planner { get; } = new();
		public override IEnemyBehavior Behavior => Planner;
		public PlannedEnemy() : base(Vector2I.Right, "Planned", 100, 1) { }
	}
	private static void FrozenIntent()
	{
		var enemy = new PlannedEnemy();
		var battle = new CombatEncounter(new PlayerCharacter(Vector2I.Zero), new[] { enemy }, 1);
		enemy.Planner.Power = 9;
		battle.BeginRound(); battle.EndPlayerTurn();
		Check(battle.Player.Health == 9 && battle.Events.Single(e => e.Kind == CombatEventKind.Attack).Damage == 1
			&& enemy.Planner.Calls == 2 && battle.Intents[0].Damage == 9, "Enemy replanned during execution instead of honoring intent.");
	}
	private static void FactoryDeterminism()
	{
		var a = new EnemyFactory(new GodotRandomSource(12));
		var b = new EnemyFactory(new GodotRandomSource(12));
		var types = new HashSet<Type>();
		for (int i = 0; i < 100; i++)
		{
			var type = a.Create(new(i, 0)).GetType(); types.Add(type);
			Check(type == b.Create(new(i, 0)).GetType(), "Same population seed changed enemy types.");
		}
		Check(types.SetEquals(new[] { typeof(BasicEnemy), typeof(GuardianEnemy), typeof(BruteEnemy) }), "Factory omitted an enemy role.");
	}
	private static void ShieldCard()
	{
		var hero = new PreparationSession().Hero;
		var shield = hero.Inventory.Items.Single(item => item.Definition.Id == "training-shield");
		Check(hero.Inventory.TryEquip(shield.Id, EquipmentSlot.OffHand) == InventoryResult.Success, "Shield equip failed.");
		var battle = new CombatEncounter(hero, new[] { new DummyEnemy() }, 1, new CombatOptions(handSize: 10));
		battle.BeginRound();
		int shieldIndex = battle.Hand.ToList().FindIndex(card => card.Id == "shield");
		Check(shieldIndex >= 0, "Fixture did not draw shield.");
		Check(battle.PlayCard(shieldIndex) == CombatCommandResult.Success && battle.Block == 2 && battle.ActionPoints == 2,
			"Shield card ignored 200% armor or its AP cost.");
	}
}
