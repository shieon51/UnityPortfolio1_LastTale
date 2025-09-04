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
    public Slider fatigueBar;

    public TextMeshProUGUI healthText; // 체력 텍스트
    public TextMeshProUGUI manaText; // 마나 텍스트
    public TextMeshProUGUI expText; // 경험치 텍스트
    public TextMeshProUGUI fatigueText; // 경험치 텍스트

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
        fatigueText = fatigueBar.transform.Find("FatigueText").GetComponent<TextMeshProUGUI>();

        CreateTimeCoins();

        // PlayerStats의 상태가 변할 때 UI 업데이트
        PlayerState.Instance.OnStatsChanged += UpdateSliderUI;
        TimeManager.Instance.OnTimeUpdated += UpdateTimeUI; // 시간 업데이트 이벤트 연결

        // 시작할 때도 UI 갱신
        UpdateSliderUI();
        UpdateTimeUI(TimeManager.Instance.timeCoins, TimeManager.Instance.currentDay);
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

    private void OnDestroy()
    {
        // 씬이 변경되거나 UIManager가 삭제될 때 이벤트 제거
        if (PlayerState.Instance != null)
        {
            PlayerState.Instance.OnStatsChanged -= UpdateSliderUI;
        }
    }

    private void UpdateSliderUI()
    {
        // 현재 체력, 마나, 경험치, 피로도를 UI에 반영
        healthBar.value = (float)PlayerState.Instance.currentHealth / PlayerState.Instance.maxHealth;
        manaBar.value = (float)PlayerState.Instance.currentMana / PlayerState.Instance.maxMana;
        expBar.value = (float)PlayerState.Instance.experience / PlayerState.Instance.experienceToNextLevel;
        fatigueBar.value = (float)PlayerState.Instance.currentFatigue / PlayerState.Instance.maxFatigue;

        healthText.text = $"{PlayerState.Instance.currentHealth}/{PlayerState.Instance.maxHealth}";
        manaText.text = $"{PlayerState.Instance.currentMana}/{PlayerState.Instance.maxMana}";
        expText.text = $"{PlayerState.Instance.experience}/{PlayerState.Instance.experienceToNextLevel}";
        fatigueText.text = $"{PlayerState.Instance.currentFatigue}/{PlayerState.Instance.maxFatigue}";
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
