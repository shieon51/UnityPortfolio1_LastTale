using UnityEngine;

// DebugLoopTools.cs (신규, 순수 정적 헬퍼)
public static class DebugLoopTools
{
    // ★ 2-B — 실제 경로 3(닻 없이 사망)과 같은 절차를 쓴다. 기존에는 따로 구현돼 있어
    //   닻이 남고, 몸이 초기화되지 않고, 회귀 기록이 남지 않았다
    public static void AdvanceToNextLoop()
    {
        if (!(PlayerManager.Instance?.CurrentCharacter is SoraStats sora))
        {
            Debug.LogWarning("[Debug] 소라를 조종 중일 때만 다음 회차로 넘길 수 있습니다");
            return;
        }

        TimeLoopManager.Instance.EndCurrentLoop(LoopEndType.DebugSkip);   // ★ 3-A — 실제 회귀처럼 회차를 먼저 닫는다
        sora.loopCount++;   // 실제 회귀(HandleDeath)와 같은 방식. 새 회차 번호는 Loop 기록에 함께 남는다
        DialogueManager.Instance.ResetStoryState(); // ink 자체 지역변수(친밀도 등)도 새로 시작 — 디버그에만 있는 단계
        TimeLoopManager.Instance.StartNewLoopFromDay1(sora, RecordKeys.DebugSource);
        // MemoryManager._acquiredFlags(정보/기억)는 절대 안 건드림 — 이게 유지되는지 확인하는 게 목적

        Debug.Log("[Debug] 다음 회차로 이동 (경로 3과 같은 절차, 기억은 유지됨)");
    }

    // ★ 2-B — 오버뷰 창의 회귀 횟수 수정. 직접 대입하지 않고 기록을 남긴다
    public static void SetLoopCount(int value)
    {
        if (PlayerManager.Instance?.CurrentCharacter is SoraStats sora) sora.SetLoopCountForDebug(value);
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
        // ★ 기록 지우기는 다른 초기화가 모두 끝난 뒤, 회차 이력을 새로 열기 직전에 한다.
        //   예전에는 ResetToDay1보다 먼저 지워, 시각을 되돌리며 남긴 기록이 옛 회차 번호(예: 11회차)로 새 세계에 남았다
        PlayerActionLog.Instance.ClearAll();
        TimeLoopManager.Instance.ResetForHardReset();   // ★ 3-A — 닻·회차 이력도 지우고 첫 회차(0)를 새로 연다. loopCount = 0 뒤에 불러야 한다

        var cfg = SceneLoader.Instance.startConfig;
        SceneLoader.Instance.LoadScene(cfg.startSceneID, cfg.startPosition);
        Debug.Log("[Debug] 완전 리셋 완료");
    }
}