using System;

// PlayerController가 구현.
// 시즌2/3에서 완전히 다른 이동 로직(예: 논-점프, 그리드 이동 등)을 만들어도
// 이 인터페이스만 구현하면 PlayerVisual/PlayerCombat/PlayerCutsceneAnimator를 그대로 재사용 가능
public interface IPlayerMotor
{
    float CurrentSpeed { get; }
    float VelocityY { get; }
    bool IsGrounded { get; }
    bool IsDashing { get; }
    bool CanFlip { get; }
    bool IsDialogueLocked { get; }
    bool IsActionLocked { get; }
    bool IsExternallyLocked { get; }  // 내가 스스로 공격 중이라서 잠긴 것은 무시하고 그 외의 진짜 외부 잠금
    float EffectiveHorizontalInput { get; } // 잠금/방어 등이 반영된 이번 프레임의 실제 유효 입력
    bool IsKnockedBack { get; }
    float SpeedMultiplier { get; }

    event Action OnJumpTriggered;
    event Action OnFallStarted;
    event Action OnLanded;
}