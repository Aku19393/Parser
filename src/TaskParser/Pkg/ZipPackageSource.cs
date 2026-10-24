using System.IO.Compression;

namespace TaskParser.Pkg;

/// <summary>Пакет в виде ZIP-архива. Содержимое не распаковывается на диск - файлы читаются прямо из архива.</summary>
public sealed class ZipPackageSource : IPackageSource
{
    private readonly ZipArchive zip;
    private readonly EntryIndex index;

    public ZipPackageSource(string file)
    {
        Name = Path.GetFileName(file);
        zip = ZipFile.OpenRead(file);
        index = new EntryIndex(zip.Entries.Where(e => e.Name.Length > 0).Select(e => e.FullName));
    }

    public string Name { get; }

    public IReadOnlyList<string> Files => index.Paths;

    public bool Exists(string path) => index.Contains(path);

    public Stream Open(string path)
    {
        string? raw = index.Raw(path);
        ZipArchiveEntry? entry = raw == null ? null : zip.GetEntry(raw);
        if (entry == null) throw new FileNotFoundException($"В пакете {Name} нет файла {path}");
        return entry.Open();
    }

    public void Dispose() => zip.Dispose();
}
