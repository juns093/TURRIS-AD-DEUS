using UnityEngine;

/// <summary>
/// 플레이어가 씬에 도착했을 때 위치할 지점을 표시하는 마커.
/// 씬마다 필요한 만큼 빈 오브젝트에 붙이고 고유한 id를 부여한다.
/// (예: SideScene에는 "from_boss_exit", BossScene에는 "boss_entrance")
/// </summary>
public class PlayerSpawnPoint : MonoBehaviour
{
    [Tooltip("SceneTransitionDoor의 targetSpawnPointId와 일치해야 함")]
    [SerializeField] private string id;

    public string Id => id;

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.3f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 0.6f);
    }
}
