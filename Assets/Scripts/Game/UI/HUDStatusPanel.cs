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
        {
            PlayerManager.Instance.OnCharacterPossessed -= HandleCharacterChanged;
            UnsubscribeFromCharacter(PlayerManager.Instance.CurrentCharacter);
        }
    }

    private void HandleCharacterChanged(PlayableCharacter c) { SubscribeToCharacter(c); UpdateSliderUI(); }

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