namespace TaskParser.Parsers.Ejudge;

/// <summary>Содержимое valuer.cfg.</summary>
/// <param name="GlobalOptions">параметры блока global в текстовом виде ("stat_to_users 1")</param>
/// <param name="Groups">группы тестов в порядке описания</param>
internal sealed record ValuerConfig(List<string> GlobalOptions, List<ValuerGroup> Groups);

/// <summary>Группа тестов из valuer.cfg.</summary>
/// <param name="Id">номер группы</param>
/// <param name="Tests">номера тестов группы (из "tests A-B;")</param>
/// <param name="Score">"score N;" - баллы за группу целиком; null, если не задано</param>
/// <param name="TestScore">"test_score N;" - баллы за каждый тест; null, если не задано</param>
/// <param name="Requires">группы, от которых зависит данная ("requires ...;")</param>
/// <param name="Flags">остальные параметры в текстовом виде ("offline", "pass_if_count 3", ...)</param>
internal sealed record ValuerGroup(string Id, List<int> Tests, int? Score, int? TestScore,
                                   List<string> Requires, List<string> Flags);
