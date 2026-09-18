using TMPro;
using UnityEngine;
using UnityEngine.UI;

// BossHUDPanel.cs — Canvas > HUD_Battle 밑에 붙임
// ★ 표시 여부는 UIModeManager가 HUD_Battle의 CanvasGroup으로 관리한다.
//   이 패널은 알파를 건드리지 않고 "내용"만 채운다.
//   (CanvasGroup은 부모와 자식 알파가 곱해지므로, 여기서 0으로 두면 모드를 켜도 안 보인다)
public class BossHUDPanel : Singleton<BossHUDPanel>
{
    [Header("UI 참조")]
    public Slider bossHealthBar, bossManaBar;
    public TextMeshProUGUI bossNameText;
    public RectTransform phaseMarkersContainer;
    public GameObject phaseMarkerPrefab; // 얇은 세로선 이미지 하나

    [Header("페이즈 구분선")]
    [Tooltip("구분선 두께")]
    public float phaseMarkerWidth = 2f;

    private NPC _currentBoss;

    private void Awake()
    {
        // ★ 이 오브젝트에 CanvasGroup이 남아 있다면 항상 1로 둔다 (부모 알파와 곱해지므로)
        var cg = GetComponent<CanvasGroup>();
        if (cg != null) { cg.alpha = 1f; cg.blocksRaycasts = true; }

        ClearContent();
    }

    public void BindBoss(NPC boss)
    {
        UnbindBoss();
        _currentBoss = boss;
        if (_currentBoss == null) return;

        if (bossNameText != null) bossNameText.text = _currentBoss.npcName;
        _currentBoss.OnHealthChanged += UpdateHealthBar;
        _currentBoss.OnManaChanged += UpdateManaBar;
        UpdateHealthBar();
        UpdateManaBar();
    }

    public void UnbindBoss()
    {
        if (_currentBoss == null) return;
        _currentBoss.OnHealthChanged -= UpdateHealthBar;
        _currentBoss.OnManaChanged -= UpdateManaBar;
        _currentBoss = null;
        ClearContent();
    }

    // ★ 숨기는 대신 내용을 비운다. 실제 숨김은 모드 전환이 처리한다
    private void ClearContent()
    {
        if (bossNameText != null) bossNameText.text = string.Empty;
        if (bossHealthBar != null) bossHealthBar.value = 0f;
        if (bossManaBar != null) bossManaBar.value = 0f;
    }

    private void UpdateHealthBar()
    {
        if (_currentBoss == null || bossHealthBar == null) return;
        bossHealthBar.value = _currentBoss.maxHealth > 0 ? (float)_currentBoss.currentHealth / _currentBoss.maxHealth : 0f;
    }

    private void UpdateManaBar()
    {
        if (_currentBoss == null || bossManaBar == null) return;
        bossManaBar.value = _currentBoss.maxMana > 0 ? (float)_currentBoss.currentMana / _currentBoss.maxMana : 0f;
    }

    // 페이즈 개수에 맞춰 구분선을 동적 배치 (훈련=0개, 하드=2개)
    public void SetPhaseCount(int totalPhases)
    {
        if (phaseMarkersContainer == null || phaseMarkerPrefab == null)
        {
            Debug.LogError("[BossHUDPanel] phaseMarkersContainer/phaseMarkerPrefab이 비어있습니다.");
            return;
        }

        foreach (Transform child in phaseMarkersContainer) Destroy(child.gameObject);
        if (totalPhases <= 1) return;

        if (phaseMarkerPrefab.GetComponent<RectTransform>() == null)
        {
            Debug.LogError("[BossHUDPanel] phaseMarkerPrefab에 RectTransform이 없습니다. UI > Image로 만들어주세요.");
            return;
        }

        for (int i = 1; i < totalPhases; i++)
        {
            float t = (float)i / totalPhases;
            GameObject marker = Instantiate(phaseMarkerPrefab, phaseMarkersContainer);
            var rt = marker.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(t, 0f);
            rt.anchorMax = new Vector2(t, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(phaseMarkerWidth, 0f);
        }
    }
}