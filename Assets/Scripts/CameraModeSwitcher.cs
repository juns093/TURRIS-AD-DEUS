using UnityEngine;

/// <summary>
/// 씬에 사이드뷰용 카메라와 탑뷰용 카메라를 각각 두고,
/// GameModeManager의 모드 변경 이벤트에 맞춰 하나만 활성화한다.
///
/// Cinemachine을 쓴다면 이 스크립트 대신 각 카메라의 Priority를 조절하는 방식으로
/// 바꿔도 되고, 구조(트리거 → 이벤트 → 전환)는 동일하게 가져가면 된다.
/// </summary>
public class CameraModeSwitcher : MonoBehaviour
{
    [SerializeField] private Camera sideViewCamera;
    [SerializeField] private Camera topViewCamera;

    [Tooltip("사이드뷰 카메라가 플레이어를 따라갈 때 쓸 타겟(보통 Player)")]
    [SerializeField] private Transform followTarget;

    [Tooltip("사이드뷰 카메라 오프셋")]
    [SerializeField] private Vector3 sideOffset = new Vector3(0f, 1.5f, -10f);

    [Tooltip("탑뷰 카메라 오프셋 (보통 방 중앙 고정 or 플레이어 위)")]
    [SerializeField] private Vector3 topOffset = new Vector3(0f, 0f, -10f);
    [SerializeField] private bool topCameraFollowsPlayer = false;

    private void OnEnable()
    {
        if (GameModeManager.Instance != null)
        {
            GameModeManager.Instance.OnModeChanged += HandleModeChanged;
            ApplyMode(GameModeManager.Instance.CurrentMode);
        }
    }

    private void OnDisable()
    {
        if (GameModeManager.Instance != null)
            GameModeManager.Instance.OnModeChanged -= HandleModeChanged;
    }

    private void HandleModeChanged(GameModeManager.GameMode prev, GameModeManager.GameMode next)
    {
        ApplyMode(next);
    }

    private void ApplyMode(GameModeManager.GameMode mode)
    {
        bool isSide = mode == GameModeManager.GameMode.SideView;
        if (sideViewCamera != null) sideViewCamera.gameObject.SetActive(isSide);
        if (topViewCamera != null) topViewCamera.gameObject.SetActive(!isSide);
    }

    private void LateUpdate()
    {
        if (followTarget == null) return;

        if (sideViewCamera != null && sideViewCamera.gameObject.activeSelf)
        {
            sideViewCamera.transform.position = new Vector3(
                followTarget.position.x + sideOffset.x,
                followTarget.position.y + sideOffset.y,
                sideOffset.z);
        }

        if (topViewCamera != null && topViewCamera.gameObject.activeSelf && topCameraFollowsPlayer)
        {
            topViewCamera.transform.position = new Vector3(
                followTarget.position.x + topOffset.x,
                followTarget.position.y + topOffset.y,
                topOffset.z);
        }
    }
}
