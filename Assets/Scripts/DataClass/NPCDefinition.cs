// NPCDefinition.cs (신규)
using UnityEngine;

[CreateAssetMenu(menuName = "LastMarchan/NPC/NPC Definition")]
public class NPCDefinition : ScriptableObject
{
    [Header("식별")]
    public string npcName; // "Liel" — NPCData의 키가 됨

    [Header("초기 관계")]
    public int initialAffection = 0;
    [Tooltip("회귀해도 호감도가 유지되는 특별한 NPC인지")]
    public bool rememberAcrossLoops = false;
    [Tooltip("이 NPC를 완전히 이해하는 데 필요한 정보 총량(실제 정보 수보다 크게 잡으면 100% 도달 불가)")]
    public int maxObtainableUnderstanding = 100;

    [Header("의심 시스템")]
    [Tooltip("이 NPC가 직접 관찰할 수 있는 행적 카운터 키 목록")]
    public string[] observableCounterKeys;
    [Tooltip("이 NPC가 소문을 전달하는 상대들")]
    public string[] relatedNPCs;
    [Tooltip("각 상대를 얼마나 신뢰하는지 (relatedNPCs와 같은 순서)")]
    public float[] trustInRelatedNPCs;
    [Range(0f, 2f)] public float suspicionSensitivity = 1f;
    [Tooltip("소라와의 호감도가 이 값 이상이면 의심을 완화함")]
    public int trustThresholdForSora = 20;
}