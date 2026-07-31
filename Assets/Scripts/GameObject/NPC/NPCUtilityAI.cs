using System.Collections.Generic;
using UnityEngine;

// 선택 담당
public class NPCUtilityAI : MonoBehaviour
{
    public List<NPCSkillBase> availableSkills;
    private Dictionary<NPCSkillBase, float> _lastUsedTime = new();
    public NPCSkillBase LastUsedSkill { get; private set; }

    public NPCSkillBase ChooseNextAction(NPCDecisionContext ctx)
    {
        NPCSkillBase best = null;
        float bestScore = float.MinValue;

        foreach (var skill in availableSkills)
        {
            if (_lastUsedTime.TryGetValue(skill, out float lastTime) && Time.time - lastTime < skill.actionCooldown) continue;

            ctx.LastUsedSkill = LastUsedSkill;
            float score = skill.EvaluateScore(ctx);
            if (score > bestScore) { bestScore = score; best = skill; }
        }
        return bestScore > 0f ? best : null; // 0점 이하면 아예 쓸 만한 게 없다는 뜻
    }

    public void NotifyUsed(NPCSkillBase skill)
    {
        _lastUsedTime[skill] = Time.time;
        LastUsedSkill = skill;
    }

    // 에디터 미리보기
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        var sr = GetComponentInChildren<SpriteRenderer>();
        float dir = (sr != null && sr.flipX) ? -1f : 1f;
        foreach (var skill in availableSkills)
            if (skill != null) skill.DrawEditorGizmos(transform.position, dir);
    }
#endif
}