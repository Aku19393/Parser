using System.IO.Compression;
using TaskParser.Model;
using TaskParser.Output;
using TaskParser.Pkg;

namespace TaskParser.App;

/// <summary>
/// Сценарий работы программы: разобрать аргументы, открыть пакет, выбрать парсер, вывести результат.
/// Все зависимости приходят снаружи (DIP), поэтому класс легко проверить в тестах.
/// </summary>
public sealed class Application
{
    public const int ExitOk = 0;
    public const int ExitError = 1;
    public const int ExitUsage = 2;

    private readonly ParserRegistry registry;
    private readonly IPackageOpener opener;
    private readonly IProblemPrinter printer;

    public Application(ParserRegistry registry, IPackageOpener opener, IProblemPrinter printer)
    {
        this.registry = registry;
        this.opener = opener;
        this.printer = printer;
    }

    public int Run(string[] args, TextWriter output, TextWriter error)
    {
        var positional = new List<string>();
        bool brief = false;
        foreach (string arg in args)
        {
            // По умолчанию выводится всё: и таблицы тестов, и тексты. --tests оставлен для совместимости.
            if (arg == "--tests" || arg == "-t") continue;
            if (arg == "--brief" || arg == "-b") brief = true;
            else if (arg.StartsWith('-')) return Usage(error, "Неизвестный параметр: " + arg);
            else positional.Add(arg);
        }
        if (positional.Count != 2) return Usage(error, null);

        string format = positional[0];
        IProblemParser? parser = registry.Find(format);
        if (parser == null) return Usage(error, $"Неизвестный формат '{format}'");

        try
        {
            using IPackageSource source = opener.Open(positional[1]);
            ProblemInfo info = parser.Parse(source);
            printer.Print(info, output, new PrintOptions(!brief, !brief));
            return ExitOk;
        }
        catch (ParseException e)
        {
            error.WriteLine("Ошибка разбора: " + e.Message);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            error.WriteLine("Ошибка чтения: " + e.Message);
        }
        return ExitError;
    }

    private int Usage(TextWriter error, string? problem)
    {
        if (problem != null) error.WriteLine("Ошибка: " + problem);
        error.WriteLine("Использование: Program [FORMAT] [PACKAGE] [--brief]");
        error.WriteLine("  FORMAT   формат пакета: " + string.Join(", ", registry.Formats));
        error.WriteLine("  PACKAGE  путь к ZIP-архиву пакета (можно и к распакованной папке)");
        error.WriteLine("  --brief  краткий вывод: без таблиц тестов и текстов условия (по умолчанию выводятся все данные)");
        return ExitUsage;
    }
}
