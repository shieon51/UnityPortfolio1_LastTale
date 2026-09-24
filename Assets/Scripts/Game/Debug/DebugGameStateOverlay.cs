using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

// F1 디버그 오버레이.
// ★ 클래스 전체를 전처리기로 감싸면 릴리즈 빌드에서 씬의 컴포넌트가 Missing Script가 되므로,
//   클래스는 항상 컴파일하고 "동작"만 막는다.
public class DebugGameStateOverlay : Singleton<DebugGameStateOverlay>
{
    [Header("참조")]
    public CanvasGroup canvasGroup;
    public TextMeshProUGUI overlayText;

    [Header("설정")]
    public KeyCode toggleKey = KeyCode.F1;
    [Tooltip("갱신 주기(초). 0이면 매 프레임")]
    public float refreshInterval = 0.25f;
    [Tooltip("씬의 NPC 목록을 다시 찾는 주기(초)")]
    public float npcScanInterval = 2f;

    private bool _visible;
    private float _nextRefreshTime;
    private float _nextScanTime;
    private readonly List<NPC> _liveNpcs = new();

    // 에디터와 개발 빌드에서만 동작한다
    private static bool IsAvailable =>
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        true;
#else
        false;
#endif

    private void Awake()
    {
        if (!IsAvailable) { enabled = false; SetVisible(false); return; }
        SetVisible(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey)) SetVisible(!_visible);
        if (!_visible) return;

        if (Time.unscaledTime < _nextRefreshTime) return;       // ★ 매 프레임 갱신하지 않는다
        _nextRefreshTime = Time.unscaledTime + Mathf.Max(0f, refreshInterval);

        RefreshText();
    }

    private void SetVisible(bool visible)
    {
        _visible = visible;
        if (canvasGroup == null) return;
        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.blocksRaycasts = false;                     // 오버레이는 클릭을 가로채면 안 된다
    }

    // ★ 매 프레임 전체 검색 대신 주기적으로만 다시 찾는다
    private void EnsureNpcList()
    {
        if (Time.unscaledTime < _nextScanTime && _liveNpcs.Count > 0) return;
        _nextScanTime = Time.unscaledTime + Mathf.Max(0.1f, npcScanInterval);

        _liveNpcs.Clear();
        _liveNpcs.AddRange(Object.FindObjectsByType<NPC>(FindObjectsSortMode.None));
    }

    private void RefreshText()
    {
        if (overlayText == null) return;
        EnsureNpcList();

        var sb = new System.Text.StringBuilder();
        var player = PlayerManager.Instance?.CurrentCharacter;

        if (player != null)
        {
            sb.AppendLine($"[플레이어] 몸 레벨 {player.level} (영혼 레벨 {player.highestLevelReached}) " +
                          $"HP {player.currentHealth}/{player.maxHealth} MP {player.currentMana}/{player.maxMana}");

            if (player is SoraStats sora)
            {
                sb.AppendLine($"  회차: {sora.loopCount}  |  피로도 {sora.currentFatigue}/{sora.maxFatigue} " +
                              $"정신력 {sora.currentMental}/{sora.maxMental}{(sora.IsMentalDanger ? " (위험)" : "")} " +
                              $"요정화 {sora.fairyStage}단계");

                if (TimeManager.Instance != null)
                    sb.AppendLine($"  시간결정체 {sora.timeCrystals}개  |  Day {TimeManager.Instance.currentDay} " +
                                  $"{TimeManager.Instance.currentHour}시 (남은 시간 {TimeManager.Instance.timeCoins})");
            }
        }

        if (NPCManager.Instance != null)
        {
            foreach (var kvp in NPCManager.Instance.AllNPCData)
            {
                var data = kvp.Value;
                // ★ 이름이 빠져 있어 어느 NPC인지 알 수 없던 문제 수정
                sb.AppendLine($"[{kvp.Key}] 이해도 {data.CurrentUnderstandingCount} ({data.UnderstandingPercent:F0}%) " +
                              $"호감도 {data.hiddenAffection} 의심 {SuspicionManager.Instance?.GetSuspicion(kvp.Key) ?? 0} 모드 {data.currentMode}");

                var live = _liveNpcs.FirstOrDefault(n => n != null && n.npcName == kvp.Key);
                if (live != null) sb.AppendLine($"    HP {live.currentHealth}/{live.maxHealth} MP {live.currentMana}/{live.maxMana}");
            }
        }

        if (TimeLoopManager.Instance != null)
            sb.AppendLine($"[닻] {TimeLoopManager.Instance.Anchors.Count}개 " +
                          $"(다음 설치까지 {TimeLoopManager.Instance.HoursUntilNextAnchor()}시간)");

        overlayText.text = sb.ToString();
    }
}