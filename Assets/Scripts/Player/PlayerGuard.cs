// PlayerGuard.cs (신규)
using System.Collections.Generic;
using UnityEngine;

public class PlayerGuard : MonoBehaviour
{
    public KeyCode guardKey = KeyCode.F;

    [System.Serializable]
    public struct GuardContextVariant { public MovementContext context; public string animStateName; }
    public List<GuardContextVariant> contextVariants = new();

    private CharacterStats _stats;
    private PlayerVisual _visual;
    private IPlayerMotor _motor;
    private IFormStageProvider _formProvider;
    private PlayerCombat _combat;

    private void Awake()
    {
        _stats = GetComponent<CharacterStats>();
        _visual = GetComponentInChildren<PlayerVisual>();
        _motor = GetComponent<IPlayerMotor>();
        _formProvider = GetComponent<IFormStageProvider>();
        _combat = GetComponentInChildren<PlayerCombat>();
    }

    private void Update()
    {
        bool wantsGuard = Input.GetKey(guardKey); // ★ Down/Up 엣지 대신, 지금 눌려있는지 그 자체를 매 프레임 확인

        if (_motor != null && _motor.IsActionLocked && !(_combat != null && _combat.IsAttacking))
        {
            // 완전히 잠긴 상태에서도 데이터(isGuarding)만큼은 실제 키 상태와 항상 일치시켜서, 놓친 프레임이 있어도 즉시 자가 교정
            if (_stats.isGuarding != wantsGuard)
            {
                if (wantsGuard) _stats.StartGuard(); else _stats.StopGuard();
            }
            return;
        }

        if (wantsGuard && !_stats.isGuarding)
        {
            _stats.StartGuard();
            if (_combat == null || !_combat.IsAttacking) _visual?.PlayState(ResolveContextState());
        }
        else if (!wantsGuard && _stats.isGuarding)
        {
            _stats.StopGuard();
            if (_combat == null || !_combat.IsAttacking) _visual?.ReturnToLocomotion();
        }
    }

    private string ResolveContextState()
    {
        var context = ResolveMovementContext();
        foreach (var v in contextVariants) if (v.context == context) return v.animStateName;
        return PlayerAnimStateNames.Guard;
    }

    private MovementContext ResolveMovementContext()
    {
        if (_formProvider != null && _formProvider.IsFlightForm) return MovementContext.Flying;
        if (_motor != null && _motor.IsGrounded) return MovementContext.Grounded;
        return MovementContext.Airborne;
    }
}