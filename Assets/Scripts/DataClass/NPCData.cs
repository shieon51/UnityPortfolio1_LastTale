using System;
using UnityEngine;

[Serializable]
public class NPCData
{
    public string npcName; // 예: "Liel", "Diavalu"

    public int hiddenAffection = 0; // 숨겨진 호감도 (애정)

    [Tooltip("이 NPC를 '완전히' 이해하는 데 필요한 총량. 실제 만든 정보 개수보다 크게 잡으면 100% 도달이 원천적으로 불가능해짐 (일부러 그런 캐릭터를 만들고 싶을 때)")]
    public int maxObtainableUnderstanding = 100; // ★ 신규

    // ------------------ 의심 시스템 ---------
    [Tooltip("소문이 전파될 때 남는 강도 비율 (0~1)")] //? 
    public float rumorSpreadRatio = 0.5f;

    [Header("의심 시스템")]
    [Tooltip("이 NPC가 직접 관찰할 수 있는 행적 카운터 키들. 여기 없는 행적은 이 NPC가 알 수 없음")]
    public string[] observableCounterKeys;

    [Tooltip("이 NPC가 소문을 전달하는 상대들")]
    public string[] relatedNPCs;

    [Tooltip("이 NPC가 각 상대를 얼마나 신뢰하는지(소문 수용도). relatedNPCs와 같은 순서")]
    public float[] trustInRelatedNPCs;

    [Tooltip("이 NPC의 기본 의심 성향. 낮을수록 의심을 잘 안하고 넘어감")]
    [Range(0f, 2f)] public float suspicionSensitivity = 1f;

    [Tooltip("소라와의 호감도가 이 값 이상이면 의심을 상당히 완화함")]
    public int trustThresholdForSora = 20;

    // 전투 및 스토리 상태 데이터
    public NPC.NPCMode currentMode = NPC.NPCMode.Normal; // 평상시인지 보스전인지
    public int bossPhase = 1; // 보스전 돌입 시 현재 페이즈
    public bool rememberAcrossLoops = false; // 회귀해도 호감도가 리셋 안 되는 특별한 NPC

    // 생성자
    public NPCData(string name)
    {
        npcName = name;
        // 캐릭터별 초기 호감도 세팅이 필요하다면 여기서 분기처리 가능
    }

    // ★ 신규 — 지금 이 순간 실제로 갖고 있는 정보 개수 (지웠으면 즉시 반영됨)
    public int CurrentUnderstandingCount
        => MemoryManager.Instance != null ? MemoryManager.Instance.GetUnderstandingScore(npcName) : 0;

    // ★ 신규 — 0~100% (maxObtainableUnderstanding보다 실제 정보 개수가 적으면 100% 도달 불가)
    public float UnderstandingPercent
        => Mathf.Clamp01((float)CurrentUnderstandingCount / Mathf.Max(1, maxObtainableUnderstanding)) * 100f;

    // 호감도 범위 (-50 ~ 100) // *
    public void AddAffection(int amount)
    {
        int before = hiddenAffection;
        hiddenAffection = Mathf.Clamp(hiddenAffection + amount, -50, 100);
        PlayerActionLog.Instance?.Record(RecordType.AffectionChange, npcName, before, hiddenAffection);
    }

    // 관계 등급 계산 (기존 NPC.cs에 있던 걸 순수 데이터 쪽으로 옮김 - 정보 전문가 패턴)
    public NPC.RelationshipTier GetRelationshipTier()
    {
        int totalScore = Mathf.RoundToInt(UnderstandingPercent) + (hiddenAffection * 2); // ★ 필드 대신 계산값
        if (totalScore < 10) return NPC.RelationshipTier.Hostile;
        if (totalScore < 30) return NPC.RelationshipTier.Wary;
        if (totalScore < 60) return NPC.RelationshipTier.Acquaintance;
        if (totalScore < 100) return NPC.RelationshipTier.Friend;
        if (totalScore < 150) return NPC.RelationshipTier.Trusted;
        return NPC.RelationshipTier.Romance;
    }
}
