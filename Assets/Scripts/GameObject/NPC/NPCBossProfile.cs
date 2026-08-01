using System.Collections.Generic;
using UnityEngine;

// NPCBossProfile.cs — "이 NPC가 이 난이도일 때, 각 페이즈마다 뭘 쓸지"를 통째로 담는 애셋
[CreateAssetMenu(menuName = "LastMarchan/NPC/Boss Profile")]
public class NPCBossProfile : ScriptableObject
{
    public string npcName; // NPCManager의 npcName과 매칭
    public BossDifficultyTier difficultyTier;
    [TextArea] public string storyBranchNote; // 예: "3회차 타락 버전"

    [System.Serializable]
    public class PhaseConfig
    {
        public int phaseNumber = 1;
        public List<NPCActionBase> availableActions = new();
    }

    public List<PhaseConfig> phases = new List<PhaseConfig>();
}