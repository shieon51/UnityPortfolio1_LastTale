using UnityEngine;

// BattleBarkData.cs (신규) — 전투 중 짧은 대사(대화 트리 아님)
[CreateAssetMenu(menuName = "LastMarchan/Narrative/Battle Bark")]
public class BattleBarkData : ScriptableObject
{
    public string speakerKey;
    public string speakerDisplayName;
    [TextArea] public string[] possibleLines; // 여러 개면 무작위 선택
    public float displayDuration = 3f;
}