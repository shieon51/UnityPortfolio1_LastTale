using UnityEngine;

// TypewriterText 동작 확인용 임시 스크립트. 확인이 끝나면 오브젝트째 지우면 된다.
public class TypewriterTester : MonoBehaviour
{
    public TypewriterText typewriter;

    private int _completedCount;

    private void OnEnable()
    {
        if (typewriter != null) typewriter.OnFullyDisplayed += HandleCompleted;
    }

    private void OnDisable()
    {
        if (typewriter != null) typewriter.OnFullyDisplayed -= HandleCompleted;
    }

    private void HandleCompleted()
    {
        _completedCount++;
        Debug.Log($"[TypewriterTester] 완료 이벤트 발생 — 누적 {_completedCount}회");
    }

    [ContextMenu("1) 긴 대사 (들썩임 확인)")]
    private void TestLong()
    {
        _completedCount = 0;
        typewriter.Play("안녕하십니까. 무슨 말을 해야 최대한 길게 채울 수 있을까요. " +
                        "텍스트 채우기 참 힘드네요. 글자 수 어디까지 되려나 확인해야겠군요. " +
                        "스탠딩 일러스트는 넣으면 어떨까 넣어봤는데 너무 좋군요.");
    }

    [ContextMenu("2) 리치 텍스트 태그")]
    private void TestRichText()
    {
        _completedCount = 0;
        typewriter.Play("이건 <color=#FF6666>붉은 글씨</color>와 <b>굵은 글씨</b>가 섞인 대사다.");
    }

    [ContextMenu("3) 빈 문자열 (완료 이벤트가 뜨면 안 됨)")]
    private void TestEmpty()
    {
        _completedCount = 0;
        typewriter.Play("");
        Debug.Log($"[TypewriterTester] 빈 문자열 재생 후 완료 횟수: {_completedCount} (0이어야 정상)");
    }

    [ContextMenu("4) 스킵 (완료 이벤트 1회만)")]
    private void TestSkip()
    {
        _completedCount = 0;
        typewriter.Play("이 대사를 치는 도중에 스킵이 호출된다. 완료 이벤트는 한 번만 발생해야 한다.");
        StartCoroutine(SkipSoon());
    }

    private System.Collections.IEnumerator SkipSoon()
    {
        yield return new WaitForSecondsRealtime(0.3f);
        typewriter.Skip();
        yield return new WaitForSecondsRealtime(1f);
        Debug.Log($"[TypewriterTester] 스킵 후 완료 횟수: {_completedCount} (1이어야 정상)");
    }

    [ContextMenu("5) 일시정지 중 타이핑")]
    private void TestPaused()
    {
        StopAllCoroutines();
        StartCoroutine(PausedRoutine());
    }

    private System.Collections.IEnumerator PausedRoutine()
    {
        Time.timeScale = 0f;
        typewriter.Play("일시정지 중에도 글자가 이어서 찍혀야 한다.");
        yield return new WaitForSecondsRealtime(3f);
        Time.timeScale = 1f;
    }
}