using System.Xml.Linq;
using TaskParser.Model;
using TaskParser.Pkg;
using TaskParser.Util;

namespace TaskParser.Parsers.Pcms2;

/// <summary>
/// Вариант problem.xml, выгруженный из Polygon: &lt;judging&gt;&lt;testset&gt;&lt;tests&gt;&lt;test group=.. method=..&gt;.
/// Время в файле - миллисекунды, память - байты. Если наборов тестов несколько, основным считается самый большой,
/// остальные разбираются полностью и попадают в AdditionalTestSets.
/// </summary>
internal sealed class PolygonVariant : IPcms2Variant
{
    public string Name => "Polygon (judging/testset)";

    public bool Supports(XElement root)
    {
        XElement? judging = XmlUtil.Child(root, "judging");
        return judging != null && XmlUtil.Child(judging, "testset") != null;
    }

    public ProblemInfo Parse(XElement root) => Parse(root, null);

    public ProblemInfo Parse(XElement root, IPackageSource? source)
    {
        var b = ProblemInfo.CreateBuilder(Pcms2Parser.Format);
        b.Detail("Вариант problem.xml", Name);
        b.Detail("Короткое имя", XmlUtil.Attr(root, "short-name"));
        b.Detail("Ревизия", XmlUtil.Attr(root, "revision"));
        b.Detail("Адрес в Polygon", XmlUtil.Attr(root, "url"));

        List<XElement> names = Nested(root, "names", "name");
        if (names.Count > 0) b.Name(XmlUtil.Attr(names[0], "value"));
        if (names.Count > 1) b.Section("Названия на языках", names.Select(n => $"{XmlUtil.Attr(n, "language")}: {XmlUtil.Attr(n, "value")}"));
        foreach (XElement s in Nested(root, "statements", "statement")) b.Statement(XmlUtil.Attr(s, "path"));
        b.Section("Файлы условия (statements)", Nested(root, "statements", "statement").Select(PathWithAttrs));
        b.Section("Разбор решения (tutorials)", Nested(root, "tutorials", "tutorial").Select(PathWithAttrs));
        b.Section("Документы (documents)", Nested(root, "documents", "document").Select(PathWithAttrs));

        XElement judging = XmlUtil.Child(root, "judging")!;
        b.InputFile(XmlUtil.Attr(judging, "input-file") ?? ProblemInfo.Stdin);
        b.OutputFile(XmlUtil.Attr(judging, "output-file") ?? ProblemInfo.Stdout);
        b.Detail("Процессор (cpu-name)", XmlUtil.Attr(judging, "cpu-name"));
        b.Detail("Частота процессора (cpu-speed)", XmlUtil.Attr(judging, "cpu-speed"));
        string? judgingRest = AttrText.Describe(judging, "input-file", "output-file", "cpu-name", "cpu-speed");
        b.Detail("Прочие параметры judging", judgingRest);

        List<XElement> testsets = XmlUtil.Children(judging, "testset");
        XElement main = ChooseMain(testsets);
        b.Detail("Разобранный набор тестов", XmlUtil.Attr(main, "name"));
        b.Detail("Наборы тестов в файле", string.Join(", ", testsets.Select(t => $"{XmlUtil.Attr(t, "name")} ({Nested(t, "tests", "test").Count})")));

        var mainWarnings = new List<string>();
        TestSet mainSet = ReadTestset(main, mainWarnings, out string mainStats);
        b.TimeLimitMillis(mainSet.TimeLimitMillis);
        b.MemoryLimitBytes(mainSet.MemoryLimitBytes);
        foreach (TestCase t in mainSet.Tests) b.Test(t);
        foreach (TestGroup g in mainSet.Groups) b.Group(g);
        foreach (string w in mainWarnings) b.Warning(w);
        b.Detail("Тесты: вручную / генератором", mainStats);
        b.Detail("Тестов-примеров (sample)", CountSamples(main));
        if (mainSet.Notes.Count > 0) b.Detail("Параметры основного набора", string.Join("; ", mainSet.Notes));

        foreach (XElement ts in testsets)
        {
            if (ts == main) continue;
            var warnings = new List<string>();
            TestSet set = ReadTestset(ts, warnings, out _);
            b.AdditionalTestSet(new TestSet(set.Name, set.InputFile ?? null, set.OutputFile, set.TimeLimitMillis,
                set.MemoryLimitBytes, set.Tests, set.Groups, set.Notes.Concat(warnings.Select(w => "! " + w))));
        }

        ReadFiles(root, b);
        ReadAssets(root, b);
        b.Section("Свойства (properties)", Nested(root, "properties", "property").Select(p => $"{XmlUtil.Attr(p, "name")} = {XmlUtil.Attr(p, "value")}"));
        ReadStresses(root, b);
        b.Detail("Теги", JoinAttr(Nested(root, "tags", "tag"), "value"));
        ReportUnknown(root, b);
        if (source != null)
        {
            PackageTexts.Add(b, source, "Текст условия", Nested(root, "statements", "statement").Select(x => XmlUtil.Attr(x, "path")));
            PackageTexts.Add(b, source, "Текст разбора", Nested(root, "tutorials", "tutorial").Select(x => XmlUtil.Attr(x, "path")));
            PackageTexts.Add(b, source, "Документ", Nested(root, "documents", "document").Select(x => XmlUtil.Attr(x, "path")));
        }
        return b.Build();
    }

    private static readonly HashSet<string> KnownRootElements = new()
    {
        "names", "statements", "tutorials", "judging", "files", "assets", "properties", "stresses", "documents", "tags",
    };

    private static readonly HashSet<string> KnownRootAttributes = new() { "short-name", "revision", "url" };

    /// <summary>Корневые атрибуты и элементы, которых нет в описании формата, не теряются.</summary>
    private static void ReportUnknown(XElement root, ProblemInfo.Builder b)
    {
        b.Detail("Прочие атрибуты problem", string.Join(", ",
            root.Attributes().Where(a => !KnownRootAttributes.Contains(a.Name.LocalName) && !a.IsNamespaceDeclaration)
                .Select(a => $"{a.Name.LocalName}={a.Value}")));
        b.Detail("Прочие элементы problem", string.Join("; ",
            XmlUtil.Children(root).Where(e => !KnownRootElements.Contains(e.Name.LocalName)).Select(e =>
            {
                string attrs = string.Join(", ", e.Attributes().Select(a => $"{a.Name.LocalName}={a.Value}"));
                return e.Name.LocalName + (attrs.Length > 0 ? " [" + attrs + "]" : "") + (XmlUtil.Text(e) is { } t ? " = " + t : "");
            })));
    }

    private static XElement ChooseMain(List<XElement> testsets)
    {
        XElement? main = null;
        int best = -1;
        foreach (XElement ts in testsets)
        {
            int count = Nested(ts, "tests", "test").Count;
            if (count > best)
            {
                best = count;
                main = ts;
            }
        }
        return main!;
    }

    private static TestSet ReadTestset(XElement testset, List<string> warnings, out string stats)
    {
        string? inputPattern = XmlUtil.ChildText(testset, "input-path-pattern");
        string? answerPattern = XmlUtil.ChildText(testset, "answer-path-pattern");
        var groupDefs = ReadGroupDefinitions(testset);

        var tests = new List<TestCase>();
        var groupOrder = new List<string>();
        var byGroup = new Dictionary<string, List<int>>();
        var groupPoints = new Dictionary<string, int>();
        int index = 0;
        int manual = 0;
        int generated = 0;
        foreach (XElement t in Nested(testset, "tests", "test"))
        {
            index++;
            int? points = Util.Values.Integer(XmlUtil.Attr(t, "points"), $"points теста №{index}");
            tests.Add(new TestCase(index, IndexPatterns.Printf(inputPattern, index), IndexPatterns.Printf(answerPattern, index),
                points, AttrText.Describe(t, "group", "points")));
            string? group = XmlUtil.Attr(t, "group");
            if (group != null)
            {
                if (!byGroup.TryGetValue(group, out List<int>? members))
                {
                    byGroup[group] = members = new List<int>();
                    groupOrder.Add(group);
                }
                members.Add(index);
                if (points != null) groupPoints[group] = groupPoints.GetValueOrDefault(group) + points.Value;
            }
            string? method = XmlUtil.Attr(t, "method");
            if (method == "manual") manual++;
            if (method == "generated") generated++;
        }
        // Группы, описанные в <groups>, но без тестов, тоже показываем.
        foreach (string g in groupDefs.Keys)
        {
            if (!byGroup.ContainsKey(g)) { byGroup[g] = new List<int>(); groupOrder.Add(g); }
        }
        var groups = new List<TestGroup>();
        foreach (string g in groupOrder)
        {
            groupDefs.TryGetValue(g, out GroupDef? def);
            int? points = def?.Points ?? (groupPoints.TryGetValue(g, out int p) ? p : null);
            groups.Add(new TestGroup(g, null, points, def?.Dependencies ?? new List<string>(), byGroup[g], def?.Note));
        }

        int? declared = Util.Values.Integer(XmlUtil.ChildText(testset, "test-count"), "test-count");
        if (declared != null && declared != index)
        {
            warnings.Add($"В <test-count> указано тестов: {declared}, а описано в <tests>: {index}.");
        }
        stats = $"{manual} / {generated}";

        var notes = new List<string>();
        string? inPat = XmlUtil.ChildText(testset, "input-path-pattern");
        if (inPat != null) notes.Add("шаблон входных файлов: " + inPat);
        string? ansPat = XmlUtil.ChildText(testset, "answer-path-pattern");
        if (ansPat != null) notes.Add("шаблон ответов: " + ansPat);
        if (declared != null) notes.Add("test-count: " + declared);
        foreach (XElement e in XmlUtil.Children(testset))
        {
            string tag = e.Name.LocalName;
            if (tag is "time-limit" or "memory-limit" or "test-count" or "input-path-pattern" or "answer-path-pattern" or "tests" or "groups") continue;
            notes.Add($"{tag}: {XmlUtil.Text(e) ?? AttrText.Describe(e) ?? ""}".TrimEnd(' ', ':'));
        }
        string? setAttrs = AttrText.Describe(testset, "name");
        if (setAttrs != null) notes.Add(setAttrs);
        return new TestSet(XmlUtil.Attr(testset, "name") ?? "(без имени)", null, null,
            Util.Values.DurationMillis(XmlUtil.ChildText(testset, "time-limit"), Util.Values.Millisecond, "time-limit"),
            Util.Values.MemoryBytes(XmlUtil.ChildText(testset, "memory-limit"), Util.Values.Bytes, "memory-limit"),
            tests, groups, notes);
    }

    private sealed record GroupDef(int? Points, List<string> Dependencies, string? Note);

    /// <summary>Необязательный элемент &lt;groups&gt; с баллами, политиками и зависимостями (новые выгрузки Polygon).</summary>
    private static Dictionary<string, GroupDef> ReadGroupDefinitions(XElement testset)
    {
        var result = new Dictionary<string, GroupDef>();
        foreach (XElement g in Nested(testset, "groups", "group"))
        {
            string? name = XmlUtil.Attr(g, "name");
            if (name == null) continue;
            var deps = Nested(g, "dependencies", "dependency").Select(d => XmlUtil.Attr(d, "group")).Where(d => d != null).Select(d => d!).ToList();
            result[name] = new GroupDef(Util.Values.Integer(XmlUtil.Attr(g, "points"), $"points группы {name}"), deps,
                AttrText.Describe(g, "name", "points"));
        }
        return result;
    }

    private static string? CountSamples(XElement testset)
    {
        int samples = Nested(testset, "tests", "test").Count(t => XmlUtil.Attr(t, "sample") == "true");
        return samples == 0 ? null : samples.ToString();
    }

    private static void ReadFiles(XElement root, ProblemInfo.Builder b)
    {
        XElement? files = XmlUtil.Child(root, "files");
        if (files == null) return;
        b.Section("Ресурсы (files/resources)", Nested(files, "resources", "file").Select(PathWithAttrs));
        b.Section("Исполняемые файлы (files/executables)", Nested(files, "executables", "executable").Select(SourceAndBinary));
    }

    private static void ReadAssets(XElement root, ProblemInfo.Builder b)
    {
        XElement? assets = XmlUtil.Child(root, "assets");
        if (assets == null) return;
        XElement? checker = XmlUtil.Child(assets, "checker");
        b.Checker(Executable(checker));
        b.Interactor(Executable(XmlUtil.Child(assets, "interactor")));
        if (checker != null)
        {
            var lines = new List<string>();
            lines.Add("имя и тип: " + (AttrText.Describe(checker) ?? "-"));
            lines.Add("исходник и бинарник: " + SourceAndBinary(checker));
            foreach (XElement c in XmlUtil.Children(checker, "copy")) lines.Add("копия: " + XmlUtil.Attr(c, "path"));
            XElement? ts = XmlUtil.Child(checker, "testset");
            if (ts != null) lines.Add("тесты проверяющей программы: " + TestsetSummary(ts));
            b.Section("Проверяющая программа (assets/checker)", lines);
        }
        var validators = new List<string>();
        foreach (XElement v in Nested(assets, "validators", "validator"))
        {
            XElement? ts = XmlUtil.Child(v, "testset");
            validators.Add(SourceAndBinary(v) + (ts == null ? "" : "; тесты: " + TestsetSummary(ts)));
        }
        b.Section("Валидаторы (assets/validators)", validators);
        b.Section("Решения (assets/solutions)", Nested(assets, "solutions", "solution").Select(s => $"{XmlUtil.Attr(s, "tag")}: {SourceAndBinary(s)}"));
    }

    /// <summary>"4 тестов, шаблон ...; valid: 2, invalid: 1".</summary>
    private static string TestsetSummary(XElement ts)
    {
        List<XElement> tests = Nested(ts, "tests", "test");
        var parts = new List<string> { (XmlUtil.ChildText(ts, "test-count") ?? tests.Count.ToString()) + " тестов" };
        foreach (string pattern in new[] { "input-path-pattern", "output-path-pattern", "answer-path-pattern" })
        {
            string? v = XmlUtil.ChildText(ts, pattern);
            if (v != null) parts.Add(pattern + " " + v);
        }
        foreach (var g in tests.Select(t => XmlUtil.Attr(t, "verdict")).Where(v => v != null).GroupBy(v => v))
        {
            parts.Add($"{g.Key}: {g.Count()}");
        }
        return string.Join(", ", parts);
    }

    private static void ReadStresses(XElement root, ProblemInfo.Builder b)
    {
        XElement? s = XmlUtil.Child(root, "stresses");
        if (s == null) return;
        var lines = new List<string>();
        string? count = XmlUtil.ChildText(s, "stress-count");
        string? pattern = XmlUtil.ChildText(s, "stress-path-pattern");
        if (count != null) lines.Add("количество: " + count);
        if (pattern != null) lines.Add("шаблон путей: " + pattern);
        foreach (XElement e in Nested(s, "list", "stress")) lines.Add(AttrText.Describe(e) ?? XmlUtil.Text(e) ?? "stress");
        // Сам факт наличия секции с нулём стресс-тестов тоже сведение: оставляем строку.
        b.Section("Стресс-тесты (stresses)", lines);
    }

    // ---------------------------------------------------------------- помощники

    private static string? Executable(XElement? e)
    {
        if (e == null) return null;
        string? path = SourcePath(e);
        string? name = XmlUtil.Attr(e, "name");
        if (name == null) return path;
        return path == null ? name : $"{name} ({path})";
    }

    private static string? SourcePath(XElement e) => XmlUtil.Attr(XmlUtil.Child(e, "source"), "path");

    /// <summary>"исходник (тип) -> бинарник (тип)".</summary>
    private static string SourceAndBinary(XElement e)
    {
        
        XElement? src = XmlUtil.Child(e, "source");
        XElement? bin = XmlUtil.Child(e, "binary");
        string Part(XElement? x) => x == null ? "-" : XmlUtil.Attr(x, "path") + (XmlUtil.Attr(x, "type") is { } t ? $" ({t})" : "");
        if (src == null && bin == null) return PathWithAttrs(e);
        return Part(src) + " -> " + Part(bin);
    }

    private static string PathWithAttrs(XElement e)
    {
        string? rest = AttrText.Describe(e, "path");
        return (XmlUtil.Attr(e, "path") ?? "-") + (rest == null ? "" : " (" + rest + ")");
    }

    private static string JoinAttr(List<XElement> elements, string attribute) =>
        string.Join(", ", elements.Select(e => XmlUtil.Attr(e, attribute)).Where(v => v != null));

    private static List<XElement> Nested(XElement parent, string container, string item) =>
        XmlUtil.Children(XmlUtil.Child(parent, container), item);
}
