using System.Text;
using TaskParser.Model;

namespace TaskParser.Pkg;

/// <summary>
/// Добавляет в описание задачи содержимое текстовых файлов пакета (условие, разбор и т.п.),
/// если они действительно лежат в пакете. Бинарные файлы (pdf, exe) пропускаются.
/// </summary>
public static class PackageTexts
{
    private static readonly string[] TextExtensions = { ".tex", ".txt", ".md", ".html", ".htm", ".xml", ".stml" };
    private const int MaxChars = 200_000;

    public static void Add(ProblemInfo.Builder b, IPackageSource source, string titlePrefix, IEnumerable<string?> paths)
    {
        foreach (string? raw in paths.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct())
        {
            string path = raw!.Replace('\\', '/').TrimStart('/');
            if (!TextExtensions.Any(e => path.EndsWith(e, StringComparison.OrdinalIgnoreCase))) continue;
            if (!source.Exists(path)) continue;
            string text = source.ReadText(path);
            bool cut = text.Length > MaxChars;
            if (cut) text = text.Substring(0, MaxChars);
            var lines = text.Replace("\r\n", "\n").Split('\n').Select(l => l.TrimEnd()).ToList();
            if (cut) lines.Add("… (текст обрезан)");
            b.Section($"{titlePrefix}: {path}", lines, true);
        }
    }
}
