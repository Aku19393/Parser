using System.Text;
using System.Text.RegularExpressions;
using TaskParser.Model;

namespace TaskParser.Parsers.Ejudge;

/// <summary>
/// Минимальный разбор problem.cfg: секции "[имя]" и строки "ключ = значение".
/// Значение может быть в кавычках; вне кавычек комментарием считается всё после "#" или "//"
/// (если им предшествует пробел). Ключ без значения означает включённый флаг.
/// </summary>
internal sealed class IniFile
{
    private readonly Dictionary<string, Dictionary<string, string>> sections = new();

    public static IniFile Parse(string text)
    {
        var ini = new IniFile();
        var current = new Dictionary<string, string>();
        ini.sections[""] = current;
        int lineNumber = 0;
        foreach (string rawLine in Regex.Split(text, @"\r\n|\n|\r"))
        {
            lineNumber++;
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith(';') || line.StartsWith("//", StringComparison.Ordinal)) continue;
            if (line.StartsWith('['))
            {
                if (!line.EndsWith(']'))
                {
                    throw new ParseException($"EJUDGE: problem.cfg, строка {lineNumber}: не закрыта скобка в заголовке секции");
                }
                string name = line.Substring(1, line.Length - 2).Trim();
                if (!ini.sections.TryGetValue(name, out current!))
                {
                    ini.sections[name] = current = new Dictionary<string, string>();
                }
                continue;
            }
            int eq = line.IndexOf('=');
            string key = (eq < 0 ? line : line.Substring(0, eq)).Trim();
            if (key.Length == 0)
            {
                throw new ParseException($"EJUDGE: problem.cfg, строка {lineNumber}: пустое имя параметра");
            }
            current[key] = eq < 0 ? "1" : ParseValue(line.Substring(eq + 1));
        }
        return ini;
    }

    /// <summary>Все секции в порядке добавления; секция с пустым именем - ключи до первого заголовка.</summary>
    public IEnumerable<KeyValuePair<string, Dictionary<string, string>>> AllSections => sections;

    public Dictionary<string, string>? Section(string name) => sections.TryGetValue(name, out var s) ? s : null;

    private static string ParseValue(string raw)
    {
        string s = raw.Trim();
        if (s.StartsWith('"'))
        {
            var sb = new StringBuilder();
            for (int i = 1; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\\' && i + 1 < s.Length) sb.Append(s[++i]);
                else if (c == '"') break;
                else sb.Append(c);
            }
            return sb.ToString();
        }
        return StripComment(s).Trim();
    }

    private static string StripComment(string s)
    {
        for (int i = 0; i < s.Length; i++)
        {
            bool boundary = i == 0 || char.IsWhiteSpace(s[i - 1]);
            if (boundary && (s[i] == '#' || string.CompareOrdinal(s, i, "//", 0, 2) == 0)) return s.Substring(0, i);
        }
        return s;
    }
}
