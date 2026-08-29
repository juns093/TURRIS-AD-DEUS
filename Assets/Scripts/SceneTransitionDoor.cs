using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 보스방 입구/출구 문에 붙이는 스크립트.
/// 플레이어가 트리거 범위 안에 들어와 있을 때 F키(기본값)를 누르면
/// 지정한 씬을 로드하고, 그 씬의 지정된 스폰포인트로 플레이어를 이동시킨다.
///
/// 사용법:
///  1) 문 오브젝트에 Collider2D(Is Trigger 체크) + 이 스크립트 부착
///  2) targetSceneName: 이동할 씬 이름 (Build Settings에 등록되어 있어야 함)
///  3) targetSpawnPointId: 도착할 씬에 있는 PlayerSpawnPoint의 id와 동일하게 입력
///  4) interactPrompt: (선택) "F : 입장" 같은 안내 UI/스프라이트, 범위 안에 들어오면 자동으로 켜고 끔
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class SceneTransitionDoor : MonoBehaviour
{
    [Header("전환 대상")]
    [SerializeField] private string targetSceneName;
    [SerializeField] private string targetSpawnPointId;

    [Header("상호작용")]
    [SerializeField] private KeyCode interactKey = KeyCode.F;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private GameObject interactPrompt; // 없으면 그냥 생략됨

    private bool playerInRange;

    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log($"[SceneTransitionDoor] OnTriggerEnter2D: {other.name} (tag={other.tag})");
        if (!other.CompareTag(playerTag)) return;
        playerInRange = true;
        Debug.Log("[SceneTransitionDoor] 플레이어가 범위 안에 들어옴. F키 대기 중.");
        if (interactPrompt != null) interactPrompt.SetActive(true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;
        playerInRange = false;
        if (interactPrompt != null) interactPrompt.SetActive(false);
    }

    private void Update()
    {
        if (!playerInRange) return;
        if (!Input.GetKeyDown(interactKey)) return;

        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogWarning($"{name}: targetSceneName이 비어있습니다.");
            return;
        }

        PendingSpawn.SpawnPointId = targetSpawnPointId;

        if (SceneTransition.Instance != null)
        {
            AsyncOperation op = SceneManager.LoadSceneAsync(targetSceneName);
            op.allowSceneActivation = false;
            // sceneIndex는 pendingOp가 있을 때는 안 쓰이니 아무 값(0)이나 넣어도 됨
            SceneTransition.Instance.LoadScene(0, null, op);
        }
        else
        {
            // SceneTransition이 씬에 없으면(테스트 중 등) 그냥 즉시 전환
            SceneManager.LoadScene(targetSceneName);
        }
    }
}