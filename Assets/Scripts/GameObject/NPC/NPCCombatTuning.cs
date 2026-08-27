
using UnityEngine;

public class NPCCombatTuning : Singleton<NPCCombatTuning>
{
    [Header("텔레그래프(공격 예고) 시간")]
    [Tooltip("격차가 너무 크면(NPC가 압도적으로 빠름) 예고가 이 아래로는 절대 안 줄어듦")]
    public float MinTelegraphLeadTime = 0.25f;

    [Tooltip("플레이어가 AGI를 올려서 격차를 좁혔을 때, 예고로 얻을 수 있는 최대 시간")]
    public float MaxTelegraphLeadTime = 2.0f;

    [Header("텔레그래프 초과분 → 슬로우모션 보너스")]
    [Tooltip("초과분(초) 1당 슬로우모션 강도(0~1)가 늘어나는 비율")]
    public float SlowmoOverflowScale = 0.5f;

    [Tooltip("슬로우모션 최대 지속시간(초) — 강도 1.0일 때 이 값 그대로 적용")]
    public float MaxSlowmoDuration = 0.6f;

    [Tooltip("슬로우모션 최대 강도일 때 NPC Animator.speed 배율 (1보다 작을수록 느림)")]
    public float SlowmoAnimatorSpeed = 0.35f;

    [Header("그로기 진입 시 밀림")]
    public float GroggyPushForce = 3f;
    public float GroggyPushDuration = 0.15f;

    [Header("패링 확률 보정 (연속 실패 시 상승)")]
    public float ParryMissBonusPerMiss = 0.08f;
    public float ParryMaxChance = 0.75f;
}