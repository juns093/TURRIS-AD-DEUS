using UnityEngine;

/// <summary>
/// 플레이어가 움직일 때 발소리를 재생하는 스크립트.
///
/// 재생 타이밍은 두 가지 방식을 쓴다.
///  1) 애니메이션 싱크 : 걷기/달리기 애니메이션의 "발이 닿는 지점"을 지날 때마다 1회.
///                       애니메이션 속도가 바뀌어도 소리가 자동으로 따라간다.
///  2) 폴백 고정 간격  : Animator 가 없거나 해당 스테이트가 아닐 때 일정 간격으로.
///
/// PlayerEffects(달리기 먼지)와 같은 판정 방식을 쓰므로 먼지와 발소리가 같은 프레임에 나온다.
/// 클립을 하나도 넣지 않으면 아무 것도 하지 않는다. (에러/경고 없음)
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerFootsteps : MonoBehaviour
{
    [Header("사운드 출력")]
    [Tooltip("발소리를 낼 AudioSource. 비워두면 이 오브젝트에서 찾고, 없으면 자동으로 하나 만든다.\n" +
             "회피 사운드와 섞이는 게 싫으면 전용 AudioSource 를 따로 만들어 연결할 것.")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("걷기 발소리. 여러 개를 넣으면 매번 랜덤으로 하나 고른다. (같은 소리가 연속으로 나오지 않게 처리됨)")]
    [SerializeField] private AudioClip[] walkClips;

    [Tooltip("달리기 발소리. 비워두면 걷기 클립을 그대로 쓴다.")]
    [SerializeField] private AudioClip[] runClips;

    [Header("볼륨 / 피치")]
    [Range(0f, 1f)] [SerializeField] private float walkVolume = 0.5f;
    [Range(0f, 1f)] [SerializeField] private float runVolume = 0.8f;

    [Tooltip("한 걸음마다 피치를 이 범위 안에서 랜덤하게 흔든다. 같은 소리가 반복되는 느낌을 줄여준다.")]
    [SerializeField] private Vector2 pitchRange = new Vector2(0.94f, 1.06f);

    [Header("재생 조건")]
    [Tooltip("켜면 사이드뷰에서 바닥에 닿아있을 때만 소리가 난다. 탑뷰에서는 이 옵션과 무관하게 항상 난다.")]
    [SerializeField] private bool needsGrounded = true;

    [Tooltip("켜면 회피(러쉬) 중에는 발소리를 내지 않는다.")]
    [SerializeField] private bool muteWhileDodging = true;

    [Tooltip("켜면 벽에 매달려 있는 동안 발소리를 내지 않는다.")]
    [SerializeField] private bool muteWhileWallCling = true;

    [Header("발 싱크 (애니메이션에 맞춰 재생)")]
    [Tooltip("켜면 고정 간격 대신 걷기/달리기 애니메이션의 발 착지 프레임에 맞춰 재생한다.")]
    [SerializeField] private bool syncToAnimation = true;

    [Tooltip("비워두면 플레이어(또는 자식)에서 자동으로 찾는다.")]
    [SerializeField] private Animator animator;

    [Tooltip("걷기 애니메이션 스테이트 이름. Animator 창에서 보이는 이름 그대로.")]
    [SerializeField] private string walkStateName = "PlayerWalking";

    [Tooltip("달리기 애니메이션 스테이트 이름.")]
    [SerializeField] private string runStateName = "PlayerRun";

    [SerializeField] private int animatorLayer = 0;

    [Tooltip("한 사이클(0~1) 안에서 발이 땅에 닿는 지점. 다리가 2개니까 기본 2개.\n" +
             "착지 프레임 번호 / 전체 프레임 수 가 그 값이다. (예: 10프레임 클립의 2번, 7번 프레임 -> 0.2, 0.7)")]
    [SerializeField] private float[] footfallPhases = new float[] { 0f, 0.5f };

    [Tooltip("켜면 발이 닿을 때마다 Console에 로그를 찍는다. footfallPhases 값을 맞출 때만 잠깐 사용.")]
    [SerializeField] private bool logFootstep = false;

    [Header("재생 간격 (싱크를 못 쓸 때의 폴백)")]
    [Tooltip("Animator가 없거나 걷기 스테이트가 아닐 때 쓰는 걷기 간격(초).")]
    [SerializeField] private float walkInterval = 0.42f;

    [Tooltip("Animator가 없거나 달리기 스테이트가 아닐 때 쓰는 달리기 간격(초).")]
    [SerializeField] private float runInterval = 0.26f;

    [Tooltip("어떤 경우에도 이 간격보다 빨리는 재생하지 않는다. 소리가 드르륵 겹치는 걸 막는 안전장치.")]
    [SerializeField] private float minStepInterval = 0.08f;

    private PlayerController pc;

    private float stepTimer;
    private float lastStepTime = -999f;
    private int lastClipIndex = -1;

    private float prevNormalizedTime;
    private bool hasPrevNormalizedTime;

    private void Awake()
    {
        pc = GetComponent<PlayerController>();

        if (animator == null) animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D. 거리 감쇠를 쓰고 싶으면 직접 AudioSource를 만들어 연결할 것
        }
    }

    private void Update()
    {
        if (pc == null || audioSource == null) return;

        if (!ShouldStep())
        {
            stepTimer = 0f;                 // 멈췄다가 다시 걸으면 바로 첫 발소리가 나오도록
            hasPrevNormalizedTime = false;  // 싱크 추적도 리셋
            return;
        }

        if (TryAnimationSync()) return;     // 애니메이션 싱크가 처리했으면 타이머는 안 쓴다

        // --- 폴백: 고정 간격 ---
        stepTimer -= Time.deltaTime;
        if (stepTimer > 0f) return;

        bool running = pc.IsRunningState;
        stepTimer = Mathf.Max(0.01f, running ? runInterval : walkInterval);

        PlayStep(running);
    }

    /// <summary>지금 발소리가 나야 하는 상황인지.</summary>
    private bool ShouldStep()
    {
        if (!pc.IsMoving) return false;
        if (muteWhileDodging && pc.IsDodging) return false;
        if (muteWhileWallCling && pc.IsWallClinging) return false;

        bool isSide = GameModeManager.Instance != null && GameModeManager.Instance.IsSideView;
        if (isSide && needsGrounded && !pc.IsGrounded) return false;

        return true;
    }

    /// <summary>
    /// 걷기/달리기 애니메이션의 재생 위치(normalizedTime)를 보고,
    /// 발이 땅에 닿는 지점을 지나칠 때마다 소리를 낸다.
    ///
    /// normalizedTime 은 루프해도 계속 커지는 값(0 -> 1 -> 2 ...)이라,
    /// Floor(t - phase) 가 1 늘어난 순간이 곧 "그 발이 방금 땅에 닿았다"는 뜻이 된다.
    /// 프레임이 튀거나 애니메이션 속도가 바뀌어도 발소리를 빠뜨리지 않는다.
    /// </summary>
    /// <returns>애니메이션 싱크로 처리했으면 true (폴백 타이머를 쓰지 말라는 신호)</returns>
    private bool TryAnimationSync()
    {
        if (!syncToAnimation) return false;
        if (animator == null || animator.runtimeAnimatorController == null) return false;
        if (footfallPhases == null || footfallPhases.Length == 0) return false;

        // 전환 중에는 재생 위치가 불안정하므로 이번 프레임은 그냥 쉰다.
        if (animator.IsInTransition(animatorLayer))
        {
            hasPrevNormalizedTime = false;
            return true;
        }

        AnimatorStateInfo st = animator.GetCurrentAnimatorStateInfo(animatorLayer);

        bool isRunState = !string.IsNullOrEmpty(runStateName) && st.IsName(runStateName);
        bool isWalkState = !string.IsNullOrEmpty(walkStateName) && st.IsName(walkStateName);
        if (!isRunState && !isWalkState) return false; // 걷기/달리기 스테이트가 아니면 폴백에 맡긴다

        float nt = st.normalizedTime;

        // 스테이트에 막 들어온 프레임은 기준점만 잡는다.
        // (안 그러면 큰 점프로 오인해서 발소리가 한 번에 여러 번 겹친다)
        if (!hasPrevNormalizedTime)
        {
            prevNormalizedTime = nt;
            hasPrevNormalizedTime = true;
            return true;
        }

        for (int i = 0; i < footfallPhases.Length; i++)
        {
            float phase = Mathf.Repeat(footfallPhases[i], 1f);

            if (Mathf.Floor(nt - phase) > Mathf.Floor(prevNormalizedTime - phase))
            {
                PlayStep(isRunState);

                if (logFootstep)
                    Debug.Log($"[PlayerFootsteps] {i}번 발 착지 / phase={phase:F2} normalizedTime={nt:F3}", this);
            }
        }

        prevNormalizedTime = nt;
        return true;
    }

    /// <param name="running">달리기 발소리로 재생할지. false 면 걷기.</param>
    private void PlayStep(bool running)
    {
        // 어느 경로로 들어왔든 너무 촘촘하게 겹치는 건 여기서 한 번 걸러준다.
        if (Time.time - lastStepTime < minStepInterval) return;

        AudioClip[] set = running && runClips != null && runClips.Length > 0 ? runClips : walkClips;
        AudioClip clip = PickClip(set);
        if (clip == null) return; // 클립을 안 넣었으면 조용히 아무 것도 안 한다

        audioSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
        audioSource.PlayOneShot(clip, running ? runVolume : walkVolume);

        lastStepTime = Time.time;
    }

    /// <summary>클립 배열에서 하나를 고른다. 직전에 쓴 것과 같은 게 연속으로 나오지 않게 한다.</summary>
    private AudioClip PickClip(AudioClip[] set)
    {
        if (set == null || set.Length == 0) return null;
        if (set.Length == 1) return set[0];

        int index = Random.Range(0, set.Length);
        if (index == lastClipIndex) index = (index + 1) % set.Length;

        lastClipIndex = index;
        return set[index];
    }
}
