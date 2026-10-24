namespace TaskParser.Model;

/// <summary>Именованный блок строк (список файлов, примеры, текст условия и т.п.).</summary>
/// <param name="Title">заголовок блока</param>
/// <param name="Lines">строки блока</param>
/// <param name="IsText">true для длинного текста (условие задачи); при краткой выдаче такие блоки пропускаются</param>
public sealed record InfoSection(string Title, IReadOnlyList<string> Lines, bool IsText = false);
