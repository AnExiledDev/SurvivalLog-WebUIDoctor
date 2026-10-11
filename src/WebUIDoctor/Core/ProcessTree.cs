using System;
using System.Collections.Generic;
using System.Linq;

namespace WebUIDoctor.Core;

public readonly record struct ProcessEntry(int Pid, int ParentPid, string ExeName);

public static class ProcessTree
{
    /// <summary>
    /// Every process below <paramref name="rootPid"/> (children, grandchildren, ...) whose exe name matches.
    /// Non-matching processes are still walked through, so a match under a non-matching parent is found.
    /// </summary>
    public static IReadOnlyList<int> DescendantsNamed(IReadOnlyList<ProcessEntry> table, int rootPid, string exeName)
    {
        var childrenByParent = table
            .Where(p => p.Pid != p.ParentPid)
            .GroupBy(p => p.ParentPid)
            .ToDictionary(g => g.Key, g => g.ToList());
        var found = new List<int>();
        var visited = new HashSet<int> { rootPid };
        var frontier = new Queue<int>();

        frontier.Enqueue(rootPid);

        while (frontier.Count > 0)
        {
            var parent = frontier.Dequeue();

            if (!childrenByParent.TryGetValue(parent, out var children)) continue;

            foreach (var child in children)
            {
                if (!visited.Add(child.Pid)) continue;

                frontier.Enqueue(child.Pid);

                if (string.Equals(child.ExeName, exeName, StringComparison.OrdinalIgnoreCase)) found.Add(child.Pid);
            }
        }

        return found;
    }
}
