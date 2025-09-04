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
                T[] _finds = FindObjectsByType<T>(FindObjectsSortMode.None);

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
