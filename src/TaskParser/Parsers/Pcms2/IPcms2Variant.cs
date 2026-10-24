using System.Xml.Linq;
using TaskParser.Model;
using TaskParser.Pkg;

namespace TaskParser.Parsers.Pcms2;

/// <summary>
/// Один из вариантов файла problem.xml. В PCMS2 их несколько, и они сильно различаются,
/// поэтому каждый вариант - отдельный класс, а <see cref="Pcms2Parser"/> лишь выбирает подходящий.
/// </summary>
internal interface IPcms2Variant
{
    /// <summary>Название варианта для вывода пользователю.</summary>
    string Name { get; }

    /// <summary>Подходит ли этот вариант к документу с данным корневым элементом.</summary>
    bool Supports(XElement root);

    ProblemInfo Parse(XElement root);

    /// <summary>Разбор с доступом к файлам пакета; по умолчанию файлы не используются.</summary>
    ProblemInfo Parse(XElement root, IPackageSource source) => Parse(root);
}
