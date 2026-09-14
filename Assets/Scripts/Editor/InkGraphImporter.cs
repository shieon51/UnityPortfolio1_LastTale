// InkGraphImporter.cs (신규, Editor 폴더)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class InkGraphImporter
{
    private static readonly Regex KnotRx = new(@"^\s*===\s*(\w+)\s*===");
    private static readonly Regex ChoiceRx = new(@"^\s*\+\s*(?:\{(.+?)\}\s*)?(.*)$");
    private static readonly Regex DivertRx = new(@"^\s*->\s*(\w+)");
    private static readonly Regex LogicRx = new(@"^\s*~\s*(\w+)\s*\((.*)\)");
    private static readonly Regex TagRx = new(@"#(\w+)(?::([^\s#]+))?(?::([^\s#]+))?");

    public static void Import(DialogueGraphData target, string inkPath, List<string> warnings)
    {
        string[] lines = File.ReadAllLines(inkPath);
        string fileName = Path.GetFileNameWithoutExtension(inkPath);

        GraphNodeData currentStart = null;
        GraphNodeData currentLine = null;
        GraphNodeData pendingChoice = null;
        var knotNodes = new Dictionary<string, GraphNodeData>();
        var pendingDiverts = new List<(GraphNodeData from, int port, string targetKnot)>();
        float y = 0f;

        void FlushLine() { currentLine = null; }

        for (int i = 0; i < lines.Length; i++)
        {
            string raw = lines[i];
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("//")) continue;

            // === knot ===
            var knotMatch = KnotRx.Match(line);
            if (knotMatch.Success)
            {
                string knotName = knotMatch.Groups[1].Value;
                currentStart = new GraphNodeData
                {
                    guid = Guid.NewGuid().ToString(),
                    nodeType = "Start",
                    knotName = knotName,
                    note = $"{fileName}.ink에서 가져옴",
                    position = new Vector2(0, y),
                };
                target.nodes.Add(currentStart);
                knotNodes[knotName] = currentStart;
                y += 320f;
                FlushLine();
                pendingChoice = null;
                continue;
            }

            if (currentStart == null) continue; // knot 밖의 내용은 무시

            // + 선택지
            var choiceMatch = ChoiceRx.Match(line);
            if (choiceMatch.Success && line.StartsWith("+"))
            {
                if (pendingChoice == null)
                {
                    pendingChoice = NewNode(target, "Choice", ref y);
                    Connect(target, currentLine ?? currentStart, 0, pendingChoice);
                    FlushLine();
                }
                var opt = new ChoiceOption { text = choiceMatch.Groups[2].Value.Trim() };
                string cond = choiceMatch.Groups[1].Value;
                if (!string.IsNullOrWhiteSpace(cond) && !TryParseCondition(cond, opt.condition))
                    warnings.Add($"[{fileName}:{i + 1}] 선택지 조건을 해석하지 못했습니다: {{{cond}}}");
                pendingChoice.choiceOptions.Add(opt);
                continue;
            }

            // -> 다이버트
            var divertMatch = DivertRx.Match(line);
            if (divertMatch.Success)
            {
                string targetKnot = divertMatch.Groups[1].Value;
                if (targetKnot == "DONE" || targetKnot == "END") { pendingChoice = null; FlushLine(); continue; }

                var from = pendingChoice ?? currentLine ?? currentStart;
                int port = pendingChoice != null ? pendingChoice.choiceOptions.Count - 1 : 0;
                pendingDiverts.Add((from, port, targetKnot));
                continue;
            }

            // ~ 로직
            var logicMatch = LogicRx.Match(line);
            if (logicMatch.Success)
            {
                if (currentLine == null || currentLine.lines.Count == 0)
                {
                    warnings.Add($"[{fileName}:{i + 1}] 대사 없이 등장한 로직은 건너뜁니다: {line}");
                    continue;
                }
                var entry = ParseLogic(logicMatch.Groups[1].Value, logicMatch.Groups[2].Value);
                if (entry != null) currentLine.lines[^1].logics.Add(entry);
                else warnings.Add($"[{fileName}:{i + 1}] 알 수 없는 함수: {logicMatch.Groups[1].Value}");
                continue;
            }

            // { 조건 블록 — 자동 변환 불가
            if (line.StartsWith("{") || line.StartsWith("- "))
            {
                warnings.Add($"[{fileName}:{i + 1}] 조건 블록은 수동으로 분기 노드를 만들어야 합니다: {line}");
                continue;
            }

            // 일반 대사
            if (pendingChoice != null) { pendingChoice = null; FlushLine(); }
            if (currentLine == null)
            {
                currentLine = NewNode(target, "Line", ref y);
                Connect(target, currentStart, 0, currentLine);
            }
            currentLine.lines.Add(ParseDialogueLine(line, currentStart));
        }

        // 다이버트 연결 (모든 knot이 등록된 뒤에)
        foreach (var (from, port, targetKnot) in pendingDiverts)
        {
            if (!knotNodes.TryGetValue(targetKnot, out var to))
            {
                warnings.Add($"[{fileName}] 대상 knot을 찾지 못했습니다: -> {targetKnot}");
                continue;
            }
            Connect(target, from, port, to);
        }
    }

    private static GraphNodeData NewNode(DialogueGraphData target, string type, ref float y)
    {
        var node = new GraphNodeData
        {
            guid = Guid.NewGuid().ToString(),
            nodeType = type,
            position = new Vector2(600, y),
        };
        target.nodes.Add(node);
        y += 320f;
        return node;
    }

    private static void Connect(DialogueGraphData target, GraphNodeData from, int port, GraphNodeData to)
    {
        if (from == null || to == null) return;
        target.edges.Add(new GraphEdgeData { fromGuid = from.guid, fromPortIndex = port, toGuid = to.guid });
    }

    private static DialogueLine ParseDialogueLine(string line, GraphNodeData owner)
    {
        var result = new DialogueLine();
        string text = line;

        foreach (Match m in TagRx.Matches(line))
        {
            string tag = m.Groups[1].Value;
            string a1 = m.Groups[2].Value;
            string a2 = m.Groups[3].Value;

            switch (tag)
            {
                case "speak":
                    result.speakerKey = a1;
                    result.speakerName = a2;
                    if (string.IsNullOrEmpty(owner.npcTag)) owner.npcTag = a1;
                    break;
                case "system": result.isSystem = true; break;
                case "panel": result.forcePanel = true; break;
                case "lockinput": result.lockInput = true; break;
                case "cue": result.cueId = a1; break;
                case "auto":
                    if (float.TryParse(a1, out float sec)) result.autoAdvance = sec;
                    break;
            }
            text = text.Replace(m.Value, "");
        }

        result.text = text.TrimStart('-', ' ').Trim();
        return result;
    }

    private static GraphLogicEntry ParseLogic(string func, string args)
    {
        var parts = args.Split(',').Select(s => s.Trim().Trim('"')).ToArray();
        if (parts.Length == 0) return null;

        int amount = parts.Length > 1 && int.TryParse(parts[1], out int v) ? v : 1;

        return func switch
        {
            "acquire_memory" => new GraphLogicEntry { varType = GraphVarType.Memory, key = parts[0] },
            "erase_memory" => new GraphLogicEntry { varType = GraphVarType.Memory, key = parts[0], isErase = true },
            "increment_counter" => new GraphLogicEntry { varType = GraphVarType.Counter, key = parts[0] },
            "add_affection" => new GraphLogicEntry { varType = GraphVarType.Affection, key = parts[0], amount = amount },
            "add_suspicion" or "add_suspicion_for" => new GraphLogicEntry { varType = GraphVarType.Suspicion, key = parts[0], amount = amount },
            "add_trust_earned" => new GraphLogicEntry { varType = GraphVarType.TrustEarned, key = parts[0], amount = amount },
            "add_line_crossed" => new GraphLogicEntry { varType = GraphVarType.LineCrossed, key = parts[0], amount = amount },
            "add_personal_bond" => new GraphLogicEntry { varType = GraphVarType.PersonalBond, key = parts[0], amount = amount },
            _ => null,
        };
    }

    /// <summary>단일 조건식만 해석 (복합 조건은 실패 처리)</summary>
    private static bool TryParseCondition(string expr, ConditionGroup group)
    {
        expr = expr.Trim();
        bool negate = false;
        if (expr.StartsWith("not "))
        {
            negate = true;
            expr = expr.Substring(4).Trim();
        }

        var fnMatch = Regex.Match(expr, @"^(\w+)\s*\(\s*""?([^""\)]*)""?\s*\)\s*(?:(>=|<=|==|!=)\s*(-?\d+))?$");
        if (!fnMatch.Success) return false;

        string fn = fnMatch.Groups[1].Value;
        string key = fnMatch.Groups[2].Value;
        string op = fnMatch.Groups[3].Value;
        string valStr = fnMatch.Groups[4].Value;

        var entry = new ConditionEntry { key = key };
        entry.varType = fn switch
        {
            "has_memory" => GraphVarType.Memory,
            "get_counter" => GraphVarType.Counter,
            "get_affection" => GraphVarType.Affection,
            "get_suspicion" => GraphVarType.Suspicion,
            "get_understanding_percent" => GraphVarType.Understanding,
            "get_trust_earned" => GraphVarType.TrustEarned,
            "get_line_crossed" => GraphVarType.LineCrossed,
            "get_personal_bond" => GraphVarType.PersonalBond,
            "get_mental_ratio" => GraphVarType.MentalPercent,
            _ => GraphVarType.Memory,
        };

        if (entry.varType == GraphVarType.Memory)
            entry.op = negate ? CondOp.NotHas : CondOp.Has;
        else
        {
            entry.op = op switch
            {
                ">=" => CondOp.GreaterOrEqual,
                "<=" => CondOp.LessOrEqual,
                "==" => CondOp.Equal,
                "!=" => CondOp.NotEqual,
                _ => CondOp.GreaterOrEqual,
            };
            int.TryParse(valStr, out int val);
            entry.value = val;
        }

        group.entries.Add(entry);
        return true;
    }
}