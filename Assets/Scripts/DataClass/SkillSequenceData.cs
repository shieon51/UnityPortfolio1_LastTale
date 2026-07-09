using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Skill Sequence", menuName = "LastMarchan/Skills/Skill Sequence")]
public class SkillSequenceData : ScriptableObject
{
    [Header("Combo Steps")]
    [Tooltip("순서대로 1타, 2타, 3타 스킬 데이터를 넣으세요.")]
    public List<SkillBase> comboSteps;
}