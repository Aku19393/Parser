using System.Text;
using System.Text.RegularExpressions;

namespace TaskParser.Util;

/// <summary>Подстановка номера теста в шаблоны имён файлов, которые используются в разных форматах.</summary>
public static class IndexPatterns
{
    private static readonly Regex PrintfRegex = new("%(0?)([0-9]*)d");
    private static readonly Regex HashesRegex = new("#+");
    private static readonly Regex CatsVarRegex = new("%(0*)n");

    /// <summary>printf-стиль: "%d", "%02d", "%03d" (EJUDGE, PCMS2 + Polygon).</summary>
    public static string? Printf(string? pattern, int index)
    {
        if (pattern == null) return null;
        return PrintfRegex.Replace(pattern, m =>
        {
            int width = 0;
            if (m.Groups[2].Value.Length > 0 && !int.TryParse(m.Groups[2].Value, out width)) width = 0;
            width = Math.Min(width, 1000);
            char fill = m.Groups[1].Value.Length == 0 ? ' ' : '0';
            return index.ToString().PadLeft(width, fill);
        });
    }

    /// <summary>Регулярное выражение, которое узнаёт имена файлов, созданные по printf-шаблону; номер - группа 1.</summary>
    public static Regex PrintfToRegex(string pattern)
    {
        var regex = new StringBuilder("^");
        int last = 0;
        bool hasNumber = false;
        foreach (Match m in PrintfRegex.Matches(pattern))
        {
            regex.Append(Regex.Escape(pattern.Substring(last, m.Index - last)));
            regex.Append(hasNumber ? "[0-9]+" : "([0-9]+)");
            hasNumber = true;
            last = m.Index + m.Length;
        }
        regex.Append(Regex.Escape(pattern.Substring(last))).Append('$');
        return new Regex(hasNumber ? regex.ToString() : "(?!)");
    }

    /// <summary>Стиль PCMS2 (IOI): последовательность '#' заменяется номером с ведущими нулями до длины последовательности.</summary>
    public static string? Hashes(string? pattern, int index)
    {
        if (pattern == null) return null;
        return HashesRegex.Replace(pattern, m => ZeroPad(index, m.Length));
    }

    /// <summary>Стиль CATS: "%n" - номер, "%0n" - номер с ведущими нулями до двух знаков, "%00n" - до трёх и т.д.</summary>
    public static string? CatsVariables(string? template, int index)
    {
        if (template == null) return null;
        return CatsVarRegex.Replace(template, m =>
        {
            int width = m.Groups[1].Length == 0 ? 0 : m.Groups[1].Length + 1;
            return ZeroPad(index, width);
        });
    }

    private static string ZeroPad(int value, int width) => value.ToString().PadLeft(Math.Min(width, 1000), '0');
}
