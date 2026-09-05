using System.Collections;
using Unity.Cinemachine;
using UnityEngine;


public class CameraDashEffect : MonoBehaviour
{
    public static CameraDashEffect Instance { get; private set; }

    [Header("평상시 값")]
    [SerializeField] private float normalDeadZoneWidth = 0f;
    [SerializeField] private float normalDampingX = 1f;

    [Header("대시 중")]
    [Tooltip("대시하는 동안 카메라 X축 데드존 너비 (0~2, 화면 비율)")]
    [SerializeField] private float dashDeadZoneWidth = 0.4f;

    [Header("대시 후 복귀")]
    [Tooltip("대시가 끝난 직후 잠깐 적용할 댐핑값 (클수록 천천히 부드럽게 따라감)")]
    [SerializeField] private float returnDampingX = 3f;
    [Tooltip("returnDampingX를 유지하는 시간(초). 이후 전부 평상시 값으로 리셋됨")]
    [SerializeField] private float returnDampingDuration = 0.35f;

    [Header("탑뷰 - 세로축")]
    [Tooltip("탑뷰에서는 위/아래로도 대시하므로 세로축에도 똑같은 연출을 적용한다.\n" +
             "값은 위의 가로축 설정을 그대로 재사용하므로 따로 맞출 필요가 없다.\n" +
             "사이드뷰에서는 점프 카메라와 싸우게 되므로 쓰지 않는다.")]
    [SerializeField] private bool applyToVerticalInTopView = true;

    private CinemachinePositionComposer composer;
    private Coroutine returnRoutine;

    // 세로축의 "평상시" 값은 인스펙터에 또 적지 않고, 씬에 세팅돼 있던 값을 그대로 기억해서 되돌린다.
    // 씬마다 탑뷰 카메라 세팅이 다를 수 있는데 여기에 하나로 박아두면 그걸 덮어써 버린다.
    private float authoredDeadZoneHeight;
    private float authoredDampingY;

    // 세로축을 실제로 건드렸는지. 건드리지도 않았는데 되돌리면 씬 세팅이 망가진다.
    private bool verticalTouched;

    /// <summary>지금 세로축까지 연출할 상황인지. (탑뷰일 때만)</summary>
    private bool UseVertical =>
        applyToVerticalInTopView
        && GameModeManager.Instance != null
        && GameModeManager.Instance.IsTopView;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>씬 진입 시 SceneEntryManager가 호출해서 현재 씬의 카메라 컴포저를 등록한다.</summary>
    public void SetComposer(CinemachinePositionComposer newComposer)
    {
        // 씬이 바뀌는 도중이었다면 이전 코루틴은 정리
        if (returnRoutine != null)
        {
            StopCoroutine(returnRoutine);
            returnRoutine = null;
        }

        composer = newComposer;
        verticalTouched = false;

        // 이 씬 카메라에 세팅돼 있던 세로축 값을 원본으로 기억해둔다.
        if (composer != null)
        {
            authoredDeadZoneHeight = composer.Composition.DeadZone.Size.y;
            authoredDampingY = composer.Damping.y;
        }

        ResetToNormal();
    }

    /// <summary>PlayerController.StartDodge()에서 호출.</summary>
    public void OnDashStart()
    {
        if (composer == null) return;

        if (returnRoutine != null)
        {
            StopCoroutine(returnRoutine);
            returnRoutine = null;
        }

        // OnDashStart()
        var dz = composer.Composition.DeadZone;
        dz.Size.x = dashDeadZoneWidth; // dz.x → dz.Size.x

        var damping = composer.Damping;
        damping.x = normalDampingX; // 대시 중엔 평상시 댐핑 유지 (데드존만 넓힘)

        // 탑뷰는 위아래로도 대시하므로 세로축에도 똑같이 걸어준다.
        if (UseVertical)
        {
            dz.Size.y = dashDeadZoneWidth;  // 가로축과 같은 값
            damping.y = normalDampingX;
            verticalTouched = true;
        }

        composer.Composition.DeadZone = dz;
        composer.Damping = damping;
    }

    /// <summary>PlayerController.EndDodge()에서 호출.</summary>
    public void OnDashEnd()
    {
        if (composer == null) return;

        if (returnRoutine != null)
            StopCoroutine(returnRoutine);

        returnRoutine = StartCoroutine(ReturnRoutine());
    }

    private IEnumerator ReturnRoutine()
    {
        var startDeadZoneWidth = composer.Composition.DeadZone.Size.x;
        var startDeadZoneHeight = composer.Composition.DeadZone.Size.y;
        bool vertical = verticalTouched;

        var initialDamping = composer.Damping;
        initialDamping.x = returnDampingX;
        if (vertical) initialDamping.y = returnDampingX;
        composer.Damping = initialDamping;

        float elapsed = 0f;

        while (elapsed < returnDampingDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / returnDampingDuration);

            // 시작과 끝이 부드럽게 이어지도록 보간
            t = Mathf.SmoothStep(0f, 1f, t);

            var dz = composer.Composition.DeadZone;
            dz.Size.x = Mathf.Lerp(
                startDeadZoneWidth,
                normalDeadZoneWidth,
                t
            );

            var damping = composer.Damping;
            damping.x = Mathf.Lerp(
                returnDampingX,
                normalDampingX,
                t
            );

            if (vertical)
            {
                dz.Size.y = Mathf.Lerp(startDeadZoneHeight, authoredDeadZoneHeight, t);
                damping.y = Mathf.Lerp(returnDampingX, authoredDampingY, t);
            }

            composer.Composition.DeadZone = dz;
            composer.Damping = damping;

            yield return null;
        }

        ResetToNormal();
        returnRoutine = null;
    }

    private void ResetToNormal()
    {
        if (composer == null) return;

        // ResetToNormal()
        var dz = composer.Composition.DeadZone;
        dz.Size.x = normalDeadZoneWidth; // dz.x → dz.Size.x

        var damping = composer.Damping;
        damping.x = normalDampingX;

        // 세로축은 우리가 건드린 경우에만 되돌린다. 그것도 씬에 원래 있던 값으로.
        if (verticalTouched)
        {
            dz.Size.y = authoredDeadZoneHeight;
            damping.y = authoredDampingY;
            verticalTouched = false;
        }

        composer.Composition.DeadZone = dz;
        composer.Damping = damping;
    }
}