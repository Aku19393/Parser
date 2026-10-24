using System.Xml.Linq;
using TaskParser.Model;
using TaskParser.Util;

namespace TaskParser.Parsers.Pcms2;

/// <summary>
/// Вариант problem.xml со сценарием проверки IOI: &lt;judging&gt;&lt;script type="%ioi"&gt;&lt;testset&gt;&lt;test-group&gt;.
/// Время задаётся строкой вида "2.0s", память - в байтах. Группы нумеруются с 1 в пределах набора тестов,
/// на эти номера ссылается атрибут require-groups. Баллы за группу = group-bonus + сумма баллов её тестов.
/// Разбираются все наборы тестов: самый большой становится основным, остальные попадают в AdditionalTestSets.
/// </summary>
internal sealed class IoiVariant : IPcms2Variant
{
    public string Name => "IOI (judging/script)";

    public bool Supports(XElement root)
    {
        XElement? judging = XmlUtil.Child(root, "judging");
        return judging != null && XmlUtil.Child(judging, "script") != null;
    }

    public ProblemInfo Parse(XElement root)
    {
        var b = ProblemInfo.CreateBuilder(Pcms2Parser.Format);
        b.Detail("Вариант problem.xml", Name);
        b.Detail("Идентификатор", XmlUtil.Attr(root, "id"));
        b.Detail("Версия формата", XmlUtil.Attr(root, "version"));

        XElement script = XmlUtil.Child(XmlUtil.Child(root, "judging"), "script")!;
        b.Detail("Тип сценария", XmlUtil.Attr(script, "type"));
        b.Checker(Binary(XmlUtil.Child(script, "verifier")));
        b.Interactor(Binary(XmlUtil.Child(script, "interactor")));
        b.Detail("Проверяющая программа: тип и среда", Executable(XmlUtil.Child(script, "verifier")));
        b.Detail("Интерактор: тип и среда", Executable(XmlUtil.Child(script, "interactor")));
        foreach (XElement e in XmlUtil.Children(script))
        {
            string tag = e.Name.LocalName;
            if (tag is "testset" or "verifier" or "interactor") continue;
            b.Detail("Элемент <" + tag + "> сценария", AttrText.Describe(e) ?? "(без атрибутов)");
        }

        List<XElement> testsets = XmlUtil.Children(script, "testset");
        if (testsets.Count == 0) throw new ParseException("PCMS2: в <script> нет ни одного <testset>");
        XElement? main = null;
        int best = -1;
        foreach (XElement ts in testsets)
        {
            int count = TestsIn(ts).Count;
            if (count > best)
            {
                best = count;
                main = ts;
            }
        }
        b.Detail("Разобранный набор тестов", XmlUtil.Attr(main, "name"));
        b.Detail("Наборы тестов в файле", string.Join(", ", testsets.Select(t => $"{XmlUtil.Attr(t, "name")} ({TestsIn(t).Count})")));

        TestSet mainSet = ReadTestset(main!);
        b.InputFile(mainSet.InputFile);
        b.OutputFile(mainSet.OutputFile);
        b.TimeLimitMillis(mainSet.TimeLimitMillis);
        b.MemoryLimitBytes(mainSet.MemoryLimitBytes);
        foreach (TestCase t in mainSet.Tests) b.Test(t);
        foreach (TestGroup g in mainSet.Groups) b.Group(g);
        if (mainSet.Notes.Count > 0) b.Detail("Параметры основного набора", string.Join("; ", mainSet.Notes));
        foreach (XElement ts in testsets)
        {
            if (ts != main) b.AdditionalTestSet(ReadTestset(ts));
        }
        return b.Build();
    }

    private static TestSet ReadTestset(XElement testset)
    {
        string? inputHref = XmlUtil.Attr(testset, "input-href");
        string? answerHref = XmlUtil.Attr(testset, "answer-href");
        int totalGroups = XmlUtil.Children(testset, "test-group").Count;
        var tests = new List<TestCase>();
        var groups = new List<TestGroup>();

        int index = 0;
        int groupNumber = 0;
        // Тесты идут подряд в порядке документа: "голые" <test> и тесты внутри <test-group>.
        foreach (XElement child in XmlUtil.Children(testset))
        {
            string tag = child.Name.LocalName;
            if (tag == "test")
            {
                index++;
                tests.Add(ToTest(child, index, inputHref, answerHref));
            }
            else if (tag == "test-group")
            {
                groupNumber++;
                var members = new List<int>();
                int sum = 0;
                foreach (XElement t in XmlUtil.Children(child, "test"))
                {
                    index++;
                    TestCase tc = ToTest(t, index, inputHref, answerHref);
                    tests.Add(tc);
                    members.Add(index);
                    if (tc.Points != null) sum += tc.Points.Value;
                }
                int? bonus = Values.Integer(XmlUtil.Attr(child, "group-bonus"), $"group-bonus группы {groupNumber}");
                groups.Add(new TestGroup(groupNumber.ToString(), XmlUtil.Attr(child, "comment"),
                    (bonus ?? 0) + sum, Prerequisites(child, groupNumber, totalGroups), members,
                    AttrText.Describe(child, "comment", "require-groups")));
            }
        }

        var notes = new List<string>();
        string? extra = AttrText.Describe(testset, "name", "input-name", "output-name", "time-limit", "memory-limit");
        if (extra != null) notes.Add(extra);
        return new TestSet(XmlUtil.Attr(testset, "name") ?? "(без имени)",
            XmlUtil.Attr(testset, "input-name"), XmlUtil.Attr(testset, "output-name"),
            Values.DurationMillis(XmlUtil.Attr(testset, "time-limit"), Values.Millisecond, "time-limit"),
            Values.MemoryBytes(XmlUtil.Attr(testset, "memory-limit"), Values.Bytes, "memory-limit"),
            tests, groups, notes);
    }

    private static TestCase ToTest(XElement t, int index, string? inputHref, string? answerHref) =>
        new(index, IndexPatterns.Hashes(inputHref, index), IndexPatterns.Hashes(answerHref, index),
            Values.Integer(XmlUtil.Attr(t, "points"), $"points теста №{index}"), AttrText.Describe(t, "points"));

    private static List<string> Prerequisites(XElement group, int number, int totalGroups)
    {
        var result = new List<string>();
        foreach (string id in Values.SplitList(XmlUtil.Attr(group, "require-groups"), @"[\s,]+"))
        {
            int n = Values.Integer(id, $"require-groups группы {number}")!.Value;
            if (n < 1 || n > totalGroups)
            {
                throw new ParseException($"PCMS2: группа {number} требует несуществующую группу {n} (в наборе групп: {totalGroups})");
            }
            if (!result.Contains(id)) result.Add(id);
        }
        if (XmlUtil.Attr(group, "require-previous") == "true" && number > 1)
        {
            string previous = (number - 1).ToString();
            if (!result.Contains(previous)) result.Add(previous);
        }
        return result;
    }

    private static List<XElement> TestsIn(XElement testset)
    {
        var tests = new List<XElement>(XmlUtil.Children(testset, "test"));
        foreach (XElement g in XmlUtil.Children(testset, "test-group")) tests.AddRange(XmlUtil.Children(g, "test"));
        return tests;
    }

    private static string? Binary(XElement? e) => XmlUtil.Attr(XmlUtil.Child(e, "binary"), "file");

    /// <summary>"тип %alt, среда exe.universal, файл check.exe".</summary>
    private static string? Executable(XElement? e)
    {
        if (e == null) return null;
        XElement? bin = XmlUtil.Child(e, "binary");
        return AttrText.Join(", ", XmlUtil.Attr(e, "type") is { } t ? "тип " + t : null,
            XmlUtil.Attr(bin, "executable-id") is { } id ? "среда " + id : null,
            XmlUtil.Attr(bin, "file") is { } f ? "файл " + f : null);
    }
}
