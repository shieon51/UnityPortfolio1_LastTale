// ★ 기록 시스템 2단계 — 상태를 가진 모든 시스템이 구현한다 (기록시스템_설계 3장)
//   구현하고 RecordSystem.Register만 하면 닻·세이브·복원에 자동으로 포함된다
//   변경 재적용(ApplyChange)은 재생 검증(5단계)에서 추가한다
public interface IRecordable
{
    // 세이브와 맞추는 고정 이름표. 클래스 이름이 바뀌어도 그대로 둔다 (RecordIds 참고)
    string RecordId { get; }

    // 어느 층위인가. 회귀 경로마다 어떤 층위를 되돌릴지가 정해진다
    RecordLayer Layer { get; }

    // 이 시스템의 저장 형식 버전. 형식을 바꾸면 올리고, ReadState에서 옛 버전을 변환한다
    int StateVersion { get; }

    void WriteState(StateWriter writer);
    void ReadState(StateReader reader, int version);
}

// ★ RecordId 목록. 한 번 정하면 바꾸지 않는다 (옛 스냅샷·세이브가 이 문자열로 덩어리를 찾는다)
public static class RecordIds
{
    public const string NpcAffection = "npc.affection";
    public const string NpcSuspicion = "npc.suspicion";
    public const string MemoryCounters = "memory.counters";
    public const string SoraBody = "sora.body";

    // ★ 2-B — 한 클래스에 두 층위가 있으면 어댑터로 덩어리를 나눈다 (기록시스템_설계 13-2-5)
    public const string MemoryAcquired = "memory.acquired";   // MemoryManager 안 어댑터 (Will)
    public const string SoraWill = "sora.will";               // SoraStats 안 어댑터 (Will)
    public const string SoraLoop = "sora.loop";               // SoraStats 안 어댑터 (Player)
    public const string VisitedNodes = "player.visited_nodes";

    // ★ 2-C-2 — 시각과 위치 (World). 회귀에서는 RestoreLayers가 건너뛰고, 회귀 기록 뒤 LoadScene으로 옮긴다
    public const string TimeClock = "time.clock";             // TimeManager
    public const string SceneLocation = "scene.location";     // SceneLoader
}
