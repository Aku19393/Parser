using System.Globalization;
using System.Text.RegularExpressions;
using TaskParser.Model;

namespace TaskParser.Parsers.Cats;

/// <summary>
/// Спецификация порядковых номеров тестов CATS: список через запятую, где каждый элемент - это
/// номер ("5"), диапазон ("1-10"), диапазон с шагом ("1-10-2") или имя набора тестов (&lt;Testset&gt;).
/// </summary>
internal static class RankSpec
{
    private static readonly Regex Number = new("^[0-9]+$");
    private static readonly Regex Range = new("^([0-9]+)-([0-9]+)(?:-([0-9]+))?$");

    /// <summary>Защита от спецификаций вроде "1-2000000000", которые съели бы всю память.</summary>
    private const long MaxItems = 1_000_000L;

    internal static List<int> Expand(string? spec, Func<string, List<int>> resolveName)
    {
        var result = new HashSet<int>();
        var ordered = new List<int>();
        if (spec == null) return ordered;

        void Add(int value)
        {
            if (result.Add(value)) ordered.Add(value);
        }

        foreach (string raw in spec.Split(','))
        {
            string token = raw.Trim();
            if (token.Length == 0) continue;
            Match range = Range.Match(token);
            if (Number.IsMatch(token))
            {
                if (!int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out int n))
                {
                    throw new ParseException($"CATS: слишком большой номер теста '{token}'");
                }
                Add(n);
            }
            else if (range.Success)
            {
                foreach (int v in ExpandRange(token, range)) Add(v);
            }
            else
            {
                foreach (int v in resolveName(token)) Add(v);
            }
            if (ordered.Count > MaxItems)
            {
                throw new ParseException($"CATS: спецификация '{spec}' слишком велика");
            }
        }
        return ordered;
    }

    private static IEnumerable<int> ExpandRange(string token, Match m)
    {
        if (!long.TryParse(m.Groups[1].Value, out long from) || !long.TryParse(m.Groups[2].Value, out long to)
            || (m.Groups[3].Success && !long.TryParse(m.Groups[3].Value, out _)) || to > int.MaxValue)
        {
            throw new ParseException($"CATS: слишком большие числа в диапазоне '{token}'");
        }
        long step = m.Groups[3].Success ? long.Parse(m.Groups[3].Value) : 1;
        if (from > to) throw new ParseException($"CATS: в диапазоне '{token}' начало больше конца");
        if (step == 0) throw new ParseException($"CATS: в диапазоне '{token}' шаг равен нулю");
        if ((to - from) / step > MaxItems) throw new ParseException($"CATS: диапазон '{token}' слишком велик");
        var values = new List<int>();
        for (long i = from; i <= to; i += step) values.Add((int)i);
        return values;
    }
}
