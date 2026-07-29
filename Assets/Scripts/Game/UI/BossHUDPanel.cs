using TMPro;
using UnityEngine;
using UnityEngine.UI;

// BossHUDPanel.cs — Canvas > HUD_Battle 밑에 붙임
public class BossHUDPanel : Singleton<BossHUDPanel>
{
    public Slider bossHealthBar, bossManaBar;
    public TextMeshProUGUI bossNameText;
    public Transform phaseMarkersContainer;
    public GameObject phaseMarkerPrefab; // 얇은 세로선 이미지 하나

    private NPC _currentBoss;

    public void BindBoss(NPC boss)
    {
        UnbindBoss();
        _currentBoss = boss;
        if (_currentBoss == null) return;

        bossNameText.text = _currentBoss.npcName;
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
    }

    private void UpdateHealthBar() { if (_currentBoss != null) bossHealthBar.value = (float)_currentBoss.currentHealth / _currentBoss.maxHealth; }
    private void UpdateManaBar() { if (_currentBoss != null) bossManaBar.value = (float)_currentBoss.currentMana / _currentBoss.maxMana; }

    // 페이즈 개수에 맞춰 구분선을 동적 배치 (훈련=0개, 하드=2개)
    public void SetPhaseCount(int totalPhases)
    {
        foreach (Transform child in phaseMarkersContainer) Destroy(child.gameObject);
        if (totalPhases <= 1) return;

        for (int i = 1; i < totalPhases; i++)
        {
            float t = (float)i / totalPhases;
            GameObject marker = Instantiate(phaseMarkerPrefab, phaseMarkersContainer);
            var rt = marker.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(t, 0f);
            rt.anchorMax = new Vector2(t, 1f);
            rt.anchoredPosition = Vector2.zero;
        }
    }
}