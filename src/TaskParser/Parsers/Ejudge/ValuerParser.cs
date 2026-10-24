using System.Globalization;
using System.Text.RegularExpressions;
using TaskParser.Model;
using TaskParser.Util;

namespace TaskParser.Parsers.Ejudge;

/// <summary>
/// Разбор valuer.cfg по грамматике из описания формата:
/// <code>
/// file   : [ "global" "{" { option } "}" ] { "group" ID "{" { option } "}" } ;
/// option : NAME { argument } ";" ;
/// </code>
/// Формат свободный: элементы разделяются любыми пробельными символами, "#" начинает комментарий до конца строки.
/// Сначала текст режется на лексемы, затем рекурсивным спуском собирается структура.
/// </summary>
internal sealed class ValuerParser
{
    private static readonly Regex RangeRegex = new("^([0-9]+)(?:-([0-9]+))?$");
    private const long MaxTests = 1_000_000L;

    private readonly List<string> tokens;
    private int pos;

    private ValuerParser(List<string> tokens) => this.tokens = tokens;

    public static ValuerConfig Parse(string text) => new ValuerParser(Tokenize(text)).ParseFile();

    // ---------------------------------------------------------------- лексер

    internal static List<string> Tokenize(string text)
    {
        var result = new List<string>();
        int i = 0;
        while (i < text.Length)
        {
            char c = text[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
            }
            else if (c == '#')
            {
                while (i < text.Length && text[i] != '\n') i++;
            }
            else if (IsPunctuation(c))
            {
                result.Add(c.ToString());
                i++;
            }
            else
            {
                int start = i;
                while (i < text.Length && !char.IsWhiteSpace(text[i]) && !IsPunctuation(text[i]) && text[i] != '#') i++;
                result.Add(text.Substring(start, i - start));
            }
        }
        return result;
    }

    private static bool IsPunctuation(char c) => c == '{' || c == '}' || c == ';' || c == ',';

    // ---------------------------------------------------------------- грамматика

    private ValuerConfig ParseFile()
    {
        var global = new List<string>();
        if (Peek() == "global")
        {
            pos++;
            Expect("{");
            while (Peek() != "}") global.Add(string.Join(" ", ParseOption(null)));
            Expect("}");
        }
        var groups = new List<ValuerGroup>();
        var ids = new HashSet<string>();
        while (Peek() != null)
        {
            Expect("group");
            string id = Next("номер группы");
            if (!ids.Add(id)) throw new ParseException($"EJUDGE: valuer.cfg: группа {id} описана дважды");
            Expect("{");
            groups.Add(ParseGroupBody(id));
            Expect("}");
        }
        return new ValuerConfig(global, groups);
    }

    private ValuerGroup ParseGroupBody(string id)
    {
        var tests = new List<int>();
        int? score = null;
        int? testScore = null;
        var requires = new List<string>();
        var flags = new List<string>();
        while (Peek() != "}")
        {
            List<string> option = ParseOption(id);
            string name = option[0];
            List<string> args = option.GetRange(1, option.Count - 1);
            switch (name)
            {
                case "tests": tests.AddRange(ParseTests(id, args)); break;
                case "score": score = SingleInt(id, name, args); break;
                case "test_score": testScore = SingleInt(id, name, args); break;
                case "requires": requires.AddRange(args); break;
                default: flags.Add(string.Join(" ", option)); break;
            }
        }
        return new ValuerGroup(id, tests, score, testScore, requires, flags);
    }

    /// <summary>Разбирает "имя аргумент ... ;" и возвращает [имя, аргументы без запятых].</summary>
    private List<string> ParseOption(string? groupId)
    {
        string where = groupId == null ? "блоке global" : "группе " + groupId;
        string name = Next("имя параметра в " + where);
        if (IsPunctuation(name[0]))
        {
            throw new ParseException($"EJUDGE: valuer.cfg: ожидалось имя параметра в {where}, а найдено '{name}'");
        }
        var option = new List<string> { name };
        while (true)
        {
            string token = Next($"';' после параметра {name}");
            if (token == ";") return option;
            if (token == "{" || token == "}")
            {
                throw new ParseException($"EJUDGE: valuer.cfg: параметр {name} в {where} не завершён символом ';'");
            }
            if (token != ",") option.Add(token);
        }
    }

    private static List<int> ParseTests(string groupId, List<string> args)
    {
        Match m = RangeRegex.Match(string.Concat(args));
        if (!m.Success)
        {
            throw new ParseException($"EJUDGE: valuer.cfg: в группе {groupId} некорректный диапазон тестов '{string.Join(" ", args)}'");
        }
        if (!long.TryParse(m.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out long from)
            || (m.Groups[2].Success && !long.TryParse(m.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out _))
            || from > int.MaxValue)
        {
            throw new ParseException($"EJUDGE: valuer.cfg: в группе {groupId} слишком большой номер теста");
        }
        long to = m.Groups[2].Success ? long.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) : from;
        if (from > to) throw new ParseException($"EJUDGE: valuer.cfg: в группе {groupId} начало диапазона тестов больше конца");
        if (to > int.MaxValue || to - from > MaxTests)
        {
            throw new ParseException($"EJUDGE: valuer.cfg: в группе {groupId} слишком большой диапазон тестов");
        }
        var result = new List<int>();
        for (long i = from; i <= to; i++) result.Add((int)i);
        return result;
    }

    private static int? SingleInt(string groupId, string name, List<string> args)
    {
        if (args.Count != 1)
        {
            throw new ParseException($"EJUDGE: valuer.cfg: параметр {name} в группе {groupId} должен иметь одно число");
        }
        return Values.Integer(args[0], $"{name} группы {groupId}");
    }

    // ---------------------------------------------------------------- работа с лексемами

    private string? Peek() => pos < tokens.Count ? tokens[pos] : null;

    private string Next(string expected)
    {
        if (pos >= tokens.Count) throw new ParseException("EJUDGE: valuer.cfg: неожиданный конец файла, ожидалось: " + expected);
        return tokens[pos++];
    }

    private void Expect(string token)
    {
        string actual = Next($"'{token}'");
        if (actual != token) throw new ParseException($"EJUDGE: valuer.cfg: ожидалось '{token}', а найдено '{actual}'");
    }
}
