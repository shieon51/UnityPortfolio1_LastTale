using UnityEngine;

// NPCAnimationRelay.cs (신규) — Visual 오브젝트에 부착 (PlayerAnimationRelay와 동일 역할)
public class NPCAnimationRelay : MonoBehaviour
{
    private NPC _npc;
    private void Awake() => _npc = GetComponentInParent<NPC>();

    public void AE_DashStart() => _npc.NotifyDashStart();     // 팍 치고 대시 시작
    public void AE_HitboxStart() => _npc.NotifyHitboxStart(); // 한발 내밀며 찌르기 (판정 켜짐)
    public void AE_SlideStart() => _npc.NotifySlideStart();   // 끼익 멈춤 시작
    public void AE_ActionEnd() => _npc.NotifyActionEnd();     // 애니메이션 끝, 정리
    public void PlaySkillVFX(string cueId) => _npc.PlayCurrentSkillVFX(cueId);
    public void PlayCameraCue(string cueId) => _npc.PlayCurrentSkillCamera(cueId); // 또는 NPC 버전
}