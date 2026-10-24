using System.Xml.Linq;
using TaskParser.Model;
using TaskParser.Pkg;
using TaskParser.Util;

namespace TaskParser.Parsers.Cats;

/// <summary>
/// Формат CATS: в корне ZIP-архива ровно один XML-файл вида &lt;CATS&gt;&lt;Problem ...&gt;.
/// Время в атрибуте tlimit - секунды, память в mlimit - мегабайты (если нет суффикса B/K/M).
/// Наборы тестов (&lt;Testset&gt;) отображаются на группы тестов.
/// </summary>
public sealed class CatsParser : IProblemParser
{
    public string FormatName => "CATS";

    public ProblemInfo Parse(IPackageSource source)
    {
        string file = FindDescriptor(source);
        XElement root;
        using (Stream input = source.Open(file)) root = XmlUtil.Parse(input, file);
        if (root.Name.LocalName != "CATS")
        {
            throw new ParseException($"CATS: корневой элемент {file} должен называться <CATS>, а не <{root.Name.LocalName}>");
        }
        XElement problem = XmlUtil.Child(root, "Problem")
                           ?? throw new ParseException($"CATS: в файле {file} нет элемента <Problem>");

        var b = ProblemInfo.CreateBuilder(FormatName);
        ReadGeneral(problem, root, b);
        ReadTestsAndGroups(problem, b);
        return b.Build();
    }

    /// <summary>По описанию формата в корне должен лежать ровно один XML-файл.</summary>
    private static string FindDescriptor(IPackageSource source)
    {
        List<string> xml = source.Files
            .Where(f => f.IndexOf('/') < 0 && f.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (xml.Count == 0) throw new ParseException($"CATS: в корне пакета {source.Name} нет XML-файла с описанием задачи");
        if (xml.Count > 1)
        {
            throw new ParseException($"CATS: в корне пакета должен быть ровно один XML-файл, а найдено {xml.Count}: [{string.Join(", ", xml)}]");
        }
        return xml[0];
    }

    private static void ReadGeneral(XElement problem, XElement root, ProblemInfo.Builder b)
    {
        b.Name(XmlUtil.Attr(problem, "title"));
        b.TimeLimitMillis(Values.DurationMillis(XmlUtil.Attr(problem, "tlimit"), Values.Second, "tlimit"));
        b.MemoryLimitBytes(Values.MemoryBytes(XmlUtil.Attr(problem, "mlimit"), Values.MB, "mlimit"));
        b.InputFile(StdName(XmlUtil.Attr(problem, "inputFile"), ProblemInfo.Stdin));
        b.OutputFile(StdName(XmlUtil.Attr(problem, "outputFile"), ProblemInfo.Stdout));

        // Проверяющая программа: stdChecker главнее <Checker>; интерактор - отдельный элемент.
        string? stdChecker = XmlUtil.Attr(problem, "stdChecker");
        XElement? checker = XmlUtil.Child(problem, "Checker");
        if (stdChecker != null)
        {
            b.Checker("стандартная: " + stdChecker);
        }
        else if (checker != null)
        {
            string? style = XmlUtil.Attr(checker, "style");
            b.Checker((XmlUtil.Attr(checker, "src") ?? "?") + (style == null ? "" : $" (style={style})"));
        }
        XElement? interactor = XmlUtil.Child(problem, "Interactor");
        if (interactor != null) b.Interactor(XmlUtil.Attr(interactor, "src"));

        ReadStatement(problem, b);

        b.Detail("Версия формата CATS", XmlUtil.Attr(root, "version") ?? "1.4");
        b.Detail("Автор", XmlUtil.Attr(problem, "author"));
        b.Detail("Язык условия", XmlUtil.Attr(problem, "lang"));
        b.Detail("Сложность", XmlUtil.Attr(problem, "difficulty"));
        b.Detail("Максимальный балл", XmlUtil.Attr(problem, "maxPoints"));
        b.Detail("Лимит записи", XmlUtil.Attr(problem, "wlimit"));
        b.Detail("Сохранять начало входа теста", XmlUtil.Attr(problem, "saveInputPrefix"));
        b.Detail("Сохранять начало вывода попытки", XmlUtil.Attr(problem, "saveOutputPrefix"));
        b.Detail("Сохранять начало ответа теста", XmlUtil.Attr(problem, "saveAnswerPrefix"));
        b.Detail("Ключевые слова", Names(problem, "Keyword", "code"));
        XElement? run = XmlUtil.Child(problem, "Run");
        if (run != null) b.Detail("Способ запуска", XmlUtil.Attr(run, "method"));
        b.Detail("Примеры в условии", CountSamples(problem));

        // Все программы и файлы со всеми атрибутами (имя, исходник, среда, лимиты, export и т.д.).
        foreach ((string tag, string title) in Objects)
        {
            b.Section(title, XmlUtil.Children(problem, tag).Select(e => DescribeObject(e)));
        }
        ReadSamples(problem, b);
        ReadTexts(problem, b);
    }

    /// <summary>Теги с описанием файлов и программ и заголовки блоков вывода.</summary>
    private static readonly (string Tag, string Title)[] Objects =
    {
        ("Checker", "Проверяющая программа (Checker)"),
        ("Interactor", "Интерактор (Interactor)"),
        ("Solution", "Эталонные решения (Solution)"),
        ("Generator", "Генераторы (Generator)"),
        ("GeneratorRange", "Диапазоны генераторов (GeneratorRange)"),
        ("Validator", "Валидаторы (Validator)"),
        ("Visualizer", "Визуализаторы (Visualizer)"),
        ("Linter", "Линтеры (Linter)"),
        ("Module", "Модули (Module)"),
        ("Import", "Импорт (Import)"),
        ("Picture", "Картинки (Picture)"),
        ("Attachment", "Вложения (Attachment)"),
    };

    private static string DescribeObject(XElement e)
    {
        string? name = XmlUtil.Attr(e, "name") ?? XmlUtil.Attr(e, "guid");
        string? rest = AttrText.Describe(e, "name");
        return (name ?? "-") + (rest == null ? "" : ": " + rest);
    }

    private static void ReadSamples(XElement problem, ProblemInfo.Builder b)
    {
        var lines = new List<string>();
        int number = 0;
        foreach (XElement s in XmlUtil.Children(problem, "Sample"))
        {
            number++;
            string rank = XmlUtil.Attr(s, "rank") ?? number.ToString();
            ExpandedSample(lines, "вход", XmlUtil.Child(s, "SampleIn"), rank);
            ExpandedSample(lines, "выход", XmlUtil.Child(s, "SampleOut"), rank);
        }
        b.Section("Примеры (Sample)", lines);
    }

    private static void ExpandedSample(List<string> lines, string kind, XElement? element, string rank)
    {
        if (element == null) return;
        string? src = XmlUtil.Attr(element, "src");
        if (src != null)
        {
            lines.Add($"пример {rank}, {kind}: файл {src}");
            return;
        }
        string text = element.Value.Trim('\r', '\n');
        lines.Add($"пример {rank}, {kind}:");
        foreach (string l in text.Split('\n')) lines.Add("    " + l.TrimEnd('\r'));
    }

    /// <summary>Тексты условия, форматов и ограничений - простым текстом.</summary>
    private static void ReadTexts(XElement problem, ProblemInfo.Builder b)
    {
        foreach (string tag in new[] { "ProblemStatement", "InputFormat", "OutputFormat", "ProblemConstraints", "Explanation", "JsonData" })
        {
            XElement? e = XmlUtil.Child(problem, tag);
            if (e == null) continue;
            var lines = new List<string>();
            foreach (string link in new[] { "attachment", "url" })
            {
                if (XmlUtil.Attr(e, link) is { } v) lines.Add($"{link}: {v}");
            }
            lines.AddRange(AttrText.PlainText(e));
            b.Section("Текст: " + tag, lines, true);
        }
    }

    private static void ReadStatement(XElement problem, ProblemInfo.Builder b)
    {
        XElement? statement = XmlUtil.Child(problem, "ProblemStatement");
        if (statement == null) return;
        string? attachment = XmlUtil.Attr(statement, "attachment");
        string? url = XmlUtil.Attr(statement, "url");
        if (attachment != null)
        {
            string? src = null;
            foreach (XElement a in XmlUtil.Children(problem, "Attachment"))
            {
                if (attachment == XmlUtil.Attr(a, "name")) src = XmlUtil.Attr(a, "src");
            }
            b.Statement(src ?? "вложение " + attachment);
        }
        else if (url != null)
        {
            b.Statement(url);
        }
        else
        {
            b.Statement("встроено в XML (<ProblemStatement>)");
        }
        var sections = new List<string>();
        foreach (string s in new[] { "ProblemStatement", "InputFormat", "OutputFormat", "ProblemConstraints", "Explanation" })
        {
            if (XmlUtil.Child(problem, s) != null) sections.Add(s);
        }
        b.Detail("Разделы условия", string.Join(", ", sections));
    }

    // ---------------------------------------------------------------- тесты и наборы тестов

    /// <summary>Что известно о тесте на момент разбора; поля заполняются из разных тегов.</summary>
    private sealed class TestDraft
    {
        public int? Points;
        public string? Input;
        public string? Output;
        public string? Comment;
    }

    /// <summary>Правило из одного тега &lt;Test&gt; или &lt;TestRange&gt;.</summary>
    private sealed record Rule(int? Points, XElement? In, XElement? Out);

    private static void ReadTestsAndGroups(XElement problem, ProblemInfo.Builder b)
    {
        var resolver = new TestsetResolver(problem);

        var drafts = new SortedDictionary<int, TestDraft>();
        var defaults = new List<Rule>();
        foreach (XElement e in XmlUtil.Children(problem))
        {
            string tag = e.Name.LocalName;
            if (tag != "Test" && tag != "TestRange") continue;
            var rule = new Rule(Values.Integer(XmlUtil.Attr(e, "points"), "points"), XmlUtil.Child(e, "In"), XmlUtil.Child(e, "Out"));
            if (tag == "TestRange")
            {
                Apply(drafts, Ranks(e), rule);
                continue;
            }
            string rank = XmlUtil.Attr(e, "rank") ?? throw new ParseException("CATS: у элемента <Test> не указан атрибут rank");
            if (rank == "*") defaults.Add(rule);        // значения по умолчанию для всех тестов
            else Apply(drafts, RankSpec.Expand(rank, resolver.Resolve), rule);
        }
        foreach (Rule rule in defaults)
        {
            foreach (var t in drafts) Fill(t.Value, t.Key, rule, false);
        }

        foreach (var t in drafts)
        {
            b.Test(new TestCase(t.Key, t.Value.Input, t.Value.Output, t.Value.Points, t.Value.Comment));
        }
        if (drafts.Count == 0)
        {
            b.Warning("В описании нет ни одного теста (<Test>/<TestRange>): по правилам CATS принимается любое компилируемое решение.");
        }

        foreach (XElement ts in XmlUtil.Children(problem, "Testset"))
        {
            string name = XmlUtil.Attr(ts, "name")!;
            var notes = new List<string>();
            string? spec = XmlUtil.Attr(ts, "tests");
            if (spec != null) notes.Add("tests=" + spec);
            if (XmlUtil.Attr(ts, "hideDetails") == "1") notes.Add("результаты отдельных тестов скрыты");
            b.Group(new TestGroup(name, XmlUtil.Attr(ts, "comment"),
                Values.Integer(XmlUtil.Attr(ts, "points"), $"points набора {name}"),
                Values.SplitList(XmlUtil.Attr(ts, "depends_on"), ","),
                resolver.Resolve(name), notes.Count == 0 ? null : string.Join("; ", notes)));
        }
    }

    private static List<int> Ranks(XElement testRange)
    {
        int? from = Values.Integer(XmlUtil.Attr(testRange, "from"), "from");
        int? to = Values.Integer(XmlUtil.Attr(testRange, "to"), "to");
        if (from == null || to == null) throw new ParseException("CATS: у <TestRange> должны быть атрибуты from и to");
        return RankSpec.Expand($"{from}-{to}", _ => throw new ParseException($"CATS: некорректный диапазон {from}-{to}"));
    }

    private static void Apply(SortedDictionary<int, TestDraft> drafts, List<int> indices, Rule rule)
    {
        foreach (int index in indices)
        {
            if (!drafts.TryGetValue(index, out TestDraft? draft)) drafts[index] = draft = new TestDraft();
            Fill(draft, index, rule, true);
        }
    }

    /// <summary>overrideValues=false используется для значений по умолчанию (rank="*"): они не затирают уже известное.</summary>
    private static void Fill(TestDraft d, int index, Rule rule, bool overrideValues)
    {
        if (rule.Points != null && (overrideValues || d.Points == null)) d.Points = rule.Points;
        string? input = DescribeInput(rule.In, index);
        if (input != null && (overrideValues || d.Input == null)) d.Input = input;
        string? output = DescribeOutput(rule.Out, index);
        if (output != null && (overrideValues || d.Output == null)) d.Output = output;
        string? comment = AttrText.Join("; ", AttrText.Describe(rule.In, "src", "use", "param"), AttrText.Describe(rule.Out, "src", "use"));
        if (comment != null && (overrideValues || d.Comment == null)) d.Comment = comment;
    }

    private static string? DescribeInput(XElement? element, int index)
    {
        if (element == null) return null;
        string? src = XmlUtil.Attr(element, "src");
        if (src != null) return IndexPatterns.CatsVariables(src, index);
        string? use = XmlUtil.Attr(element, "use");
        if (use != null)
        {
            string? param = XmlUtil.Attr(element, "param");
            return "генератор " + use + (param == null ? "" : " " + IndexPatterns.CatsVariables(param, index));
        }
        return Inline(element);
    }

    /// <summary>Текст теста, записанный прямо в XML: показываем сам текст (в одну строку), а не только факт его наличия.</summary>
    private static string? Inline(XElement element)
    {
        string? text = XmlUtil.Text(element);
        if (text == null) return null;
        string flat = string.Join(" ⏎ ", text.Split('\n').Select(l => l.Trim('\r')));
        return "встроен: " + (flat.Length > 80 ? flat.Substring(0, 80) + "…" : flat);
    }

    private static string? DescribeOutput(XElement? element, int index)
    {
        if (element == null) return null;
        string? src = XmlUtil.Attr(element, "src");
        if (src != null) return IndexPatterns.CatsVariables(src, index);
        string? use = XmlUtil.Attr(element, "use");
        if (use != null) return "решение " + use;
        return Inline(element);
    }

    /// <summary>Раскрывает имена наборов тестов (&lt;Testset&gt;), которые могут ссылаться друг на друга.</summary>
    private sealed class TestsetResolver
    {
        private readonly Dictionary<string, string> specs = new();
        private readonly Dictionary<string, List<int>> cache = new();
        private readonly HashSet<string> inProgress = new();

        public TestsetResolver(XElement problem)
        {
            foreach (XElement ts in XmlUtil.Children(problem, "Testset"))
            {
                string name = XmlUtil.Attr(ts, "name") ?? throw new ParseException("CATS: у элемента <Testset> не указан атрибут name");
                if (!specs.TryAdd(name, XmlUtil.Attr(ts, "tests") ?? ""))
                {
                    throw new ParseException($"CATS: набор тестов '{name}' описан дважды");
                }
            }
        }

        public List<int> Resolve(string name)
        {
            if (cache.TryGetValue(name, out List<int>? known)) return known;
            if (!specs.TryGetValue(name, out string? spec))
            {
                throw new ParseException($"CATS: '{name}' - не номер теста и не имя набора тестов");
            }
            if (!inProgress.Add(name)) throw new ParseException($"CATS: наборы тестов ссылаются друг на друга по кругу ({name})");
            List<int> result = RankSpec.Expand(spec, Resolve);
            inProgress.Remove(name);
            cache[name] = result;
            return result;
        }
    }

    // ---------------------------------------------------------------- мелкие помощники

    private static string? StdName(string? value, string standard) => value switch
    {
        null => null,
        "*STDIN" or "*STDOUT" => standard,
        "*NONE" => "нет",
        _ => value,
    };

    private static string Names(XElement problem, string tag, string attribute) =>
        string.Join(", ", XmlUtil.Children(problem, tag).Select(e => XmlUtil.Attr(e, attribute)).Where(v => v != null));

    private static string? CountSamples(XElement problem)
    {
        var ranks = new HashSet<int>();
        int withoutRank = 0;
        foreach (XElement s in XmlUtil.Children(problem, "Sample"))
        {
            string? rank = XmlUtil.Attr(s, "rank");
            if (rank == null) withoutRank++;
            else ranks.UnionWith(RankSpec.Expand(rank, name => throw new ParseException($"CATS: некорректный rank примера '{name}'")));
        }
        int total = ranks.Count + withoutRank;
        return total == 0 ? null : total.ToString();
    }
}
