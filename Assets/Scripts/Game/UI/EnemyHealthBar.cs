using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class EnemyHealthBar : MonoBehaviour
{
    public Slider hpSlider;
    public Vector3 offset = new Vector3(0, 0.8f, 0); // 몬스터 머리 위 오프셋

    private CanvasGroup canvasGroup;
    private Coroutine fadeCoroutine;
    private Transform targetEnemy; // 내가 쫓아다닐 몬스터

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }

    // 풀에서 꺼내질 때 몬스터가 호출해 줄 초기화 함수
    public void Init(Transform target, int maxHp, int currentHp)
    {
        targetEnemy = target;
        hpSlider.maxValue = maxHp;
        hpSlider.value = currentHp;

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        canvasGroup.alpha = 1f; // 즉시 보임

        // 3초 뒤에 사라지는 코루틴 시작
        fadeCoroutine = StartCoroutine(WaitAndFadeOut(3.0f));
    }

    public void UpdateHealth(int currentHp)
    {
        hpSlider.value = currentHp;

        // 맞을 때마다 투명도 원상복구 및 타이머 리셋
        canvasGroup.alpha = 1f;
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(WaitAndFadeOut(3.0f));
    }

    // 몬스터를 매 프레임 부드럽게 쫓아다님
    private void LateUpdate()
    {
        if (targetEnemy != null)
        {
            transform.position = targetEnemy.position + offset;
        }
        else
        {
            // 타겟이 죽거나 사라지면 즉시 풀로 반납
            PoolManager.Instance.ReturnToPool(gameObject);
        }
    }

    private IEnumerator WaitAndFadeOut(float delay)
    {
        yield return new WaitForSeconds(delay); // 3초 대기

        float duration = 1.0f; // 1초 동안 스르륵
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
            yield return null;
        }

        canvasGroup.alpha = 0f;

        // 투명해지면 스스로 PoolManager에게 반납 (메모리 절약)
        targetEnemy = null;
        PoolManager.Instance.ReturnToPool(gameObject);
    }
}