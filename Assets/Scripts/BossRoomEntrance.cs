using UnityEngine;

/// <summary>
/// 사이드뷰 → 탑뷰(보스방) 전환용 트리거.
/// 보스방 "문" 오브젝트에 붙이고, Collider2D의 Is Trigger를 켠다.
///
/// 사용법:
///  1) 사이드 레벨의 문 위치에 빈 오브젝트 생성 + BoxCollider2D(Is Trigger) + 이 스크립트
///  2) topViewSpawnPoint: 보스방(탑뷰) 안에서 플레이어가 등장할 위치의 Transform
///  3) direction을 EnterBossRoom으로 설정
///
/// 보스방 안쪽 출구에는 같은 스크립트를 direction = ExitBossRoom으로 설정해서
/// sideViewSpawnPoint로 되돌아가게 한다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class BossRoomEntrance : MonoBehaviour
{
    public enum Direction { EnterBossRoom, ExitBossRoom }

    [SerializeField] private Direction direction = Direction.EnterBossRoom;

    [Tooltip("전환 후 플레이어가 이동할 위치")]
    [SerializeField] private Transform destinationSpawnPoint;

    [Tooltip("트리거로 인식할 태그")]
    [SerializeField] private string playerTag = "Player";

    [Header("연출 (선택)")]
    [SerializeField] private float transitionDelay = 0.15f; // 화면 전환 연출 대기 시간
    [SerializeField] private ScreenFader screenFader; // 선택: 페이드 연출용, 없으면 즉시 전환

    private bool isTransitioning;

    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isTransitioning) return;
        if (!other.CompareTag(playerTag)) return;
        if (destinationSpawnPoint == null)
        {
            Debug.LogWarning($"{name}: destinationSpawnPoint가 설정되지 않았습니다.");
            return;
        }

        var player = other.GetComponent<PlayerController>();
        StartCoroutine(DoTransition(other.transform, player));
    }

    private System.Collections.IEnumerator DoTransition(Transform playerTransform, PlayerController player)
    {
        isTransitioning = true;

        if (screenFader != null)
            yield return screenFader.FadeOut(transitionDelay);
        else
            yield return new WaitForSeconds(transitionDelay);

        // 모드 전환
        var targetMode = direction == Direction.EnterBossRoom
            ? GameModeManager.GameMode.TopView
            : GameModeManager.GameMode.SideView;
        GameModeManager.Instance.SetMode(targetMode);

        // 위치 이동
        playerTransform.position = destinationSpawnPoint.position;
        player?.ResetPhysicsState();

        if (screenFader != null)
            yield return screenFader.FadeIn(transitionDelay);

        isTransitioning = false;
    }
}
