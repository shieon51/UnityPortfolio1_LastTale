using System;
using System.Collections.Generic;
using UnityEngine;

// ★ 3-C — 자동 마일스톤 규칙. TimeLoopManager 인스펙터에서 조절한다 (하드코딩 금지)
[Serializable]
public class LoopSummaryRules
{
    [Tooltip("실전 보스전 결과(승패)를 자동 마일스톤으로 넣을 때의 중요도. 작가 마일스톤의 중요도와 같은 눈금이다")]
    public int bossBattleImportance = 2;
    [Tooltip("영혼 레벨 경신을 자동 마일스톤으로 넣을 때의 중요도. 한 회차에 여러 번 경신해도 마지막 하나만 넣는다")]
    public int soulRecordImportance = 1;
    [Tooltip("훈련(대련) 결과도 마일스톤에 넣을지. 기본은 실전만")]
    public bool includeTrainingBattles = false;
}

// ★ 3-C — 회차 요약 계산 (기록시스템_설계 7-1). 회차의 자기 구간 기록만 본다 —
//   부모에서 물려받은 구간은 부모 회차의 요약에 이미 있다 (기획서 6-6-4 "이번 회차에 직접 겪은 일")
public static class LoopSummaryBuilder
{
    // endSnapshot: 진행 중인 회차는 지금 상태를 찍어 넘긴다 (닫힌 회차는 loop.endSnapshot)
    public static LoopSummary Build(LoopRecord loop, IReadOnlyList<ActionRecord> all, LoopSummaryRules rules, Snapshot endSnapshot = null)
    {
        var s = new LoopSummary();
        if (loop == null) return s;
        rules ??= new LoopSummaryRules();

        s.startLevel = ReadBodyLevel(loop.startSnapshot);
        s.endLevel = ReadBodyLevel(loop.endSnapshot ?? endSnapshot);

        if (all == null) return s;
        long end = loop.IsOpen ? long.MaxValue : loop.endSeq;
        string trainingTier = BossDifficultyTier.Training.ToString();
        var authoredKeys = new HashSet<string>();   // 같은 장면을 한 회차에 두 번 지나도 한 번만
        MilestoneEntry lastSoulRecord = null;

        foreach (var r in all)
        {
            if (r.seq < loop.startSeq || r.seq > end) continue;

            switch (r.type)
            {
                case RecordType.MemoryHeard:
                    if (r.valueAfter == 1) s.newInfoCount++;   // 기록 시점에 "처음 들음"으로 판정한 값
                    break;

                case RecordType.AnchorSet:
                    s.anchorsSet.Add(r.valueAfter);            // after = 누적 닻 번호
                    break;

                case RecordType.Milestone:
                    if (authoredKeys.Add(r.key ?? ""))
                        s.milestones.Add(Entry(r, MilestoneKind.Authored, r.key, 0, r.valueAfter));
                    break;

                case RecordType.BattleResult:   // key: 난이도, before: 승 1·패 0, source: 보스 NPC 키
                    if (!rules.includeTrainingBattles && r.key == trainingTier) break;
                    s.milestones.Add(Entry(r, MilestoneKind.BossBattle, r.source, r.valueBefore, rules.bossBattleImportance));
                    break;

                case RecordType.LevelUp:        // key "soul_record" = 영혼 레벨 경신 (PlayableCharacter)
                    if (r.key != "soul_record") break;
                    s.soulRecord = true;
                    lastSoulRecord = Entry(r, MilestoneKind.SoulRecord, null, r.valueAfter, rules.soulRecordImportance);
                    break;
            }
        }

        if (lastSoulRecord != null) s.milestones.Add(lastSoulRecord);
        s.milestones.Sort((a, b) => a.seq.CompareTo(b.seq));   // 시간순
        return s;
    }

    private static MilestoneEntry Entry(ActionRecord r, MilestoneKind kind, string key, int value, int importance) => new()
    {
        kind = kind, key = key, value = value, importance = importance,
        seq = r.seq, day = r.day, hour = r.hour,
    };

    private static int ReadBodyLevel(Snapshot snapshot)
        => snapshot != null && snapshot.blocks.TryGetValue(RecordIds.SoraBody, out var b)
           && b.ints.TryGetValue(SoraStats.StateKeyLevel, out int level) ? level : 0;
}
