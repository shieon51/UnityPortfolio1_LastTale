using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 스텟바 전담
public class HUDStatusPanel : MonoBehaviour
{
    [Header("Sliders")]
    public Slider healthBar, manaBar, expBar, specialStatBar;

    [Header("Texts (Find() 대신 직접 연결)")]
    public TextMeshProUGUI healthText, manaText, expText, specialStatText;

    private PlayableCharacter _subscribed;   // ★ 추가: 지금 구독 중인 캐릭터

    private void Start()
    {
        //healthText = healthBar.transform.Find("HealthText").GetComponent<TextMeshProUGUI>();
        //manaText = manaBar.transform.Find("ManaText").GetComponent<TextMeshProUGUI>();
        //expText = expBar.transform.Find("ExpText").GetComponent<TextMeshProUGUI>();
        //specialStatText = specialStatBar.transform.Find("FatigueText").GetComponent<TextMeshProUGUI>();

        PlayerManager.Instance.OnCharacterPossessed += HandleCharacterChanged;
        if (PlayerManager.Instance.CurrentCharacter != null) HandleCharacterChanged(PlayerManager.Instance.CurrentCharacter);
    }

    private void OnDestroy()
    {
        if (PlayerManager.Instance != null)
            PlayerManager.Instance.OnCharacterPossessed -= HandleCharacterChanged;

        UnsubscribeFromCharacter(_subscribed);   // ★ 현재 캐릭터가 아니라 실제 구독한 대상을 해제
        _subscribed = null;
    }

    private void HandleCharacterChanged(PlayableCharacter c)
    {
        // ★ 이전 캐릭터 구독을 먼저 끊는다.
        //   (소라 ↔ 리엘 빙의를 반복하면 옛 캐릭터 이벤트가 계속 쌓이던 문제)
        UnsubscribeFromCharacter(_subscribed);
        _subscribed = c;
        SubscribeToCharacter(c);
        UpdateSliderUI();
    }

    private void SubscribeToCharacter(PlayableCharacter c)
    {
        if (c == null) return;
        c.OnHealthChanged += UpdateSliderUI;
        c.OnManaChanged += UpdateSliderUI;
        c.OnProgressionChanged += UpdateSliderUI;
        c.OnSpecialStatChanged += UpdateSliderUI;
    }

    private void UnsubscribeFromCharacter(PlayableCharacter c)
    {
        if (c == null) return;
        c.OnHealthChanged -= UpdateSliderUI;
        c.OnManaChanged -= UpdateSliderUI;
        c.OnProgressionChanged -= UpdateSliderUI;
        c.OnSpecialStatChanged -= UpdateSliderUI;
    }

    private void UpdateSliderUI()
    {
        Debug.Log("[HUDStatusPanel] UpdateSliderUI 호출");   // ★ 임시 확인용, 나중에 삭제
        var c = PlayerManager.Instance.CurrentCharacter;
        if (c == null) return;

        healthBar.value = (float)c.currentHealth / c.maxHealth;
        manaBar.value = (float)c.currentMana / c.maxMana;
        expBar.value = (float)c.experience / c.experienceToNextLevel;
        healthText.text = $"{c.currentHealth}/{c.maxHealth}";
        manaText.text = $"{c.currentMana}/{c.maxMana}";
        expText.text = $"{c.experience}/{c.experienceToNextLevel}";

        specialStatBar.gameObject.SetActive(c.HasSpecialStat);
        if (c.HasSpecialStat)
        {
            specialStatBar.value = c.SpecialStatPercentage;
            specialStatText.text = c.SpecialStatText;
        }
    }
}