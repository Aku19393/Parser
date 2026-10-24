namespace TaskParser.Model;

/// <summary>Группа тестов (подзадача).</summary>
public sealed class TestGroup
{
    /// <summary>Идентификатор группы в терминах исходного формата.</summary>
    public string Id { get; }

    /// <summary>Человекочитаемое имя; может быть null.</summary>
    public string? Name { get; }

    /// <summary>Баллы за группу; null, если не заданы.</summary>
    public int? Points { get; }

    /// <summary>То, от чего зависит группа (идентификаторы групп либо, для CATS, имена наборов).</summary>
    public IReadOnlyList<string> Prerequisites { get; }

    /// <summary>Номера тестов, входящих в группу.</summary>
    public IReadOnlyList<int> Tests { get; }

    /// <summary>Дополнительные параметры группы в свободной форме; может быть null.</summary>
    public string? Note { get; }

    public TestGroup(string id, string? name, int? points,
                     IEnumerable<string> prerequisites, IEnumerable<int> tests, string? note)
    {
        Id = id;
        Name = name;
        Points = points;
        Prerequisites = prerequisites.ToList().AsReadOnly();
        Tests = tests.ToList().AsReadOnly();
        Note = note;
    }
}
