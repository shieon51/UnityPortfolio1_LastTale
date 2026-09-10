using UnityEngine;

// DebugLoopTools.cs (신규, 순수 정적 헬퍼)
public static class DebugLoopTools
{
    public static void AdvanceToNextLoop()
    {
        if (PlayerManager.Instance.CurrentCharacter is SoraStats sora) sora.loopCount++;

        NPCManager.Instance.ResetAffectionForNewLoop(); // ★ 추가
        TimeManager.Instance.ResetToDay1();
        MemoryManager.Instance.ClearAllCounters(); // ★ 만남 카운터는 리셋(회차마다 "오늘 처음 만남"이어야 하니까)
        DialogueManager.Instance.ResetStoryState(); // ink 자체 지역변수(친밀도 등)도 새로 시작
        // MemoryManager._acquiredFlags(정보/기억)는 절대 안 건드림 — 이게 유지되는지 확인하는 게 목적

        var cfg = SceneLoader.Instance.startConfig;
        SceneLoader.Instance.LoadScene(cfg.startSceneID, cfg.startPosition);
        Debug.Log("[Debug] 다음 회차로 이동 (기억은 유지됨)");
    }

    public static void FullReset()
    {
        MemoryManager.Instance.ClearAllAcquired();
        MemoryManager.Instance.ClearAllCounters();
        NPCManager.Instance.ResetAllNPCData(); // ★ 추가 — 완전 리셋은 rememberAcrossLoops도 무시하고 전부 초기화
        DialogueManager.Instance.ResetStoryState();
        TimeManager.Instance.ResetToDay1();

        if (PlayerManager.Instance.CurrentCharacter is SoraStats sora)
        {
            sora.loopCount = 0;
            // 레벨/경험치도 초기화하려면 PlayableCharacter에 리셋 메서드가 필요 — 아래 참고
        }

        var cfg = SceneLoader.Instance.startConfig;
        SceneLoader.Instance.LoadScene(cfg.startSceneID, cfg.startPosition);
        Debug.Log("[Debug] 완전 리셋 완료");
    }
}