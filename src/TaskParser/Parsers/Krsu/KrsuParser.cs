using System.Xml.Linq;
using TaskParser.Model;
using TaskParser.Pkg;
using TaskParser.Util;

namespace TaskParser.Parsers.Krsu;

/// <summary>
/// Формат KRSU: в корне пакета лежит testinfo.xml с лимитами, проверяющей программой, группами и тестами.
/// Время в файле задано в миллисекундах, память - в байтах.
/// </summary>
public sealed class KrsuParser : IProblemParser
{
    private const string Descriptor = "testinfo.xml";

    public string FormatName => "KRSU";

    public ProblemInfo Parse(IPackageSource source)
    {
        if (!source.Exists(Descriptor))
        {
            throw new ParseException($"KRSU: в корне пакета {source.Name} нет файла {Descriptor}");
        }
        XElement root;
        using (Stream input = source.Open(Descriptor)) root = XmlUtil.Parse(input, Descriptor);
        if (root.Name.LocalName != "testinfo")
        {
            throw new ParseException($"KRSU: корневой элемент {Descriptor} должен называться <testinfo>, а не <{root.Name.LocalName}>");
        }

        var b = ProblemInfo.CreateBuilder(FormatName);
        b.Checker(XmlUtil.ChildText(root, "checker"));
        b.Interactor(XmlUtil.ChildText(root, "interactor"));
        b.Statement(XmlUtil.ChildText(root, "problem"));
        b.TimeLimitMillis(Values.DurationMillis(XmlUtil.ChildText(root, "timelimit"), Values.Millisecond, "timelimit"));
        b.MemoryLimitBytes(Values.MemoryBytes(XmlUtil.ChildText(root, "memorylimit"), Values.Bytes, "memorylimit"));
        b.Detail("Версия пакета", XmlUtil.ChildText(root, "testversion"));
        b.Detail("Режим проверки", DescribeRunType(XmlUtil.ChildText(root, "runtype")));

        PackageTexts.Add(b, source, "Текст условия", new[] { XmlUtil.ChildText(root, "problem") });
        ReportUnknown(root, b);

        var testsByGroup = new Dictionary<string, List<int>>();
        List<XElement> groupElements = XmlUtil.Children(root, "group");
        foreach (XElement g in groupElements)
        {
            string id = XmlUtil.Attr(g, "id") ?? throw new ParseException("KRSU: у элемента <group> не указан атрибут id");
            if (!testsByGroup.TryAdd(id, new List<int>()))
            {
                throw new ParseException($"KRSU: группа с id={id} описана дважды");
            }
        }

        int index = 0;
        foreach (XElement t in XmlUtil.Children(root, "test"))
        {
            index++;
            string? groupId = XmlUtil.Attr(t, "groupid");
            if (groupId != null)
            {
                if (!testsByGroup.TryGetValue(groupId, out List<int>? members))
                {
                    throw new ParseException($"KRSU: тест №{index} ссылается на неизвестную группу {groupId}");
                }
                members.Add(index);
            }
            b.Test(new TestCase(index, XmlUtil.Attr(t, "input"), XmlUtil.Attr(t, "output"),
                Values.Integer(XmlUtil.Attr(t, "points"), $"points теста №{index}")));
        }

        foreach (XElement g in groupElements)
        {
            string id = XmlUtil.Attr(g, "id")!;
            List<string> prerequisites = Values.SplitList(XmlUtil.Attr(g, "prereq"), ",");
            foreach (string p in prerequisites)
            {
                if (!testsByGroup.ContainsKey(p))
                {
                    throw new ParseException($"KRSU: группа {id} зависит от неизвестной группы {p}");
                }
            }
            b.Group(new TestGroup(id, null, Values.Integer(XmlUtil.Attr(g, "points"), $"points группы {id}"),
                prerequisites, testsByGroup[id], null));
        }
        return b.Build();
    }

    private static readonly HashSet<string> KnownElements = new()
    {
        "checker", "interactor", "problem", "memorylimit", "timelimit", "testversion", "runtype", "group", "test",
    };

    private static readonly Dictionary<string, string[]> KnownAttributes = new()
    {
        ["group"] = new[] { "id", "points", "prereq" },
        ["test"] = new[] { "input", "output", "groupid", "points" },
    };

    /// <summary>Всё, чего нет в описании формата, не теряется, а выводится отдельной строкой.</summary>
    private static void ReportUnknown(XElement root, ProblemInfo.Builder b)
    {
        var elements = new SortedSet<string>();
        var attributes = new SortedSet<string>();
        foreach (XElement e in XmlUtil.Children(root))
        {
            string name = e.Name.LocalName;
            if (!KnownElements.Contains(name))
            {
                string attrs = string.Join(", ", e.Attributes().Select(a => $"{a.Name.LocalName}={a.Value}"));
                elements.Add(name + (attrs.Length > 0 ? " [" + attrs + "]" : "") + (XmlUtil.Text(e) is { } text ? " = " + text : ""));
                continue;
            }
            if (!KnownAttributes.TryGetValue(name, out string[]? known)) continue;
            foreach (XAttribute a in e.Attributes())
            {
                if (!known.Contains(a.Name.LocalName)) attributes.Add($"{name}@{a.Name.LocalName}");
            }
        }
        b.Detail("Неизвестные элементы", string.Join("; ", elements));
        b.Detail("Неизвестные атрибуты", string.Join(", ", attributes));
    }

    private static string? DescribeRunType(string? runType) => runType switch
    {
        null => null,
        "0" => "0 - до первого упавшего теста",
        "1" => "1 - все тесты",
        "2" => "2 - по группам тестов",
        _ => runType + " - неизвестный режим",
    };
}
