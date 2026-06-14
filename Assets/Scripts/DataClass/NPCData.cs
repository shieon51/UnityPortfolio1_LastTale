using System;

[Serializable]
public class NPCData
{
    public string npcName; // 예: "Liel", "Diavalu"

    // 감정 데이터
    public int understanding = 0;   // 표면적 이해도 (우정)
    public int hiddenAffection = 0; // 숨겨진 호감도 (애정)

    // 전투 및 스토리 상태 데이터
    public NPC.NPCMode currentMode = NPC.NPCMode.Normal; // 평상시인지 보스전인지
    public int bossPhase = 1; // 보스전 돌입 시 현재 페이즈
    //public bool hasDiscoveredSecret = false; // (예시) 디아베르의 비밀을 들켰는가?

    // 생성자
    public NPCData(string name)
    {
        npcName = name;
        // 캐릭터별 초기 호감도 세팅이 필요하다면 여기서 분기처리 가능
    }

    // 관계 등급 계산 (기존 NPC.cs에 있던 걸 순수 데이터 쪽으로 옮김 - 정보 전문가 패턴)
    public NPC.RelationshipTier GetRelationshipTier()
    {
        int totalScore = understanding + (hiddenAffection * 2);
        if (totalScore < 10) return NPC.RelationshipTier.Hostile;
        if (totalScore < 30) return NPC.RelationshipTier.Wary;
        if (totalScore < 60) return NPC.RelationshipTier.Acquaintance;
        if (totalScore < 100) return NPC.RelationshipTier.Friend;
        if (totalScore < 150) return NPC.RelationshipTier.Trusted;
        return NPC.RelationshipTier.Romance;
    }
}
