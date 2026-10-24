namespace TaskParser.Pkg;

/// <summary>Умеет открыть пакет по пути. Отдельный интерфейс нужен, чтобы приложение не зависело от ZIP.</summary>
public interface IPackageOpener
{
    IPackageSource Open(string path);
}
