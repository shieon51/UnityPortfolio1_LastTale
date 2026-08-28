// Liel_ExecutingActionState.cs 전체 교체
using System.Collections;
using UnityEngine;

public class Liel_ExecutingActionState : NPCState
{
    private Liel_AI liel;
    private NPCActionBase _action;

    private float _pendingSlowmoIntensity = 0f; // ★ 신규 필드 (0~1)

    private Coroutine _runActionHandle;

    public Liel_ExecutingActionState(Liel_AI npc, NPCVisual visual, Transform p, NPCActionBase action) : base(npc, visual, p)
    {
        liel = npc;
        _action = action;
    }

    public override void Enter()
    {
        Debug.Log($"[DBG Action] Enter: {_action.name} at {Time.time:F3}"); // *

        liel.canRotate = false;
        _runActionHandle = liel.StartCoroutine(RunAction());
    }

    public void ForceCancel() // ★ 외부(그로기 등)에서 정확히 이것만 끊을 때 사용
    {
        if (_runActionHandle != null) liel.StopCoroutine(_runActionHandle);
    }

    private IEnumerator RunAction()
    {
        try
        {
            if (_action is NPCSkillBase skill)
            {
                float leadTime = 0f;
                if (skill.hasTelegraph)
                {
                    var defender = PlayerManager.Instance.CurrentCharacter;
                    leadTime = CombatFormulaService.Instance.CalculateTelegraphDuration(skill.baseTelegraphDuration, liel, defender);
                    Debug.Log($"[DBG Telegraph] {skill.name}: leadTime={leadTime:F3}, timeToActive={skill.timeToActive:F3}, preDelay={Mathf.Max(0f, leadTime - skill.timeToActive):F3}"); //?

                    float overflow = CombatFormulaService.Instance.CalculateTelegraphOverflow(skill.baseTelegraphDuration, liel, defender); // ★ 추가
                    _pendingSlowmoIntensity = Mathf.Clamp01(overflow * NPCCombatTuning.Instance.SlowmoOverflowScale);

                    if (WorldSpaceTelegraphIndicator.Instance != null)
                    {
                        WorldSpaceTelegraphIndicator.Instance.SetFollowTarget(liel.transform);
                        liel.StartCoroutine(WorldSpaceTelegraphIndicator.Instance.PlayCountdown(leadTime));
                    }
                }

                float preDelay = Mathf.Max(0f, leadTime - skill.timeToActive);
                if (preDelay > 0f)
                    yield return liel.StartCoroutine(RepositionWhileWaiting(preDelay)); // ★ 가만히 안 서있고 계속 거리 조절

                liel.UseMana(skill.GetManaCost(1));

                if (_pendingSlowmoIntensity > 0f) // ★ 추가 — 공격 시전 직전에 슬로우 적용
                    yield return liel.StartCoroutine(ApplyAttackSlowmo(_pendingSlowmoIntensity));
            }

            yield return liel.StartCoroutine(_action.Execute(liel, visual, player));
            liel.GetComponent<NPCUtilityAI>().NotifyUsed(_action);
        }
        finally
        {
            liel.StateMachine.ChangeState(new Liel_RecoveryState(liel, visual, player, _action.recoveryDuration));
        }
    }

    private IEnumerator ApplyAttackSlowmo(float intensity)
    {
        var tuning = NPCCombatTuning.Instance;
        float duration = NPCCombatTuning.Instance.MaxSlowmoDuration * intensity;
        float speed = Mathf.Lerp(1f, tuning.SlowmoAnimatorSpeed, intensity); // ★ 강도 0=평소속도, 1=최대로 느림

        liel.RaiseAttackSlowmo(intensity, duration); // ★ 화면 연출 훅 — 지금은 구독자 없어도 됨

        visual.SetAnimatorSpeed(speed);
        yield return new WaitForSeconds(duration);
        visual.SetAnimatorSpeed(1f);
    }

    // 예고가 뜬 채로 대기하는 동안, 이동 행동만 계속 재평가하며 진행
    private IEnumerator RepositionWhileWaiting(float duration)
    {
        //var ai = liel.GetComponent<NPCUtilityAI>();
        //float startTime = Time.time; // ★ 시작 시각 고정
        //while (Time.time - startTime < duration) // ★ 서브 액션이 몇 프레임을 쓰든 실제 경과시간으로 정확히 판단
        //{
        //    var movementAction = ai.ChooseMovementOnly(BuildContext());
        //    if (movementAction != null)
        //    {
        //        float remaining = duration - (Time.time - startTime);
        //        yield return liel.StartCoroutine(RunWithDeadline(movementAction, remaining)); // ★ 마감 넘기면 강제 중단
        //    }
        //    else
        //    {
        //        visual.PlayIfChanged(NPCAnimStateNames.Idle);
        //        yield return null;
        //    }
        //} //?

        liel.Rb.linearVelocity = new Vector2(0f, liel.Rb.linearVelocity.y); // ★ 확실히 정지
        visual.PlayIfChanged(NPCAnimStateNames.Idle);
        yield return new WaitForSeconds(duration); // ★ 더 이상 별도 행동 선택 안 함
    }

    // 주어진 행동을 실행하되, maxDuration이 지나면 완료를 기다리지 않고 그 자리에서 끊음.
    // "텔레그래프가 다 찼는데 이동이 안 끝나서 공격이 밀리는" 일을 원천 차단.
    private IEnumerator RunWithDeadline(NPCActionBase action, float maxDuration)
    {
        bool done = false;
        Coroutine inner = liel.StartCoroutine(RunAndFlag(action, () => done = true));

        float elapsed = 0f;
        while (!done && elapsed < maxDuration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!done)
        {
            liel.StopCoroutine(inner);
            liel.Rb.linearVelocity = Vector2.zero; // ★ 중간에 끊긴 이동의 잔여 관성 제거 (안전장치)
        }
    }

    private IEnumerator RunAndFlag(NPCActionBase action, System.Action onDone)
    {
        yield return liel.StartCoroutine(action.Execute(liel, visual, player));
        onDone();
    }

    private NPCDecisionContext BuildContext()
    {
        var playerCombat = player.GetComponentInChildren<PlayerCombat>();
        return new NPCDecisionContext
        {
            Self = liel,
            Player = player,
            DistanceToPlayer = Vector2.Distance(liel.transform.position, player.position),
            SelfHealthPercent = liel.maxHealth > 0 ? (float)liel.currentHealth / liel.maxHealth : 0f,
            SelfManaPercent = liel.maxMana > 0 ? (float)liel.currentMana / liel.maxMana : 0f,
            PlayerIsAttacking = playerCombat != null && playerCombat.IsAttacking,
        };
    }

    public override void Exit() => liel.canRotate = true;
}