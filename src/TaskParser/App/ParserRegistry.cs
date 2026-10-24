using TaskParser.Model;

namespace TaskParser.App;

/// <summary>
/// Реестр известных форматов. Приложение ищет парсер по имени формата и не знает ни одной конкретной реализации,
/// поэтому новый формат добавляется регистрацией нового класса, без правок существующего кода (принцип OCP).
/// </summary>
public sealed class ParserRegistry
{
    private readonly Dictionary<string, IProblemParser> parsers = new();
    private readonly List<string> order = new();

    public ParserRegistry Register(IProblemParser parser)
    {
        string key = Key(parser.FormatName);
        if (!parsers.TryAdd(key, parser))
        {
            throw new ArgumentException($"Формат {parser.FormatName} уже зарегистрирован");
        }
        order.Add(key);
        return this;
    }

    public IProblemParser? Find(string format) => parsers.TryGetValue(Key(format), out var p) ? p : null;

    public IReadOnlyList<string> Formats => order.Select(k => parsers[k].FormatName).ToList();

    private static string Key(string format) => format.Trim().ToUpperInvariant();
}
