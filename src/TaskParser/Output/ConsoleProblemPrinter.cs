using TaskParser.Model;
using TaskParser.Util;

namespace TaskParser.Output;

/// <summary>Текстовый вывод для консоли. Поля, которых в формате нет (null), просто не печатаются.</summary>
public sealed class ConsoleProblemPrinter : IProblemPrinter
{
    private const int LabelWidth = 24;

    public void Print(ProblemInfo info, TextWriter output, PrintOptions options)
    {
        output.WriteLine("=== Информация о задаче ===");
        Row(output, "Формат пакета", info.Format);
        Row(output, "Название", info.Name);
        Row(output, "Лимит времени", FormatTime(info.TimeLimitMillis));
        Row(output, "Лимит памяти", FormatMemory(info.MemoryLimitBytes));
        Row(output, "Файл ввода", info.InputFile);
        Row(output, "Файл вывода", info.OutputFile);
        Row(output, "Проверяющая программа", info.Checker);
        Row(output, "Интерактор", info.Interactor);
        if (info.Statements.Count > 0) Row(output, "Условие", string.Join(", ", info.Statements));
        Row(output, "Количество тестов", info.Tests.Count.ToString());
        Row(output, "Сумма баллов", TotalPoints(info));
        if (info.Groups.Count > 0) Row(output, "Групп тестов", info.Groups.Count.ToString());
        if (info.AdditionalTestSets.Count > 0) Row(output, "Других наборов тестов", info.AdditionalTestSets.Count.ToString());

        if (info.Groups.Count > 0) PrintGroups(info.Groups, output);
        if (info.Details.Count > 0)
        {
            output.WriteLine();
            output.WriteLine("Дополнительно:");
            foreach (var d in info.Details) output.WriteLine("  " + d.Key + ": " + d.Value);
        }
        foreach (InfoSection section in info.Sections.Where(x => !x.IsText)) PrintSection(section, output);
        foreach (TestSet set in info.AdditionalTestSets) PrintTestSet(set, output, options);
        if (info.Warnings.Count > 0)
        {
            output.WriteLine();
            output.WriteLine("Предупреждения:");
            foreach (string w in info.Warnings) output.WriteLine("  ! " + w);
        }
        if (options.ShowTests && info.Tests.Count > 0)
        {
            output.WriteLine();
            output.WriteLine("Тесты:");
            PrintTests(info.Tests, info.Groups, output);
        }
        if (options.ShowText)
        {
            foreach (InfoSection section in info.Sections.Where(x => x.IsText)) PrintSection(section, output);
        }
    }

    private static void PrintSection(InfoSection section, TextWriter output)
    {
        output.WriteLine();
        output.WriteLine(section.Title + ":");
        foreach (string line in section.Lines) output.WriteLine("  " + line);
    }

    private static void PrintTestSet(TestSet set, TextWriter output, PrintOptions options)
    {
        output.WriteLine();
        output.WriteLine($"Набор тестов «{set.Name}» (не основной):");
        Row(output, "  Лимит времени", FormatTime(set.TimeLimitMillis));
        Row(output, "  Лимит памяти", FormatMemory(set.MemoryLimitBytes));
        Row(output, "  Файл ввода", set.InputFile);
        Row(output, "  Файл вывода", set.OutputFile);
        Row(output, "  Количество тестов", set.Tests.Count.ToString());
        foreach (string note in set.Notes) output.WriteLine("  " + note);
        if (set.Groups.Count > 0) PrintGroups(set.Groups, output);
        if (options.ShowTests && set.Tests.Count > 0)
        {
            output.WriteLine();
            output.WriteLine($"Тесты набора «{set.Name}»:");
            PrintTests(set.Tests, set.Groups, output);
        }
    }

    private static void PrintGroups(IReadOnlyList<TestGroup> groups, TextWriter output)
    {
        output.WriteLine();
        output.WriteLine("Группы тестов:");
        foreach (TestGroup g in groups)
        {
            var parts = new List<string>
            {
                "[" + g.Id + "]" + (g.Name == null ? "" : " " + g.Name),
                "баллы: " + (g.Points?.ToString() ?? "-"),
                "зависит от: " + (g.Prerequisites.Count == 0 ? "-" : string.Join(", ", g.Prerequisites)),
                "тесты: " + IntRanges.Format(g.Tests) + " (" + g.Tests.Count + ")",
            };
            if (g.Note != null) parts.Add(g.Note);
            output.WriteLine("  " + string.Join(" | ", parts));
        }
    }

    private static void PrintTests(IReadOnlyList<TestCase> tests, IReadOnlyList<TestGroup> groups, TextWriter output)
    {
        var groupsOfTest = new Dictionary<int, List<string>>();
        foreach (TestGroup g in groups)
        {
            foreach (int t in g.Tests)
            {
                if (!groupsOfTest.TryGetValue(t, out var list)) groupsOfTest[t] = list = new List<string>();
                list.Add(g.Id);
            }
        }
        bool hasComments = tests.Any(t => t.Comment != null);
        var rows = new List<string[]> { new[] { "№", "вход", "ответ", "баллы", "группы", "примечание" } };
        foreach (TestCase t in tests)
        {
            rows.Add(new[]
            {
                t.Index.ToString(), OrDash(t.Input), OrDash(t.Output), t.Points?.ToString() ?? "-",
                groupsOfTest.TryGetValue(t.Index, out var gs) ? string.Join(", ", gs) : "-",
                t.Comment ?? "",
            });
        }
        var width = new int[5];
        foreach (string[] r in rows)
        {
            for (int i = 0; i < 5; i++) width[i] = Math.Max(width[i], r[i].Length);
        }
        foreach (string[] r in rows)
        {
            string line = "  " + Pad(r[0], width[0], true) + "  " + Pad(r[1], width[1], false) + "  " + Pad(r[2], width[2], false)
                          + "  " + Pad(r[3], width[3], true) + "  " + (hasComments ? Pad(r[4], width[4], false) + "  " + r[5] : r[4]);
            output.WriteLine(line.TrimEnd());
        }
    }

    /// <summary>Если у групп есть баллы - суммирует их, иначе суммирует баллы отдельных тестов; null, если баллов нет вовсе.</summary>
    private static string? TotalPoints(ProblemInfo info)
    {
        bool anyGroupPoints = false;
        long sum = 0;
        foreach (TestGroup g in info.Groups)
        {
            if (g.Points != null)
            {
                anyGroupPoints = true;
                sum += g.Points.Value;
            }
        }
        if (anyGroupPoints) return sum.ToString();
        bool anyTestPoints = false;
        foreach (TestCase t in info.Tests)
        {
            if (t.Points != null)
            {
                anyTestPoints = true;
                sum += t.Points.Value;
            }
        }
        return anyTestPoints ? sum.ToString() : null;
    }

    internal static string? FormatTime(long? millis)
    {
        if (millis == null) return null;
        return millis % Values.Second == 0 ? millis / Values.Second + " с" : millis + " мс";
    }

    internal static string? FormatMemory(long? bytes)
    {
        if (bytes == null) return null;
        if (bytes % Values.MB == 0) return bytes / Values.MB + " МБ";
        if (bytes % Values.KB == 0) return bytes / Values.KB + " КБ";
        return bytes + " Б";
    }

    private static void Row(TextWriter output, string label, string? value)
    {
        if (value == null) return;
        output.WriteLine(Pad(label + ":", LabelWidth, false) + " " + value);
    }

    private static string OrDash(string? s) => s ?? "-";

    private static string Pad(string s, int width, bool right) => right ? s.PadLeft(width) : s.PadRight(width);
}
