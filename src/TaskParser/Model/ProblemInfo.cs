namespace TaskParser.Model;

/// <summary>
/// Единое, не зависящее от формата описание задачи. Все парсеры приводят свой формат к этой модели,
/// поэтому остальной код (вывод на консоль и т.д.) о форматах пакетов ничего не знает.
/// Нормализованные единицы: время - миллисекунды, память - байты.
/// </summary>
public sealed class ProblemInfo
{
    public const string Stdin = "stdin";
    public const string Stdout = "stdout";

    public string Format { get; }
    public string? Name { get; }
    public long? TimeLimitMillis { get; }
    public long? MemoryLimitBytes { get; }
    public string? InputFile { get; }
    public string? OutputFile { get; }
    public string? Checker { get; }
    public string? Interactor { get; }
    public IReadOnlyList<string> Statements { get; }
    public IReadOnlyList<TestCase> Tests { get; }
    public IReadOnlyList<TestGroup> Groups { get; }

    /// <summary>Остальные наборы тестов, если в описании их несколько (основной набор - это Tests и Groups).</summary>
    public IReadOnlyList<TestSet> AdditionalTestSets { get; }

    /// <summary>Блоки строк: списки файлов, примеры, текст условия и т.д.</summary>
    public IReadOnlyList<InfoSection> Sections { get; }

    /// <summary>Специфичные для формата сведения в порядке добавления (название -> значение).</summary>
    public IReadOnlyDictionary<string, string> Details { get; }

    public IReadOnlyList<string> Warnings { get; }

    private ProblemInfo(Builder b)
    {
        Format = b.FormatName;
        Name = b.NameValue;
        TimeLimitMillis = b.TimeLimit;
        MemoryLimitBytes = b.MemoryLimit;
        InputFile = b.InputFileValue;
        OutputFile = b.OutputFileValue;
        Checker = b.CheckerValue;
        Interactor = b.InteractorValue;
        Statements = b.StatementList.ToList().AsReadOnly();
        Tests = b.TestList.ToList().AsReadOnly();
        Groups = b.GroupList.ToList().AsReadOnly();
        AdditionalTestSets = b.TestSetList.ToList().AsReadOnly();
        Sections = b.SectionList.ToList().AsReadOnly();
        Details = new Dictionary<string, string>(b.DetailMap);
        Warnings = b.WarningList.ToList().AsReadOnly();
    }

    public static Builder CreateBuilder(string format) => new(format);

    public sealed class Builder
    {
        internal string FormatName { get; }
        internal string? NameValue;
        internal long? TimeLimit;
        internal long? MemoryLimit;
        internal string? InputFileValue;
        internal string? OutputFileValue;
        internal string? CheckerValue;
        internal string? InteractorValue;
        internal readonly List<string> StatementList = new();
        internal readonly List<TestCase> TestList = new();
        internal readonly List<TestGroup> GroupList = new();
        internal readonly List<TestSet> TestSetList = new();
        internal readonly List<InfoSection> SectionList = new();
        internal readonly Dictionary<string, string> DetailMap = new();
        internal readonly List<string> WarningList = new();

        internal Builder(string format) => FormatName = format;

        public Builder Name(string? v) { NameValue = v; return this; }
        public Builder TimeLimitMillis(long? v) { TimeLimit = v; return this; }
        public Builder MemoryLimitBytes(long? v) { MemoryLimit = v; return this; }
        public Builder InputFile(string? v) { InputFileValue = v; return this; }
        public Builder OutputFile(string? v) { OutputFileValue = v; return this; }
        public Builder Checker(string? v) { CheckerValue = v; return this; }
        public Builder Interactor(string? v) { InteractorValue = v; return this; }

        public Builder Statement(string? v)
        {
            if (!string.IsNullOrWhiteSpace(v)) StatementList.Add(v);
            return this;
        }

        public Builder Test(TestCase t) { TestList.Add(t); return this; }
        public Builder Group(TestGroup g) { GroupList.Add(g); return this; }
        public Builder AdditionalTestSet(TestSet s) { TestSetList.Add(s); return this; }

        /// <summary>Добавляет блок строк; пустые блоки игнорируются.</summary>
        public Builder Section(string title, IEnumerable<string> lines, bool isText = false)
        {
            List<string> list = lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
            if (list.Count > 0) SectionList.Add(new InfoSection(title, list.AsReadOnly(), isText));
            return this;
        }

        /// <summary>Пустые значения игнорируются, чтобы парсерам не надо было проверять каждое поле.</summary>
        public Builder Detail(string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value)) DetailMap[key] = value;
            return this;
        }

        /// <summary>Номера уже добавленных тестов (нужны парсерам, которые дополняют список тестов).</summary>
        public IEnumerable<int> KnownTestNumbers() => TestList.Select(t => t.Index);

        public Builder Warning(string text) { WarningList.Add(text); return this; }

        public ProblemInfo Build() => new(this);
    }
}
