using System;
using UnityEngine;

[Serializable]
public class NPCData
{
    public string npcName; // 예: "Liel", "Diavalu"

    // 감정 데이터
    //[Obsolete("더 이상 직접 저장 안 함 — CurrentUnderstandingCount/UnderstandingPercent 사용")]
    //public int understanding = 0; // ★ 이제 로직에서 안 씀, 과거 세이브 호환용으로만 남겨둠   // 표면적 이해도 (우정)

    public int hiddenAffection = 0; // 숨겨진 호감도 (애정)

    [Tooltip("이 NPC를 '완전히' 이해하는 데 필요한 총량. 실제 만든 정보 개수보다 크게 잡으면 100% 도달이 원천적으로 불가능해짐 (일부러 그런 캐릭터를 만들고 싶을 때)")]
    public int maxObtainableUnderstanding = 100; // ★ 신규

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
    public void AddAffection(int amount) => hiddenAffection = Mathf.Clamp(hiddenAffection + amount, -50, 100);

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
