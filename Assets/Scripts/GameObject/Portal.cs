using System;
using UnityEngine;



public class Portal : MonoBehaviour
{
    public int myPortalID; // 인스펙터에서 1001, 1002 입력

    // 이동할 목적지의 데이터를 직접 들고 있음
    private PortalData _destinationData;

    private bool playerInRange = false;

    // ★ 생성되자마자 매니저가 호출해줄 초기화 함수
    public void Init(int id)
    {
        myPortalID = id;

        // 1. 내 ID로 매니저한테 내 데이터 원본 받아오기
        PortalData myData = PortalManager.Instance.GetData(myPortalID);

        if (myData == null)
        {
            Debug.LogError($"Portal {myPortalID} Data is NULL! (CSV 확인 필요)");
            return;
        }

        // 2. 미리 연결된 도착지 데이터(참조) 가져오기
        // (매니저가 LoadAndBuildData 할 때 이미 연결해둠)
        _destinationData = myData.ConnectedTargetData;

        // (디버깅용) 타겟이 잘 연결됐나 로그 확인
        // if (_destinationData != null) 
        //    Debug.Log($"Portal {id} initialized. Target: {_destinationData.ID}");
    }

    void Update()
    {
        // 플레이어가 범위 안에 있고 윗키 누르면 이동
        if (playerInRange && Input.GetKeyDown(KeyCode.UpArrow))
        {
            TryEnterPortal();
        }
    }

    private void TryEnterPortal()
    {
        // 이동 가능한 포탈인지 확인
        if (_destinationData != null)
        {
            // * 검색(Find) 없이 즉시 접근: 목적지 씬 ID와 목적지 좌표를 바로 씬 로더에 전달(Zero-lookup 방식)
            SceneLoader.Instance.LoadScene(_destinationData.OwnerSceneID, _destinationData.Position);
        }
        else
        {
            Debug.Log("이동할 수 없는 포탈입니다.");
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
