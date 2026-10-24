namespace TaskParser.Model;

/// <summary>Один тест задачи.</summary>
/// <param name="Index">порядковый номер теста (с 1)</param>
/// <param name="Input">имя входного файла или описание источника (например, генератор); может быть null</param>
/// <param name="Output">имя файла с ответом; может быть null</param>
/// <param name="Points">баллы за тест; null, если формат их не задаёт</param>
/// <param name="Comment">прочие сведения о тесте из формата (способ создания, команда генератора, признак примера и т.д.); может быть null</param>
public sealed record TestCase(int Index, string? Input, string? Output, int? Points, string? Comment = null);
