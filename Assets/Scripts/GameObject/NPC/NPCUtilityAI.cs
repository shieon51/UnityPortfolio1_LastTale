using System.Collections.Generic;
using UnityEngine;

// 선택 담당
public class NPCUtilityAI : MonoBehaviour
{
    public List<NPCActionBase> availableActions;
    private Dictionary<NPCActionBase, float> _lastUsedTime = new();
    public NPCActionBase LastUsedAction { get; private set; }

    public NPCActionBase ChooseNextAction(NPCDecisionContext ctx)
    {
        NPCActionBase best = null;
        float bestScore = float.MinValue;

        foreach (var action in availableActions)
        {
            if (_lastUsedTime.TryGetValue(action, out float lastTime) && Time.time - lastTime < action.actionCooldown) continue;

            ctx.LastUsedAction = LastUsedAction;
            float score = action.EvaluateScore(ctx);
            if (score > bestScore) { bestScore = score; best = action; }
        }
        return bestScore > 0f ? best : null;
    }

    public void NotifyUsed(NPCActionBase action) { _lastUsedTime[action] = Time.time; LastUsedAction = action; }

    // 보스 프로필 매니저가 페이즈 전환 시 행동 목록을 통째로 교체할 때 사용
    public void ApplyActionList(List<NPCActionBase> actions) => availableActions = actions;

    public void ApplyProfile(NPCBossProfile profile, int phaseNumber)
    {
        if (profile == null) return;
        var phase = profile.phases.Find(p => p.phaseNumber == phaseNumber);
        if (phase != null) availableActions = phase.availableActions;
    }

    // 에디터 미리보기
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        var sr = GetComponentInChildren<SpriteRenderer>();
        float dir = (sr != null && sr.flipX) ? -1f : 1f;
        if (availableActions == null) return;
        foreach (var action in availableActions)
            if (action != null) action.DrawEditorGizmos(transform.position, dir);
    }
#endif
}