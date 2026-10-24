namespace TaskParser.Model;

/// <summary>Пакет прочитан, но его содержимое не соответствует ожидаемому формату.</summary>
public class ParseException : Exception
{
    public ParseException(string message) : base(message) { }

    public ParseException(string message, Exception inner) : base(message, inner) { }
}
