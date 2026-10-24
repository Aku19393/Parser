namespace TaskParser.Output;

/// <summary>Настройки вывода: ShowTests - таблицы тестов, ShowText - длинные тексты (условие задачи).</summary>
public sealed record PrintOptions(bool ShowTests, bool ShowText = true);
