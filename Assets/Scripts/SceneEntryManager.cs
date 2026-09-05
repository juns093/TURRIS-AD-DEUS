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
        EnsureGameModeManager();
        GameModeManager.Instance.SetMode(modeForThisScene);

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

    /// <summary>
    /// GameModeManager 가 아직 없으면 여기서 만든다.
    ///
    /// 원래는 tutorialScene 에 놓인 것 하나가 DontDestroyOnLoad 로 따라다니는 구조였다.
    /// 그래서 에디터에서 BossRoom1 이나 RestScene 을 직접 열고 Play 하면 매니저가 없고,
    /// PlayerController.FixedUpdate 가 맨 첫 줄에서 return 해버려서 플레이어가 아예 안 움직였다.
    ///
    /// 씬마다 하나씩 놓는 대신 없을 때만 만들어주면, 어느 씬에서 Play 를 눌러도 똑같이 동작한다.
    /// (만들어진 것도 DontDestroyOnLoad 라 이후 씬으로 그대로 따라간다)
    /// </summary>
    private void EnsureGameModeManager()
    {
        if (GameModeManager.Instance != null) return;

        new GameObject("GameModeManager (auto)").AddComponent<GameModeManager>();
        Debug.Log("[SceneEntryManager] GameModeManager 가 없어서 하나 만들었습니다.", this);
    }

    private void PlacePlayerAtSpawn(GameObject playerObj)
    {
        if (string.IsNullOrEmpty(PendingSpawn.SpawnPointId))
        {
            // 넘어온 스폰 정보가 없는 경우.
            // 플레이어가 씬을 넘어 살아남게 되면서, 그냥 두면 이전 씬의 좌표를 그대로 들고 와서
            // 맵 밖에 떨어진다. 이 씬에 원래 놓여 있던(중복이라 지워진) 플레이어 자리로 데려간다.
            Vector3? home = PersistentPlayer.ConsumeSceneSpawnHint();
            if (home.HasValue)
            {
                playerObj.transform.position = home.Value;
                playerObj.GetComponent<PlayerController>()?.ResetPhysicsState();
            }
            return;
        }

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

            // 못 찾았다고 이전 씬 좌표에 그대로 두면 맵 밖이다. 씬에 놓여 있던 자리로라도 데려간다.
            Vector3? home = PersistentPlayer.ConsumeSceneSpawnHint();
            if (home.HasValue)
            {
                playerObj.transform.position = home.Value;
                playerObj.GetComponent<PlayerController>()?.ResetPhysicsState();
            }
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