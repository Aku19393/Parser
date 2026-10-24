using System.Xml.Linq;
using TaskParser.Model;
using TaskParser.Pkg;
using TaskParser.Util;

namespace TaskParser.Parsers.Pcms2;

/// <summary>
/// Формат PCMS2: в корне пакета лежит problem.xml. Файл бывает в нескольких вариантах
/// (выгрузка из Polygon и сценарий IOI), каждый из которых разбирает своя <see cref="IPcms2Variant"/>.
/// </summary>
public sealed class Pcms2Parser : IProblemParser
{
    internal const string Format = "PCMS2";
    private const string Descriptor = "problem.xml";

    private readonly IReadOnlyList<IPcms2Variant> variants;

    public Pcms2Parser() : this(new IPcms2Variant[] { new PolygonVariant(), new IoiVariant() }) { }

    internal Pcms2Parser(IReadOnlyList<IPcms2Variant> variants) => this.variants = variants;

    public string FormatName => Format;

    public ProblemInfo Parse(IPackageSource source)
    {
        if (!source.Exists(Descriptor))
        {
            throw new ParseException($"PCMS2: в корне пакета {source.Name} нет файла {Descriptor}");
        }
        XElement root;
        using (Stream input = source.Open(Descriptor)) root = XmlUtil.Parse(input, Descriptor);
        if (root.Name.LocalName != "problem")
        {
            throw new ParseException($"PCMS2: корневой элемент {Descriptor} должен называться <problem>, а не <{root.Name.LocalName}>");
        }
        foreach (IPcms2Variant variant in variants)
        {
            if (variant.Supports(root)) return variant.Parse(root, source);
        }
        throw new ParseException($"PCMS2: структура {Descriptor} не похожа ни на один из известных вариантов ("
                                 + string.Join(", ", variants.Select(v => v.Name)) + ")");
    }
}
