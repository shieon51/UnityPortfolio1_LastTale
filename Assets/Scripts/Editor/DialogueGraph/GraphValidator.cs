// GraphValidator.cs (신규, Editor 폴더)
using System.Collections.Generic;
using System.Linq;

public class ValidationIssue
{
    public enum Severity { Error, Warning }
    public Severity severity;
    public string nodeGuid;
    public string message;
}

public static class GraphValidator
{
    // 판넬 4줄 / 말풍선 3줄 기준의 근사치. 정확한 측정은 TMP 폰트로 대체 예정
    private const int PanelCharLimit = 175;
    private const int BubbleCharLimit = 80;

    public static List<ValidationIssue> Validate(DialogueGraphData asset)
    {
        var issues = new List<ValidationIssue>();
        if (asset == null) return issues;

        var nodeById = asset.nodes.ToDictionary(n => n.guid, n => n);
        var memoryIds = new HashSet<string>(GraphKeySource.GetMemoryFlagIds());
        var npcNames = new HashSet<string>(GraphKeySource.GetNPCNames());
        var counterIds = new HashSet<string>(GraphKeySource.GetCounterIds());
        var cueIds = new HashSet<string>(GraphKeySource.GetCueIds());

        // 1) Knot 이름 검사
        var startNodes = asset.nodes.Where(n => n.nodeType == "Start").ToList();
        if (startNodes.Count == 0)
            issues.Add(new ValidationIssue { severity = ValidationIssue.Severity.Error, message = "시작(Knot) 노드가 하나도 없습니다." });

        var knotNames = new HashSet<string>();
        foreach (var s in startNodes)
        {
            if (string.IsNullOrWhiteSpace(s.knotName))
                issues.Add(Err(s.guid, "시작 노드의 Knot 이름이 비어 있습니다."));
            else if (!knotNames.Add(s.knotName))
                issues.Add(Err(s.guid, $"Knot 이름이 중복됩니다: '{s.knotName}'"));
            else if (s.knotName.Contains(' '))
                issues.Add(Err(s.guid, $"Knot 이름에 공백이 있습니다: '{s.knotName}'"));
        }

        // 2) 연결 안 된 출력 포트
        foreach (var n in asset.nodes)
        {
            int expected = n.nodeType switch
            {
                "Start" => 1,
                "Line" => 1,
                "Choice" => n.choiceOptions.Count,
                "Branch" => n.branchCases.Count + 1,
                _ => 0,
            };
            for (int i = 0; i < expected; i++)
            {
                if (asset.edges.Any(e => e.fromGuid == n.guid && e.fromPortIndex == i)) continue;
                string portName = n.nodeType switch
                {
                    "Choice" => $"선택 {i + 1}('{(i < n.choiceOptions.Count ? n.choiceOptions[i].text : "?")}')",
                    "Branch" => i < n.branchCases.Count ? $"분기 {i + 1}" : "그 외(else)",
                    _ => "다음",
                };
                issues.Add(Warn(n.guid, $"{Desc(n)} — {portName} 포트가 연결되지 않았습니다. (DONE으로 종료됨)"));
            }
        }

        // 3) 도달 불가 노드
        var reachable = new HashSet<string>();
        foreach (var s in startNodes) Traverse(s.guid, asset, reachable);
        foreach (var n in asset.nodes)
        {
            if (n.nodeType == "Start" || reachable.Contains(n.guid)) continue;
            issues.Add(Warn(n.guid, $"{Desc(n)} — 어떤 시작 노드에서도 도달할 수 없습니다."));
        }

        // 4) 내용이 빈 노드
        foreach (var n in asset.nodes)
        {
            if (n.nodeType == "Line")
            {
                if (n.lines.Count == 0 || n.lines.All(l => string.IsNullOrWhiteSpace(l.text)))
                    issues.Add(Warn(n.guid, $"{Desc(n)} — 대사 내용이 비어 있습니다."));
            }
            else if (n.nodeType == "Choice")
            {
                foreach (var opt in n.choiceOptions.Where(o => string.IsNullOrWhiteSpace(o.text)))
                    issues.Add(Warn(n.guid, $"{Desc(n)} — 내용이 빈 선택지가 있습니다."));
            }
        }

        // 5) 키 유효성
        foreach (var n in asset.nodes)
        {
            foreach (var line in n.lines)
            {
                if (!string.IsNullOrEmpty(line.cueId) && !cueIds.Contains(line.cueId))
                    issues.Add(Err(n.guid, $"{Desc(n)} — 등록되지 않은 연출 큐: '{line.cueId}'"));
                if (!string.IsNullOrEmpty(line.speakerKey) && line.speakerKey != "Player" && !npcNames.Contains(line.speakerKey))
                    issues.Add(Err(n.guid, $"{Desc(n)} — 등록되지 않은 화자: '{line.speakerKey}'"));
                foreach (var l in line.logics) CheckKey(issues, n, l.varType, l.key, memoryIds, npcNames, counterIds);
            }
            foreach (var bc in n.branchCases)
                foreach (var c in bc.condition.entries) CheckKey(issues, n, c.varType, c.key, memoryIds, npcNames, counterIds);
            foreach (var opt in n.choiceOptions)
                foreach (var c in opt.condition.entries) CheckKey(issues, n, c.varType, c.key, memoryIds, npcNames, counterIds);
        }

        // 6) 조건이 비어있는 분기
        foreach (var n in asset.nodes.Where(x => x.nodeType == "Branch"))
            for (int i = 0; i < n.branchCases.Count; i++)
                if (n.branchCases[i].condition.entries.Count == 0)
                    issues.Add(Warn(n.guid, $"{Desc(n)} — 분기 {i + 1}에 조건이 없습니다. (항상 참으로 처리됨)"));

        // 7) 대사 길이 — 화자가 있고 패널 강제가 아니면 말풍선, 아니면 판넬
        foreach (var n in asset.nodes)
        {
            foreach (var line in n.lines)
            {
                if (string.IsNullOrWhiteSpace(line.text)) continue;
                bool isBubble = !line.isSystem && !line.forcePanel && !string.IsNullOrEmpty(line.speakerKey);
                int limit = isBubble ? BubbleCharLimit : PanelCharLimit;
                if (line.text.Length > limit)
                    issues.Add(Warn(n.guid, $"{Desc(n)} — {(isBubble ? "말풍선" : "판넬")} 한도({limit}자)를 넘습니다: {line.text.Length}자"));
            }
        }

        // 8) 주제 단계 순서 — 앞 단계 조건 없이 뒷 단계를 획득시키면 경고
        foreach (var topic in GraphKeySource.GetTopicStages())
        {
            for (int stage = 1; stage < topic.Value.Count; stage++)
            {
                string flag = topic.Value[stage];
                foreach (var n in asset.nodes)
                {
                    bool grants = n.lines.Any(l => l.logics.Any(g => g.varType == GraphVarType.Memory && !g.isErase && g.key == flag));
                    if (!grants) continue;

                    string prev = topic.Value[stage - 1];
                    bool guarded = n.lines.Any(l => l.logics.Any(g => g.key == prev))
                        || asset.nodes.Any(x => x.branchCases.Any(b => b.condition.entries.Any(c => c.key == prev))
                                             || x.choiceOptions.Any(o => o.condition.entries.Any(c => c.key == prev)));
                    if (!guarded)
                        issues.Add(Warn(n.guid, $"{Desc(n)} — '{topic.Key}' 주제의 {stage + 1}단계('{flag}')를 주는데 앞 단계('{prev}') 조건이 어디에도 없습니다. 순서를 건너뛸 수 있습니다."));
                }
            }
        }

        return issues;
    }

    private static void CheckKey(List<ValidationIssue> issues, GraphNodeData n, GraphVarType type, string key,
        HashSet<string> memories, HashSet<string> npcs, HashSet<string> counters)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            if (ConditionUtil.NeedsKey(type)) issues.Add(Err(n.guid, $"{Desc(n)} — {ConditionUtil.GetVarLabel(type)} 항목의 대상이 비어 있습니다."));
            return;
        }

        bool valid = type switch
        {
            GraphVarType.Memory => memories.Contains(key),
            GraphVarType.Counter => counters.Count == 0 || counters.Contains(key), // 애셋이 없으면 자유 입력 허용
            GraphVarType.MentalPercent => true,
            _ => npcs.Contains(key),
        };
        if (!valid)
            issues.Add(Err(n.guid, $"{Desc(n)} — 등록되지 않은 {ConditionUtil.GetVarLabel(type)} 키: '{key}'"));
    }

    private static void Traverse(string guid, DialogueGraphData asset, HashSet<string> visited)
    {
        if (!visited.Add(guid)) return;
        foreach (var e in asset.edges.Where(e => e.fromGuid == guid))
            Traverse(e.toGuid, asset, visited);
    }

    private static string Desc(GraphNodeData n)
    {
        string label = n.nodeType switch
        {
            "Start" => $"시작 '{n.knotName}'",
            "Line" => n.lines.Count > 0 && !string.IsNullOrWhiteSpace(n.lines[0].text)
                ? $"대사 '{Truncate(n.lines[0].text)}'" : "대사(빈 노드)",
            "Choice" => n.choiceOptions.Count > 0 ? $"선택지 '{Truncate(n.choiceOptions[0].text)}'" : "선택지",
            "Branch" => "조건 분기",
            _ => n.nodeType,
        };
        return label;
    }

    private static string Truncate(string s) => s.Length <= 12 ? s : s.Substring(0, 12) + "…";

    private static ValidationIssue Err(string guid, string msg)
        => new() { severity = ValidationIssue.Severity.Error, nodeGuid = guid, message = msg };
    private static ValidationIssue Warn(string guid, string msg)
        => new() { severity = ValidationIssue.Severity.Warning, nodeGuid = guid, message = msg };
}