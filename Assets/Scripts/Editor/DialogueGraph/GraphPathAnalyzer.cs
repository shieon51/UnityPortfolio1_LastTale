// GraphPathAnalyzer.cs (신규, Editor 폴더)
using System.Collections.Generic;
using System.Linq;
using System.Text;

public class PathResult
{
    public List<string> steps = new();                      // 지나온 경로 설명
    public Dictionary<string, int> deltas = new();           // 변수별 총 변화량
    public List<string> acquiredMemories = new();
    public bool truncated;                                   // 깊이 제한에 걸림
}

public static class GraphPathAnalyzer
{
    private const int MaxPaths = 40;
    private const int MaxDepth = 60;

    public static List<PathResult> Trace(DialogueGraphData asset, string startGuid)
    {
        var results = new List<PathResult>();
        var startNode = asset.nodes.FirstOrDefault(n => n.guid == startGuid);
        if (startNode == null) return results;

        Walk(asset, startNode, new PathResult(), new HashSet<string>(), results, 0);
        return results;
    }

    private static void Walk(DialogueGraphData asset, GraphNodeData node, PathResult current,
        HashSet<string> visited, List<PathResult> results, int depth)
    {
        if (results.Count >= MaxPaths) return;
        if (node == null || depth > MaxDepth) { current.truncated = true; results.Add(current); return; }
        if (!visited.Add(node.guid)) { results.Add(current); return; } // 루프 방지

        switch (node.nodeType)
        {
            case "Start":
                current.steps.Add($"▶ {node.knotName}");
                Walk(asset, Next(asset, node.guid, 0), current, visited, results, depth + 1);
                break;

            case "Line":
                foreach (var line in node.lines)
                {
                    if (!string.IsNullOrWhiteSpace(line.text))
                        current.steps.Add($"  \"{Trunc(line.text)}\"");
                    foreach (var l in line.logics) ApplyLogic(current, l);
                }
                Walk(asset, Next(asset, node.guid, 0), current, visited, results, depth + 1);
                break;

            case "Branch":
                for (int i = 0; i <= node.branchCases.Count; i++)
                {
                    string label = i < node.branchCases.Count
                        ? $"[조건: {ConditionUtil.BuildSummary(node.branchCases[i].condition)}]"
                        : "[그 외]";
                    Fork(asset, node, i, label, current, visited, results, depth);
                }
                return;

            case "Choice":
                for (int i = 0; i < node.choiceOptions.Count; i++)
                {
                    var opt = node.choiceOptions[i];
                    string cond = opt.condition.entries.Count > 0
                        ? $" (조건: {ConditionUtil.BuildSummary(opt.condition)})" : "";
                    Fork(asset, node, i, $"→ 선택 \"{Trunc(opt.text)}\"{cond}", current, visited, results, depth);
                }
                return;
        }
    }

    private static void Fork(DialogueGraphData asset, GraphNodeData node, int port, string label,
        PathResult current, HashSet<string> visited, List<PathResult> results, int depth)
    {
        var branch = Clone(current);
        branch.steps.Add(label);
        Walk(asset, Next(asset, node.guid, port), branch, new HashSet<string>(visited), results, depth + 1);
    }

    private static void ApplyLogic(PathResult r, GraphLogicEntry l)
    {
        if (l.varType == GraphVarType.Memory)
        {
            r.acquiredMemories.Add(l.isErase ? $"-{l.key}" : $"+{l.key}");
            return;
        }
        string label = $"{l.key} {ConditionUtil.GetVarLabel(l.varType)}";
        int amount = l.varType == GraphVarType.Counter ? 1 : l.amount;
        r.deltas.TryGetValue(label, out int cur);
        r.deltas[label] = cur + amount;
    }

    private static PathResult Clone(PathResult src) => new()
    {
        steps = new List<string>(src.steps),
        deltas = new Dictionary<string, int>(src.deltas),
        acquiredMemories = new List<string>(src.acquiredMemories),
    };

    private static GraphNodeData Next(DialogueGraphData asset, string guid, int port)
    {
        var edge = asset.edges.FirstOrDefault(e => e.fromGuid == guid && e.fromPortIndex == port);
        return edge == null ? null : asset.nodes.FirstOrDefault(n => n.guid == edge.toGuid);
    }

    private static string Trunc(string s) => s.Length <= 24 ? s : s.Substring(0, 24) + "…";

    public static string Format(List<PathResult> paths)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"총 {paths.Count}개 경로{(paths.Any(p => p.truncated) ? " (일부는 깊이 제한으로 잘림)" : "")}\n");

        for (int i = 0; i < paths.Count; i++)
        {
            sb.AppendLine($"── 경로 {i + 1} ──");
            foreach (var s in paths[i].steps) sb.AppendLine(s);

            if (paths[i].deltas.Count > 0)
            {
                var parts = paths[i].deltas.Select(d => $"{d.Key} {(d.Value >= 0 ? "+" : "")}{d.Value}");
                sb.AppendLine($"  ▷ 수치 변화: {string.Join(", ", parts)}");
            }
            if (paths[i].acquiredMemories.Count > 0)
                sb.AppendLine($"  ▷ 정보: {string.Join(", ", paths[i].acquiredMemories)}");
            sb.AppendLine();
        }
        return sb.ToString();
    }
}