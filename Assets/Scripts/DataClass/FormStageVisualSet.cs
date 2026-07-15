// FormStageVisualSet.cs (½Å±Ô)
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "LastMarchan/Visual/Form Stage Visual Set")]
public class FormStageVisualSet : ScriptableObject
{
    public int formStage; // 0, 1, 2 ...
    public List<BodyPartVisualData> parts;
}