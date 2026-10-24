using System.Text;

namespace TaskParser.Pkg;

/// <summary>
/// Доступ к файлам пакета «только для чтения». Парсеры зависят только от этого интерфейса
/// и не знают, где физически лежит пакет (ZIP, папка, память в тестах).
/// Пути всегда относительные, с разделителем '/'.
/// </summary>
public interface IPackageSource : IDisposable
{
    /// <summary>Имя пакета для сообщений об ошибках.</summary>
    string Name { get; }

    /// <summary>Все файлы пакета (без каталогов), отсортированные.</summary>
    IReadOnlyList<string> Files { get; }

    bool Exists(string path);

    Stream Open(string path);

    /// <summary>Читает файл как UTF-8 текст (BOM отбрасывается).</summary>
    string ReadText(string path)
    {
        using Stream input = Open(path);
        using var buffer = new MemoryStream();
        input.CopyTo(buffer);
        string text = new UTF8Encoding(false).GetString(buffer.ToArray());
        return text.StartsWith('\uFEFF') ? text.Substring(1) : text;
    }
}
