using System.IO.Compression;

namespace TaskParser.Pkg;

/// <summary>Реализация <see cref="IPackageOpener"/> по умолчанию: ZIP-файл или папка.</summary>
public sealed class FilePackageOpener : IPackageOpener
{
    public IPackageSource Open(string path)
    {
        if (Directory.Exists(path)) return new DirectoryPackageSource(path);
        if (!File.Exists(path)) throw new FileNotFoundException($"Пакет не найден: {path}");
        try
        {
            return new ZipPackageSource(path);
        }
        catch (InvalidDataException e)
        {
            throw new IOException($"Файл не является корректным ZIP-архивом: {path} ({e.Message})", e);
        }
    }
}
