using System.Collections.Generic;
using TMPro;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : Singleton<UIManager>
{
    //체력, 마나, 경험치, 피로도 바 UI
    [Header("플레이어 State UI")]
    public Slider healthBar;
    public Slider manaBar;
    public Slider expBar;
    public Slider specialStatBar; // * fatigueBar를 범용적인 이름으로 변경함

    public TextMeshProUGUI healthText; // 체력 텍스트
    public TextMeshProUGUI manaText; // 마나 텍스트
    public TextMeshProUGUI expText; // 경험치 텍스트
    public TextMeshProUGUI specialStatText; // (소라인 경우) 피로도 텍스트

    //시간 코인 UI
    [Header("시간 코인 UI")]
    public Transform coinParent;
    public GameObject coinPrefab;
    public TextMeshProUGUI timeText;
    public TextMeshProUGUI dayText;
    public Color usedCoinColor = new Color(0.5f, 0, 0, 1); // 검붉은 색
    public Color unusedCoinColor = new Color(0, 1, 0.8f, 1); // 민트색

    private List<Image> coinImages = new List<Image>();
    private int totalCoins = 24;

    // 다이얼로그 UI
    [Header("다이얼로그 UI")]
    public TextMeshProUGUI dialogueText;
    //private GameObject dialogPanelPrefab;
    public GameObject dialogPanel;
    public GameObject choiceContainer;  // 선택지를 담을 부모 오브젝트 (Canvas 안에 있어야 함)
    private GameObject choiceButtonPrefab; // 선택지 버튼 프리팹


    private void Awake()
    {
        //dialogPanelPrefab = Resources.Load<GameObject>("Prefabs/Dialogue");
        choiceButtonPrefab = Resources.Load<GameObject>("Prefabs/ChoiceButton");
    }

    private void Start()
    {
        //다이얼로그 UI
        //dialogueText = GetComponent<TextMeshProUGUI>();
        HideDialogUI();

        healthText = healthBar.transform.Find("HealthText").GetComponent<TextMeshProUGUI>();
        manaText = manaBar.transform.Find("ManaText").GetComponent<TextMeshProUGUI>();
        expText = expBar.transform.Find("ExpText").GetComponent<TextMeshProUGUI>();
        specialStatText = specialStatBar.transform.Find("FatigueText").GetComponent<TextMeshProUGUI>();

        CreateTimeCoins();

        // 1. PlayerManager의 '캐릭터 변경 이벤트'를 구독
        PlayerManager.Instance.OnCharacterPossessed += HandleCharacterChanged;
        TimeManager.Instance.OnTimeUpdated += UpdateTimeUI;

        // 현재 활성화된 캐릭터의 이벤트를 구독 (추후 캐릭터 변경 시 재구독 로직 필요)
        if (PlayerManager.Instance.CurrentCharacter != null)
        {
            HandleCharacterChanged(PlayerManager.Instance.CurrentCharacter);
        }
        TimeManager.Instance.OnTimeUpdated += UpdateTimeUI; // 시간 업데이트 이벤트 연결

        // 시작할 때도 UI 갱신
        //UpdateSliderUI();
        UpdateTimeUI(TimeManager.Instance.timeCoins, TimeManager.Instance.currentDay);
    }

    private void OnDestroy()
    {
        // (PlayerManager가 파괴되지 않고 살아있을 때만 접근하도록)
        if (PlayerManager.Instance != null)
        {
            PlayerManager.Instance.OnCharacterPossessed -= HandleCharacterChanged;

            // 현재 캐릭터의 이벤트도 해제
            UnsubscribeFromCharacter(PlayerManager.Instance.CurrentCharacter);
        }
    }

    // 3. 캐릭터가 변경될 때 호출되는 로직 (이전 캐릭터 구독 해제 -> 새 캐릭터 구독)
    private void HandleCharacterChanged(PlayableCharacter newCharacter)
    {
        // (주의: 이전 캐릭터 정보를 가져올 수 있도록 PlayerManager에서 처리해줌)
        SubscribeToCharacter(newCharacter);
        UpdateSliderUI(); // UI 즉시 갱신
    }

    private void SubscribeToCharacter(PlayableCharacter character)
    {
        if (character == null) return;
        character.OnHealthChanged += UpdateSliderUI;
        character.OnManaChanged += UpdateSliderUI;
        character.OnProgressionChanged += UpdateSliderUI;
        character.OnSpecialStatChanged += UpdateSliderUI;
    }

    private void UnsubscribeFromCharacter(PlayableCharacter character)
    {
        if (character == null) return;
        character.OnHealthChanged -= UpdateSliderUI;
        character.OnManaChanged -= UpdateSliderUI;
        character.OnProgressionChanged -= UpdateSliderUI;
        character.OnSpecialStatChanged -= UpdateSliderUI;
    }


    // 시간 코인 UI 생성
    private void CreateTimeCoins()
    {
        float radius = 80f;
        float angleStep = 360f / totalCoins;

        for (int i = 0; i < totalCoins; i++)
        {
            GameObject coin = Instantiate(coinPrefab, coinParent);
            Image coinImage = coin.GetComponent<Image>();
            coinImages.Add(coinImage);

            float angle = (i * angleStep + 90) * Mathf.Deg2Rad;
            float x = Mathf.Cos(angle) * radius;
            float y = Mathf.Sin(angle) * radius;
            coin.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, y);
        }
    }

    private void UpdateSliderUI()
    {
        PlayableCharacter currentCharacter = PlayerManager.Instance.CurrentCharacter;
        if (currentCharacter == null) return;

        // 공통 스탯 업데이트
        healthBar.value = (float)currentCharacter.currentHealth / currentCharacter.maxHealth;
        manaBar.value = (float)currentCharacter.currentMana / currentCharacter.maxMana;
        expBar.value = (float)currentCharacter.experience / currentCharacter.experienceToNextLevel;

        healthText.text = $"{currentCharacter.currentHealth}/{currentCharacter.maxHealth}";
        manaText.text = $"{currentCharacter.currentMana}/{currentCharacter.maxMana}";
        expText.text = $"{currentCharacter.experience}/{currentCharacter.experienceToNextLevel}";

        // 4. UIManager는 해당 캐릭터의 특수 스탯을 넘겨줌
        if (currentCharacter.HasSpecialStat)
        {
            specialStatBar.gameObject.SetActive(true); // 특수 스탯이 있는 캐릭터면 UI 켜기
            specialStatBar.value = currentCharacter.SpecialStatPercentage;
            specialStatText.text = currentCharacter.SpecialStatText;
        }
        else
        {
            specialStatBar.gameObject.SetActive(false); // 없는 캐릭터면 UI 끄기
        }

    }

    // 시간 코인 UI 업데이트
    public void UpdateTimeUI(int remainingCoins, int currentDay)
    {
        for (int i = 0; i < totalCoins; i++)
        {
            coinImages[i].color = (i < remainingCoins) ? unusedCoinColor : usedCoinColor;
        }

        int hour = (24 - remainingCoins) * 1;
        string period = (hour < 12) ? "AM" : "PM";
        int displayHour = (hour % 12 == 0) ? 12 : hour % 12;

        timeText.text = $"{period} {displayHour}:00";
        dayText.text = $"Day {currentDay}";
    }

    //다이얼로그 창 & 선택지 창 UI
    public void ShowDialogUI()
    {
        dialogPanel.SetActive(true);
    }

    public void HideDialogUI()
    {
        dialogPanel.SetActive(false);
        UpdateDialogueText("");
        ClearChoices();
    }
    public void UpdateDialogueText(string text)
    {
        dialogueText.text = text;
    }

    public void ShowChoices(List<Ink.Runtime.Choice> choices)
    {
        foreach (Ink.Runtime.Choice choice in choices)
        {
            GameObject choiceButton = Instantiate(choiceButtonPrefab, choiceContainer.transform);
            choiceButton.GetComponentInChildren<TextMeshProUGUI>().text = choice.text;
            choiceButton.GetComponent<Button>().onClick.AddListener(() => DialogueManager.Instance.OnChoiceSelected(choice.index));
        }
    }

    // 선택지 정리 (다음 선택지를 위해 기존 UI 제거)
    public void ClearChoices()
    {
        foreach (Transform child in choiceContainer.transform)
        {
            Destroy(child.gameObject);
        }
    }
}
