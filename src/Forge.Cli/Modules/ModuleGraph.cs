namespace Forge.Cli.Modules;

public static class ModuleGraph
{
    /// <summary>
    /// Validates the module graph: unique ids, known dependencies, no cycles,
    /// no database schema owned by more than one module (ADR 03).
    /// Returns a deterministic, ordered list of errors; empty means valid.
    /// </summary>
    public static IReadOnlyList<string> Validate(IReadOnlyList<ModuleManifest> manifests)
    {
        var errors = new List<string>();

        foreach (var dup in manifests.GroupBy(m => m.Id, StringComparer.Ordinal).Where(g => g.Count() > 1))
        {
            errors.Add($"duplicate module id '{dup.Key}'");
        }

        var byId = manifests.GroupBy(m => m.Id, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        foreach (var m in manifests.OrderBy(m => m.Id, StringComparer.Ordinal))
        {
            foreach (var dep in m.Dependencies.Order(StringComparer.Ordinal))
            {
                if (!byId.ContainsKey(dep))
                {
                    errors.Add($"module '{m.Id}' depends on unknown module '{dep}'");
                }
            }
        }

        foreach (var dup in manifests
            .SelectMany(m => m.OwnedSchemas.Select(s => (Schema: s, m.Id)))
            .GroupBy(x => x.Schema, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() > 1)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            var owners = string.Join(", ", dup.Select(x => $"'{x.Id}'").Order(StringComparer.Ordinal));
            errors.Add($"schema '{dup.Key}' owned by more than one module: {owners}");
        }

        errors.AddRange(FindCycles(byId));
        return errors;
    }

    /// <summary>Modules in dependency order (dependencies before dependents), ties broken by id.</summary>
    public static IReadOnlyList<ModuleManifest> TopologicalSort(IReadOnlyList<ModuleManifest> manifests)
    {
        if (Validate(manifests) is { Count: > 0 } errors)
        {
            throw new InvalidOperationException("invalid module graph: " + string.Join("; ", errors));
        }

        var byId = manifests.ToDictionary(m => m.Id, StringComparer.Ordinal);
        var result = new List<ModuleManifest>();
        var visited = new HashSet<string>(StringComparer.Ordinal);

        void Visit(ModuleManifest m)
        {
            if (!visited.Add(m.Id))
            {
                return;
            }

            foreach (var dep in m.Dependencies.Order(StringComparer.Ordinal))
            {
                Visit(byId[dep]);
            }

            result.Add(m);
        }

        foreach (var m in manifests.OrderBy(m => m.Id, StringComparer.Ordinal))
        {
            Visit(m);
        }

        return result;
    }

    private static List<string> FindCycles(Dictionary<string, ModuleManifest> byId)
    {
        var state = new Dictionary<string, int>(StringComparer.Ordinal); // 0 unvisited, 1 in progress, 2 done
        var reported = new List<string>();

        void Visit(string id, Stack<string> path)
        {
            if (!byId.TryGetValue(id, out var m) || state.GetValueOrDefault(id) == 2)
            {
                return;
            }

            if (state.GetValueOrDefault(id) == 1)
            {
                var cycle = path.Reverse().SkipWhile(p => p != id).Append(id);
                reported.Add($"dependency cycle: {string.Join(" -> ", cycle)}");
                return;
            }

            state[id] = 1;
            path.Push(id);
            foreach (var dep in m.Dependencies.Order(StringComparer.Ordinal))
            {
                Visit(dep, path);
            }

            path.Pop();
            state[id] = 2;
        }

        foreach (var id in byId.Keys.Order(StringComparer.Ordinal))
        {
            Visit(id, new Stack<string>());
        }

        return reported;
    }
}
