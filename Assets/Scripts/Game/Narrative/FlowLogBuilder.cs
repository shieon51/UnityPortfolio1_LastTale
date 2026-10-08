using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// 행적 기록을 기록장 표시용 구조로 바꾼다.
// ★ 기록에는 ID와 숫자만 있으므로, 문구는 모두 여기서 표시할 때 조립한다 (언어·시간제 변경이 지난 기록에도 적용)
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
            // BuildEntry에서 이미 켠 강조를 유지한다
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
            durationHours = r.valueAfter,     // 이벤트·이동·사냥은 소모 시간을 여기에 담는다
            place = PlayerActionLog.ResolvePlaceName(r),
        };

        switch (r.type)
        {
            case RecordType.EventCompleted:
                entry.speaker = string.IsNullOrEmpty(r.source) ? null : ResolveNpcName(r.source);
                entry.title = ResolveEventTitle(r);                          // ★
                break;

            case RecordType.BattleResult:
                entry.speaker = ResolveNpcName(r.source);
                entry.title = ResolveBattleText(r);                          // ★
                entry.durationHours = 0;                                     // ★ after는 경과 초다
                break;

            case RecordType.AnchorSet:
                entry.title = Format("flow_anchor_set", "시간의 닻 설치 ({0}번 닻)", r.key);
                entry.durationHours = 0;
                break;

            case RecordType.Loop:
                entry.title = PlayerActionLog.TryDecodeDayHour(r.payload, out int loopDay, out int loopHour)
                    ? Format("flow_loop_return_at", "{0}회차 {1}에서 회귀",
                             r.valueBefore, GameTimeFormatter.FormatDayTime(loopDay, loopHour))
                    : Text("flow_loop_reset", "처음으로 되돌아감");
                entry.durationHours = 0;
                entry.highlight = true;
                break;

            case RecordType.Travel:
                {
                    // ★ key: 이동 방식, before: 출발 씬, 기록 위치: 도착 씬
                    var tracker = TravelTimeTracker.Instance;
                    bool instant = r.key == TravelTimeTracker.RecordKeyInstant;
                    string modeKey = instant
                        ? (tracker != null ? tracker.keyTravelInstant : "flow_travel_instant")
                        : (tracker != null ? tracker.keyTravelWalk : "flow_travel_walk");
                    entry.speaker = Text(modeKey, instant ? "순간이동" : "이동");
                    entry.title = Format("flow_travel_route", "{0} → {1}",
                        SceneNameUtil.GetDisplayName(r.valueBefore), entry.place);
                    entry.dimmed = true;                   // 회색 줄
                    break;
                }

            case RecordType.Hunt:
                {
                    // ★ key: 대상 ID, payload: 마릿수
                    int.TryParse(r.payload, NumberStyles.Integer, CultureInfo.InvariantCulture, out int kills);
                    entry.speaker = Text("flow_hunt", "사냥");
                    entry.title = Format("flow_hunt_kills", "{0} {1}마리", Enemy.ResolveName(r.key, r.key), kills);
                    break;
                }

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
                        tooltip = isRecord ? Text("flow_soul_record", "영혼 레벨 경신") : null,
                    };
                }

            case RecordType.StatGain:
                return new FlowTag { kind = FlowTagKind.Growth, text = $"{r.key} +{r.valueAfter - r.valueBefore}" };

            case RecordType.BattleResult:
                return new FlowTag { kind = FlowTagKind.Battle, text = ResolveBattleText(r) };   // ★

            case RecordType.AnchorSet:
                return new FlowTag { kind = FlowTagKind.Anchor, text = "⚓" };

            default:
                return null;
        }
    }

    // ---------------- 문구 조회 ----------------

    // ★ 이벤트 제목: 표시 키 → 원래 이벤트의 표시 이름 → 노드 이름 순
    //   (1-B 이전 기록은 key에 제목이 그대로 들어 있으므로 마지막에 key를 그대로 쓴다)
    private static string ResolveEventTitle(ActionRecord r)
    {
        var loc = LocalizationManager.Instance;
        if (!string.IsNullOrEmpty(r.payload) && loc != null && loc.Has(r.payload)) return loc.Get(r.payload);

        var data = EventData.FindByRecordKey(r.key);
        if (data != null) return data.ResolveDisplayName();

        const string nodePrefix = "node:";
        if (r.key != null && r.key.StartsWith(nodePrefix)) return r.key.Substring(nodePrefix.Length);
        return r.key;
    }

    // ★ "훈련(승) 3분 12초" — key: 난이도, before: 승(1)/패(0), after: 경과 초
    private static string ResolveBattleText(ActionRecord r)
    {
        string tier = string.IsNullOrEmpty(r.key)
            ? ""
            : Text($"battle_tier_{r.key.ToLowerInvariant()}", r.key);
        string result = r.valueBefore == 1 ? Text("battle_win", "승") : Text("battle_lose", "패");
        int seconds = Mathf.Max(0, r.valueAfter);
        return Format("flow_battle_result", "{0}({1}) {2}분 {3}초", tier, result, seconds / 60, seconds % 60);
    }

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