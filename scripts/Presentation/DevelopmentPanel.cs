using CmdRoguelike.Domain.Progression;
using CmdRoguelike.World;
using Godot;

namespace CmdRoguelike.Presentation;

internal sealed partial class DevelopmentPanel : TerminalOverlay
{
    private readonly CampaignSession _campaign;
    private readonly Action _close;
    private readonly Func<bool> _save;
    private int _selection;
    private string _status = "Каждый узел стоит 1 очко. Вложенные очки теряются вместе с героем.";
    public DevelopmentPanel(CampaignSession campaign, Action close, Func<bool> save)
    { _campaign = campaign; _close = close; _save = save; Layer = 4; }
    public void HandleKey(InputEventKey key)
    {
        if (!key.Pressed) return;
        if (!key.Echo && key.Keycode is Key.Escape or Key.P) { _close(); return; }
        if (key.Keycode is Key.W or Key.Up) _selection = _selection / 3 * 3 + Math.Max(0, _selection % 3 - 1);
        else if (key.Keycode is Key.S or Key.Down) _selection = _selection / 3 * 3 + Math.Min(2, _selection % 3 + 1);
        else if (key.Keycode is Key.A or Key.Left) _selection = Math.Max(0, _selection / 3 - 1) * 3 + _selection % 3;
        else if (key.Keycode is Key.D or Key.Right) _selection = Math.Min(2, _selection / 3 + 1) * 3 + _selection % 3;
        else if (!key.Echo && key.Keycode is Key.Enter or Key.E)
        {
            var node = DevelopmentTree.Nodes[_selection];
            var result = _campaign.LearnDevelopment(node.Id);
            _status = result switch
            {
                DevelopmentResult.Success => _save() ? $"Изучено: {node.Name}. Прохождение сохранено." : $"Изучено: {node.Name}. Сохранить не удалось; повторите F5 на базе.",
                DevelopmentResult.NoPoints => "Нет свободных очков. Получайте опыт за победы и повышайте уровень.",
                DevelopmentResult.ParentRequired => "Сначала изучите предыдущий узел этой ветви.",
                DevelopmentResult.AlreadyLearned => "Этот узел уже изучен.",
                DevelopmentResult.Unknown => "Открывается для прохождения после победы над дубом и возвращения через портал.",
                _ => "Развитие доступно только живому герою на базе.",
            };
        }
        Refresh();
    }
    protected override List<Line> Compose()
    {
        var lines = new List<Line>();
        var progress = _campaign.Hero.Progression;
        Put(lines, 3, 1, "CMD ROGUELIKE / РАЗВИТИЕ ГЕРОЯ", Accent);
        Put(lines, 3, 3, $"Уровень {progress.Level}/{HeroProgression.MaximumLevel} | Свободных очков: {progress.AvailablePoints} | Всего опыта: {progress.Experience}", Ink);
        Put(lines, 3, 5, progress.Level == HeroProgression.MaximumLevel ? "Достигнут предел уровня этого прототипа."
            : $"Опыт уровня: {progress.ExperienceInLevel}/{progress.ExperienceToNextLevel}. Следующее повышение даёт 1 очко.", Gold);
        Box(lines, 3, 7, 106, 24, "ДЕРЕВО РАЗВИТИЯ");
        Put(lines, 48, 8, "+---------------+", Accent, 17);
        Put(lines, 48, 9, "|     ГЕРОЙ     |", Accent, 17);
        Put(lines, 48, 10, "+-------+-------+", Accent, 17);
        Put(lines, 56, 11, "|", Accent, 1);
        Put(lines, 20, 12, "+" + new string('-', 35) + "+" + new string('-', 35) + "+", Accent, 73);
        for (int i = 0; i < DevelopmentTree.Nodes.Count; i++)
        {
            var node = DevelopmentTree.Nodes[i];
            bool known = _campaign.UnlockedDevelopment.Contains(node.Id);
            bool learned = progress.LearnedNodes.Contains(node.Id);
            string state = learned ? "ИЗУЧЕНО" : !known ? "ЗАКРЫТО" : progress.CanLearn(node.Id, _campaign.UnlockedDevelopment) == DevelopmentResult.Success ? "ДОСТУПНО" : "ОТКРЫТО";
            int x = 7 + i / 3 * 36, y = 15 + i % 3 * 6, center = x + 13;
            Color color = i == _selection ? Gold : learned ? Accent : known ? Ink : Muted;
            if (i % 3 == 0)
            {
                Put(lines, x, 13, node.Branch.ToUpperInvariant().PadLeft((27 + node.Branch.Length) / 2), Gold, 27);
                Put(lines, center, 14, "v", Accent, 1);
            }
            else
            {
                var parent = DevelopmentTree.Find(node.Parent!);
                Color link = progress.LearnedNodes.Contains(parent.Id) ? Accent : Muted;
                Put(lines, center, y - 3, "|", link, 1);
                Put(lines, center, y - 2, "|", link, 1);
                Put(lines, center, y - 1, "v", link, 1);
            }
            Put(lines, x, y, ("+-- " + state + " ").PadRight(26, '-') + "+", color, 27);
            string name = node.Name.PadLeft((23 + node.Name.Length) / 2).PadRight(23);
            Put(lines, x, y + 1, "|" + (i == _selection ? ">" : " ") + name + " |", color, 27);
            Put(lines, x, y + 2, "+" + new string('-', 12) + (i % 3 < 2 ? "+" : "-") + new string('-', 12) + "+", color, 27);
        }
        var chosen = DevelopmentTree.Nodes[_selection];
        Box(lines, 3, 32, 106, 6, "ВЫБРАННЫЙ НАВЫК / 1 ОЧКО");
        Put(lines, 5, 33, chosen.Name + " — " + chosen.Description, Gold, 102);
        Put(lines, 5, 34, chosen.Parent is null ? "Начальный узел: не требует других навыков."
            : "Требуется изучить: " + DevelopmentTree.Find(chosen.Parent).Name, Ink, 102);
        Put(lines, 5, 35, chosen.RequiresOakVictory ? "Открытие: победить дуба и вернуться через портал. Остаётся после смерти героя."
            : "Открыт с начала прохождения. Вложенное очко теряется вместе с героем.", Muted, 102);
        Put(lines, 5, 36, _status, Gold, 102);
        Put(lines, 3, 39, "[W/S] вдоль ветви  [A/D] другая ветвь  [ENTER/E] изучить  [P/ESC] на базу", Accent);
        Put(lines, 3, 41, "Уровень не лечит. Карты навыков добавляются в следующем бою.", Muted);
        return lines;
    }
}
