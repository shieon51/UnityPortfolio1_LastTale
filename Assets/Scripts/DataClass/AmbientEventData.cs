// AmbientEventData.cs (신규)
using UnityEngine;

// 위치 무관 자동 이벤트 (꿈, 회상 등)
[CreateAssetMenu(menuName = "LastMarchan/Narrative/Ambient Event")]
public class AmbientEventData : ScriptableObject
{
    public string eventId;
    public string inkNodeName;

    [Header("발동 조건")]
    [Tooltip("이 시각 이후에만 (0이면 무관)")]
    public int minHour = 0;
    [Tooltip("이 시각 이전에만 (24면 무관)")]
    public int maxHour = 24;
    [Tooltip("이 회차 이상일 때만 발동 (0이면 무관)")]
    public int minLoopCount = 0;
    [Tooltip("이 정보를 가지고 있어야 발동 (비우면 무관)")]
    public string requiredFlagId = "";
    [Tooltip("정신력이 이 비율(%) 이하일 때만 (100이면 무관)")]
    public int maxMentalPercent = 100;

    [Header("발동 방식")]
    [Tooltip("조건 충족 시 매 판정마다 발동할 확률 (0~1)")]
    [Range(0f, 1f)] public float chance = 0.2f;
    [Tooltip("한 번 발동하면 다시 발동하지 않음")]
    public bool onceOnly = true;
    [Tooltip("잠자기 직후에만 판정 (꿈 연출용)")]
    public bool onlyAfterSleep = false;
}