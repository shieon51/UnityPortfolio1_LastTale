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
                    DontDestroyOnLoad(_instance.gameObject);
                }

                if (_finds.Length > 1)
                {
                    for (int i = 1; i < _finds.Length; i++)
                        Destroy(_finds[i].gameObject);
                }

                if (_instance == null)
                {
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
