using System.Collections.Generic;
using UnityEngine;

// 캐릭터 1명당 1개 (Sora용, 나중에 Liel용 등)
[CreateAssetMenu(menuName = "LastMarchan/Visual/Character Visual Profile")]
public class CharacterVisualProfile : ScriptableObject
{
    public string characterId;
    public List<FormStageVisualSet> formStages;

    public FormStageVisualSet GetStageSet(int stage)
        => formStages.Find(s => s.formStage == stage) ?? formStages[0];
}