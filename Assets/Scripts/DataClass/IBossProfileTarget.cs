// IBossProfileTarget.cs (신규)
using System.Collections.Generic;

// "난이도별 AI 프로필을 적용받을 수 있는 보스"라면 무조건 구현해야 하는 계약.
// BossAIProfileWindow 같은 도구가 Liel_AI를 직접 몰라도 되게 해줌 (SOLID: 의존성 역전)
public interface IBossProfileTarget
{
    BossDifficultyTier CurrentDifficultyTier { get; set; }
    int BossPhase { get; }
    List<NPCBossProfile> BossProfiles { get; }
    void ApplyResolvedProfile(NPCBossProfile profile, bool isBattleStart = false);
}