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

    private CinemachinePositionComposer composer;
    private Coroutine returnRoutine;

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
        composer.Composition.DeadZone = dz;

        var damping = composer.Damping;
        damping.x = normalDampingX; // 대시 중엔 평상시 댐핑 유지 (데드존만 넓힘)
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

        var initialDamping = composer.Damping;
        initialDamping.x = returnDampingX;
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
            composer.Composition.DeadZone = dz;

            var damping = composer.Damping;
            damping.x = Mathf.Lerp(
                returnDampingX,
                normalDampingX,
                t
            );
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
        composer.Composition.DeadZone = dz;

        var damping = composer.Damping;
        damping.x = normalDampingX;
        composer.Damping = damping;
    }
}