using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
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
    }

    private void OnDisable()
    {
        // 이벤트 연결 해제
        EditorSceneManager.sceneOpened -= OnSceneOpened;
    }

    // ★ [유령 데이터 방지] 씬 진입 시 좀비 마커 삭제 및 데이터 동기화
    private void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode)
    {
        ClearMarkers(); // 씬에 남아있는 좀비 마커 제거
        DetectCurrentSceneID(); // 현재 씬 ID 갱신

        // 필요하다면 자동으로 로드 (원치 않으면 주석 처리)
        LoadMarkers(); 

        Debug.Log($"[MapEditor] 씬 진입: {scene.name} (ID: {targetSceneID}) - 유령 마커 정리 완료");
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
        GUILayout.Label("1. 씬 이동 & 설정", EditorStyles.boldLabel);

        string currentSceneName = EditorSceneManager.GetActiveScene().name;
        string targetNameFromCSV = GetSceneNameByID(targetSceneID);
        bool isMatch = (currentSceneName == targetNameFromCSV);

        GUIStyle statusStyle = new GUIStyle(EditorStyles.label);
        statusStyle.normal.textColor = isMatch ? Color.green : Color.red;
        statusStyle.fontStyle = FontStyle.Bold;
        GUILayout.Label($"현재 씬: {currentSceneName} / 타겟 ID({targetSceneID}): {targetNameFromCSV}", statusStyle);

        GUILayout.BeginHorizontal();
        targetSceneID = EditorGUILayout.IntField("이동할 Scene ID", targetSceneID);

        // ★ [안전장치] 안전한 이동 버튼
        if (GUILayout.Button("이동 (Move)", GUILayout.Width(120)))
        {
            TryOpenScene(targetSceneID);
        }
        GUILayout.EndHorizontal();

        if (!isMatch) EditorGUILayout.HelpBox("현재 씬과 타겟 ID가 다릅니다. 이동 시 저장 여부를 확인합니다.", MessageType.Warning);

        markerPrefab = (GameObject)EditorGUILayout.ObjectField("Prefab", markerPrefab, typeof(GameObject), false);

        GUILayout.Space(10);
        GUILayout.Label("2. 생성 및 배치", EditorStyles.boldLabel);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("+ NPC")) CreateNewMarker(EventMarkerType.Normal_NPC);
        if (GUILayout.Button("+ System")) CreateNewMarker(EventMarkerType.System_Repeat);
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
        if (GUILayout.Button("Load CSV", GUILayout.Height(40))) LoadMarkers();

        // ★ [안전장치] 현재 씬 ID로 강제 저장
        if (GUILayout.Button("Save Current Scene", GUILayout.Height(40)))
        {
            int realID = GetSceneIDByName(EditorSceneManager.GetActiveScene().name);
            if (realID != -1)
            {
                SaveMarkers(realID);
                targetSceneID = realID;
            }
            else
            {
                if (EditorUtility.DisplayDialog("경고", "SceneTable에 없는 씬입니다. 입력된 ID로 저장할까요?", "네", "아니오"))
                    SaveMarkers(targetSceneID);
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(5);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("ID 재정렬")) AutoAssignIDs();
        if (GUILayout.Button("전체 데이터 삭제")) ClearMarkers();
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
    }

    private void DrawFilterUI()
    {
        GUILayout.Label("3. 필터링 (View Filter)", EditorStyles.boldLabel);
        GUILayout.BeginHorizontal();
        bool prev = filterEnable;
        filterEnable = EditorGUILayout.Toggle("필터 적용", filterEnable);
        if (prev != filterEnable) ApplyFilter();

        if (filterEnable)
        {
            filterDay = EditorGUILayout.IntField("Day", filterDay);
            filterTime = EditorGUILayout.IntField("Time", filterTime);
            if (GUILayout.Button("Apply")) ApplyFilter();
        }
        else
        {
            if (GUILayout.Button("Show All")) ShowAllMarkers();
        }
        GUILayout.EndHorizontal();
    }

    // =================================================================================
    // [탭 2] NPC 스케줄표 (요청하신 기능 복구!)
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
                Pos = new Vector2(float.Parse(cols[8]), float.Parse(cols[9]))
            });
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
                GUILayout.Label($"Day {item.Day}", GUILayout.Width(50));

                string timeStr = (item.Start == 0 && item.End == 24) ? "All Day" : $"{item.Start}시 ~ {item.End}시";
                GUILayout.Label(timeStr, GUILayout.Width(100));

                GUILayout.Label($"Scene {item.SceneID}", GUILayout.Width(80));
                GUILayout.Label($"Pos {item.Pos}");
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
        // 1. 현재 씬 ID 파악
        string currentSceneName = EditorSceneManager.GetActiveScene().name;
        int currentID = GetSceneIDByName(currentSceneName);

        // 2. ★ 변경사항이 있는지 검사 (Smart Check) ★
        bool isDirty = IsCurrentSceneDirty(currentID);

        // 3. 변경사항이 있을 때만 물어봄
        if (isDirty)
        {
            int option = EditorUtility.DisplayDialogComplex("변경사항 감지",
                $"현재 씬({currentSceneName})에 '저장되지 않은 변경사항'이 있습니다.\n저장하지 않고 이동하면 사라집니다.",
                "저장 후 이동", "그냥 이동 (삭제됨)", "취소");

            switch (option)
            {
                case 0: // 저장 후 이동
                    if (currentID != -1) { SaveMarkers(currentID); ClearMarkers(); OpenSceneByID(nextSceneID); }
                    else EditorUtility.DisplayDialog("오류", "현재 씬 ID를 알 수 없어 저장할 수 없습니다.", "확인");
                    break;
                case 1: // 그냥 이동
                    ClearMarkers(); OpenSceneByID(nextSceneID);
                    break;
                case 2: return; // 취소
            }
        }
        else
        {
            // 변경사항 없으면 묻지도 따지지도 않고 바로 이동
            ClearMarkers();
            OpenSceneByID(nextSceneID);
        }
    }

    // 1. 현재 씬과 CSV 파일 내용 비교 함수
    private bool IsCurrentSceneDirty(int sceneID)
    {
        if (sceneID == -1) return false;

        // A. 현재 화면에 있는 마커들을 문자열 리스트로 변환
        List<string> currentMarkerData = new List<string>();
        var markers = FindObjectsOfType<EventMarker>(true);
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
    private string GetMarkerCsvString(EventMarker m)
    {
        string pX = m.transform.position.x.ToString("F2");
        string pY = m.transform.position.y.ToString("F2");
        return $"{m.EventID},{m.EventName},{m.IsAnytime},{m.Day},{m.StartTime},{m.EndTime},{m.InkNodeName},{m.SceneID},{pX},{pY},{m.TimeTaken}";
    }

    // 3. CSV 읽은 줄 -> 비교용 표준 포맷 변환
    private string NormalizeCsvLine(string[] cols)
    {
        // 파일에 4.5라고 적혀있어도 4.50으로 변환해서 비교해야 함
        float x = float.Parse(cols[8]);
        float y = float.Parse(cols[9]);
        string pX = x.ToString("F2");
        string pY = y.ToString("F2");

        return $"{cols[0]},{cols[1]},{cols[2]},{cols[3]},{cols[4]},{cols[5]},{cols[6]},{cols[7]},{pX},{pY},{cols[10]}";
    }


    private void SaveMarkers(int saveAsID)
    {
        CreateBackup();
        AutoAssignIDs();

        List<string> allRows = new List<string>();
        string header = "EventID,EventName,IsAnytime,EventDay,StartTime,EndTime,NodeName,SceneID,PositionX,PositionY,TimeTaken";

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

        var markers = FindObjectsOfType<EventMarker>(true);
        foreach (var m in markers)
        {
            m.SceneID = saveAsID;
            string pX = m.transform.position.x.ToString("F2");
            string pY = m.transform.position.y.ToString("F2");
            allRows.Add($"{m.EventID},{m.EventName},{m.IsAnytime},{m.Day},{m.StartTime},{m.EndTime},{m.InkNodeName},{m.SceneID},{pX},{pY},{m.TimeTaken}");
        }

        allRows.Sort((a, b) => int.Parse(a.Split(',')[0]).CompareTo(int.Parse(b.Split(',')[0])));
        List<string> final = new List<string> { header };
        final.AddRange(allRows);

        File.WriteAllLines(eventCsvPath, final.ToArray(), new UTF8Encoding(true)); // UTF-8 BOM
        AssetDatabase.Refresh();
        Debug.Log($"[Save] Scene {saveAsID} 저장 완료.");
    }

    private void CreateBackup()
    {
        if (!File.Exists(eventCsvPath)) return;
        if (!Directory.Exists(backupFolderPath)) Directory.CreateDirectory(backupFolderPath);
        string f = Path.Combine(backupFolderPath, $"EventTable_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv");
        File.WriteAllLines(f, File.ReadAllLines(eventCsvPath), new UTF8Encoding(true));
    }

    private void LoadMarkers()
    {
        ClearMarkers();
        if (!File.Exists(eventCsvPath)) return;
        string[] lines = File.ReadAllLines(eventCsvPath);

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrEmpty(lines[i])) continue;
            string[] cols = lines[i].Split(',');
            if (int.Parse(cols[7]) != targetSceneID) continue;

            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(markerPrefab);
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
            m.markerType = m.EventID >= 90000 ? EventMarkerType.System_Repeat : EventMarkerType.Normal_NPC;
            go.name = $"Marker_{m.EventID}_{m.EventName}";
        }
        if (filterEnable) ApplyFilter();
    }

    private void ClearMarkers() { foreach (var m in FindObjectsOfType<EventMarker>(true)) DestroyImmediate(m.gameObject); }

    private void CreateNewMarker(EventMarkerType type)
    {
        if (markerPrefab == null) return;
        SceneView view = SceneView.lastActiveSceneView;
        Vector3 spawnPos = view ? view.camera.transform.position : Vector3.zero; spawnPos.z = 0;
        float x = Mathf.Round(spawnPos.x / gridSize) * gridSize;
        float y = Mathf.Round(spawnPos.y / gridSize) * gridSize;
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(markerPrefab);
        go.transform.position = new Vector3(x, y, 0);
        var m = go.GetComponent<EventMarker>();
        m.markerType = type; m.EventID = 0; m.SceneID = targetSceneID; m.EventName = (type == EventMarkerType.Normal_NPC) ? "NPC" : "System";
        Selection.activeGameObject = go;
    }

    private void AutoAssignIDs()
    {
        HashSet<int> used = new HashSet<int>();
        if (File.Exists(eventCsvPath)) { var lines = File.ReadAllLines(eventCsvPath); for (int i = 1; i < lines.Length; i++) if (!string.IsNullOrEmpty(lines[i])) used.Add(int.Parse(lines[i].Split(',')[0])); }
        int n = 10000, s = 90000;
        foreach (var m in FindObjectsOfType<EventMarker>(true)) { if (m.EventID == 0) { int newID = m.markerType == EventMarkerType.Normal_NPC ? n : s; while (used.Contains(newID)) newID++; m.EventID = newID; used.Add(newID); m.name = $"Marker_{newID}_{m.EventName}"; if (m.markerType == EventMarkerType.Normal_NPC) n = newID + 1; else s = newID + 1; EditorUtility.SetDirty(m); } }
    }

    private void SnapAllMarkers() { foreach (var m in FindObjectsOfType<EventMarker>(true)) { m.transform.position = new Vector3(Mathf.Round(m.transform.position.x / gridSize) * gridSize, Mathf.Round(m.transform.position.y / gridSize) * gridSize, 0); } }
    private void ApplyFilter() { foreach (var m in FindObjectsOfType<EventMarker>(true)) m.gameObject.SetActive(m.IsAnytime || (m.Day == filterDay && filterTime >= m.StartTime && filterTime < m.EndTime)); }
    private void ShowAllMarkers() { foreach (var m in FindObjectsOfType<EventMarker>(true)) m.gameObject.SetActive(true); }

    // 헬퍼 함수
    private void OpenSceneByID(int id)
    {
        string tName = GetSceneNameByID(id);
        if (tName != "Unknown")
        {
            var guids = AssetDatabase.FindAssets($"{tName} t:Scene");
            if (guids.Length > 0) { EditorSceneManager.OpenScene(AssetDatabase.GUIDToAssetPath(guids[0])); targetSceneID = id; }
        }
    }
    private string GetSceneNameByID(int id)
    {
        if (!File.Exists(sceneCsvPath)) return "Unknown";
        var lines = File.ReadAllLines(sceneCsvPath);
        for (int i = 1; i < lines.Length; i++) { var c = lines[i].Split(','); if (c.Length > 1 && int.Parse(c[0]) == id) return c[1].Trim(); }
        return "Unknown";
    }
    private int GetSceneIDByName(string name)
    {
        if (!File.Exists(sceneCsvPath)) return -1;
        var lines = File.ReadAllLines(sceneCsvPath);
        for (int i = 1; i < lines.Length; i++) { var c = lines[i].Split(','); if (c.Length > 1 && c[1].Trim() == name.Trim()) return int.Parse(c[0]); }
        return -1;
    }
    private void DetectCurrentSceneID() { int id = GetSceneIDByName(EditorSceneManager.GetActiveScene().name); if (id != -1) targetSceneID = id; }

    class ScheduleItem { public string Name; public int Day; public int Start; public int End; public int SceneID; public Vector2 Pos; }
}