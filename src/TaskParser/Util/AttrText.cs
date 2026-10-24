using System.Text;
using System.Xml.Linq;

namespace TaskParser.Util;

/// <summary>Вспомогательные методы, чтобы ничего из XML не терялось при выводе: атрибуты и текст условия.</summary>
public static class AttrText
{
    /// <summary>
    /// Все атрибуты элемента в виде "имя=значение, ...", кроме перечисленных в <paramref name="skip"/>.
    /// Атрибут comment выводится одним значением (в PCMS2 это описание способа создания теста).
    /// </summary>
    public static string? Describe(XElement? e, params string[] skip)
    {
        if (e == null) return null;
        var parts = new List<string>();
        foreach (XAttribute a in e.Attributes())
        {
            string name = a.Name.LocalName;
            if (skip.Contains(name) || string.IsNullOrWhiteSpace(a.Value)) continue;
            parts.Add(name == "comment" ? a.Value.Trim() : name + "=" + a.Value.Trim());
        }
        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    /// <summary>Склеивает непустые части через разделитель; null, если частей нет.</summary>
    public static string? Join(string separator, params string?[] parts)
    {
        var list = parts.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        return list.Count == 0 ? null : string.Join(separator, list);
    }

    /// <summary>
    /// Превращает фрагмент STML/XML в простой текст по строкам: абзацы и строки таблиц разделяются переводами строк,
    /// рисунки показываются как [рисунок имя]. Формулы и пробелы внутри абзаца сохраняются.
    /// </summary>
    public static List<string> PlainText(XElement element)
    {
        var sb = new StringBuilder();
        Walk(element, sb);
        var lines = new List<string>();
        foreach (string raw in sb.ToString().Split('\n'))
        {
            string line = string.Join(" ", raw.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            if (line.Length > 0) lines.Add(line);
        }
        return lines;
    }

    private static void Walk(XNode node, StringBuilder sb)
    {
        switch (node)
        {
            case XText t:
                sb.Append(t.Value);
                break;
            case XElement e:
                string tag = e.Name.LocalName.ToLowerInvariant();
                if (tag == "img") { sb.Append(" [рисунок ").Append(e.Attribute("picture")?.Value).Append("] "); return; }
                bool block = tag is "p" or "br" or "tr" or "table" or "div" or "li" or "ul" or "ol" or "pre";
                if (block) sb.Append('\n');
                if (tag is "td" or "th") sb.Append(" | ");
                foreach (XNode child in e.Nodes()) Walk(child, sb);
                if (block) sb.Append('\n');
                break;
        }
    }
}
