namespace TaskParser.Pkg;

/// <summary>
/// Приводит имена файлов к единому виду и прячет лишнюю обёртку.
/// Если архив создан «упаковкой папки», то все файлы лежат внутри одной корневой папки;
/// в этом случае она отбрасывается, и парсеры видят пакет так, как будто он упакован «изнутри».
/// </summary>
internal sealed class EntryIndex
{
    private readonly SortedDictionary<string, string> rawByPath = new(StringComparer.Ordinal);

    public EntryIndex(IEnumerable<string> rawNames)
    {
        var normalized = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (string raw in rawNames)
        {
            string n = Normalize(raw);
            if (n.Length == 0 || n.EndsWith('/') || n.StartsWith("__MACOSX/", StringComparison.Ordinal)) continue;
            normalized[n] = raw;
        }
        string prefix = CommonTopFolder(normalized.Keys);
        foreach (var e in normalized) rawByPath[e.Key.Substring(prefix.Length)] = e.Value;
    }

    public IReadOnlyList<string> Paths => rawByPath.Keys.ToList().AsReadOnly();

    public bool Contains(string path) => rawByPath.ContainsKey(path);

    /// <summary>Исходное имя (как в архиве / на диске) для нормализованного пути; null, если файла нет.</summary>
    public string? Raw(string path) => rawByPath.TryGetValue(path, out string? raw) ? raw : null;

    private static string Normalize(string raw)
    {
        string s = raw.Replace('\\', '/');
        while (s.StartsWith("./", StringComparison.Ordinal)) s = s.Substring(2);
        while (s.StartsWith('/')) s = s.Substring(1);
        return s;
    }

    private static string CommonTopFolder(IEnumerable<string> paths)
    {
        string? top = null;
        foreach (string p in paths)
        {
            int slash = p.IndexOf('/');
            if (slash < 0) return "";                 // есть файл прямо в корне - обёртки нет
            string first = p.Substring(0, slash + 1);
            if (top == null) top = first;
            else if (top != first) return "";
        }
        return top ?? "";
    }
}
