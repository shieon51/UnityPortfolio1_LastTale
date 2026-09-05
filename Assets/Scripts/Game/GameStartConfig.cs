// GameStartConfig.cs (신규)
using UnityEngine;

// 플레이어 시작 위치 관련
[CreateAssetMenu(menuName = "LastMarchan/Game Start Config")]
public class GameStartConfig : ScriptableObject
{
    public int startSceneID = 1;
    public Vector2 startPosition;
}