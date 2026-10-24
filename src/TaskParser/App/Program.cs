using System.Text;
using TaskParser.Output;
using TaskParser.Parsers.Cats;
using TaskParser.Parsers.Ejudge;
using TaskParser.Parsers.Krsu;
using TaskParser.Parsers.Pcms2;
using TaskParser.Pkg;

namespace TaskParser.App;

/// <summary>
/// Точка входа и единственное место, где собираются конкретные классы.
/// Чтобы добавить новый формат, достаточно написать парсер и добавить сюда одну строку Register(...).
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        // Вывод всегда в UTF-8, чтобы результат не зависел от кодировки консоли по умолчанию.
        var utf8 = new UTF8Encoding(false);

        // В окне консоли Windows по умолчанию кодировка 866, и русский текст превращается в «кракозябры».
        // Поэтому на время работы переключаем консоль в UTF-8 и в конце возвращаем как было.
        // Если вывод перенаправлен в файл или канал, консоль не трогаем.
        Encoding? originalConsoleEncoding = null;
        if (OperatingSystem.IsWindows() && !Console.IsOutputRedirected)
        {
            try
            {
                originalConsoleEncoding = Console.OutputEncoding;
                Console.OutputEncoding = utf8;
            }
            catch (IOException)
            {
                originalConsoleEncoding = null;   // консоли нет (например, запуск без окна) - просто пишем UTF-8
            }
        }
        using var output = new StreamWriter(Console.OpenStandardOutput(), utf8) { AutoFlush = true };
        using var error = new StreamWriter(Console.OpenStandardError(), utf8) { AutoFlush = true };

        var registry = new ParserRegistry()
            .Register(new KrsuParser())
            .Register(new CatsParser())
            .Register(new Pcms2Parser())
            .Register(new EjudgeParser());

        var app = new Application(registry, new FilePackageOpener(), new ConsoleProblemPrinter());
        try
        {
            return app.Run(args, output, error);
        }
        catch (Exception e)
        {
            // Сюда попадают только настоящие ошибки программы; пользователю показываем коротко, без стека.
            error.WriteLine("Внутренняя ошибка: " + e.Message);
            return Application.ExitError;
        }
        finally
        {
            if (originalConsoleEncoding != null)
            {
                try { Console.OutputEncoding = originalConsoleEncoding; }
                catch (IOException) { /* возвращать нечего */ }
            }
        }
    }
}
