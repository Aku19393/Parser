using TaskParser.Pkg;

namespace TaskParser.Model;

/// <summary>
/// Разборщик одного формата пакета. Чтобы поддержать новый формат, достаточно написать
/// реализацию этого интерфейса и зарегистрировать её в <c>ParserRegistry</c>; остальной код менять не нужно.
/// </summary>
public interface IProblemParser
{
    /// <summary>Имя формата, которое пользователь указывает в командной строке (KRSU, CATS, ...).</summary>
    string FormatName { get; }

    /// <exception cref="ParseException">содержимое пакета не соответствует формату</exception>
    /// <exception cref="IOException">пакет не удалось прочитать</exception>
    ProblemInfo Parse(IPackageSource source);
}
