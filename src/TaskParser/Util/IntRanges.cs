using System.Text;

namespace TaskParser.Util;

/// <summary>Сжатая запись наборов номеров: [1,2,3,5,7,8] -> "1-3, 5, 7-8".</summary>
public static class IntRanges
{
    public static string Format(IEnumerable<int> numbers)
    {
        List<int> sorted = numbers.Distinct().OrderBy(n => n).ToList();
        if (sorted.Count == 0) return "-";
        var sb = new StringBuilder();
        int start = sorted[0];
        int prev = start;
        for (int i = 1; i <= sorted.Count; i++)
        {
            int? cur = i < sorted.Count ? sorted[i] : null;
            if (cur != null && cur == prev + 1)
            {
                prev = cur.Value;
                continue;
            }
            if (sb.Length > 0) sb.Append(", ");
            sb.Append(start == prev ? start.ToString() : $"{start}-{prev}");
            if (cur != null)
            {
                start = cur.Value;
                prev = cur.Value;
            }
        }
        return sb.ToString();
    }
}
