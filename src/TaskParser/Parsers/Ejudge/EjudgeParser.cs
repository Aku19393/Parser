using System.Text.RegularExpressions;
using TaskParser.Model;
using TaskParser.Pkg;
using TaskParser.Util;

namespace TaskParser.Parsers.Ejudge;

/// <summary>
/// Формат EJUDGE: параметры задачи в problem.cfg, группы тестов и баллы - в необязательном valuer.cfg.
/// Время time_limit - секунды, объём max_vm_size - байты с возможными суффиксами K/M/G.
/// </summary>
public sealed class EjudgeParser : IProblemParser
{
    private const string ProblemCfg = "problem.cfg";
    private const string ValuerCfg = "valuer.cfg";

    /// <summary>Ключи problem.cfg, которые уже разобраны в основные поля ProblemInfo и отдельно не выводятся.</summary>
    private static readonly HashSet<string> ConsumedKeys = new()
    {
        "long_name", "time_limit", "time_limit_millis", "max_vm_size", "use_stdin", "use_stdout",
        "input_file", "output_file", "check_cmd", "interactor_cmd",
    };

    /// <summary>Остальные известные ключи и их названия при выводе; неизвестные выводятся под своим именем.</summary>
    private static readonly Dictionary<string, string> Titles = new()
    {
        ["long_name_en"] = "Название (англ.)",
        ["internal_name"] = "Внутреннее имя",
        ["solution_cmd"] = "Авторское решение",
        ["extid"] = "Внешний идентификатор",
        ["revision"] = "Ревизия",
        ["test_pat"] = "Шаблон имени теста",
        ["corr_pat"] = "Шаблон имени ответа",
        ["use_corr"] = "Файлы ответов (use_corr)",
        ["enable_testlib_mode"] = "Режим testlib",
        ["max_stack_size"] = "Лимит стека",
        ["real_time_limit"] = "Лимит реального времени (с)",
    };

    public string FormatName => "EJUDGE";

    public ProblemInfo Parse(IPackageSource source)
    {
        if (!source.Exists(ProblemCfg))
        {
            throw new ParseException($"EJUDGE: в корне пакета {source.Name} нет файла {ProblemCfg}");
        }
        IniFile ini = IniFile.Parse(source.ReadText(ProblemCfg));
        Dictionary<string, string> cfg = ini.Section("problem")
                                         ?? throw new ParseException($"EJUDGE: в {ProblemCfg} нет секции [problem]");

        var b = ProblemInfo.CreateBuilder(FormatName);
        ReadProblemCfg(cfg, b);
        // Остальные секции problem.cfg (и ключи вне секций) тоже выводим целиком, чтобы ничего не терялось.
        foreach (var section in ini.AllSections)
        {
            if (section.Key == "problem" || section.Value.Count == 0) continue;
            b.Section(section.Key.Length == 0 ? "problem.cfg: параметры вне секций" : $"problem.cfg: секция [{section.Key}]",
                section.Value.Select(kv => kv.Key + " = " + kv.Value));
        }

        cfg.TryGetValue("test_pat", out string? testPattern);
        cfg.TryGetValue("use_corr", out string? useCorr);
        cfg.TryGetValue("corr_pat", out string? corrPattern);
        string? answerPattern = useCorr == "0" ? null : corrPattern;
        if (source.Exists(ValuerCfg))
        {
            ReadValuer(ValuerParser.Parse(source.ReadText(ValuerCfg)), testPattern, answerPattern, b);
            ReportUngroupedTests(source, testPattern, answerPattern, b);
        }
        else
        {
            b.Warning($"Файла {ValuerCfg} нет: группы тестов и баллы неизвестны.");
            ReadTestsFromFolder(source, testPattern, answerPattern, b);
        }
        return b.Build();
    }

    private static string? Get(Dictionary<string, string> cfg, string key) => cfg.TryGetValue(key, out string? v) ? v : null;

    private static void ReadProblemCfg(Dictionary<string, string> cfg, ProblemInfo.Builder b)
    {
        b.Name(Get(cfg, "long_name"));
        b.Checker(Get(cfg, "check_cmd"));
        b.Interactor(Get(cfg, "interactor_cmd"));

        long? millis = Values.DurationMillis(Get(cfg, "time_limit_millis"), Values.Millisecond, "time_limit_millis");
        millis ??= Values.DurationMillis(Get(cfg, "time_limit"), Values.Second, "time_limit");
        b.TimeLimitMillis(millis);
        b.MemoryLimitBytes(Values.MemoryBytes(Get(cfg, "max_vm_size"), Values.Bytes, "max_vm_size"));

        b.InputFile(IsOn(Get(cfg, "use_stdin")) ? ProblemInfo.Stdin : Get(cfg, "input_file"));
        b.OutputFile(IsOn(Get(cfg, "use_stdout")) ? ProblemInfo.Stdout : Get(cfg, "output_file"));

        foreach (var e in cfg)
        {
            if (ConsumedKeys.Contains(e.Key)) continue;
            b.Detail(Titles.TryGetValue(e.Key, out string? title) ? title : "problem.cfg: " + e.Key, e.Value);
        }
    }

    private static void ReadValuer(ValuerConfig valuer, string? testPattern, string? answerPattern, ProblemInfo.Builder b)
    {
        b.Detail("valuer.cfg: общие параметры", string.Join(", ", valuer.GlobalOptions));

        var groupIds = new HashSet<string>(valuer.Groups.Select(g => g.Id));
        var testPoints = new SortedDictionary<int, int?>();
        foreach (ValuerGroup g in valuer.Groups)
        {
            foreach (string r in g.Requires)
            {
                if (!groupIds.Contains(r))
                {
                    throw new ParseException($"EJUDGE: valuer.cfg: группа {g.Id} требует несуществующую группу {r}");
                }
            }
            foreach (int t in g.Tests) testPoints[t] = g.TestScore;   // null - баллы за тест не заданы
        }
        foreach (var t in testPoints)
        {
            b.Test(new TestCase(t.Key, IndexPatterns.Printf(testPattern, t.Key), IndexPatterns.Printf(answerPattern, t.Key), t.Value));
        }

        foreach (ValuerGroup g in valuer.Groups)
        {
            int? points = g.Score;
            if (points == null && g.TestScore != null) points = g.TestScore * g.Tests.Count;
            var notes = new List<string>(g.Flags);
            if (g.Score != null && g.TestScore != null) notes.Add("test_score " + g.TestScore);
            b.Group(new TestGroup(g.Id, null, points, g.Requires, g.Tests,
                notes.Count == 0 ? null : string.Join("; ", notes)));
        }
    }

    /// <summary>
    /// Тесты, лежащие в папке tests/, но не вошедшие ни в одну группу valuer.cfg, тоже показываем (без баллов) и предупреждаем о них.
    /// </summary>
    private static void ReportUngroupedTests(IPackageSource source, string? testPattern, string? answerPattern, ProblemInfo.Builder b)
    {
        if (testPattern == null) return;
        Regex fileName = IndexPatterns.PrintfToRegex(testPattern);
        var known = new HashSet<int>(b.KnownTestNumbers());
        var extra = new SortedSet<int>();
        foreach (string file in source.Files)
        {
            if (!file.StartsWith("tests/", StringComparison.Ordinal)) continue;
            Match m = fileName.Match(file.Substring("tests/".Length));
            if (m.Success && int.TryParse(m.Groups[1].Value, out int n) && !known.Contains(n)) extra.Add(n);
        }
        if (extra.Count == 0) return;
        foreach (int n in extra)
        {
            b.Test(new TestCase(n, IndexPatterns.Printf(testPattern, n), IndexPatterns.Printf(answerPattern, n), null, "нет в valuer.cfg"));
        }
        b.Warning("В папке tests/ есть тесты, не входящие ни в одну группу valuer.cfg: " + IntRanges.Format(extra));
    }

    /// <summary>Если valuer.cfg нет, перечисляет тесты по именам файлов в папке tests/.</summary>
    private static void ReadTestsFromFolder(IPackageSource source, string? testPattern, string? answerPattern, ProblemInfo.Builder b)
    {
        if (testPattern == null) return;
        Regex fileName = IndexPatterns.PrintfToRegex(testPattern);
        var numbers = new SortedSet<int>();
        foreach (string file in source.Files)
        {
            if (!file.StartsWith("tests/", StringComparison.Ordinal)) continue;
            Match m = fileName.Match(file.Substring("tests/".Length));
            if (m.Success && int.TryParse(m.Groups[1].Value, out int n)) numbers.Add(n);
        }
        foreach (int n in numbers)
        {
            b.Test(new TestCase(n, IndexPatterns.Printf(testPattern, n), IndexPatterns.Printf(answerPattern, n), null));
        }
    }

    private static bool IsOn(string? value) =>
        value != null && value != "0" && !value.Equals("false", StringComparison.OrdinalIgnoreCase);
}
