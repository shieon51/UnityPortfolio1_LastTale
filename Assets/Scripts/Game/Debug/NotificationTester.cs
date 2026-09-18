using UnityEngine;

// 알림 큐 동작 확인용 임시 스크립트. 확인이 끝나면 오브젝트째 지우면 된다.
public class NotificationTester : MonoBehaviour
{
    [ContextMenu("1) 알림 하나")]
    private void TestSingle()
    {
        NotificationManager.Instance.Show("첫 번째 알림", NotificationType.Info);
    }

    [ContextMenu("2) 연속 3개 (큐 확인)")]
    private void TestQueue()
    {
        NotificationManager.Instance.Show("공중에서는 시간을 고정할 수 없습니다", NotificationType.Warning);
        NotificationManager.Instance.Show("마나가 부족합니다", NotificationType.Failure);
        NotificationManager.Instance.Show("새로운 정보를 얻었습니다", NotificationType.Info);
    }

    [ContextMenu("3) 같은 문구 연속 (중복 무시 확인)")]
    private void TestDuplicate()
    {
        NotificationManager.Instance.Show("마나가 부족합니다", NotificationType.Failure);
        NotificationManager.Instance.Show("마나가 부족합니다", NotificationType.Failure);
        NotificationManager.Instance.Show("마나가 부족합니다", NotificationType.Failure);
    }

    [ContextMenu("4) 일시정지 중 알림 (timeScale 0)")]
    private void TestWhilePaused()
    {
        StopAllCoroutines();
        StartCoroutine(PauseRoutine());
    }

    private System.Collections.IEnumerator PauseRoutine()
    {
        Time.timeScale = 0f;
        NotificationManager.Instance.Show("일시정지 중에도 보이고 사라져야 함", NotificationType.Info);

        // ★ Invoke나 WaitForSeconds는 timeScale 0에서 진행되지 않으므로 Realtime 사용
        yield return new WaitForSecondsRealtime(3f);

        Time.timeScale = 1f;
        Debug.Log("[NotificationTester] 시간 복구 완료");
    }

    private void ResumeTime() => Time.timeScale = 1f;

    [ContextMenu("5) 큐 비우기")]
    private void TestClear()
    {
        NotificationManager.Instance.ClearAll();
    }

    [ContextMenu("6) 시간 복구 (timeScale = 1)")]
    private void ForceResume() => Time.timeScale = 1f;
}