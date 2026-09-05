// TimeLoopManager.cs (신규) — 실제 게임 내 회귀 로직 (디버그 도구인 DebugLoopTools와는 별개)
using System.Collections.Generic;
using UnityEngine;

public class TimeLoopManager : Singleton<TimeLoopManager>
{
    private TimeAnchorSnapshot _anchor;
    public bool HasAnchor => _anchor != null;

    public int setAnchorManaCost = 20; // 임시 값, 밸런스 조정 필요
    public int returnManaCost = 30;

    // "시간 고정" — 공중/비행 중엔 불가 (5번 항목과 연동)
    public bool TrySetAnchor()
    {
        var sora = PlayerManager.Instance.CurrentCharacter as SoraStats;
        if (sora == null) return false;

        var motor = sora.GetComponent<IPlayerMotor>();
        if (sora.IsFlightForm || (motor != null && !motor.IsGrounded))
        {
            NotificationManager.Instance?.Show("공중에서는 시간을 고정할 수 없습니다", NotificationType.Warning);
            return false;
        }
        if (sora.currentMana < setAnchorManaCost) return false;

        sora.UseMana(setAnchorManaCost);
        _anchor = new TimeAnchorSnapshot
        {
            sceneID = SceneLoader.Instance.CurrentSceneID,
            position = sora.transform.position,
            day = TimeManager.Instance.currentDay,
            hour = TimeManager.Instance.currentHour,
            level = sora.level,
            maxHealth = sora.maxHealth,
            maxMana = sora.maxMana,
            experience = sora.experience,
            acquiredMemoryFlags = new HashSet<string>(MemoryManager.Instance.GetAllAcquired()),
        };
        return true;
    }

    // 사망 시 진입점 — keepBodyLevel은 나중에 선택 UI에서 넘겨줄 값 (지금은 항상 true로 임시 고정 가능)
    public void HandleDeath(bool keepBodyLevel = true)
    {
        var sora = PlayerManager.Instance.CurrentCharacter as SoraStats;
        sora.loopCount++;

        bool canReturnToAnchor = HasAnchor && sora.currentMana >= returnManaCost;

        if (canReturnToAnchor)
        {
            sora.UseMana(returnManaCost);
            if (!keepBodyLevel) // 몸 레벨을 앵커 시점으로 되돌림 (정보/기억은 유지 — highestLevelReached도 유지됨)
            {
                sora.level = _anchor.level;
                sora.maxHealth = _anchor.maxHealth;
                sora.maxMana = _anchor.maxMana;
                sora.experience = _anchor.experience;
            }
            sora.currentHealth = Mathf.Max(10, sora.currentHealth);
            sora.currentMana = Mathf.Min(sora.currentMana, sora.maxMana);
            LoadScene(_anchor.sceneID, _anchor.position, _anchor.day, _anchor.hour);
        }
        else if (HasAnchor) // 앵커는 있지만 복귀할 마나가 없음 → 강제로 앵커 시점 상태+정보로
        {
            MemoryManager.Instance.RestoreAcquired(_anchor.acquiredMemoryFlags);
            sora.level = _anchor.level;
            sora.maxHealth = _anchor.maxHealth;
            sora.maxMana = _anchor.maxMana;
            sora.experience = _anchor.experience;
            sora.currentHealth = Mathf.Max(10, sora.maxHealth / 2);
            sora.currentMana = sora.maxMana;
            LoadScene(_anchor.sceneID, _anchor.position, _anchor.day, _anchor.hour);
        }
        else // 앵커 자체가 없음 → Day1 처음으로 (시간의 결정체=경험은 안 잊음, highestLevelReached 등은 그대로 남김)
        {
            MemoryManager.Instance.ClearAllAcquired();
            var cfg = SceneLoader.Instance.startConfig;
            TimeManager.Instance.ResetToDay1();
            LoadScene(cfg.startSceneID, cfg.startPosition, 1, 0);
        }
    }

    private void LoadScene(int sceneID, Vector2 pos, int day, int hour)
    {
        TimeManager.Instance.SetTime(day, hour); // ★ TimeManager에 이 메서드 추가 필요 (아래 참고)
        SceneLoader.Instance.LoadScene(sceneID, pos);
    }
}