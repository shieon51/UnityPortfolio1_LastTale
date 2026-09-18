using UnityEngine;

// 빙의 중복 구독 확인용 임시 스크립트. 확인이 끝나면 오브젝트째 지우면 된다.
public class PossessionTester : MonoBehaviour
{
    [ContextMenu("1) 같은 캐릭터에 3번 빙의")]
    private void RepossessThreeTimes()
    {
        var current = PlayerManager.Instance.CurrentCharacter;
        if (current == null) { Debug.LogWarning("[PossessionTester] 현재 캐릭터가 없음"); return; }

        for (int i = 0; i < 3; i++) PlayerManager.Instance.PossessCharacter(current);
        Debug.Log("[PossessionTester] 3번 빙의 완료 — 이제 2번을 눌러 호출 횟수를 확인");
    }

    [ContextMenu("2) 체력 1 깎기 (로그 횟수 확인)")]
    private void DamageOnce()
    {
        Debug.Log("---- 여기부터 UpdateSliderUI 호출 횟수를 센다 ----");
        PlayerManager.Instance.TakeDamageToCurrentCharacter(1);
    }
}