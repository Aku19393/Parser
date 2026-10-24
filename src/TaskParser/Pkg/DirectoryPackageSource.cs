namespace TaskParser.Pkg;

/// <summary>Уже распакованный пакет (папка на диске). Удобно для отладки.</summary>
public sealed class DirectoryPackageSource : IPackageSource
{
    private readonly string root;
    private readonly EntryIndex index;

    public DirectoryPackageSource(string root)
    {
        this.root = root;
        IEnumerable<string> names = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(root, p));
        index = new EntryIndex(names);
    }

    public string Name
    {
        get
        {
            string trimmed = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string name = Path.GetFileName(trimmed);
            return name.Length == 0 ? root : name;
        }
    }

    public IReadOnlyList<string> Files => index.Paths;

    public bool Exists(string path) => index.Contains(path);

    public Stream Open(string path)
    {
        string? raw = index.Raw(path);
        if (raw == null) throw new FileNotFoundException($"В папке {root} нет файла {path}");
        return File.OpenRead(Path.Combine(root, raw));
    }

    public void Dispose() { }
}
