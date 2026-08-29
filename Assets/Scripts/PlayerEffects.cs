using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 상태(달리기 / 러쉬)에 맞춰 EffectImage 를 뿌려주는 스크립트.
///
/// 역할 분담
///  - 언제 만들지  : 이 스크립트 (달리기 중 runDustInterval 간격 / 러쉬 시작 순간)
///  - 얼마나 살지  : EffectImage (자기 lifetime 이 끝나면 스스로 종료 + OnFinished 통보)
///  - 어디에 보관 : 아래 Pool (Destroy 대신 꺼뒀다가 재사용, 동시 개수 상한도 여기서 관리)
///
/// PlayerController 는 상태만 읽어가므로, 이 스크립트를 빼도 게임은 그대로 돌아간다.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerEffects : MonoBehaviour
{
    [Header("이펙트 프리팹 (EffectImage 가 붙은 프리팹)")]
    [Tooltip("달릴 때 발밑에 일정 간격으로 뜨는 이미지. 비워두면 생략됨.")]
    [SerializeField] private EffectImage runDustPrefab;

    [Tooltip("러쉬(회피) 시작 순간에 1회 뜨는 이미지. 비워두면 생략됨.")]
    [SerializeField] private EffectImage dashPrefab;

    [Header("스폰 위치")]
    [Tooltip("발밑 기준점. PlayerController 의 GroundCheck 를 그대로 끌어다 써도 된다. 비우면 플레이어 원점.")]
    [SerializeField] private Transform footPoint;

    [Tooltip("발밑 기준점에서 얼마나 밀어낼지. x는 바라보는 방향 기준이라 보통 음수(뒤쪽).")]
    [SerializeField] private Vector2 runDustOffset = new Vector2(-0.15f, 0.02f);

    [Tooltip("러쉬 이펙트 오프셋. 몸 중심에 맞추려면 y를 올린다.")]
    [SerializeField] private Vector2 dashOffset = new Vector2(0f, 0.5f);

    [Tooltip("스폰 위치를 이만큼 랜덤하게 흔든다. 같은 이미지가 일렬로 찍히는 걸 막아준다.")]
    [SerializeField] private float positionJitter = 0.04f;

    [Header("발 싱크 (달리기 애니메이션에 맞춰 생성)")]
    [Tooltip("켜면 고정 간격 대신 달리기 애니메이션의 발 착지 프레임에 맞춰 먼지를 만든다.\n애니메이션 속도가 바뀌어도 자동으로 따라간다.")]
    [SerializeField] private bool syncToAnimation = true;

    [Tooltip("비워두면 플레이어(또는 자식)에서 자동으로 찾는다.")]
    [SerializeField] private Animator animator;

    [Tooltip("달리기 애니메이션 스테이트 이름. Animator 창에서 보이는 이름 그대로.")]
    [SerializeField] private string runStateName = "PlayerRun";

    [SerializeField] private int animatorLayer = 0;

    [Tooltip("한 사이클(0~1) 안에서 발이 땅에 닿는 지점. 다리가 2개니까 기본 2개.\n" +
             "PlayerRun 클립은 10프레임이므로 '착지 프레임 번호 / 10' 이 그 값이다. (예: 2번, 7번 프레임 -> 0.2, 0.7)")]
    [SerializeField] private float[] footfallPhases = new float[] { 0f, 0.5f };

    [Tooltip("두 번째 발(반대쪽 다리)에만 더해줄 위치 보정. 두 발이 완전히 같은 자리에 찍히는 걸 막는다.")]
    [SerializeField] private Vector2 secondFootOffset = new Vector2(0.06f, 0f);

    [Tooltip("켜면 발이 닿을 때마다 Console에 로그를 찍는다. footfallPhases 값을 맞출 때만 잠깐 사용.")]
    [SerializeField] private bool logFootfall = false;

    [Header("생성 타이밍 (싱크를 못 쓸 때의 폴백)")]
    [Tooltip("Animator가 없거나 달리기 스테이트가 아닐 때 쓰는 고정 간격(초).")]
    [SerializeField] private float runDustInterval = 0.16f;

    [Tooltip("켜면 사이드뷰에서 바닥에 닿아있을 때만 만든다. 탑뷰에서는 이 옵션과 무관하게 항상 만든다.")]
    [SerializeField] private bool runDustNeedsGrounded = true;

    [Tooltip("동시에 떠 있을 수 있는 먼지 최대 개수. 넘으면 새로 만들지 않고 건너뛴다.")]
    [SerializeField] private int maxActiveRunDust = 12;

    [Header("그리기 순서")]
    [Tooltip("플레이어 SpriteRenderer 기준 오프셋. 음수면 플레이어 뒤에 그려진다.")]
    [SerializeField] private int sortingOrderOffset = -1;

    [Header("풀")]
    [Tooltip("시작할 때 미리 만들어둘 개수. 게임 중 모자라면 자동으로 더 만든다.")]
    [SerializeField] private int prewarmCount = 6;

    private PlayerController pc;
    private SpriteRenderer playerSprite;
    private Transform poolRoot;

    private Pool runDustPool;
    private Pool dashPool;

    private float dustTimer;
    private float prevNormalizedTime;
    private bool hasPrevNormalizedTime;

    private void Awake()
    {
        pc = GetComponent<PlayerController>();
        playerSprite = GetComponent<SpriteRenderer>();
        if (playerSprite == null) playerSprite = GetComponentInChildren<SpriteRenderer>();

        if (animator == null) animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    // 풀 생성은 Start 에서.
    // PersistentPlayer 가 Awake 에서 중복 플레이어를 Destroy 하기 때문에,
    // Awake 에 만들면 곧 사라질 복제본이 만든 풀이 씬에 남아버린다.
    private void Start()
    {
        poolRoot = new GameObject("PlayerEffectPool").transform;

        // 플레이어가 씬을 넘어가며 살아남는 구조이므로 풀도 같이 살려둔다.
        if (GetComponentInParent<PersistentPlayer>() != null)
            DontDestroyOnLoad(poolRoot.gameObject);

        runDustPool = new Pool(runDustPrefab, poolRoot, prewarmCount);
        dashPool = new Pool(dashPrefab, poolRoot, 2);
    }

    private void OnEnable()
    {
        if (pc != null) pc.OnDodgeStart += PlayDashEffect;
    }

    private void OnDisable()
    {
        if (pc != null) pc.OnDodgeStart -= PlayDashEffect;
    }

    private void Update()
    {
        HandleRunDust();
    }

    // ---------------- 달리기 먼지 ----------------
    private void HandleRunDust()
    {
        if (runDustPool == null || !runDustPool.IsValid || pc == null) return;

        if (!ShouldEmitRunDust())
        {
            dustTimer = 0f;                 // 멈췄다가 다시 달리면 바로 첫 먼지가 나오도록
            hasPrevNormalizedTime = false;  // 싱크 추적도 리셋
            return;
        }

        if (TryAnimationSync()) return;     // 애니메이션 싱크가 처리했으면 타이머는 안 쓴다

        // --- 폴백: 고정 간격 ---
        dustTimer -= Time.deltaTime;
        if (dustTimer > 0f) return;
        dustTimer = Mathf.Max(0.01f, runDustInterval);

        SpawnFootDust(0);
    }

    /// <summary>
    /// 달리기 애니메이션의 재생 위치(normalizedTime)를 보고, 발이 땅에 닿는 지점을
    /// 지나칠 때마다 먼지를 만든다.
    ///
    /// normalizedTime 은 루프해도 계속 커지는 값(0 -> 1 -> 2 ...)이라,
    /// Floor(t - phase) 가 1 늘어난 순간이 곧 "그 발이 방금 땅에 닿았다"는 뜻이 된다.
    /// 프레임이 튀거나 애니메이션 속도가 바뀌어도 발자국을 빠뜨리지 않는다.
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
        if (!st.IsName(runStateName)) return false; // 달리기 스테이트가 아니면 폴백에 맡긴다

        float nt = st.normalizedTime;

        // 스테이트에 막 들어온 프레임은 기준점만 잡는다. (안 그러면 큰 점프로 오인해서 한 번에 여러 발이 찍힘)
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
                SpawnFootDust(i);

                if (logFootfall)
                    Debug.Log($"[PlayerEffects] {i}번 발 착지 / phase={phase:F2} normalizedTime={nt:F3}", this);
            }
        }

        prevNormalizedTime = nt;
        return true;
    }

    /// <param name="footIndex">몇 번째 발인지. 홀수면 반대쪽 다리로 보고 위치를 살짝 어긋나게 준다.</param>
    private void SpawnFootDust(int footIndex)
    {
        if (runDustPool.ActiveCount >= maxActiveRunDust) return;

        Vector2 offset = runDustOffset;
        if (footIndex % 2 == 1) offset += secondFootOffset;

        offset.x *= pc.Facing; // 진행 방향 반대쪽에 깔리도록

        Spawn(runDustPool, FootPosition() + offset);
    }

    private bool ShouldEmitRunDust()
    {
        // 달리는 중 + 실제로 움직이는 중 + 회피 중이 아닐 것
        if (!pc.IsRunningState || !pc.IsMoving || pc.IsDodging) return false;

        bool isSide = GameModeManager.Instance != null && GameModeManager.Instance.IsSideView;
        if (isSide && runDustNeedsGrounded && !pc.IsGrounded) return false;

        return true;
    }

    // ---------------- 러쉬(대시) ----------------
    private void PlayDashEffect()
    {
        if (dashPool == null || !dashPool.IsValid) return;

        Vector2 offset = dashOffset;
        offset.x *= pc.Facing;

        Spawn(dashPool, (Vector2)transform.position + offset);
    }

    // ---------------- 공통 ----------------
    private void Spawn(Pool pool, Vector2 position)
    {
        EffectImage fx = pool.Get();
        if (fx == null) return;

        if (positionJitter > 0f)
        {
            position.x += Random.Range(-positionJitter, positionJitter);
            position.y += Random.Range(-positionJitter, positionJitter);
        }

        if (playerSprite != null)
            fx.SetSorting(playerSprite.sortingLayerName, playerSprite.sortingOrder + sortingOrderOffset);

        fx.Spawn(position, pc.Facing < 0);
    }

    private Vector2 FootPosition()
    {
        return footPoint != null ? (Vector2)footPoint.position : (Vector2)transform.position;
    }

    // ---------------- 풀 ----------------
    /// <summary>
    /// EffectImage 인스턴스를 Destroy 하지 않고 껐다 켜며 재사용한다.
    /// 회수 시점은 EffectImage.OnFinished 가 알려주므로, 여기서 시간을 재지 않는다.
    /// </summary>
    private class Pool
    {
        private readonly EffectImage prefab;
        private readonly Transform root;
        private readonly Stack<EffectImage> idle = new Stack<EffectImage>();

        private int activeCount;

        /// <summary>지금 화면에 떠 있는 개수.</summary>
        public int ActiveCount => activeCount;

        /// <summary>프리팹이 연결돼 있는지. 비어있으면 이 풀은 아무것도 안 한다.</summary>
        public bool IsValid => prefab != null;

        public Pool(EffectImage prefab, Transform root, int prewarm)
        {
            this.prefab = prefab;
            this.root = root;

            if (prefab == null) return;

            for (int i = 0; i < Mathf.Max(0, prewarm); i++)
                idle.Push(Create());
        }

        public EffectImage Get()
        {
            if (prefab == null) return null;

            EffectImage fx = null;

            // 씬 전환 등으로 파괴된 슬롯은 건너뛴다.
            while (fx == null && idle.Count > 0)
                fx = idle.Pop();

            if (fx == null) fx = Create();

            activeCount++;
            return fx;
        }

        private EffectImage Create()
        {
            EffectImage fx = Object.Instantiate(prefab, root);
            fx.gameObject.SetActive(false);
            fx.OnFinished += Release; // 수명이 다하면 스스로 여기로 돌아온다
            return fx;
        }

        private void Release(EffectImage fx)
        {
            activeCount = Mathf.Max(0, activeCount - 1);
            if (fx != null) idle.Push(fx);
        }
    }
}
