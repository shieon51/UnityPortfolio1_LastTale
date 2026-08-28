// Liel_ConcentrationState.cs (신규)
using UnityEngine;

public class Liel_ConcentrationState : NPCState
{
    private Liel_AI liel;
    private float _channelElapsed = 0f;
    private bool _isRetreating = true;
    private bool _interrupted = false;

    public Liel_ConcentrationState(Liel_AI npc, NPCVisual visual, Transform p) : base(npc, visual, p) { liel = npc; }

    public override void Enter()
    {
        liel.canRotate = false;
        _channelElapsed = 0f; _isRetreating = true; _interrupted = false;
        liel.OnDamageTaken += HandleInterrupted; // 피격 시 중단
        visual.PlayIfChanged(NPCAnimStateNames.Walk);
    }

    public override void Execute()
    {
        var tuning = NPCCombatTuning.Instance;

        if (_isRetreating)
        {
            float dist = player != null ? Vector2.Distance(liel.transform.position, player.position) : 999f;
            if (dist >= tuning.ConcentrationRetreatDistance || _interrupted)
            {
                _isRetreating = false;
                liel.Rb.linearVelocity = Vector2.zero;
                visual.PlayIfChanged(NPCAnimStateNames.Idle); // 전용 "집중" 모션 있으면 나중에 교체
                return;
            }
            float awayDir = (liel.transform.position.x > player.position.x) ? 1f : -1f;
            liel.Rb.linearVelocity = new Vector2(awayDir * tuning.ConcentrationRetreatSpeed, liel.Rb.linearVelocity.y);
            return;
        }

        if (_interrupted || player == null || Vector2.Distance(liel.transform.position, player.position) < tuning.ConcentrationInterruptRange)
        {
            EndChannel();
            return;
        }

        _channelElapsed += Time.deltaTime;
        int manaPerFrame = Mathf.RoundToInt(liel.maxMana * tuning.ConcentrationRecoverRatioPerSecond * Time.deltaTime);
        if (manaPerFrame > 0) liel.RecoverMana(manaPerFrame); // ★ 프레임마다 조금씩 — 중단돼도 이미 받은 만큼은 유지됨

        if (_channelElapsed >= tuning.ConcentrationDuration) EndChannel();
    }

    private void HandleInterrupted(int dmg, CharacterStats attacker) => _interrupted = true;

    private void EndChannel() => liel.StateMachine.ChangeState(new Liel_UtilityDecisionState(liel, visual, player));

    public override void Exit()
    {
        liel.canRotate = true;
        liel.OnDamageTaken -= HandleInterrupted;
    }
}