using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement; // ★ 맵 씬을 Scene 단위로 다루기 위해
using System.Linq;
using System;
using System.Text; // 한글 깨짐 방지(UTF-8)

public class MapDataEditor : EditorWindow
{
    // 탭 관리 (0: 맵 에디터, 1: NPC 스케줄표)
    private int toolbarOption = 0;
    private string[] toolbarTexts = { "Map Editor", "NPC Schedule" };

    // 경로 설정
    private string eventCsvPath => Path.Combine(Application.streamingAssetsPath, "Datas", "EventTable.csv");
    private string sceneCsvPath => Path.Combine(Application.streamingAssetsPath, "Datas", "SceneTable.csv");
    private string backupFolderPath => Path.Combine(Application.streamingAssetsPath, "Datas", "Backups");

    // 에디터 변수
    private GameObject markerPrefab;
    private int targetSceneID = 1;
    private float gridSize = 1.0f;

    // 필터링 변수
    private bool filterEnable = false;
    private int filterDay = 1;
    private int filterTime = 9;

    // UI 상태
    private Vector2 scrollPos;
    private bool showHelp = false;

    // 플레이어 게임 시작 위치
    private GameStartConfig startConfig;

    [MenuItem("Tools/Map Data Editor")]
    public static void ShowWindow()
    {
        MapDataEditor window = GetWindow<MapDataEditor>("Map Tool");
        window.minSize = new Vector2(450, 700);
    }

    private void OnEnable()
    {
        if (markerPrefab == null) markerPrefab = Resources.Load<GameObject>("Editor/EventMarkerPrefab");

        // ★ [유령 데이터 방지] 씬 열릴 때 자동 정화 이벤트 연결
        EditorSceneManager.sceneOpened += OnSceneOpened;

        DetectCurrentSceneID();

        startConfig = AssetDatabase.LoadAssetAtPath<GameStartConfig>("Assets/Datas/GameStartConfig.asset");
    }

    private void OnDisable()
    {
        // 이벤트 연결 해제
        EditorSceneManager.sceneOpened -= OnSceneOpened;
    }

    // ★ [유령 데이터 방지] 씬 진입 시 좀비 마커 삭제 및 데이터 동기화
    private void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        // ★ 상주 씬 등 SceneTable에 없는 씬이 열릴 때는 마커를 건드리지 않는다
        //    (예전에는 모든 씬의 마커를 지우고 활성 씬에 다시 불러와, 상주 씬에 마커가 생겼다)
        if (!IsMapScene(scene)) return;

        LoadMarkers(scene); // ★ 열린 맵 씬의 좀비 마커만 지우고 그 씬에 다시 불러온다
        DetectCurrentSceneID(); // 현재 씬 ID 갱신

        Debug.Log($"[MapEditor] 씬 진입: {scene.name} (ID: {GetSceneIDByName(scene.name)}) - 유령 마커 정리 완료"); // ★ 열린 씬 기준 ID
    }

    private int GetBaseIdFor(EventMarkerType type) => type switch
    {
        EventMarkerType.Normal_NPC => 10000,
        EventMarkerType.Cutscene => 50000,
        EventMarkerType.Interactable => 60000,
        EventMarkerType.System_Repeat => 90000,
        _ => 10000,
    };

    private EventMarkerType GetTypeFromId(int id)
    {
        if (id >= 90000) return EventMarkerType.System_Repeat;
        if (id >= 60000) return EventMarkerType.Interactable;
        if (id >= 50000) return EventMarkerType.Cutscene;
        return EventMarkerType.Normal_NPC;
    }

    private void OnGUI()
    {
        GUILayout.Space(10);
        toolbarOption = GUILayout.Toolbar(toolbarOption, toolbarTexts, GUILayout.Height(30));
        GUILayout.Space(10);

        if (toolbarOption == 0) DrawMapEditorTab();
        else DrawScheduleTab();
    }

    // =================================================================================
    // [탭 1] 맵 에디터
    // =================================================================================
    private void DrawMapEditorTab()
    {
        Scene mapScene = FindMapScene(); // ★ 활성 씬 대신 열린 씬 중 SceneTable에 있는 씬을 기준으로 삼는다
        DrawStrayMarkerWarning(mapScene); // ★ 상주 씬 등에 잘못 들어간 마커 안내

        GUILayout.Label("1. 씬 이동 & 설정", EditorStyles.boldLabel);

        string targetNameFromCSV = GetSceneNameByID(targetSceneID);

        if (!mapScene.IsValid())
        {
            // ★ 상주 씬만 열려 있는 경우 — 저장·불러오기·생성이 모두 막힌다
            EditorGUILayout.HelpBox($"열린 씬 중 SceneTable.csv에 등록된 맵 씬이 없습니다. 이동할 Scene ID를 입력하고 '이동'을 누르세요. (타겟 ID({targetSceneID}): {targetNameFromCSV})", MessageType.Warning);
        }
        else
        {
            int mapID = GetSceneIDByName(mapScene.name);
            bool isMatch = (mapID == targetSceneID);

            GUIStyle statusStyle = new GUIStyle(EditorStyles.label);
            statusStyle.normal.textColor = isMatch ? Color.green : Color.red;
            statusStyle.fontStyle = FontStyle.Bold;
            GUILayout.Label($"맵 씬: {mapScene.name} (ID {mapID}) / 타겟 ID({targetSceneID}): {targetNameFromCSV}", statusStyle); // ★ 활성 씬 이름 대신 맵 씬

            if (!isMatch) EditorGUILayout.HelpBox("맵 씬과 타겟 ID가 다릅니다. 이동 시 저장 여부를 확인합니다.", MessageType.Warning);

            // ★ 맵 씬이 여러 개 열려 있으면 어느 씬에 작업하는지 알린다
            if (GetOpenMapScenes().Count > 1)
                EditorGUILayout.HelpBox($"맵 씬이 2개 이상 열려 있습니다. 마커 작업은 '{mapScene.name}'에만 적용됩니다.", MessageType.Warning);

            // ★ 활성 씬이 상주 씬이면, 하이어라키에서 직접 만든 오브젝트가 상주 씬에 생긴다
            Scene activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene != mapScene)
            {
                EditorGUILayout.HelpBox($"활성 씬이 '{activeScene.name}'입니다. 직접 만드는 오브젝트는 활성 씬에 생깁니다.", MessageType.Warning);
                if (GUILayout.Button($"'{mapScene.name}'을 활성 씬으로")) EditorSceneManager.SetActiveScene(mapScene);
            }
        }

        GUILayout.BeginHorizontal();
        targetSceneID = EditorGUILayout.IntField("이동할 Scene ID", targetSceneID);

        // ★ [안전장치] 안전한 이동 버튼
        if (GUILayout.Button("이동 (Move)", GUILayout.Width(120)))
        {
            TryOpenScene(targetSceneID);
        }
        GUILayout.EndHorizontal();

        markerPrefab = (GameObject)EditorGUILayout.ObjectField("Prefab", markerPrefab, typeof(GameObject), false);

        GUILayout.Space(10);
        GUILayout.Label("2. 생성 및 배치", EditorStyles.boldLabel);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("+ NPC")) CreateNewMarker(EventMarkerType.Normal_NPC);
        if (GUILayout.Button("+ System")) CreateNewMarker(EventMarkerType.System_Repeat);
        if (GUILayout.Button("+ Cutscene")) CreateNewMarker(EventMarkerType.Cutscene);
        if (GUILayout.Button("+ Interactable")) CreateNewMarker(EventMarkerType.Interactable);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        gridSize = EditorGUILayout.FloatField("Grid Size", gridSize);
        if (GUILayout.Button("Snap All")) SnapAllMarkers();
        GUILayout.EndHorizontal();

        GUILayout.Space(10);
        DrawFilterUI();

        GUILayout.Space(10);
        GUILayout.Label("4. 데이터 관리", EditorStyles.boldLabel);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Load CSV", GUILayout.Height(40)))
        {
            if (TryGetMapScene(out Scene scene)) LoadMarkers(scene); // ★ 맵 씬의 ID로 불러온다 (입력칸 ID를 쓰면 다른 씬 마커가 섞였다)
        }

        // ★ [안전장치] 맵 씬 ID로만 저장 — "입력된 ID로 저장" 대체 경로는 상주 씬 마커가 섞이는 통로라 없앴다
        if (GUILayout.Button("Save Current Scene", GUILayout.Height(40)))
        {
            if (TryGetMapScene(out Scene scene))
            {
                int realID = GetSceneIDByName(scene.name);
                if (SaveMarkers(scene, realID)) targetSceneID = realID;
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(5);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("ID 재정렬")) { if (TryGetMapScene(out Scene scene)) AutoAssignIDs(scene); } // ★ 맵 씬 한정
        if (GUILayout.Button("전체 데이터 삭제")) { if (TryGetMapScene(out Scene scene)) ClearMarkers(scene); } // ★ 맵 씬 한정
        GUILayout.EndHorizontal();

        GUILayout.Space(10);
        showHelp = EditorGUILayout.Foldout(showHelp, "사용 설명서");
        if (showHelp)
        {
            string helpText =
                " [안전 기능]\n" +
                " - 이동 시 저장 여부를 물어 데이터 손실/덮어쓰기를 방지합니다.\n" +
                " - 씬 진입 시 '유령 마커'를 자동으로 청소합니다.\n" +
                " - 저장 시 .csv 형식으로 백업되며 한글 깨짐(UTF-8 BOM)이 해결되었습니다.\n\n" +
                " [상주 씬과 함께 쓰기]\n" + // ★ 상주 씬 + 맵 씬 작업 방식 설명
                " - 열린 씬 중 SceneTable.csv에 있는 씬을 '맵 씬'으로 보고, 마커는 맵 씬에만 만들고 저장합니다.\n" +
                " - 이동은 새 맵을 추가로 열고 옛 맵만 닫습니다. 상주 씬은 그대로 남습니다.\n" +
                " - 맵 씬이 아닌 씬에 마커가 있으면 맨 위에 빨간 안내가 뜹니다.\n\n" +
                " [NPC Schedule]\n" +
                " - 상단 탭을 눌러 NPC들의 전체 동선을 확인할 수 있습니다."+
                " [기본 사용법]\n" +
                " 1. 'Scene ID' 입력 후 '이동' 버튼으로 씬을 엽니다. (씬 이름은 초록색이어야 합니다)\n" +
                " 2. 'Load CSV'로 데이터를 불러옵니다.\n" +
                " 3. '+ NPC' 버튼으로 중앙에 새 마커를 생성합니다.\n" +
                " 4. 위치를 잡고 'Save to CSV'를 누르면 저장됩니다.\n\n" +
                " [Ink 파일 연동]\n" +
                " - 마커의 'Ink Node Name'을 'NPC1_Day1_001' 처럼 짓습니다.\n" +
                " - 마커를 클릭하고 인스펙터에서 'Create Ink'를 누르면\n" +
                "   Assets/Datas/NPC1/NPC1_Day1.ink 파일이 생성됩니다.\n\n" +
                " [필터링 주의사항]\n" +
                " - 필터 적용 중에도 'Load CSV'를 누르면 모든 데이터가 로드됩니다.\n" +
                " - 로드 시 중복 생성을 막기 위해 숨겨진 마커까지 모두 삭제 후 로드합니다.";
            EditorGUILayout.TextArea(helpText, EditorStyles.helpBox);
        }

        DrawStartPositionUI();
    }

    private void DrawFilterUI()
    {
        GUILayout.Label("3. 필터링 (View Filter)", EditorStyles.boldLabel);
        GUILayout.BeginHorizontal();
        bool prev = filterEnable;
        filterEnable = EditorGUILayout.Toggle("필터 적용", filterEnable);
        if (prev != filterEnable) ApplyFilter(FindMapScene()); // ★ 맵 씬 한정

        if (filterEnable)
        {
            filterDay = EditorGUILayout.IntField("Day", filterDay);
            filterTime = EditorGUILayout.IntField("Time", filterTime);
            if (GUILayout.Button("Apply")) ApplyFilter(FindMapScene()); // ★ 맵 씬 한정
        }
        else
        {
            if (GUILayout.Button("Show All")) ShowAllMarkers();
        }
        GUILayout.EndHorizontal();
    }

    // ★ 맵 씬이 아닌 씬(상주 씬 등)에 들어간 마커를 알리고, 맵 씬으로 옮기거나 지우게 한다
    private void DrawStrayMarkerWarning(Scene mapScene)
    {
        var strays = GetStrayMarkers();
        if (strays.Count == 0) return;

        string where = string.Join(", ", strays.GroupBy(m => m.gameObject.scene.name).Select(g => $"{g.Key} {g.Count()}개"));
        EditorGUILayout.HelpBox(
            $"맵 씬이 아닌 씬에 이벤트 마커가 있습니다: {where}\n" +
            "이 마커는 저장·불러오기에서 빠집니다. 맵 씬으로 옮기거나 삭제하세요.\n" +
            "처리한 뒤 해당 씬을 저장(Ctrl+S)해야 씬 파일에 반영됩니다.", MessageType.Error);

        GUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(!mapScene.IsValid()))
        {
            string label = mapScene.IsValid() ? $"'{mapScene.name}'으로 옮기기" : "옮길 맵 씬 없음";
            if (GUILayout.Button(label)) MoveStrayMarkers(strays, mapScene);
        }
        if (GUILayout.Button("삭제")) DeleteStrayMarkers(strays);
        GUILayout.EndHorizontal();
        GUILayout.Space(5);
    }

    // ★ 잘못 들어간 마커를 맵 씬으로 옮긴다. 같은 EventID가 이미 있으면 먼저 알린다 (Ctrl+Z로 되돌릴 수 있다)
    private void MoveStrayMarkers(List<EventMarker> strays, Scene mapScene)
    {
        var existingIDs = new HashSet<int>(GetMarkersIn(mapScene).Select(m => m.EventID).Where(id => id != 0));
        int dupCount = strays.Count(m => existingIDs.Contains(m.EventID));

        string msg = $"마커 {strays.Count}개를 '{mapScene.name}'으로 옮깁니다.";
        if (dupCount > 0)
            msg += $"\n\n이 중 {dupCount}개는 맵 씬에 같은 EventID가 이미 있습니다. 옮기면 저장할 때 ID 중복으로 표시됩니다.\n같은 마커의 복사본이라면 '삭제'가 맞습니다.";
        if (!EditorUtility.DisplayDialog("마커 옮기기", msg, "옮기기", "취소")) return;

        foreach (var m in strays)
        {
            if (m == null) continue;
            GameObject go = m.gameObject;
            if (go.transform.parent != null) Undo.SetTransformParent(go.transform, null, "Move Stray Markers"); // 씬 이동은 루트 오브젝트만 가능
            Undo.MoveGameObjectToScene(go, mapScene, "Move Stray Markers");
        }
        Debug.Log($"[MapEditor] 맵 씬 밖 마커 {strays.Count}개를 {mapScene.name}으로 옮김");
    }

    // ★ 잘못 들어간 마커를 지운다 (Ctrl+Z로 되돌릴 수 있다)
    private void DeleteStrayMarkers(List<EventMarker> strays)
    {
        string where = string.Join(", ", strays.GroupBy(m => m.gameObject.scene.name).Select(g => $"{g.Key} {g.Count()}개"));
        if (!EditorUtility.DisplayDialog("마커 삭제", $"맵 씬 밖의 마커를 삭제합니다: {where}\nCSV는 바뀌지 않습니다.", "삭제", "취소")) return;

        foreach (var m in strays)
        {
            if (m == null) continue;
            Undo.DestroyObjectImmediate(m.gameObject);
        }
        Debug.Log($"[MapEditor] 맵 씬 밖 마커 삭제: {where}");
    }

    // =================================================================================
    // [탭 2] NPC 스케줄표
    // =================================================================================
    private void DrawScheduleTab()
    {
        GUILayout.Label("NPC 전체 스케줄 (CSV 기반)", EditorStyles.boldLabel);

        if (GUILayout.Button("데이터 새로고침 (Refresh)")) { } // GUI 갱신

        if (!File.Exists(eventCsvPath))
        {
            GUILayout.Label("EventTable.csv 파일이 없습니다.");
            return;
        }

        string[] lines = File.ReadAllLines(eventCsvPath);
        List<ScheduleItem> scheduleList = new List<ScheduleItem>();

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrEmpty(lines[i])) continue;
            string[] cols = lines[i].Split(',');

            // 데이터 파싱 예외처리
            if (cols.Length < 10) continue;

            scheduleList.Add(new ScheduleItem
            {
                Name = cols[1],
                Day = int.Parse(cols[3]),
                Start = int.Parse(cols[4]),
                End = int.Parse(cols[5]),
                SceneID = int.Parse(cols[7]),
                Pos = new Vector2(float.Parse(cols[8]), float.Parse(cols[9])),
                IsCutscene = false,
            });

            // ★ 컷씬이 소환하는 NPC를 그 NPC의 스케줄로도 표시
            string summon = CsvTableLoader.Get(cols, 14, "");
            if (!string.IsNullOrEmpty(summon))
            {
                foreach (var entry in summon.Split(','))
                {
                    string npcName = entry.Trim();
                    int at = npcName.IndexOf('@');
                    if (at > 0) npcName = npcName.Substring(0, at).Trim();
                    if (string.IsNullOrEmpty(npcName)) continue;
                    scheduleList.Add(new ScheduleItem
                    {
                        Name = npcName,
                        Day = int.Parse(cols[3]),
                        Start = int.Parse(cols[4]),
                        End = int.Parse(cols[5]),
                        SceneID = int.Parse(cols[7]),
                        Pos = new Vector2(float.Parse(cols[8]), float.Parse(cols[9])),
                        IsCutscene = true,
                    });
                }
            }
        }

        // 이름 -> 날짜 -> 시간 순 정렬
        var grouped = scheduleList
            .OrderBy(x => x.Name)
            .ThenBy(x => x.Day)
            .ThenBy(x => x.Start)
            .GroupBy(x => x.Name);

        scrollPos = GUILayout.BeginScrollView(scrollPos);

        foreach (var group in grouped)
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label($"[ {group.Key} ]", EditorStyles.boldLabel); // NPC 이름

            foreach (var item in group)
            {
                GUILayout.BeginHorizontal();
                if (item.IsCutscene) GUI.color = Color.magenta;
                GUILayout.Label($"Day {item.Day}", GUILayout.Width(50));
                string timeStr = (item.Start == 0 && item.End == 24) ? "All Day" : $"{item.Start}시 ~ {item.End}시";
                GUILayout.Label(timeStr, GUILayout.Width(100));
                GUILayout.Label($"Scene {item.SceneID}", GUILayout.Width(80));
                GUILayout.Label(item.IsCutscene ? $"[컷씬 등장] Pos {item.Pos}" : $"Pos {item.Pos}");
                GUI.color = Color.white;
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
            GUILayout.Space(5);
        }
        GUILayout.EndScrollView();
    }

    // =================================================================================
    // 핵심 로직 구현 (안전장치 포함)
    // =================================================================================

    private void TryOpenScene(int nextSceneID)
    {
        // 1. 현재 맵 씬 파악 (★ 활성 씬은 상주 씬일 수 있어 맵 씬으로 판별)
        Scene oldMap = FindMapScene();
        int currentID = oldMap.IsValid() ? GetSceneIDByName(oldMap.name) : -1;

        // ★ 이미 그 맵에 있으면 활성 씬만 맞춘다
        if (currentID == nextSceneID)
        {
            EditorSceneManager.SetActiveScene(oldMap);
            return;
        }

        if (oldMap.IsValid())
        {
            // 2. 변경사항이 있는지 검사 (Smart Check)
            bool isDirty = IsCurrentSceneDirty(oldMap, currentID);

            // 3. 변경사항이 있을 때만 물어봄
            if (isDirty)
            {
                int option = EditorUtility.DisplayDialogComplex("변경사항 감지",
                    $"현재 씬({oldMap.name})에 '저장되지 않은 변경사항'이 있습니다.\n저장하지 않고 이동하면 사라집니다.",
                    "저장 후 이동", "그냥 이동 (삭제됨)", "취소");

                switch (option)
                {
                    case 0: // 저장 후 이동
                        if (!SaveMarkers(oldMap, currentID)) return; // ★ 검증 창에서 취소하면 이동도 멈춘다 (예전에는 저장 없이 이동했다)
                        break;
                    case 1: // 그냥 이동
                        break;
                    case 2: return; // 취소
                }
            }

            // ★ 마커는 CSV가 원본이므로 씬에서 걷어낸 뒤, 마커 외 변경(타일 등)이 있으면 씬 저장 여부를 묻는다
            //    Additive 씬 닫기(CloseScene)는 Single 열기와 마찬가지로 변경을 말없이 버리기 때문
            ClearMarkers(oldMap);
            if (oldMap.isDirty && !EditorSceneManager.SaveModifiedScenesIfUserWantsTo(new[] { oldMap }))
            {
                LoadMarkers(oldMap); // 취소 — 걷어낸 마커를 CSV에서 되살린다
                return;
            }
        }

        OpenSceneByID(nextSceneID, oldMap);
    }

    // 1. 현재 씬과 CSV 파일 내용 비교 함수
    private bool IsCurrentSceneDirty(Scene scene, int sceneID) // ★ 비교 대상을 맵 씬으로 한정
    {
        if (sceneID == -1) return false;

        // A. 현재 화면에 있는 마커들을 문자열 리스트로 변환
        List<string> currentMarkerData = new List<string>();
        var markers = GetMarkersIn(scene); // ★ 열린 모든 씬 대신 맵 씬만
        foreach (var m in markers) SyncSummonPointsToData(m); // ★ 추가
        foreach (var m in markers)
        {
            m.SceneID = sceneID; // 비교를 위해 ID 잠시 동기화
            currentMarkerData.Add(GetMarkerCsvString(m));
        }
        currentMarkerData.Sort(); // 순서 섞여도 내용만 같으면 되니까 정렬

        // B. CSV 파일에서 해당 씬 데이터만 가져옴
        List<string> csvData = new List<string>();
        if (File.Exists(eventCsvPath))
        {
            var lines = File.ReadAllLines(eventCsvPath);
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrEmpty(lines[i])) continue;
                string[] cols = lines[i].Split(',');

                if (int.Parse(cols[7]) == sceneID)
                {
                    // 비교를 위해 포맷 통일 (소수점 처리 등)
                    csvData.Add(NormalizeCsvLine(cols));
                }
            }
        }
        csvData.Sort();

        // C. 두 리스트 비교
        if (currentMarkerData.Count != csvData.Count) return true; // 개수 다르면 바뀐 거임

        for (int i = 0; i < currentMarkerData.Count; i++)
        {
            if (currentMarkerData[i] != csvData[i]) return true; // 내용 하나라도 다르면 바뀐 거임
        }

        return false; // 완벽히 똑같음 (저장 불필요)
    }


    // 2. 마커 -> CSV 포맷 문자열 변환
    // ★ AutoTrigger 컬럼 추가
    private string GetMarkerCsvString(EventMarker m)
    {
        string pX = m.transform.position.x.ToString("F2");
        string pY = m.transform.position.y.ToString("F2");
        return $"{m.EventID},{m.EventName},{m.IsAnytime},{m.Day},{m.StartTime},{m.EndTime},{m.InkNodeName},{m.SceneID},{pX},{pY},{m.TimeTaken},{m.AutoTrigger},{m.maxTriggerCount},{m.exhaustedInkNode},{m.summonNPCs},{m.despawnAfterEvent},{m.triggerZoneSize.x:F2},{m.triggerZoneSize.y:F2},{m.triggerZoneOffset.x:F2},{m.triggerZoneOffset.y:F2},{m.DisplayKey}";
    }

    // 3. CSV 읽은 줄 -> 비교용 표준 포맷 변환
    // ★ AutoTrigger 컬럼 추가 (구버전 CSV와의 호환을 위해 없으면 False로 처리)
    private string NormalizeCsvLine(string[] cols)
    {
        float x = float.Parse(cols[8]);
        float y = float.Parse(cols[9]);
        string pX = x.ToString("F2");
        string pY = y.ToString("F2");
        string autoTrigger = CsvTableLoader.GetBool(cols, 11).ToString();      // ★ 안전 파싱
        string maxCount = CsvTableLoader.GetInt(cols, 12, 0).ToString();       // ★ 안전 파싱
        string exhausted = CsvTableLoader.Get(cols, 13, "");                    // ★ 안전 파싱
        string summon = CsvTableLoader.Get(cols, 14, "");
        string despawn = CsvTableLoader.GetBool(cols, 15, true).ToString();
        string zoneW = CsvTableLoader.GetFloat(cols, 16, 0f).ToString("F2");
        string zoneH = CsvTableLoader.GetFloat(cols, 17, 0f).ToString("F2");
        string zoneOX = CsvTableLoader.GetFloat(cols, 18, 0f).ToString("F2");
        string zoneOY = CsvTableLoader.GetFloat(cols, 19, 0f).ToString("F2");
        string displayKey = CsvTableLoader.Get(cols, 20, "");                  // ★ 추가
        return $"{cols[0]},{cols[1]},{cols[2]},{cols[3]},{cols[4]},{cols[5]},{cols[6]},{cols[7]},{pX},{pY},{cols[10]},{autoTrigger},{maxCount},{exhausted},{summon},{despawn},{zoneW},{zoneH},{zoneOX},{zoneOY},{displayKey}";
    }

    // ★ 저장 대상을 맵 씬으로 한정. 검증 창에서 취소하면 false를 돌려준다
    private bool SaveMarkers(Scene scene, int saveAsID)
    {
        var markersToCheck = GetMarkersIn(scene); // ★ 열린 모든 씬 대신 맵 씬만
        foreach (var m in markersToCheck) SyncSummonPointsToData(m); // ★ 검증 전에 먼저 동기화

        var problems = new List<string>();

        // 검증 1 — 시간 대비 실행 횟수 (복구)
        foreach (var m in markersToCheck)
        {
            if (m.IsAnytime || m.TimeTaken <= 0 || m.maxTriggerCount <= 0) continue;
            int maxPossible = (m.EndTime - m.StartTime) / m.TimeTaken;
            if (m.maxTriggerCount > maxPossible)
                problems.Add($"· [횟수 초과] {m.EventName}(ID:{m.EventID}): {m.StartTime}~{m.EndTime}시에 {m.TimeTaken}시간짜리 → 최대 {maxPossible}회 가능한데 {m.maxTriggerCount}회로 설정됨");
        }

        // 검증 2 — 컷씬 소환 NPC와 일반 스케줄 충돌
        foreach (var m in markersToCheck)
        {
            if (string.IsNullOrEmpty(m.summonNPCs)) continue;
            foreach (var entry in m.summonNPCs.Split('|'))
            {
                string npcName = entry.Trim();
                int at = npcName.IndexOf('@');
                if (at > 0) npcName = npcName.Substring(0, at).Trim();
                if (string.IsNullOrEmpty(npcName)) continue;

                foreach (var other in markersToCheck)
                {
                    if (other == m || other.markerType != EventMarkerType.Normal_NPC) continue;
                    if (other.EventName != npcName || other.Day != m.Day) continue;
                    if (m.StartTime < other.EndTime && other.StartTime < m.EndTime)
                        problems.Add($"· [컷씬 충돌] {m.EventName}(ID:{m.EventID})이 Day{m.Day} {m.StartTime}~{m.EndTime}시에 '{npcName}'을 소환하는데, 같은 시간대에 {npcName}의 일반 스케줄(ID:{other.EventID}, {other.StartTime}~{other.EndTime}시)이 있습니다");
                }
            }
        }

        // ★ 검증 3 — 같은 EventID가 둘 이상 (복사·옮기기로 생긴 중복을 저장 전에 잡는다)
        foreach (var g in markersToCheck.Where(m => m.EventID != 0).GroupBy(m => m.EventID).Where(g => g.Count() > 1))
            problems.Add($"· [ID 중복] EventID {g.Key}가 {g.Count()}개: {string.Join(", ", g.Select(m => m.EventName))}");

        // ★ 검증 4 — 맵 씬 밖(상주 씬 등)의 마커는 저장에서 빠진다
        var strays = GetStrayMarkers();
        if (strays.Count > 0)
            problems.Add($"· [저장 제외] 맵 씬 밖에 마커 {strays.Count}개가 있습니다 ({string.Join(", ", strays.Select(m => m.gameObject.scene.name).Distinct())}). 이 마커는 저장되지 않습니다");

        if (problems.Count > 0)
        {
            bool proceed = EditorUtility.DisplayDialog("설정 확인 필요",
                "다음 문제가 발견되었습니다:\n\n" + string.Join("\n", problems) + "\n\n그래도 저장할까요?",
                "저장", "취소");
            if (!proceed) return false; // ★
        }

        CreateBackup();
        AutoAssignIDs(scene); // ★ 맵 씬 한정

        List<string> allRows = new List<string>();
        string header = "EventID,EventName,IsAnytime,EventDay,StartTime,EndTime,NodeName,SceneID,PositionX,PositionY,TimeTaken,AutoTrigger,MaxTriggerCount,ExhaustedInkNode,SummonNPCs,DespawnAfterEvent,ZoneW,ZoneH,ZoneOffsetX,ZoneOffsetY,DisplayKey";

        if (File.Exists(eventCsvPath))
        {
            var lines = File.ReadAllLines(eventCsvPath);
            if (lines.Length > 0) header = lines[0];
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrEmpty(lines[i])) continue;
                if (int.Parse(lines[i].Split(',')[7]) != saveAsID) allRows.Add(lines[i]); // 타 씬 데이터 보존
            }
        }

        foreach (var m in markersToCheck) // ★ 맵 씬 마커만 저장
        {
            m.SceneID = saveAsID;
            string pX = m.transform.position.x.ToString("F2");
            string pY = m.transform.position.y.ToString("F2");
            allRows.Add($"{m.EventID},{m.EventName},{m.IsAnytime},{m.Day},{m.StartTime},{m.EndTime},{m.InkNodeName},{m.SceneID},{pX},{pY},{m.TimeTaken},{m.AutoTrigger},{m.maxTriggerCount},{m.exhaustedInkNode},{m.summonNPCs},{m.despawnAfterEvent},{m.triggerZoneSize.x:F2},{m.triggerZoneSize.y:F2},{m.triggerZoneOffset.x:F2},{m.triggerZoneOffset.y:F2},{m.DisplayKey}");
        }

        allRows.Sort((a, b) => int.Parse(a.Split(',')[0]).CompareTo(int.Parse(b.Split(',')[0])));
        List<string> final = new List<string> { header };
        final.AddRange(allRows);

        File.WriteAllLines(eventCsvPath, final.ToArray(), new UTF8Encoding(true)); // UTF-8 BOM
        AssetDatabase.Refresh();
        Debug.Log($"[Save] Scene {saveAsID} ({scene.name}) 저장 완료.");
        return true;
    }

    private void CreateBackup()
    {
        if (!File.Exists(eventCsvPath)) return;
        if (!Directory.Exists(backupFolderPath)) Directory.CreateDirectory(backupFolderPath);
        string f = Path.Combine(backupFolderPath, $"EventTable_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv");
        File.WriteAllLines(f, File.ReadAllLines(eventCsvPath), new UTF8Encoding(true));
    }

    // ★ 대상 맵 씬을 받아, 그 씬의 ID로 걸러 그 씬에 마커를 만든다
    private void LoadMarkers(Scene scene)
    {
        int sceneID = scene.IsValid() ? GetSceneIDByName(scene.name) : -1;
        if (sceneID == -1) return; // ★ 맵 씬이 아니면 불러오지 않는다
        if (markerPrefab == null) { Debug.LogWarning("[MapEditor] 마커 프리팹이 비어 있어 불러올 수 없습니다."); return; } // ★

        ClearMarkers(scene); // ★ 이 씬의 마커만 지운다
        if (!File.Exists(eventCsvPath)) return;
        string[] lines = File.ReadAllLines(eventCsvPath);

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrEmpty(lines[i])) continue;
            string[] cols = lines[i].Split(',');
            if (int.Parse(cols[7]) != sceneID) continue; // ★ 입력칸 ID 대신 맵 씬 ID

            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(markerPrefab, scene); // ★ 활성 씬이 아니라 맵 씬에 생성
            go.transform.position = new Vector3(float.Parse(cols[8]), float.Parse(cols[9]), 0);
            var m = go.GetComponent<EventMarker>();
            m.EventID = int.Parse(cols[0]);
            m.EventName = cols[1];
            m.IsAnytime = bool.Parse(cols[2]);
            m.Day = int.Parse(cols[3]);
            m.StartTime = int.Parse(cols[4]);
            m.EndTime = int.Parse(cols[5]);
            m.InkNodeName = cols[6];
            m.SceneID = int.Parse(cols[7]);
            m.TimeTaken = int.Parse(cols[10]);
            m.AutoTrigger = CsvTableLoader.GetBool(cols, 11);
            m.maxTriggerCount = CsvTableLoader.GetInt(cols, 12, 0);
            m.exhaustedInkNode = CsvTableLoader.Get(cols, 13, "");
            m.summonNPCs = CsvTableLoader.Get(cols, 14, "");           // ★ 추가
            if (!string.IsNullOrEmpty(m.summonNPCs))
            {
                foreach (var entry in m.summonNPCs.Split('|'))
                {
                    int at = entry.IndexOf('@');
                    if (at <= 0) continue;
                    string npcName = entry.Substring(0, at);
                    var parts = entry.Substring(at + 1).Split(';');
                    if (parts.Length < 2 || !float.TryParse(parts[0], out float ox) || !float.TryParse(parts[1], out float oy)) continue;

                    var pointGo = new GameObject("SummonPoint");
                    pointGo.transform.SetParent(go.transform);
                    pointGo.transform.position = go.transform.position + new Vector3(ox, oy, 0);
                    pointGo.AddComponent<SummonPointMarker>().npcName = npcName;
                }
            }
            m.despawnAfterEvent = CsvTableLoader.GetBool(cols, 15, true); // ★ 추가
            m.triggerZoneSize = new Vector2(CsvTableLoader.GetFloat(cols, 16, 0f), CsvTableLoader.GetFloat(cols, 17, 0f));
            m.triggerZoneOffset = new Vector2(CsvTableLoader.GetFloat(cols, 18, 0f), CsvTableLoader.GetFloat(cols, 19, 0f));
            m.DisplayKey = CsvTableLoader.Get(cols, 20, "");                   // ★ 추가
            m.markerType = GetTypeFromId(m.EventID);
            go.name = $"Marker_{m.EventID}_{m.EventName}";
        }
        if (filterEnable) ApplyFilter(scene); // ★
    }

    private void ClearMarkers(Scene scene) { foreach (var m in GetMarkersIn(scene)) if (m != null) DestroyImmediate(m.gameObject); } // ★ 맵 씬 한정 (예전에는 상주 씬 마커까지 지웠다)

    private void CreateNewMarker(EventMarkerType type)
    {
        if (markerPrefab == null) return;
        if (!TryGetMapScene(out Scene mapScene)) return; // ★ 맵 씬이 없으면 만들지 않는다
        SceneView view = SceneView.lastActiveSceneView;
        Vector3 spawnPos = view ? view.camera.transform.position : Vector3.zero; spawnPos.z = 0;
        float x = Mathf.Round(spawnPos.x / gridSize) * gridSize;
        float y = Mathf.Round(spawnPos.y / gridSize) * gridSize;
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(markerPrefab, mapScene); // ★ 활성 씬(상주 씬)이 아니라 맵 씬에 생성
        go.transform.position = new Vector3(x, y, 0);

        var m = go.GetComponent<EventMarker>();
        m.markerType = type;
        m.EventID = 0;
        m.SceneID = GetSceneIDByName(mapScene.name); // ★ 입력칸 ID 대신 실제로 놓인 맵 씬의 ID
        m.EventName = type switch
        {
            EventMarkerType.Normal_NPC => "NPC",
            EventMarkerType.System_Repeat => "System",
            EventMarkerType.Cutscene => "Cutscene",
            EventMarkerType.Interactable => "Object",
            _ => "Event",
        };
        if (type == EventMarkerType.Cutscene) m.AutoTrigger = true; // 연출은 보통 자동 발동
        Undo.RegisterCreatedObjectUndo(go, "Create Event Marker"); // ★ 생성도 Ctrl+Z로 되돌릴 수 있게
        Selection.activeGameObject = go;
    }

    private void AutoAssignIDs(Scene scene) // ★ 맵 씬 한정
    {
        HashSet<int> used = new HashSet<int>();
        if (File.Exists(eventCsvPath))
        {
            var lines = File.ReadAllLines(eventCsvPath);
            for (int i = 1; i < lines.Length; i++)
                if (!string.IsNullOrEmpty(lines[i])) used.Add(int.Parse(lines[i].Split(',')[0]));
        }

        foreach (var m in GetMarkersIn(scene)) // ★
        {
            if (m.EventID != 0) continue;
            int newID = GetBaseIdFor(m.markerType);
            while (used.Contains(newID)) newID++;
            m.EventID = newID;
            used.Add(newID);
            m.name = $"Marker_{newID}_{m.EventName}";
            EditorUtility.SetDirty(m);
        }
    }

    // ★ 세 함수 모두 열린 모든 씬 대신 맵 씬의 마커만 다룬다
    private void SnapAllMarkers() { foreach (var m in GetMarkersIn(FindMapScene())) { m.transform.position = new Vector3(Mathf.Round(m.transform.position.x / gridSize) * gridSize, Mathf.Round(m.transform.position.y / gridSize) * gridSize, 0); } }
    private void ApplyFilter(Scene scene) { foreach (var m in GetMarkersIn(scene)) m.gameObject.SetActive(m.IsAnytime || (m.Day == filterDay && filterTime >= m.StartTime && filterTime < m.EndTime)); }
    private void ShowAllMarkers() { foreach (var m in GetMarkersIn(FindMapScene())) m.gameObject.SetActive(true); }

    // =================================================================================
    // ★ 맵 씬 판별 — 상주 씬(Persistent Scene)과 맵 씬을 함께 열어 두고 작업하기 때문에
    //    활성 씬 대신 "열린 씬 중 SceneTable.csv에 등록된 씬"을 맵 씬으로 본다
    // =================================================================================
    private bool IsMapScene(Scene s) => s.IsValid() && s.isLoaded && GetSceneIDByName(s.name) != -1;

    private List<Scene> GetOpenMapScenes()
    {
        var list = new List<Scene>();
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            Scene s = EditorSceneManager.GetSceneAt(i);
            if (IsMapScene(s)) list.Add(s);
        }
        return list;
    }

    // 활성 씬이 맵 씬이면 그것을, 아니면 처음 찾은 맵 씬을 돌려준다. 없으면 default(IsValid() == false)
    private Scene FindMapScene()
    {
        Scene active = EditorSceneManager.GetActiveScene();
        if (IsMapScene(active)) return active;
        var maps = GetOpenMapScenes();
        return maps.Count > 0 ? maps[0] : default;
    }

    // 맵 씬이 없으면 안내 창을 띄우고 false
    private bool TryGetMapScene(out Scene scene)
    {
        scene = FindMapScene();
        if (scene.IsValid()) return true;
        EditorUtility.DisplayDialog("맵 씬 없음", "열린 씬 중 SceneTable.csv에 등록된 맵 씬이 없습니다.\n맵 씬을 열거나, 새 맵이면 SceneTable.csv에 먼저 등록하세요.", "확인");
        return false;
    }

    // 한 씬 안의 마커만 모은다 (비활성 포함)
    private List<EventMarker> GetMarkersIn(Scene scene)
    {
        var list = new List<EventMarker>();
        if (!scene.IsValid() || !scene.isLoaded) return list;
        foreach (var root in scene.GetRootGameObjects())
            list.AddRange(root.GetComponentsInChildren<EventMarker>(true));
        return list;
    }

    // 맵 씬이 아닌 씬(상주 씬 등)에 들어간 마커
    private List<EventMarker> GetStrayMarkers()
    {
        var list = new List<EventMarker>();
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            Scene s = EditorSceneManager.GetSceneAt(i);
            if (!s.isLoaded || IsMapScene(s)) continue;
            list.AddRange(GetMarkersIn(s));
        }
        return list;
    }

    // 헬퍼 함수
    // ★ 새 맵을 Additive로 열고 활성 씬으로 지정한 뒤 옛 맵만 닫는다 — 상주 씬은 그대로 남는다
    //    (예전에는 Single 모드라 상주 씬까지 모두 닫혔다)
    private void OpenSceneByID(int id, Scene oldMap)
    {
        string tName = GetSceneNameByID(id);
        if (tName == "Unknown")
        {
            EditorUtility.DisplayDialog("이동 불가", $"SceneTable.csv에 ID {id}가 없습니다.", "확인"); // ★
            return;
        }

        // 이미 열려 있으면 새로 열지 않는다
        Scene opened = default;
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            Scene s = EditorSceneManager.GetSceneAt(i);
            if (s.isLoaded && s.name == tName) { opened = s; break; }
        }

        if (!opened.IsValid())
        {
            // ★ FindAssets는 이름 일부만 맞아도 찾으므로 파일 이름이 정확히 같은 씬을 고른다
            string path = AssetDatabase.FindAssets($"{tName} t:Scene")
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == tName);
            if (string.IsNullOrEmpty(path))
            {
                EditorUtility.DisplayDialog("이동 불가", $"'{tName}' 씬 파일을 찾을 수 없습니다.", "확인");
                return;
            }
            opened = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive); // OnSceneOpened가 마커를 불러온다
        }

        EditorSceneManager.SetActiveScene(opened);
        if (oldMap.IsValid() && oldMap != opened) EditorSceneManager.CloseScene(oldMap, true);
        targetSceneID = id;
    }
    // ★ 성능 — 맵 씬 판별 때마다 SceneTable.csv를 디스크에서 다시 읽었다(한 번 그릴 때 15번 이상).
    //   파일 수정 시각이 바뀔 때만 다시 읽어 사전으로 들고 있는다
    private readonly Dictionary<int, string> _sceneNameById = new();
    private readonly Dictionary<string, int> _sceneIdByName = new();
    private DateTime _sceneTableStamp = DateTime.MinValue;

    private void EnsureSceneTable()
    {
        if (!File.Exists(sceneCsvPath)) { _sceneNameById.Clear(); _sceneIdByName.Clear(); _sceneTableStamp = DateTime.MinValue; return; }
        DateTime stamp = File.GetLastWriteTimeUtc(sceneCsvPath);
        if (stamp == _sceneTableStamp) return;

        _sceneTableStamp = stamp;
        _sceneNameById.Clear();
        _sceneIdByName.Clear();
        var lines = File.ReadAllLines(sceneCsvPath);
        for (int i = 1; i < lines.Length; i++)
        {
            var c = lines[i].Split(',');
            if (c.Length < 2 || !int.TryParse(c[0], out int id)) continue;
            string sceneName = c[1].Trim();
            _sceneNameById[id] = sceneName;
            _sceneIdByName[sceneName] = id;
        }
    }

    private string GetSceneNameByID(int id)
    {
        EnsureSceneTable();
        return _sceneNameById.TryGetValue(id, out var n) ? n : "Unknown";
    }
    private int GetSceneIDByName(string name)
    {
        EnsureSceneTable();
        return name != null && _sceneIdByName.TryGetValue(name.Trim(), out var id) ? id : -1;
    }
    private void DetectCurrentSceneID() { Scene map = FindMapScene(); if (map.IsValid()) targetSceneID = GetSceneIDByName(map.name); } // ★ 활성 씬 대신 맵 씬

    class ScheduleItem { public string Name; public int Day; public int Start; public int End; public int SceneID; public Vector2 Pos; public bool IsCutscene; }

    // ------ 시작 위치 지정 관련
    private void DrawStartPositionUI()
    {
        if (startConfig == null) startConfig = AssetDatabase.LoadAssetAtPath<GameStartConfig>("Assets/Datas/GameStartConfig.asset");
        GUILayout.Space(10);
        GUILayout.Label("게임 시작 위치", EditorStyles.boldLabel);
        if (startConfig == null) { EditorGUILayout.HelpBox("Assets/Datas/GameStartConfig.asset이 없음", MessageType.Warning); return; }

        EditorGUILayout.LabelField($"현재: Scene {startConfig.startSceneID}, {startConfig.startPosition}");

        if (Application.isPlaying)
        {
            if (GUILayout.Button("[Play 중] 현재 플레이어 위치를 시작 위치로 저장"))
            {
                var player = PlayerManager.Instance?.CurrentCharacter;
                if (player != null)
                {
                    startConfig.startSceneID = SceneLoader.Instance.CurrentSceneID;
                    startConfig.startPosition = player.transform.position;
                    EditorUtility.SetDirty(startConfig);
                    AssetDatabase.SaveAssets();
                }
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Play 모드로 실행해서 원하는 위치로 걸어간 뒤 저장하는 걸 추천해.", MessageType.Info);
        }

        // ★ 시작 위치 마커도 맵 씬 안에서만 찾는다 (FindObjectOfType은 상주 씬까지 훑었다)
        Scene mapScene = FindMapScene();
        StartPositionMarker marker = null;
        if (mapScene.IsValid())
        {
            foreach (var root in mapScene.GetRootGameObjects())
            {
                marker = root.GetComponentInChildren<StartPositionMarker>(true);
                if (marker != null) break;
            }
        }

        if (marker == null)
        {
            if (GUILayout.Button("씬에 시작 위치 마커 배치"))
            {
                if (TryGetMapScene(out Scene scene)) // ★ 맵 씬이 없으면 배치하지 않는다
                {
                    var go = new GameObject("StartPositionMarker");
                    SceneManager.MoveGameObjectToScene(go, scene); // ★ 새 오브젝트는 활성 씬에 생기므로 맵 씬으로 옮긴다
                    go.transform.position = new Vector3(startConfig.startPosition.x, startConfig.startPosition.y, 0);
                    go.AddComponent<StartPositionMarker>();
                    Selection.activeGameObject = go;
                }
            }
        }
        else if (GUILayout.Button("마커 위치를 시작 위치로 저장"))
        {
            int realID = GetSceneIDByName(marker.gameObject.scene.name); // ★ 마커가 놓인 맵 씬의 ID
            if (realID == -1)
                EditorUtility.DisplayDialog("경고", "지금 열려있는 씬이 SceneTable.csv에 등록된 맵이 아닙니다. 실제 맵 씬을 열어서 마커를 배치해주세요.", "확인");
            else
            {
                startConfig.startSceneID = realID;
                startConfig.startPosition = marker.transform.position;
                EditorUtility.SetDirty(startConfig);
                AssetDatabase.SaveAssets();
            }
        }
    }

    // MapDataEditor.cs — 저장 직전에 자식 마커를 summonNPCs 문자열로 자동 변환
    private void SyncSummonPointsToData(EventMarker m)
    {
        var points = m.GetComponentsInChildren<SummonPointMarker>();
        if (points.Length == 0) return;

        var entries = new List<string>();
        foreach (var p in points)
        {
            if (string.IsNullOrEmpty(p.npcName)) continue;
            Vector2 offset = (Vector2)(p.transform.position - m.transform.position);
            entries.Add($"{p.npcName}@{offset.x:F2};{offset.y:F2}");
        }
        m.summonNPCs = string.Join("|", entries); // ★ 쉼표 대신 파이프 — CSV 충돌 회피
        EditorUtility.SetDirty(m);
    }
}
