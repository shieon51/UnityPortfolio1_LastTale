using UnityEngine;

// NPCAnimationRelay.cs (신규) — Visual 오브젝트에 부착 (PlayerAnimationRelay와 동일 역할)
public class NPCAnimationRelay : MonoBehaviour
{
    private NPC _npc;
    private void Awake() => _npc = GetComponentInParent<NPC>();

    public void AE_ActiveStart() => _npc.NotifyActiveStart(); // 선딜 끝, 진짜 판정/이동 시작
    public void AE_ActiveEnd() => _npc.NotifyActiveEnd();     // 액티브 끝, 후딜(감속) 시작
    public void PlaySkillVFX(string cueId) => _npc.PlayCurrentSkillVFX(cueId);
}