using System.Collections;
using UnityEngine;

// NPCSkillBase.cs  — Player의 SkillBase와 같은 사상, 선택 방식만 다름
public abstract class NPCSkillBase : ScriptableObject
{
    [Header("Basic Info")]
    public string skillName;
    public string animStateName;
    public int manaCost;
    public float actionCooldown = 1.5f;

    [Header("Telegraph")]
    public bool hasTelegraph = true;
    public float baseTelegraphDuration = 0.8f;

    [Header("Targeting")]
    public LayerMask targetableLayers;

    // 점수가 높을수록 이번 턴에 선택될 확률이 높음
    public abstract float EvaluateScore(NPCDecisionContext ctx);

    public abstract IEnumerator Execute(NPC self, NPCVisual visual, Transform target);
}