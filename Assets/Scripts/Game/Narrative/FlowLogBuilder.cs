using System.Collections.Generic;
using UnityEngine;

// 행적 기록을 기록장 표시용 구조로 바꾼다.
// 문구는 로컬라이제이션 키를 쓰고, 표에 없으면 기본값으로 대체한다.
public static class FlowLogBuilder
{
    // 정보 획득 기록은 뒤따르는 이벤트 줄에 태그로 붙인다 (대화 중에 얻은 것이므로)
    public static FlowLog Build(IReadOnlyList<ActionRecord> records)
    {
        var log = new FlowLog();
        if (records == null) return log;

        FlowDay currentDay = null;
        var pendingTags = new List<FlowTag>();   // 아직 어느 줄에도 붙지 않은 태그
        int lastHour = -1;

        foreach (var r in records)
        {
            if (!PlayerActionLog.IsPlayerVisible(r.type)) continue;

            if (currentDay == null || currentDay.day != r.day)
            {
                currentDay = new FlowDay { day = r.day };
                log.days.Add(currentDay);
                lastHour = -1;
            }
            string placeName = PlayerActionLog.ResolvePlaceName(r);    // ★ 표시 시점에 조회
            if (!string.IsNullOrEmpty(placeName) && !currentDay.visitedPlaces.Contains(placeName))
                currentDay.visitedPlaces.Add(placeName);

            // 태그로만 쓰이는 기록은 줄을 만들지 않고 모아둔다
            var tag = BuildTag(r);
            if (tag != null && IsTagOnly(r.type)) { pendingTags.Add(tag); continue; }

            var entry = BuildEntry(r);
            if (entry == null) continue;

            entry.sameTimeAsPrevious = (entry.hour == lastHour);
            lastHour = entry.hour;

            entry.tags.AddRange(pendingTags);
            if (tag != null) entry.tags.Add(tag);
            // 변경 후 — BuildEntry에서 이미 켠 강조를 유지한다
            entry.highlight |= entry.tags.Exists(t => t.kind == FlowTagKind.NewInfo || t.kind == FlowTagKind.Battle);

            currentDay.entries.Add(entry);
            currentDay.totals.AddRange(entry.tags);
            pendingTags.Clear();
        }

        // 마지막까지 붙지 못한 태그는 그 날의 총 획득에만 남긴다
        if (currentDay != null) currentDay.totals.AddRange(pendingTags);
        return log;
    }

    // 줄을 만들지 않고 다음 줄에 붙기만 하는 기록
    private static bool IsTagOnly(RecordType type)
        => type == RecordType.MemoryHeard || type == RecordType.MemoryErased
        || type == RecordType.LevelUp || type == RecordType.StatGain;

    private static FlowEntry BuildEntry(ActionRecord r)
    {
        var entry = new FlowEntry
        {
            hour = r.hour,
            durationHours = r.valueAfter,     // EventCompleted는 소모 시간을 여기에 담는다
            place = PlayerActionLog.ResolvePlaceName(r),
        };

        switch (r.type)
        {
            case RecordType.EventCompleted:
                entry.speaker = string.IsNullOrEmpty(r.source) ? null : ResolveNpcName(r.source);
                entry.title = r.key;
                break;

            case RecordType.BattleResult:
                entry.speaker = ResolveNpcName(r.source);
                entry.title = r.key;
                break;

            case RecordType.AnchorSet:
                entry.title = Format("flow_anchor_set", "시간의 닻 설치 ({0}번 닻)", r.key);
                entry.durationHours = 0;
                break;

            case RecordType.Loop:
                // ★ 시각을 문자열로 저장하지 않고 숫자로 받아 포맷터로 조립한다 (설정·언어 변경이 지난 기록에도 적용)
                entry.title = PlayerActionLog.TryDecodeDayHour(r.payload, out int loopDay, out int loopHour)
                    ? Format("flow_loop_return_at", "{0}회차 {1}에서 회귀",
                             r.valueBefore, GameTimeFormatter.FormatDayTime(loopDay, loopHour))
                    : Text("flow_loop_reset", "처음으로 되돌아감");
                entry.durationHours = 0;
                entry.highlight = true;
                break;

            case RecordType.Travel:
                entry.speaker = r.source;              // "이동" / "순간이동"
                entry.title = r.key;                   // "마을 1 → 숲 초입"
                entry.dimmed = true;                   // 회색 줄
                break;

            case RecordType.Hunt:
                entry.speaker = Text("flow_hunt", "사냥");
                entry.title = r.key;                   // "슬라임 15마리"
                break;
            default:
                return null;
        }
        return entry;
    }

    private static FlowTag BuildTag(ActionRecord r)
    {
        switch (r.type)
        {
            case RecordType.MemoryHeard:
                {
                    bool isNew = r.valueAfter == 1;     // 기록 시점에 판정한 값
                    // 이 시점에 이미 알고 있었는지는 획득 기록으로 구분한다
                    string label = ResolveMemoryLabel(r.key);
                    string owner = string.IsNullOrEmpty(r.source) ? "" : ResolveNpcName(r.source) + ": ";
                    return new FlowTag
                    {
                        kind = isNew ? FlowTagKind.NewInfo : FlowTagKind.RepeatInfo,
                        text = isNew ? $"+ {owner}{label}" : $"{owner}{label} ({Text("flow_recheck", "재확인")})",
                        tooltip = r.key,
                    };
                }

            case RecordType.MemoryErased:
                return new FlowTag { kind = FlowTagKind.RepeatInfo, text = Format("flow_memory_erased", "{0} 소실", ResolveMemoryLabel(r.key)) };

            case RecordType.LevelUp:
                {
                    bool isRecord = r.key == "soul_record";
                    return new FlowTag
                    {
                        kind = isRecord ? FlowTagKind.Record : FlowTagKind.Growth,
                        text = (isRecord ? "★ " : "") + $"Lv.{r.valueBefore} → {r.valueAfter}",
                        tooltip = isRecord ? Text("flow_soul_record", "영혼 레벨 경신 — 이 몸이 처음 도달한 경지") : null,
                    };
                }

            case RecordType.StatGain:
                return new FlowTag { kind = FlowTagKind.Growth, text = $"{r.key} +{r.valueAfter - r.valueBefore}" };

            case RecordType.BattleResult:
                return new FlowTag { kind = FlowTagKind.Battle, text = r.key };

            case RecordType.AnchorSet:
                return new FlowTag { kind = FlowTagKind.Anchor, text = "⚓" };

            default:
                return null;
        }
    }

    // ---------------- 문구 조회 ----------------

    private static string ResolveNpcName(string npcKey)
    {
        if (string.IsNullOrEmpty(npcKey)) return null;
        var loc = LocalizationManager.Instance;
        string key = $"npc_name_{npcKey.ToLowerInvariant()}";
        return (loc != null && loc.Has(key)) ? loc.Get(key) : npcKey;
    }

    private static string ResolveMemoryLabel(string flagId)
    {
        var data = MemoryManager.Instance?.GetData(flagId);
        if (data == null) return flagId;
        var loc = LocalizationManager.Instance;
        return (loc != null && loc.Has(data.localizationKey)) ? loc.Get(data.localizationKey) : flagId;
    }

    private static string Text(string key, string fallback)
    {
        var loc = LocalizationManager.Instance;
        return (loc != null && loc.Has(key)) ? loc.Get(key) : fallback;
    }

    private static string Format(string key, string fallback, params object[] args)
    {
        var loc = LocalizationManager.Instance;
        if (loc != null && loc.Has(key)) return loc.GetFormat(key, args);
        return string.Format(fallback, args);
    }
}