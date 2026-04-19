using Cob.Regressivo.CLI.Configuration;

namespace Cob.Regressivo.CLI.Core;

public static class TopologicalSorter
{
    public static List<StepConfig> Sort(List<StepConfig> steps)
    {
        var stepMap = steps.ToDictionary(s => s.Id);
        var inDegree = steps.ToDictionary(s => s.Id, _ => 0);
        var adjacency = steps.ToDictionary(s => s.Id, _ => new List<string>());

        foreach (var step in steps)
        {
            foreach (var dep in step.DependsOn)
            {
                if (!stepMap.ContainsKey(dep))
                    throw new InvalidOperationException(
                        $"Step '{step.Id}' depende de step desconhecido: '{dep}'.");
                adjacency[dep].Add(step.Id);
                inDegree[step.Id]++;
            }
        }

        var queue = new Queue<string>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        var sorted = new List<StepConfig>();

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            sorted.Add(stepMap[current]);
            foreach (var neighbor in adjacency[current])
                if (--inDegree[neighbor] == 0)
                    queue.Enqueue(neighbor);
        }

        if (sorted.Count != steps.Count)
            throw new InvalidOperationException("Dependência circular detectada nos steps do pipeline.");

        return sorted;
    }
}
