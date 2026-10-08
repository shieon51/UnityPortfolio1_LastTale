using System;
using System.Collections.Generic;
using UnityEngine;

// ★ 기록 시스템 2단계 — 상태 수집과 층위별 복원의 단일 진입점 (기록시스템_설계 3장)
//   싱글톤 MonoBehaviour로 만들면 씬에 없을 때 빈 오브젝트가 자동 생성되므로 static으로 둔다
//   지금은 닻 설치 때 찍어 두고 비교만 한다. 실제 복원 교체는 2단계 후반에서 한다
public static class RecordSystem
{
    private static readonly Dictionary<string, IRecordable> _registry = new();

    // ★ 도메인 리로드를 끈 플레이 모드에서도 지난 플레이의 등록이 남지 않게 한다
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _registry.Clear();

    public static IEnumerable<IRecordable> Registered => _registry.Values;

    // replaceExisting
    //   false (싱글톤 매니저) — 먼저 등록된 쪽을 유지한다. 중복 매니저가 Awake에서 등록한 뒤
    //                          제거되면서 원본 등록까지 지우는 일을 막는다
    //   true  (소라처럼 씬마다 새로 생길 수 있는 것) — 나중에 등록된 쪽으로 교체한다
    public static void Register(IRecordable recordable, bool replaceExisting = false)
    {
        if (recordable == null || string.IsNullOrEmpty(recordable.RecordId)) return;

        if (_registry.TryGetValue(recordable.RecordId, out var existing) && existing != recordable && !IsDestroyed(existing))
        {
            if (!replaceExisting)
            {
                Debug.LogWarning($"[RecordSystem] '{recordable.RecordId}'가 이미 등록돼 있어 기존 것을 유지합니다");
                return;
            }
        }
        _registry[recordable.RecordId] = recordable;
    }

    // 자기 자신이 등록돼 있을 때만 뺀다 (교체된 뒤 옛 객체가 지워질 때 새 등록을 건드리지 않게)
    public static void Unregister(IRecordable recordable)
    {
        if (recordable == null || string.IsNullOrEmpty(recordable.RecordId)) return;
        if (_registry.TryGetValue(recordable.RecordId, out var existing) && existing == recordable)
            _registry.Remove(recordable.RecordId);
    }

    // 파괴된 MonoBehaviour는 C# 참조가 남아도 유니티 기준으로 null이다
    private static bool IsDestroyed(IRecordable r) => r is UnityEngine.Object o && o == null;

    public static Snapshot TakeSnapshot(SnapshotReason reason, RecordLayerMask mask = RecordLayerMask.All)
    {
        var time = TimeManager.Instance;
        var log = PlayerActionLog.Instance;
        var snapshot = new Snapshot
        {
            seq = log != null ? log.NextSeq : 0,
            reason = reason,
            day = time != null ? time.currentDay : 0,
            hour = time != null ? time.currentHour : 0,
        };

        foreach (var r in _registry.Values)
        {
            if (IsDestroyed(r) || !mask.Includes(r.Layer)) continue;

            // ★ 한 시스템의 오류가 닻 설치 전체를 막지 않게 덩어리 단위로 감싼다
            try
            {
                var block = new StateBlock { recordId = r.RecordId, layer = r.Layer, version = r.StateVersion };
                r.WriteState(new StateWriter(block));
                snapshot.blocks[r.RecordId] = block;
            }
            catch (Exception e)
            {
                Debug.LogError($"[RecordSystem] '{r.RecordId}' 상태 쓰기 실패 — 이 덩어리는 빠집니다\n{e}");
            }
        }
        return snapshot;
    }

    // ★ 지금은 호출하는 곳이 없다. 닻 복원을 교체할 때 쓴다
    public static void RestoreLayers(Snapshot snapshot, RecordLayerMask mask)
    {
        if (snapshot == null) return;

        foreach (var block in snapshot.blocks.Values)
        {
            if (!mask.Includes(block.layer)) continue;
            if (!_registry.TryGetValue(block.recordId, out var r) || IsDestroyed(r)) continue;   // 모르는 덩어리는 보관만

            try { r.ReadState(new StateReader(block), block.version); }
            catch (Exception e) { Debug.LogError($"[RecordSystem] '{block.recordId}' 상태 읽기 실패\n{e}"); }
        }

        foreach (var r in _registry.Values)
        {
            if (IsDestroyed(r) || !mask.Includes(r.Layer)) continue;
            if (!snapshot.blocks.ContainsKey(r.RecordId))
                Debug.LogWarning($"[RecordSystem] 스냅샷에 '{r.RecordId}' 덩어리가 없어 그대로 둡니다");
        }
    }

    // ★ 두 스냅샷이 고른 층위에서 같은지 비교한다. 다른 곳을 사람이 읽을 문장으로 돌려준다 (개발용 로그 전용)
    public static List<string> Compare(Snapshot expected, Snapshot actual, RecordLayerMask mask)
    {
        var diffs = new List<string>();
        if (expected == null || actual == null) { diffs.Add("스냅샷이 비어 있음"); return diffs; }

        var ids = new HashSet<string>(expected.blocks.Keys);
        ids.UnionWith(actual.blocks.Keys);

        foreach (var id in ids)
        {
            expected.blocks.TryGetValue(id, out var e);
            actual.blocks.TryGetValue(id, out var a);
            var layer = (e ?? a).layer;
            if (!mask.Includes(layer)) continue;

            if (e == null) { diffs.Add($"{id}: 기대 쪽에 덩어리 없음"); continue; }
            if (a == null) { diffs.Add($"{id}: 현재 쪽에 덩어리 없음"); continue; }

            CompareInts(id, null, e.ints, a.ints, diffs);

            var mapKeys = new HashSet<string>(e.intMaps.Keys);
            mapKeys.UnionWith(a.intMaps.Keys);
            foreach (var key in mapKeys)
            {
                e.intMaps.TryGetValue(key, out var em);
                a.intMaps.TryGetValue(key, out var am);
                CompareInts(id, key, em ?? new Dictionary<string, int>(), am ?? new Dictionary<string, int>(), diffs);
            }

            var listKeys = new HashSet<string>(e.stringLists.Keys);
            listKeys.UnionWith(a.stringLists.Keys);
            foreach (var key in listKeys)
            {
                e.stringLists.TryGetValue(key, out var el);
                a.stringLists.TryGetValue(key, out var al);
                string es = el != null ? string.Join(",", el) : "";
                string as_ = al != null ? string.Join(",", al) : "";
                if (es != as_) diffs.Add($"{id}.{key}: [{es}] ≠ [{as_}]");
            }
        }
        return diffs;
    }

    // 없는 키는 0으로 본다 (호감도·카운터는 없으면 0과 같다)
    private static void CompareInts(string id, string mapKey, Dictionary<string, int> e, Dictionary<string, int> a, List<string> diffs)
    {
        var keys = new HashSet<string>(e.Keys);
        keys.UnionWith(a.Keys);
        foreach (var k in keys)
        {
            int ev = e.TryGetValue(k, out var x) ? x : 0;
            int av = a.TryGetValue(k, out var y) ? y : 0;
            if (ev != av) diffs.Add(mapKey == null ? $"{id}.{k}: {ev} ≠ {av}" : $"{id}.{mapKey}[{k}]: {ev} ≠ {av}");
        }
    }
}
