using System.Text;
using System.Xml;
using System.Xml.Linq;
using TaskParser.Model;

namespace TaskParser.Util;

/// <summary>Безопасное чтение XML и несколько удобных методов обхода дерева. Методы терпимы к null.</summary>
public static class XmlUtil
{
    static XmlUtil()
    {
        // Нужно для CATS: XML бывает в Windows-1251, а эта кодировка в .NET подключается отдельно.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>
    /// Читает XML и возвращает корневой элемент. Кодировка берётся из XML-декларации (в CATS это бывает Windows-1251).
    /// DTD игнорируется, внешние сущности не загружаются (защита от XXE).
    /// </summary>
    public static XElement Parse(Stream input, string fileName)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Ignore,
            XmlResolver = null,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
        };
        try
        {
            using XmlReader reader = XmlReader.Create(input, settings);
            return XDocument.Load(reader).Root
                   ?? throw new ParseException($"Файл {fileName} не содержит корневого элемента");
        }
        catch (XmlException e)
        {
            throw new ParseException($"Файл {fileName} содержит некорректный XML (строка {e.LineNumber}): {e.Message}", e);
        }
    }

    /// <summary>Все дочерние элементы.</summary>
    public static List<XElement> Children(XElement? parent) =>
        parent == null ? new List<XElement>() : parent.Elements().ToList();

    /// <summary>Дочерние элементы с заданным именем.</summary>
    public static List<XElement> Children(XElement? parent, string name) =>
        Children(parent).Where(e => e.Name.LocalName == name).ToList();

    /// <summary>Первый дочерний элемент с заданным именем или null.</summary>
    public static XElement? Child(XElement? parent, string name) =>
        Children(parent).FirstOrDefault(e => e.Name.LocalName == name);

    /// <summary>Значение атрибута; null, если атрибута нет или он пустой.</summary>
    public static string? Attr(XElement? e, string name)
    {
        string? v = e?.Attribute(name)?.Value.Trim();
        return string.IsNullOrEmpty(v) ? null : v;
    }

    /// <summary>Текст элемента без крайних пробелов; null, если пусто.</summary>
    public static string? Text(XElement? e)
    {
        string? v = e?.Value.Trim();
        return string.IsNullOrEmpty(v) ? null : v;
    }

    public static string? ChildText(XElement? parent, string name) => Text(Child(parent, name));
}
