using System.Globalization;
using System.Text.RegularExpressions;
using TaskParser.Model;

namespace TaskParser.Util;

/// <summary>
/// Разбор чисел, времени и объёма памяти из текстовых значений.
/// Все методы возвращают null для null-входа, поэтому необязательные поля обрабатываются без лишних if.
/// Параметр <c>what</c> нужен только для понятного сообщения об ошибке.
/// </summary>
public static class Values
{
    public const long Bytes = 1L;
    public const long KB = 1024L;
    public const long MB = 1024L * 1024L;
    public const long Millisecond = 1L;
    public const long Second = 1000L;

    private static readonly Regex Memory = new(@"^\s*([0-9]+)\s*(?:([kmg])b?|(b))?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex Duration = new(@"^\s*([0-9]+(?:\.[0-9]+)?)\s*(ms|s)?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static int? Integer(string? text, string what)
    {
        if (text == null) return null;
        if (!int.TryParse(text.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int value))
        {
            throw new ParseException($"Некорректное целое число в поле «{what}»: '{text}'");
        }
        return value;
    }

    /// <summary>
    /// Объём памяти в байтах. Понимает суффиксы B, K, M, G (с необязательной буквой B после них);
    /// число без суффикса трактуется в единицах <c>defaultUnit</c>.
    /// </summary>
    public static long? MemoryBytes(string? text, long defaultUnit, string what)
    {
        if (text == null) return null;
        Match m = Memory.Match(text);
        if (!m.Success) throw new ParseException($"Некорректный объём памяти в поле «{what}»: '{text}'");
        long unit = defaultUnit;
        if (m.Groups[2].Success)
        {
            unit = m.Groups[2].Value.ToLowerInvariant() switch
            {
                "k" => KB,
                "m" => MB,
                _ => 1024L * MB,
            };
        }
        else if (m.Groups[3].Success)
        {
            unit = Bytes;
        }
        try
        {
            return checked(long.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) * unit);
        }
        catch (OverflowException)
        {
            throw new ParseException($"Слишком большой объём памяти в поле «{what}»: '{text}'");
        }
    }

    /// <summary>
    /// Длительность в миллисекундах. Понимает суффиксы ms и s, в том числе дробные значения ("2.5", "2.0s");
    /// число без суффикса трактуется в единицах <c>defaultUnitMillis</c>.
    /// </summary>
    public static long? DurationMillis(string? text, long defaultUnitMillis, string what)
    {
        if (text == null) return null;
        Match m = Duration.Match(text);
        if (!m.Success) throw new ParseException($"Некорректное время в поле «{what}»: '{text}'");
        long unit = defaultUnitMillis;
        if (m.Groups[2].Success) unit = m.Groups[2].Value.Equals("ms", StringComparison.OrdinalIgnoreCase) ? Millisecond : Second;
        try
        {
            decimal number = decimal.Parse(m.Groups[1].Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
            return checked((long)Math.Round(number * unit, 0, MidpointRounding.AwayFromZero));
        }
        catch (OverflowException)
        {
            throw new ParseException($"Слишком большое значение времени в поле «{what}»: '{text}'");
        }
    }

    /// <summary>Делит строку по регулярному выражению, обрезает пробелы и выбрасывает пустые элементы.</summary>
    public static List<string> SplitList(string? text, string separatorRegex)
    {
        var result = new List<string>();
        if (text == null) return result;
        foreach (string part in Regex.Split(text, separatorRegex))
        {
            string p = part.Trim();
            if (p.Length > 0) result.Add(p);
        }
        return result;
    }
}
