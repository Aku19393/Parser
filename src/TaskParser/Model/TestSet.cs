namespace TaskParser.Model;

/// <summary>
/// Набор тестов со своими лимитами и файлами. Нужен для форматов, где в одном описании несколько наборов
/// (например, preliminary и main в PCMS2): основной набор лежит в <see cref="ProblemInfo.Tests"/>, остальные - здесь.
/// </summary>
public sealed class TestSet
{
    public string Name { get; }
    public string? InputFile { get; }
    public string? OutputFile { get; }
    public long? TimeLimitMillis { get; }
    public long? MemoryLimitBytes { get; }
    public IReadOnlyList<TestCase> Tests { get; }
    public IReadOnlyList<TestGroup> Groups { get; }

    /// <summary>Прочие параметры набора в текстовом виде (шаблоны имён файлов и т.п.).</summary>
    public IReadOnlyList<string> Notes { get; }

    public TestSet(string name, string? inputFile, string? outputFile, long? timeLimitMillis, long? memoryLimitBytes,
                   IEnumerable<TestCase> tests, IEnumerable<TestGroup> groups, IEnumerable<string> notes)
    {
        Name = name;
        InputFile = inputFile;
        OutputFile = outputFile;
        TimeLimitMillis = timeLimitMillis;
        MemoryLimitBytes = memoryLimitBytes;
        Tests = tests.ToList().AsReadOnly();
        Groups = groups.ToList().AsReadOnly();
        Notes = notes.ToList().AsReadOnly();
    }
}
