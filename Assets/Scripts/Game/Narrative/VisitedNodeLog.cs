using System.Collections.Generic;
using UnityEngine;

// 어느 대화 노드를 지났는지 기록한다. 회귀·리셋과 무관하게 누적되는 플레이어 층위 데이터로,
// 읽은 대사 빠르게 넘기기 / 시뮬레이션 진행률 / 나만의 이야기 문양이 같은 기록을 쓴다.
public class VisitedNodeLog : Singleton<VisitedNodeLog>, IRecordable   // ★ 기록 시스템 2-B — 플레이어 층위로 등록
{
    private readonly HashSet<string> _visited = new();

    public int VisitedCount => _visited.Count;
    public bool HasVisited(string nodeId) => !string.IsNullOrEmpty(nodeId) && _visited.Contains(nodeId);

    public bool MarkVisited(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId)) return false;
        return _visited.Add(nodeId);        // true면 이번이 처음
    }

    public IEnumerable<string> All => _visited;

    // 세이브 연동 전까지는 메모리에만 유지된다
    public void RestoreFrom(IEnumerable<string> ids)
    {
        _visited.Clear();
        if (ids == null) return;
        foreach (var id in ids) _visited.Add(id);
    }

    public void ClearAll() => _visited.Clear();   // 공식 하드 리셋 전용

    // ---------------- IRecordable (★ 기록 시스템 2-B) ----------------
    private void Awake() => RecordSystem.Register(this);        // ★ 범용 스냅샷에 포함
    private void OnDestroy() => RecordSystem.Unregister(this);

    private const string StateKeyVisited = "visited";   // 덩어리 안의 키. 바꾸지 않는다

    public string RecordId => RecordIds.VisitedNodes;
    public RecordLayer Layer => RecordLayer.Player;
    public int StateVersion => 1;

    public void WriteState(StateWriter writer) => writer.WriteStringList(StateKeyVisited, _visited);
    public void ReadState(StateReader reader, int version) => RestoreFrom(reader.ReadStringList(StateKeyVisited));
}