using TaskParser.Model;

namespace TaskParser.Output;

/// <summary>Выводит информацию о задаче. Отделён от разбора, чтобы можно было добавить другой вывод (JSON, HTML).</summary>
public interface IProblemPrinter
{
    void Print(ProblemInfo info, TextWriter output, PrintOptions options);
}
