// PlayerGuard.cs (½Å±Ô)
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
        if (_motor != null && _motor.IsActionLocked) return;
        if (_combat != null && _combat.IsAttacking) return;

        if (Input.GetKeyDown(guardKey))
        {
            _stats.StartGuard();
            _visual?.PlayState(ResolveContextState());
        }
        else if (Input.GetKeyUp(guardKey))
        {
            _stats.StopGuard();
            _visual?.ReturnToLocomotion();
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