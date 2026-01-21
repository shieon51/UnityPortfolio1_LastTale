using System;
using UnityEngine;

[Serializable]
public class PortalData
{
    public int PortalID;
    public int ExistSceneID;
    public Vector2 PortalPos;
    public int SpawnPortalID;
    public string Condition; // 이동 조건 (추후 구현)
}

public class Portal : MonoBehaviour
{
    public PortalData portalData;
    private bool playerInRange = false;

    void Update()
    {
        if (playerInRange && Input.GetKeyDown(KeyCode.UpArrow)) //해당 포탈 범위 내에서 윗방향키를 누를 경우
        {
            //Vector2 spawnPos = PortalManager.Instance.GetSpawnPosition(portalData.SpawnPortalID);
            //int targetSceneID = PortalManager.Instance.GetSceneIDByPortal(portalData.SpawnPortalID);
            //SceneLoader.Instance.LoadScene(targetSceneID, spawnPos);
        }
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (col.CompareTag("Player"))
            playerInRange = true;
    }

    private void OnTriggerExit2D(Collider2D col)
    {
        if (col.CompareTag("Player"))
            playerInRange = false;
    }
}
