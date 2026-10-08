// ★ 기록 시스템 2단계 — 데이터 층위 (기록시스템_설계 4장, UI 기획서 8장)
//   숫자로 저장되므로 값을 바꾸지 않고 끝에만 추가한다
public enum RecordLayer
{
    World = 0,    // 세계: NPC 상태, 카운터, 이벤트 진행, 날짜·위치 — 경로에 따라 되돌린다
    Body = 1,     // 소라의 몸: 레벨, 경험치, 최대 HP·MP, 공·방·민 — 경로 2·3에서만 되돌린다
    Will = 2,     // 소라의 의지: 기억, 스킬 숙련도, 개인친밀도, 정신력, 영혼 레벨 — 되돌리지 않는다
    Player = 3,   // 플레이어: 행적 기록, 방문 노드, 메모·핀, 전적 — 되돌리지 않는다
}

// ★ 여러 층위를 한 번에 고를 때 쓴다 (예: 경로 2 = World | Body)
[System.Flags]
public enum RecordLayerMask
{
    None = 0,
    World = 1 << (int)RecordLayer.World,
    Body = 1 << (int)RecordLayer.Body,
    Will = 1 << (int)RecordLayer.Will,
    Player = 1 << (int)RecordLayer.Player,
    All = World | Body | Will | Player,
}

public static class RecordLayerExtensions
{
    public static RecordLayerMask ToMask(this RecordLayer layer) => (RecordLayerMask)(1 << (int)layer);
    public static bool Includes(this RecordLayerMask mask, RecordLayer layer) => (mask & layer.ToMask()) != 0;
}
