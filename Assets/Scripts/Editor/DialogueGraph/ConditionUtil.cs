// ConditionUtil.cs (신규, Editor 폴더)
using System.Collections.Generic;
using System.Linq;

public static class ConditionUtil
{
    public static string GetVarLabel(GraphVarType t) => t switch
    {
        GraphVarType.Memory => "기억",
        GraphVarType.Counter => "카운터",
        GraphVarType.Affection => "호감도",
        GraphVarType.Suspicion => "의심",
        GraphVarType.Understanding => "이해도",
        GraphVarType.TrustEarned => "신뢰",
        GraphVarType.LineCrossed => "선넘음",
        GraphVarType.PersonalBond => "개인친밀도",
        GraphVarType.MentalPercent => "정신력",
        _ => "?",
    };

    public static bool IsBoolType(GraphVarType t) => t == GraphVarType.Memory;
    public static bool NeedsKey(GraphVarType t) => t != GraphVarType.MentalPercent;

    public static List<string> GetKeyOptions(GraphVarType t) => t switch
    {
        GraphVarType.Memory => GraphKeySource.GetMemoryFlagIds(),
        GraphVarType.Counter => new List<string>(),
        GraphVarType.MentalPercent => new List<string>(),
        _ => GraphKeySource.GetNPCNames(),
    };

    public static bool UsesFreeText(GraphVarType t) => t == GraphVarType.Counter;

    /// <summary>ink 조건식으로 변환</summary>
    public static string ToInkExpr(ConditionGroup group)
    {
        if (group == null || group.entries.Count == 0) return "true";
        string joiner = group.join == CondJoin.And ? " and " : " or ";
        return string.Join(joiner, group.entries.Select(ToInkExpr));
    }

    public static string ToInkExpr(ConditionEntry e)
    {
        string getter = e.varType switch
        {
            GraphVarType.Memory => $"has_memory(\"{e.key}\")",
            GraphVarType.Counter => $"get_counter(\"{e.key}\")",
            GraphVarType.Affection => $"get_affection(\"{e.key}\")",
            GraphVarType.Suspicion => $"get_suspicion(\"{e.key}\")",
            GraphVarType.Understanding => $"get_understanding_percent(\"{e.key}\")",
            GraphVarType.TrustEarned => $"get_trust_earned(\"{e.key}\")",
            GraphVarType.LineCrossed => $"get_line_crossed(\"{e.key}\")",
            GraphVarType.PersonalBond => $"get_personal_bond(\"{e.key}\")",
            GraphVarType.MentalPercent => "get_mental_ratio()",
            _ => "true",
        };

        if (IsBoolType(e.varType)) return e.op == CondOp.NotHas ? $"not {getter}" : getter;

        string opStr = e.op switch
        {
            CondOp.GreaterOrEqual => ">=",
            CondOp.LessOrEqual => "<=",
            CondOp.Equal => "==",
            CondOp.NotEqual => "!=",
            _ => ">=",
        };
        return $"{getter} {opStr} {e.value}";
    }

    /// <summary>접었을 때 보여줄 한글 요약 자동 생성</summary>
    public static string BuildSummary(ConditionGroup group)
    {
        if (group == null) return "(조건 없음)";
        if (!string.IsNullOrEmpty(group.summaryOverride)) return group.summaryOverride;
        if (group.entries.Count == 0) return "(항상 참)";

        var parts = group.entries.Select(e =>
        {
            if (IsBoolType(e.varType))
                return e.op == CondOp.NotHas ? $"'{e.key}' 모름" : $"'{e.key}' 알고 있음";

            string target = NeedsKey(e.varType) ? $"{e.key} " : "";
            string opKor = e.op switch
            {
                CondOp.GreaterOrEqual => "이상",
                CondOp.LessOrEqual => "이하",
                CondOp.Equal => "정확히",
                CondOp.NotEqual => "이 아님",
                _ => "이상",
            };
            return $"{target}{GetVarLabel(e.varType)} {e.value} {opKor}";
        });

        return string.Join(group.join == CondJoin.And ? ", 그리고 " : ", 또는 ", parts);
    }
}