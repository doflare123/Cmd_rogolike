using System.Text.Json;
using System.Text.Json.Nodes;
using CmdRoguelike.Domain.Combat;
using CmdRoguelike.Domain.Entities;
using CmdRoguelike.Domain.Progression;
using CmdRoguelike.Domain.Rewards;
using CmdRoguelike.Domain.Stats;
using CmdRoguelike.Generation;
using CmdRoguelike.Presentation;
using CmdRoguelike.World;
using Godot;

namespace CmdRoguelike.Tests;

public partial class DevelopmentSmokeTest : Node
{
    private readonly string _directory = "res://.godot/development-test-" + Guid.NewGuid().ToString("N");
    private static void Check(bool value, string message) => CombatSmokeTest.Check(value, message);
    public override async void _Ready()
    {
        try
        {
            Rules(); OrdinaryVictory(); Saves(); await Interface();
            GD.Print("Development smoke test passed: XP/levels/cap, diminishing easy rewards, base-only tree, prerequisites, HP without healing, sourced cards, exact save/load, v1 migration, corrupt save rejection, death/quit ownership and keyboard UI.");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
    private static void OrdinaryVictory()
    {
        var hero = new PlayerCharacter(Vector2I.Zero);
        var map = new DungeonMap(42, new DungeonGenerationOptions(roomChance: 1, enemyRoomChance: 1), hero,
            combatOptions: new CombatOptions(attackDamage: 99), expeditionOptions: new ExpeditionOptions());
        ExpeditionTestDriver.FindCombat(map);
        Check(map.Combat!.Enemies.All(e => !e.IsBoss) && hero.Progression.Experience == 0, "Entering an ordinary encounter gave XP.");
        for (int turn = 0; turn < 20 && !map.Combat.IsFinished; turn++)
        {
            map.BeginCombatRound();
            while (map.Combat.ActionPoints > 0 && !map.Combat.IsFinished)
            {
                int index = map.Combat.Hand.ToList().FindIndex(c => c.Kind == CombatCardKind.Attack);
                if (index < 0) break;
                map.PlayCombatCard(index, map.Combat.Enemies.First(e => e.IsAlive).Id);
            }
            if (!map.Combat.IsFinished) map.EndCombatTurn();
        }
        long experience = hero.Progression.Experience;
        Check(map.Combat.Phase == CombatPhase.Victory && experience > 0 && map.PendingReward!.Experience.Amount == experience,
            "Ordinary world victory did not award XP.");
        map.BeginCombatRound(); map.EndCombatTurn(); map.RevealReward(map.PendingReward!.Id); map.RevealReward(map.PendingReward.Id);
        Check(hero.Progression.Experience == experience, "Repeated commands/reveal duplicated ordinary XP.");
    }
    private static CombatVictorySummary Summary(int difficulty = 5, int health = 3) => new(
        new[] { new RewardEnemy(difficulty, health, EnemyRewardRole.Attacker) }, 1, 10, 10, 0, 0);
    private static void Rules()
    {
        var campaign = new CampaignSession(1); var hero = campaign.Hero;
        Check(hero.Progression.Level == 1 && hero.Progression.AvailablePoints == 0, "New hero has free development.");
        Check(campaign.LearnDevelopment("vitality") == DevelopmentResult.NoPoints, "Learning without XP succeeded.");
        int easy = hero.Progression.AwardVictory(Summary()).Amount;
        Check(easy == 50 && hero.Progression.Level == 1, "Unexpected initial XP.");
        var gain = hero.Progression.AwardVictory(Summary());
        Check(gain.LevelsGained == 1 && hero.Progression.Level == 2 && hero.Progression.AvailablePoints == 1, "XP threshold failed.");
        Check(hero.Progression.AwardVictory(Summary()).Amount < easy, "Easy encounters never diminish.");
        Check(hero.Progression.AwardVictory(Summary(24, 30)).Amount == 150, "Scaled boss health did not affect XP.");
        Check(campaign.LearnDevelopment("guard") == DevelopmentResult.ParentRequired, "Tree skipped parent.");
        Check(campaign.LearnDevelopment("endurance") == DevelopmentResult.Unknown, "Locked campaign node was learned.");
        hero.TakeDamage(2); int health = hero.Health;
        Check(campaign.LearnDevelopment("vitality") == DevelopmentResult.Success && hero.MaxHealth == 13 && hero.Health == health, "HP node healed or failed.");
        Check(campaign.LearnDevelopment("vitality") == DevelopmentResult.AlreadyLearned, "Repeated node spent a point.");
        hero.Progression.AddExperience(10000);
        Check(hero.Progression.Level == 10 && hero.Progression.Experience == 4500 && hero.Progression.AwardVictory(Summary()).Amount == 0, "Prototype cap failed.");
        Check(campaign.LearnDevelopment("strength") == DevelopmentResult.Success && campaign.LearnDevelopment("crushing") == DevelopmentResult.Success, "Weapon branch failed.");
        Check(hero.Attributes.GetValue(AttributeId.Strength) == 2, "Strength source failed.");
        var cards = CombatDeckBuilder.Build(hero);
        Check(cards.Count(c => c.Id == "develop-crushing") == 2 && cards.Where(c => c.Id == "develop-crushing").All(c => c.SourceName.Contains("Развитие") && c.SourceItemId is null), "Skill cards lack stable source.");
        var map = campaign.StartExpedition(42, new DungeonGenerationOptions(enemyRoomChance: 0));
        Check(campaign.LearnDevelopment("guard") == DevelopmentResult.Locked && hero.Progression.AvailablePoints == 6, "Expedition allowed investments.");
        hero.TakeDamage(999);
        Check(campaign.AcceptDeath() && campaign.Hero.Progression.Level == 1 && campaign.Hero.Progression.LearnedNodes.Count == 0
            && campaign.Hero.MaxHealth == 10 && campaign.UnlockedDevelopment.Count == 6, "Death retained personal development.");
    }
    private void Saves()
    {
        var directory = ProjectSettings.GlobalizePath(_directory);
        var store = new CampaignSaveStore(directory); var campaign = new CampaignSession(1);
        campaign.Hero.Progression.AddExperience(650);
        campaign.LearnDevelopment("vitality"); campaign.LearnDevelopment("strength"); campaign.LearnDevelopment("crushing");
        campaign.Hero.TakeDamage(2);
        store.Save(campaign); var loaded = store.Load(1);
        Check(loaded.Hero.Progression.Experience == 650 && loaded.Hero.Progression.Level == 4 && loaded.Hero.Progression.AvailablePoints == 0
            && loaded.Hero.MaxHealth == 13 && loaded.Hero.Health == 8 && loaded.Hero.Attributes.GetValue(AttributeId.Strength) == 2,
            "Save lost or duplicated development bonuses/resources.");
        store.Save(loaded); loaded = store.Load(1);
        Check(loaded.Hero.MaxHealth == 13 && CombatDeckBuilder.Build(loaded.Hero).Count(c => c.Id == "develop-crushing") == 2, "Repeated save duplicated bonuses/cards.");
        loaded.StartExpedition(3, new DungeonGenerationOptions(enemyRoomChance: 0));
        loaded.Hero.Progression.AwardVictory(Summary());
        Check(store.Load(1).Hero.Progression.Experience == 650, "Safe exploration quit persisted unsaved XP.");
        store.MarkCombat(loaded, true); var fresh = store.Load(1);
        Check(fresh.Hero.Id != loaded.Hero.Id && fresh.Hero.Progression.Experience == 0, "Combat loss retained XP.");
        store.Save(fresh);
        string path = Path.Combine(directory, "campaign-1.json");
        var legacy = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        legacy["Version"] = 1; legacy.Remove("UnlockedDevelopment"); legacy["Hero"]!.AsObject().Remove("Development");
        File.WriteAllText(path, legacy.ToJsonString()); var migrated = store.Load(1);
        Check(migrated.Hero.Progression.Level == 1 && migrated.UnlockedDevelopment.Count == 6, "Legacy save did not load.");
        store.Save(migrated); Check(store.Read(1)!.Version == 2, "Legacy save was not upgraded.");
        var profile = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        profile["Version"] = 1; profile["OakVictories"] = 1; profile.Remove("UnlockedDevelopment");
        profile["Hero"]!.AsObject().Remove("Development");
        File.WriteAllText(path, profile.ToJsonString()); var veteran = store.Load(1);
        Check(veteran.UnlockedDevelopment.Count == 9, "Legacy boss history did not unlock advanced nodes.");
        veteran.Hero.Progression.AddExperience(4500);
        foreach (var node in DevelopmentTree.Nodes) Check(veteran.LearnDevelopment(node.Id) == DevelopmentResult.Success, "Unlocked full tree failed.");
        int maxHealth = veteran.Hero.MaxHealth;
        store.Save(veteran); veteran = store.Load(1);
        Check(veteran.Hero.MaxHealth == maxHealth && veteran.Hero.Progression.AvailablePoints == 0
            && CombatDeckBuilder.Build(veteran.Hero).Count(c => c.SourceName.StartsWith("Развитие:", StringComparison.Ordinal)) == 6,
            "Complete tree lost bonuses/cards on load.");
        veteran.StartExpedition(3, new DungeonGenerationOptions(enemyRoomChance: 0)); store.MarkCombat(veteran, true);
        fresh = store.Load(1);
        Check(fresh.UnlockedDevelopment.Count == 9 && fresh.Hero.Progression.Level == 1 && fresh.Hero.Progression.LearnedNodes.Count == 0,
            "Combat marker lost profile unlocks or preserved investments.");
        store.Save(fresh);
        var corrupt = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        corrupt["Hero"]!["Development"]!["LearnedNodes"] = new JsonArray("vitality");
        string corruptText = corrupt.ToJsonString(); File.WriteAllText(path, corruptText);
        bool rejected = false; try { store.Load(1); } catch (InvalidDataException) { rejected = true; }
        Check(rejected && File.ReadAllText(path) == corruptText, "Invalid investments were accepted/overwritten.");
    }
    private async Task Interface()
    {
        var game = new DungeonGame { SaveDirectory = _directory + "-ui", SettingsPath = _directory + "-settings.cfg" };
        AddChild(game); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Press(game, Key.Enter, Key.Enter); var campaign = game.CurrentCampaign!;
        campaign.Hero.Progression.AddExperience(300);
        Press(game, Key.P);
        var panel = game.GetChildren().OfType<DevelopmentPanel>().Single();
        Check(panel.ScreenText.Contains("Уровень 3") && panel.ScreenText.Contains("Свободных очков: 2"), "P failed to open progression.");
        var position = campaign.Hero.Position;
        Press(game, Key.Down, Key.Enter);
        Check(campaign.Hero.Position == position && panel.ScreenText.Contains("Сначала изучите"), "Tree input moved hero or skipped prerequisite.");
        Press(game, Key.Up, Key.Enter);
        Check(panel.ScreenText.Contains("Прохождение сохранено") && campaign.Hero.MaxHealth == 13, "Tree keyboard failed to learn/save.");
        Press(game, Key.Down, Key.Right, Key.Enter);
        Check(panel.ScreenText.Contains("Тяжёлый удар —") && panel.ScreenText.Contains("Сначала изучите"), "Horizontal tree navigation lost depth or bypassed parent.");
        Press(game, Key.Up, Key.Enter);
        Check(campaign.Hero.Progression.LearnedNodes.Contains("strength") && panel.ScreenText.Contains("Сила —"), "Spatial navigation could not learn another branch.");
        Press(game, Key.Left);
        Check(panel.ScreenText.Contains("Жизненная сила —") && campaign.Hero.Position == position, "Tree navigation changed world position.");
        Press(game, Key.F2, Key.Escape);
        Check(game.GetChildren().OfType<DevelopmentPanel>().Any(), "Settings lost development panel.");
        await Capture("development-tree.png");
        if (DisplayServer.GetName() != "headless") { GetWindow().Size = new(800, 600); await Capture("development-tree-small.png"); GetWindow().Size = new(1152, 648); }
        Press(game, Key.P, Key.Escape, Key.Enter, Key.Enter);
        Check(game.CurrentCampaign!.Hero.Progression.LearnedNodes.Contains("vitality") && game.CurrentCampaign.Hero.MaxHealth == 13, "Menu reload lost development.");
        game.QueueFree(); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
    private static void Press(DungeonGame game, params Key[] keys)
    { foreach (Key key in keys) game._UnhandledKeyInput(new InputEventKey { Keycode = key, Pressed = true }); }
    private async Task Capture(string name)
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (DisplayServer.GetName() == "headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng("res://.godot/" + name);
    }
}
