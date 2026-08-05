
public static class NPCCombatTuning
{
    public const float MaxTelegraphLeadTime = 1.5f;
    // TODO(미래 확장): 민첩 차이가 이 상한을 넘어설 만큼 크면, 예고를 억지로 늘리는 대신
    // '공격 시전 순간 NPC만 잠깐 로컬 슬로우모션' 연출로 대체 고려.
    // (Time.timeScale 전역 조정 X — 플레이어 조작감까지 같이 느려지면 안 됨.
    //  대신 이 NPC의 Animator.speed/물리 갱신 배속만 잠깐 낮추는 방식 추천)
}