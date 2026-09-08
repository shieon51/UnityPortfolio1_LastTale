using System.Linq;
using UnityEngine;

// SpeakerResolver.cs (신규) — DialogueManager/전투대사 양쪽이 공용으로 쓸 화자 탐색
// 전투 중 실시간 연출 대사 관련
public static class SpeakerResolver
{
    public static Transform Resolve(string key)
    {
        if (key == "Player") return PlayerManager.Instance?.CurrentCharacter?.transform;
        return Object.FindObjectsOfType<NPC>().FirstOrDefault(n => n.npcName == key)?.transform;
    }
}