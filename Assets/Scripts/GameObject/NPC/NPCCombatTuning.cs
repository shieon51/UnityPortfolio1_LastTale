
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
    public float GroggyDrag = 15f; // 그로기 중 밀림 마찰

    [Header("패링 확률 보정 (연속 실패 시 상승)")]
    public float ParryMissBonusPerMiss = 0.08f;
    public float ParryMaxChance = 0.75f;

    [Header("리엘 마나 집중")]
    public float ManaConcentrationTriggerRatio = 0.2f; // 이 비율 이하로 마나가 떨어지면 집중 판단
    public float ConcentrationRetreatDistance = 6f;
    public float ConcentrationRetreatSpeed = 4f;
    public float ConcentrationDuration = 3.5f;
    public float ConcentrationInterruptRange = 4f; // 이 거리 안으로 플레이어가 들어오면 중단
    public float ConcentrationRecoverRatioPerSecond = 0.15f; // 초당 최대마나 대비 회복 비율
    public float ConcentrationRetryCooldown = 5f; // 중단 후 이 시간 동안은 재시도 안 함
}