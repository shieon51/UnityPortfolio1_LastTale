using UnityEngine;

public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;
    private static bool isApplicationQuit = false;
    public static T Instance
    {
        get
        {
            if (isApplicationQuit == true)
                return null;

            if (_instance == null)
            {
                // ★ 핵심 수정: 비활성 오브젝트도 검색 대상에 포함 (Include).
                //   기존엔 기본값(Exclude)이라 잠깐이라도 꺼져있던 싱글톤을 못 찾고,
                //   설정이 텅 빈 새 오브젝트를 몰래 만들어버리는 사고가 반복됐음.
                T[] _finds = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);

                if (_finds.Length > 0)
                {
                    _instance = _finds[0];
                    // ★ DontDestroyOnLoad는 루트 오브젝트에만 적용된다.
                    //   자식에 붙은 싱글톤은 경고만 뜨고 아무 일도 일어나지 않았다
                    DontDestroyOnLoad(_instance.transform.root.gameObject);
                }

                if (_finds.Length > 1)
                {
                    for (int i = 1; i < _finds.Length; i++)
                    {
                        Debug.LogWarning($"[Singleton] '{typeof(T).Name}'이 여러 개 있어 중복을 제거합니다: {_finds[i].name}", _finds[i]);
                        Destroy(_finds[i].gameObject);
                    }
                }

                if (_instance == null)
                {
                    // ★ 자동 생성은 설정이 빈 오브젝트가 만들어지는 사고로 이어지므로 반드시 알린다
                    Debug.LogWarning($"[Singleton] 씬에 '{typeof(T).Name}'이 없어 새로 만듭니다. 인스펙터 설정이 비어 있을 수 있습니다.");
                    GameObject _createGameObject = new GameObject(typeof(T).Name);
                    DontDestroyOnLoad(_createGameObject);
                    _instance = _createGameObject.AddComponent<T>();
                }
            }
            return _instance;
        }
    }

    private void OnApplicationQuit()
    {
        isApplicationQuit = true;
    }
}
