using System.Collections.Concurrent;

namespace Issue.Api.Services;

public class ImportNotFoundCache
{
    private readonly ConcurrentDictionary<string, HashSet<string>> _sets = new(StringComparer.Ordinal);

    public void RememberIssues(IEnumerable<long> ids) =>
        _sets["issue"] = ids.Select(x => x.ToString()).ToHashSet(StringComparer.Ordinal);

    public bool TryGetIssues(out HashSet<long> ids)
    {
        if (_sets.TryGetValue("issue", out var raw))
        {
            ids = raw.Select(long.Parse).ToHashSet();
            return true;
        }
        ids = [];
        return false;
    }

    public void RememberIssueCategories(IEnumerable<long> ids, long countersignId, long doneId)
    {
        _sets["issue-cat"] = ids.Select(x => x.ToString()).ToHashSet(StringComparer.Ordinal);
        _sets["issue-cat-subs"] = [$"{countersignId}:{doneId}"];
    }

    public bool TryGetIssueCategories(out HashSet<long> ids, out long countersignId, out long doneId)
    {
        ids = [];
        countersignId = 0;
        doneId = 0;
        if (!_sets.TryGetValue("issue-cat", out var raw) || !_sets.TryGetValue("issue-cat-subs", out var subs))
        {
            return false;
        }
        ids = raw.Select(long.Parse).ToHashSet();
        var pair = subs.FirstOrDefault()?.Split(':') ?? [];
        if (pair.Length != 2 || !long.TryParse(pair[0], out countersignId) || !long.TryParse(pair[1], out doneId))
        {
            return false;
        }
        return true;
    }

    public void RememberProject(long projectId, IEnumerable<(string Kind, long Id)> keys) =>
        _sets[$"project:{projectId}"] = keys.Select(Key).ToHashSet(StringComparer.Ordinal);

    public bool TryGetProject(long projectId, out HashSet<string> keys)
    {
        if (_sets.TryGetValue($"project:{projectId}", out var raw))
        {
            keys = raw;
            return true;
        }
        keys = [];
        return false;
    }

    public static string Key(string kind, long id) => $"{kind}:{id}";

    public static string Key((string Kind, long Id) item) => Key(item.Kind, item.Id);
}
