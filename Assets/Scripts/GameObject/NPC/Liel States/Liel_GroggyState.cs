// Liel_GroggyState.cs (신규 — 이전에 준 것 대체)
using UnityEngine;
using System.Collections;

public class Liel_GroggyState : NPCState
{
    private Liel_AI liel;
    private float _elapsed = 0f;
    private float _originalDrag;

    public Liel_GroggyState(Liel_AI npc, NPCVisual visual, Transform p) : base(npc, visual, p) 
    { 
        liel = npc; 
    }

    public override void Enter()
    {
        liel.canRotate = false;
        _elapsed = 0f;
        _originalDrag = liel.Rb.linearDamping;

        Vector2 pushDir = player != null ? ((Vector2)liel.transform.position - (Vector2)player.position).normalized : Vector2.left;
        liel.Rb.linearVelocity = Vector2.zero;
        liel.Rb.linearDamping = NPCCombatTuning.Instance.GroggyDrag; // ★ 강제고정 대신 높은 마찰
        liel.Rb.AddForce(pushDir * NPCCombatTuning.Instance.GroggyPushForce, ForceMode2D.Impulse); // ★ 하드코딩 제거

        visual.PlayImmediate(NPCAnimStateNames.Groggy);
        //liel.StartCoroutine(LockAfterPush());
    }

    //private IEnumerator LockAfterPush()
    //{
    //    yield return new WaitForSeconds(NPCCombatTuning.Instance.GroggyPushDuration); // ★ 하드코딩 제거
    //    while (liel.IsGroggy)
    //    {
    //        liel.Rb.linearVelocity = Vector2.zero;
    //        yield return null;
    //    }
    //}

    public override void Execute()
    {
        _elapsed += Time.deltaTime;

        if (!liel.IsGroggy)
        {
            liel.StateMachine.ChangeState(new Liel_UtilityDecisionState(liel, visual, player));
            return;
        }

        // ★ Hit 등으로 끼어들었다가 리액션 포즈가 끝나서 돌아왔는데, 아직 그로기로 안 돌아온 상태면
        if (!visual.IsShowingReactionPose && visual.CurrentState != NPCAnimStateNames.Groggy)
        {
            float normalizedTime = liel.GroggyDuration > 0f ? Mathf.Clamp01(_elapsed / liel.GroggyDuration) : 0f;
            visual.PlayImmediate(NPCAnimStateNames.Groggy, normalizedTime); // ★ 끊긴 지점부터 이어재생
        }
    }

    public override void Exit()
    {
        liel.canRotate = true;
        liel.Rb.linearDamping = _originalDrag; // ★ 원래 값 복원
    }
}