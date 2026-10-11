using CmdRoguelike.Domain.Combat;
using CmdRoguelike.Domain.Entities;
using CmdRoguelike.Domain.Rewards;
using CmdRoguelike.Domain.Stats;

namespace CmdRoguelike.Domain.Progression;

public sealed record DevelopmentNode(string Id, string Branch, string Name, string Description,
    string? Parent = null, bool RequiresOakVictory = false, AttributeId? Attribute = null,
    DerivedStatId? Stat = null, int Bonus = 0, EquipmentCardGrant? Cards = null);

/// <summary>Small prototype tree. Unlocks belong to the campaign, investments to the living hero.</summary>
public static class DevelopmentTree
{
    public const string SourcePrefix = "development:";
    public static IReadOnlyList<DevelopmentNode> Nodes { get; } = Array.AsReadOnly(new[]
    {
        new DevelopmentNode("vitality", "Выживаемость", "Жизненная сила", "+3 максимального HP", Stat: DerivedStatId.MaxHealth, Bonus: 3),
        new DevelopmentNode("guard", "Выживаемость", "Стойкость", "+1 брони для карт блока", "vitality", Stat: DerivedStatId.Armor, Bonus: 1),
        new DevelopmentNode("endurance", "Выживаемость", "Закалка", "+5 максимального HP", "guard", true, Stat: DerivedStatId.MaxHealth, Bonus: 5),
        new DevelopmentNode("strength", "Оружейные приёмы", "Сила", "+2 Силы для требований экипировки", Attribute: AttributeId.Strength, Bonus: 2),
        new DevelopmentNode("crushing", "Оружейные приёмы", "Тяжёлый удар", "2 карты: 2 AP, 5 физического урона", "strength",
            Cards: new(new("develop-crushing", "Тяжёлый удар", CombatCardKind.Attack, 2, 5), 2)),
        new DevelopmentNode("mastery", "Оружейные приёмы", "Точный удар", "2 карты: 1 AP, 3 физического урона", "crushing", true,
            Cards: new(new("develop-mastery", "Точный удар", CombatCardKind.Attack, 1, 3), 2)),
        new DevelopmentNode("will", "Воля", "Решимость", "+2 Воли для инициативы", Attribute: AttributeId.Willpower, Bonus: 2),
        new DevelopmentNode("discipline", "Воля", "Дисциплина", "+3 Воли для инициативы", "will", Attribute: AttributeId.Willpower, Bonus: 3),
        new DevelopmentNode("bulwark", "Воля", "Несокрушимость", "2 карты: 1 AP, блок от 200% брони", "discipline", true,
            Cards: new(new("develop-bulwark", "Несокрушимость", CombatCardKind.Defense, 1, 200), 2)),
    });
    public static DevelopmentNode Find(string id) => Nodes.FirstOrDefault(n => n.Id == id)
        ?? throw new ArgumentException("Неизвестный узел развития.");
    public static string[] Unlocks(bool oakVictory) => Nodes.Where(n => oakVictory || !n.RequiresOakVictory).Select(n => n.Id).ToArray();
}

public enum DevelopmentResult { Success, Locked, Unknown, AlreadyLearned, ParentRequired, NoPoints }
public sealed record SavedDevelopment(long Experience, string[] LearnedNodes);
public sealed record ExperienceGain(int Amount, int LevelsGained);

public sealed class HeroProgression
{
    public const int MaximumLevel = 10;
    private readonly PlayerCharacter _hero;
    private readonly HashSet<string> _learned = new(StringComparer.Ordinal);
    public long Experience { get; private set; }
    public int Level { get; private set; } = 1;
    public int AvailablePoints => Level - 1 - _learned.Count;
    public IReadOnlyCollection<string> LearnedNodes => _learned.Order(StringComparer.Ordinal).ToArray();
    public long ExperienceInLevel => Experience - Threshold(Level);
    public int ExperienceToNextLevel => Level == MaximumLevel ? 0 : Level * 100;
    public static long Threshold(int level) => (long)level * (level - 1) * 50;
    internal HeroProgression(PlayerCharacter hero) => _hero = hero;

    /// <summary>Fixed encounter difficulty, no bonus for taking damage or delaying a battle.</summary>
    public ExperienceGain AwardVictory(CombatVictorySummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        if (!_hero.IsAlive || Level == MaximumLevel) return new(0, 0);
        long budget = summary.Enemies.Sum(e => (long)Math.Max(e.Difficulty, e.StartingHealth)) * 10;
        return AddExperience(checked((int)Math.Min(int.MaxValue, Math.Max(1, budget / Level))));
    }
    internal ExperienceGain AddExperience(int amount)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        int previous = Level;
        long next = Math.Min(Threshold(MaximumLevel), Experience + amount);
        int gained = (int)(next - Experience);
        Experience = next;
        while (Level < MaximumLevel && Experience >= Threshold(Level + 1)) Level++;
        return new(gained, Level - previous);
    }
    public DevelopmentResult CanLearn(string id, IReadOnlyCollection<string> unlocked)
    {
        if (_hero.Inventory.IsLocked || !_hero.IsAlive) return DevelopmentResult.Locked;
        var node = DevelopmentTree.Nodes.FirstOrDefault(n => n.Id == id);
        if (node is null || !unlocked.Contains(id)) return DevelopmentResult.Unknown;
        if (_learned.Contains(id)) return DevelopmentResult.AlreadyLearned;
        if (node.Parent is not null && !_learned.Contains(node.Parent)) return DevelopmentResult.ParentRequired;
        return AvailablePoints > 0 ? DevelopmentResult.Success : DevelopmentResult.NoPoints;
    }
    internal DevelopmentResult Learn(string id, IReadOnlyCollection<string> unlocked)
    {
        var result = CanLearn(id, unlocked);
        if (result != DevelopmentResult.Success) return result;
        Apply(DevelopmentTree.Find(id));
        _learned.Add(id);
        return DevelopmentResult.Success;
    }
    private void Apply(DevelopmentNode node)
    {
        var modifier = new StatModifier(DevelopmentTree.SourcePrefix + node.Id, StatModifierOperation.Flat, node.Bonus);
        if (node.Attribute is AttributeId attribute) _hero.Attributes.AddModifier(attribute, modifier);
        if (node.Stat is DerivedStatId stat) _hero.DerivedStats.AddModifier(stat, modifier);
    }
    internal SavedDevelopment Capture() => new(Experience, LearnedNodes.ToArray());
    internal void Restore(SavedDevelopment saved, IReadOnlyCollection<string> unlocked)
    {
        if (saved.Experience < 0 || saved.Experience > Threshold(MaximumLevel) || saved.LearnedNodes is null
            || saved.LearnedNodes.Distinct(StringComparer.Ordinal).Count() != saved.LearnedNodes.Length)
            throw new InvalidDataException("Повреждено развитие героя.");
        int level = 1;
        while (level < MaximumLevel && saved.Experience >= Threshold(level + 1)) level++;
        if (saved.LearnedNodes.Length > level - 1 || saved.LearnedNodes.Any(id => !unlocked.Contains(id)))
            throw new InvalidDataException("Недоступные узлы развития.");
        foreach (string id in saved.LearnedNodes)
        {
            var node = DevelopmentTree.Find(id);
            if (node.Parent is not null && !saved.LearnedNodes.Contains(node.Parent))
                throw new InvalidDataException("Отсутствует предыдущий узел развития.");
        }
        Experience = saved.Experience; Level = level;
        foreach (var node in DevelopmentTree.Nodes.Where(n => saved.LearnedNodes.Contains(n.Id)))
        { Apply(node); _learned.Add(node.Id); }
    }
}
