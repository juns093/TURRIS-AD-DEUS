using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// 각 씬(SideScene, BossScene 등)의 최상단에 빈 오브젝트로 하나씩 배치.
/// 씬이 시작될 때:
///  1) 이 씬에 맞는 GameMode(Side/Top)를 GameModeManager에 세팅
///  2) PendingSpawn에 담겨온 스폰포인트로 플레이어를 이동
///  3) 이 씬의 CinemachineCamera가 "지금 살아있는" 플레이어를 다시 Follow하도록 재배정
///     (플레이어는 DontDestroyOnLoad로 유지되지만, 카메라의 Follow 참조는
///      씬이 바뀌면서 끊기기 때문에 반드시 씬 시작마다 다시 연결해줘야 함)
/// </summary>
public class SceneEntryManager : MonoBehaviour
{
    [Tooltip("이 씬이 사이드뷰인지 탑뷰(보스방)인지")]
    [SerializeField] private GameModeManager.GameMode modeForThisScene;

    [SerializeField] private string playerTag = "Player";

    [Tooltip("이 씬에서 플레이어를 따라다닐 CinemachineCamera. 씬 계층에서 직접 드래그해서 연결.")]
    [SerializeField] private CinemachineCamera sceneCamera;

    private void Start()
    {
        if (GameModeManager.Instance != null)
        {
            GameModeManager.Instance.SetMode(modeForThisScene);
        }
        else
        {
            Debug.LogWarning("GameModeManager 인스턴스가 없습니다. 씬에 GameModeManager를 배치했는지 확인하세요.");
        }

        var playerObj = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObj == null)
        {
            Debug.LogWarning("씬에서 Player 태그를 가진 오브젝트를 찾지 못했습니다.");
            return;
        }

        Vector3 previousPosition = playerObj.transform.position;

        PlacePlayerAtSpawn(playerObj);

        // 카메라를 항상 "지금 씬의" Follow 대상으로 다시 연결한다.
        // (인스펙터에서 미리 연결해뒀어도, 그게 예전 씬의 Player였다면 파괴돼서 끊겨있는 상태이므로
        //  매번 코드로 다시 꽂아주는 게 안전함)
        if (sceneCamera != null)
        {
            sceneCamera.Follow = playerObj.transform;

            // 이 씬의 카메라에서 PositionComposer를 찾아서 대시 이펙트 매니저에 등록
            var composer = sceneCamera.GetComponent<CinemachinePositionComposer>();
            if (composer != null && CameraDashEffect.Instance != null)
            {
                CameraDashEffect.Instance.SetComposer(composer);
            }

            // 플레이어가 스폰포인트로 순간이동했다면, 카메라도 부드럽게 따라가지 말고
            // 그 이동분만큼 그대로 같이 점프시켜서 "화면이 스윽 밀려오는" 현상을 없앤다.
            Vector3 delta = playerObj.transform.position - previousPosition;
            if (delta.sqrMagnitude > 0.0001f)
            {
                sceneCamera.OnTargetObjectWarped(playerObj.transform, delta);
            }
        }
    }

    private void PlacePlayerAtSpawn(GameObject playerObj)
    {
        if (string.IsNullOrEmpty(PendingSpawn.SpawnPointId))
            return; // 처음 게임을 시작한 씬이라 넘어온 스폰 정보가 없는 경우 등

        var spawnPoints = FindObjectsOfType<PlayerSpawnPoint>();
        PlayerSpawnPoint target = null;
        foreach (var sp in spawnPoints)
        {
            if (sp.Id == PendingSpawn.SpawnPointId)
            {
                target = sp;
                break;
            }
        }

        if (target == null)
        {
            Debug.LogWarning($"스폰포인트 id '{PendingSpawn.SpawnPointId}'를 이 씬에서 찾을 수 없습니다.");
        }
        else
        {
            playerObj.transform.position = target.transform.position;
            playerObj.GetComponent<PlayerController>()?.ResetPhysicsState();
        }

        // 한 번 쓰고 나면 반드시 비워서, 다음에 스폰 정보 없이 씬을 열었을 때 엉뚱하게 재사용되지 않게 한다.
        PendingSpawn.SpawnPointId = null;
    }
}