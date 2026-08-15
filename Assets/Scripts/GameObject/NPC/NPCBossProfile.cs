using System.Collections.Generic;
using UnityEngine;

// NPCBossProfile.cs — "이 NPC가 이 난이도일 때, 각 페이즈마다 뭘 쓸지"를 통째로 담는 애셋
[CreateAssetMenu(menuName = "LastMarchan/NPC/Boss Profile")]
public class NPCBossProfile : ScriptableObject
{
    public string npcName; // NPCManager의 npcName과 매칭
    public BossDifficultyTier difficultyTier;
    [TextArea] public string storyBranchNote; // 예: "3회차 타락 버전"

    [Header("체력 오버라이드")]
    [Tooltip("0이면 오버라이드 없음(프리팹의 실제 maxHealth 사용). 훈련모드처럼 체력 풀을 줄이고 싶을 때만 값 입력")]
    public int maxHealthOverride = 0;

    [System.Serializable]
    public class PhaseConfig
    {
        public int phaseNumber = 1;
        public List<NPCActionBase> availableActions = new();

        [Header("스탯 오버라이드 (0 = 변화 없음)")]
        public int agilityModifier = 0; // ★ 절대값이 아니라 "증감치"임에 주의 (AddModifier와 짝이라 그래)

        [Header("스토리 분기 오버라이드 (비워두면 위 기본값 사용)")]
        public List<StyleOverride> styleOverrides = new();

    }

    [System.Serializable]
    public class StyleOverride
    {
        public Liel_AI.LielCombatStyle style; // ★ 지금은 리엘 전용 enum을 직접 참조. 보스가 늘어나면 그때 일반화하면 됨
        public List<NPCActionBase> availableActionsOverride = new(); // 비어있으면 phase 기본값 사용
        public CharacterVisualProfile appearanceOverride; // null이면 외형 안 바꿈
    }

    public List<PhaseConfig> phases = new List<PhaseConfig>();
}