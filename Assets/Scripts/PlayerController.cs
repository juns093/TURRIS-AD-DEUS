using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 이동을 담당.
/// GameModeManager.CurrentMode 값에 따라
///  - SideView: 좌우 이동 + 점프 + 중력
///  - TopView : 4/8방향 자유 이동, 중력 없음
/// 두 모드 모두 걷기/달리기/회피를 공유한다.
///
/// 필요 컴포넌트: Rigidbody2D, Collider2D (CapsuleCollider2D 권장)
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("공통 이동 속도")]
    [SerializeField] private float walkSpeed = 3.2f;
    [SerializeField] private float runSpeed = 6.0f;

    [Header("사이드뷰 전용 - 점프")]
    [SerializeField] private float jumpForce = 12f;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.15f;
    [SerializeField] private LayerMask groundLayer;

    [Header("회피 (양쪽 모드 공용)")]
    [SerializeField] private float dodgeSpeed = 14f;
    [SerializeField] private float dodgeDuration = 0.18f;
    [SerializeField] private float dodgeCooldown = 0.9f;

    [Tooltip("회피 진행도(0~1)에 따른 속도 배율. 기본값은 초반에 빠르게 튀어나갔다가 급격히 감속하는 커브.")]
    [SerializeField]
    private AnimationCurve dodgeSpeedCurve = new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(0.4f, 0.8f),
        new Keyframe(1f, 0.15f)
    );

    [Header("회피 - 공중 대시")]
    [Tooltip("켜면 대시하는 동안 중력을 끄고 수직 속도를 0으로 고정한다.\n" +
             "공중에서 대시하면 떨어지지 않고 그 높이를 유지한 채 쭉 나갔다가, 대시가 끝난 뒤에 떨어진다.")]
    [SerializeField] private bool dodgeIgnoresGravity = true;

    [Header("회피 - 시각 연출")]
    [Tooltip("무적 상태를 표현할 SpriteRenderer. 비워두면 생략됨.")]
    [SerializeField] private SpriteRenderer sprite;
    [SerializeField] private float dodgeAlpha = 0.5f;

    [Tooltip("회피 중에만 활성화할 TrailRenderer(잔상). 비워두면 생략됨.")]
    [SerializeField] private TrailRenderer dodgeTrail;

    [Tooltip("회피 방향으로 스프라이트를 기울일 각도(도). 0이면 기울임 없음. 사이드뷰에서만 적용.")]
    [SerializeField] private float dodgeTiltAngle = 12f;

    [Header("회피 - 사운드 / 파티클")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip dodgeSfx;
    [Tooltip("회피 시작 시 재생할 파티클(먼지 등). 비워두면 생략됨.")]
    [SerializeField] private ParticleSystem dodgeParticle;

    [Header("입력 키")]
    [SerializeField] private KeyCode runKey = KeyCode.LeftShift;
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;
    [SerializeField] private KeyCode dodgeKey = KeyCode.LeftControl;
    public bool Checking = true;

    [Header("자동 달리기 연출 (씬 시작용)")]
    [Tooltip("켜면 Shift 입력을 무시하고, 씬 시작 후 walkDurationBeforeRun초 동안은 무조건 걷기 -> 그 후엔 무조건 달리기로 고정된다.")]
    [SerializeField] private bool useAutoRunIntro = true;
    [SerializeField] private float walkDurationBeforeRun = 1.5f;

    // ---- 내부 상태 ----
    private Rigidbody2D rb;
    private bool isGrounded;
    private int facing = 1; // 1: 오른쪽, -1: 왼쪽

    private bool running;
    private bool moving; // 실제로 방향 입력이 들어와 움직이는 중인지 (이펙트 판정용)

    // ---- 벽 타기 ----
    private bool isWallClinging;
    private int wallDirection;        // 붙어있는 벽의 방향. 1 = 오른쪽
    private float wallClingTimer;
    private float wallContactTimer;   // 방향키 없이 벽에 붙어있던 시간
    private float wallJumpLockTimer;  // 이 시간 동안은 좌우 입력을 무시하고 다시 붙지도 않는다

    private bool isDodging;
    private float dodgeTimer;
    private float dodgeCooldownTimer;
    private Vector2 dodgeDirection;
    public bool IsInvulnerable { get; private set; } // 보스 공격 판정 등에서 참조

    // ---- 이펙트/사운드 등 외부 연출용 읽기 전용 상태 ----
    // PlayerEffects 같은 연출 스크립트가 이걸 읽어간다. 없어도 게임 로직에는 영향 없음.
    /// <summary>바라보는 방향. 1 = 오른쪽, -1 = 왼쪽.</summary>
    public int Facing => facing;
    /// <summary>지금 달리기 속도로 움직이는 중인지.</summary>
    public bool IsRunningState => running;
    /// <summary>방향 입력이 실제로 들어와서 이동 중인지. (제자리에서 달리기 판정만 켜진 경우 제외)</summary>
    public bool IsMoving => moving;
    /// <summary>사이드뷰에서 바닥에 닿아있는지. 탑뷰에서는 의미 없음.</summary>
    public bool IsGrounded => isGrounded;
    /// <summary>회피(러쉬) 중인지.</summary>
    public bool IsDodging => isDodging;
    /// <summary>회피(러쉬)가 시작되는 순간 1회 발생. 이펙트/사운드 트리거용.</summary>
    public event System.Action OnDodgeStart;

    /// <summary>벽에 매달려 있는지.</summary>
    public bool IsWallClinging => isWallClinging;
    /// <summary>붙어 있는 벽이 어느 쪽인지. 1 = 오른쪽 벽, -1 = 왼쪽 벽, 0 = 없음.</summary>
    public int WallDirection => isWallClinging ? wallDirection : 0;
    /// <summary>벽을 차고 나가는 순간 1회 발생.</summary>
    public event System.Action OnWallJump;

    // GetKeyDown은 반드시 Update에서 폴링하고, FixedUpdate에서는 이 플래그만 소비한다.
    // (FixedUpdate에서 직접 GetKeyDown을 읽으면 프레임당 0~여러 번 호출되면서
    //  입력이 씹히거나 중복 처리되어 "랜덤하게 점프되는" 현상이 생긴다.)
    private bool jumpRequested;

    // "바닥에 닿아있는 동안 1번만" 규칙: 점프하면 true로 잠그고,
    // 실제로 착지(false -> true 전이)하는 그 순간에만 다시 false로 풀어준다.
    // 키를 계속 누르고 있거나 연타해도 이 값이 true인 동안은 절대 점프 안 됨.
    private bool hasJumpedSinceGrounded;

    // 애니메이터를 쓴다면 여기 연결 (선택사항)
    [Header("벽 타기 (사이드뷰 전용)")]
    [Tooltip("끄면 벽 관련 기능이 전부 비활성화된다.")]
    [SerializeField] private bool enableWallCling = true;

    [Tooltip("벽으로 인식할 레이어. Ground 와 따로 'Wall' 레이어를 만들어서 지정할 것.")]
    [SerializeField] private LayerMask wallLayer;

    [Tooltip("벽 감지 기준점. 보통 몸통 중앙. 비워두면 플레이어 원점을 쓴다.")]
    [SerializeField] private Transform wallCheck;

    [Tooltip("기준점에서 좌우로 얼마나 떨어진 곳을 검사할지.")]
    [SerializeField] private float wallCheckDistance = 0.3f;
    [SerializeField] private float wallCheckRadius = 0.18f;

    [Tooltip("매달린 채 미끄러져 내려가는 속도. 0이면 완전히 붙어서 안 내려간다.")]
    [SerializeField] private float wallSlideSpeed = 1.6f;

    [Tooltip("붙어 있을 수 있는 최대 시간(초). 0 이하면 무제한.")]
    [SerializeField] private float maxWallClingTime = 0f;

    [Tooltip("켜면 벽 쪽으로 방향키를 눌렀을 때 '즉시' 붙는다. 끄면 닿는 순간 바로 붙는다.\n" +
             "어느 쪽이든 아래 접촉 시간이 차면 방향키 없이도 붙는다.")]
    [SerializeField] private bool requireInputTowardWall = true;

    [Tooltip("방향키를 누르지 않아도 벽에 이만큼(초) 계속 닿아 있으면 자동으로 매달린다. 0 이하면 사용 안 함.")]
    [SerializeField] private float wallContactTimeToCling = 0.5f;

    [Header("벽 점프")]
    [Tooltip("x = 벽 반대쪽으로 밀어내는 힘, y = 위로 뛰는 힘.")]
    [SerializeField] private Vector2 wallJumpForce = new Vector2(9f, 13f);

    [Tooltip("벽 점프/벽 대시 직후 좌우 입력을 무시하는 시간. 이게 없으면 방금 찬 벽에 곧바로 다시 붙는다.")]
    [SerializeField] private float wallJumpLockTime = 0.18f;

    [Header("벽 타기 - 애니메이터 파라미터 이름")]
    [Tooltip("매달려 있는 동안 true 로 유지할 Bool 파라미터.")]
    [SerializeField] private string wallClingBool = "isWallCling";

    [Tooltip("벽을 잡는 순간 한 번 쏘는 Trigger 파라미터.")]
    [SerializeField] private string wallGrabTrigger = "WallGrab";

    [Tooltip("벽을 차고 나가는 순간 한 번 쏘는 Trigger 파라미터.")]
    [SerializeField] private string wallJumpTrigger = "WallJump";

    [Header("좌우 반전 방식")]
    [Tooltip("SpriteFlipX 권장.\n" +
             "LocalScale 은 스케일 x를 음수로 뒤집는 방식이라, 백페이스 컬링(Render Face = Front)인 " +
             "머티리얼을 쓰면 왼쪽을 볼 때 캐릭터가 통째로 안 보인다.")]
    [SerializeField] private FlipMode flipMode = FlipMode.SpriteFlipX;

    [Header("옵션")]
    [SerializeField] private Animator animator;

    public enum FlipMode
    {
        SpriteFlipX,  // SpriteRenderer.flipX 로 뒤집기. 스케일을 건드리지 않아 안전하다.
        LocalScale    // transform.localScale.x 부호를 뒤집기. 자식 오브젝트도 같이 미러링된다.
    }

    private enum PlayerState { Idle, Walk, Run, Jump, Dodge, WallCling }
    private PlayerState state = PlayerState.Idle;

    private float sideViewGravityScale;

    bool isWalking = false;

    // 씬이 시작된 뒤 경과 시간. useAutoRunIntro가 켜져있을 때 걷기->달리기 전환 타이밍을 여기서 판단한다.
    private float sceneTimer;

    // 애니메이터에 실제로 존재하는 파라미터 이름들.
    // 없는 이름을 SetBool 하면 매 프레임 "Parameter 'X' does not exist" 경고가 쏟아진다.
    private HashSet<string> animatorParams;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        CacheAnimatorParameters();

        // 인스펙터에서 안 꽂아둔 경우가 많다. flipX 반전과 회피 중 알파 연출이 둘 다 이걸 쓴다.
        if (sprite == null) sprite = GetComponent<SpriteRenderer>();
        if (sprite == null) sprite = GetComponentInChildren<SpriteRenderer>();
        sideViewGravityScale = rb.gravityScale; // 인스펙터에 세팅해둔 사이드뷰용 중력값을 기억

        if (dodgeTrail != null)
            dodgeTrail.emitting = false; // 시작할 땐 꺼둔 상태로
    }

    private void OnEnable()
    {
        if (GameModeManager.Instance != null)
        {
            GameModeManager.Instance.OnModeChanged += HandleModeChanged;
            ApplyGravityForMode(GameModeManager.Instance.CurrentMode);
        }
    }

    private void OnDisable()
    {
        if (GameModeManager.Instance != null)
            GameModeManager.Instance.OnModeChanged -= HandleModeChanged;
    }

    private void HandleModeChanged(GameModeManager.GameMode prev, GameModeManager.GameMode next)
    {
        ApplyGravityForMode(next);
        ResetPhysicsState();
    }

    /// <summary>탑뷰에서는 중력을 꺼서 위/아래로도 자유롭게 움직이게 한다.</summary>
    private void ApplyGravityForMode(GameModeManager.GameMode mode)
    {
        rb.gravityScale = mode == GameModeManager.GameMode.SideView ? sideViewGravityScale : 0f;
        if (mode == GameModeManager.GameMode.TopView)
            rb.linearVelocity = Vector2.zero;
    }

    private void Update()
    {
        // 씬 시작 후 경과 시간 누적. 자동 달리기 연출(IsRunning)에서 사용.
        sceneTimer += Time.deltaTime;

        // 회피는 지속시간 동안 이동을 덮어쓰므로 타이머만 여기서 갱신,
        // 실제 이동 적용은 FixedUpdate에서.
        if (dodgeCooldownTimer > 0f) dodgeCooldownTimer -= Time.deltaTime;

        if (isDodging)
        {
            dodgeTimer -= Time.deltaTime;
            if (dodgeTimer <= 0f) EndDodge();
        }

        if (Input.GetKeyDown(dodgeKey) && !isDodging && dodgeCooldownTimer <= 0f)
        {
            StartDodge();
        }

        // 점프도 반드시 Update에서 폴링. 실제로 뛸 수 있는지는 FixedUpdate 쪽에서
        // hasJumpedSinceGrounded로 판단하므로 여기서는 그냥 "눌렀다"만 기록한다.
        if (Input.GetKeyDown(jumpKey))
        {
            jumpRequested = true;
        }

        UpdateAnimatorState();
    }

    private void FixedUpdate()
    {
        // 이번 물리 스텝에서만 유효한 값으로 즉시 빼내고 필드는 바로 비운다.
        // -> 회피 중이거나 탑뷰라서 이번 스텝에 안 쓰더라도 다음 스텝으로 절대 넘어가지 않는다.
        //    (이게 새서 나중에 엉뚱한 타이밍에 점프가 튀는 게 진짜 원인이었음)
        bool consumeJump = jumpRequested;
        jumpRequested = false;

        if (GameModeManager.Instance == null) return;

        bool isSide = GameModeManager.Instance.IsSideView;

        if (isDodging)
        {
            ApplyDodgeMotion(isSide);
            return; // 회피 중 점프 입력은 그냥 버림
        }

        if (isSide) HandleSideView(consumeJump);
        else HandleTopView(); // 탑뷰에서는 점프 자체가 없으므로 consumeJump는 버려짐
    }

    /// <summary>
    /// 지금 달리기 상태인지 판정.
    /// useAutoRunIntro가 켜져있으면 Shift 입력은 완전히 무시하고,
    /// 씬 시작 후 walkDurationBeforeRun초 전엔 무조건 false(걷기), 그 이후엔 무조건 true(달리기)로 고정한다.
    /// 꺼두면 기존처럼 Shift 수동 조작으로 돌아간다.
    /// </summary>
    private bool IsRunning()
    {
        if (useAutoRunIntro)
            return sceneTimer >= walkDurationBeforeRun;

        return Input.GetKey(runKey);
    }

    // ---------------- 사이드뷰 ----------------
    private void HandleSideView(bool jumpThisStep)
    {
        bool wasGrounded = isGrounded;
        isGrounded = groundCheck != null &&
            Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        // 공중에 있다가 방금 막 바닥에 닿은 "그 순간"에만 점프 잠금을 풀어준다.
        // 이 조건이 핵심: 키 입력과 무관하게 오직 착지 이벤트로만 재점프가 허용됨.
        if (isGrounded && !wasGrounded)
        {
            hasJumpedSinceGrounded = false;
        }

        if (wallJumpLockTimer > 0f) wallJumpLockTimer -= Time.fixedDeltaTime;

        float h = Input.GetAxisRaw("Horizontal"); // <-/-> 또는 A/D

        // 벽을 찬 직후에는 방향키를 통째로 무시한다.
        // (벽 쪽 키를 누른 채로 차는 게 보통이라, 안 막으면 튕겨나가자마자 도로 빨려들어간다)
        if (wallJumpLockTimer > 0f) h = 0f;

        running = IsRunning();
        float speed = running ? runSpeed : walkSpeed;

        // ---- 벽 타기 판정 ----
        UpdateWallCling(h);

        if (isWallClinging)
        {
            if (jumpThisStep)
            {
                DoWallJump();
            }
            else
            {
                // 붙어 있는 동안은 수평 이동을 막고 정해진 속도로만 미끄러진다.
                rb.linearVelocity = new Vector2(0f, -wallSlideSpeed);
                moving = false;
                state = PlayerState.WallCling;
            }

            FlipSprite();
            return;
        }

        if (h != 0) facing = (int)Mathf.Sign(h);
        moving = Mathf.Abs(h) > 0.01f;

        // 벽을 찬 직후에는 수평 속도를 그대로 살려둔다.
        // 여기서 입력으로 덮어쓰면 벽에서 튕겨나가는 힘이 즉시 사라진다.
        if (wallJumpLockTimer <= 0f)
            rb.linearVelocity = new Vector2(h * speed, rb.linearVelocity.y);

        if (jumpThisStep && isGrounded && !hasJumpedSinceGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            hasJumpedSinceGrounded = true; // 다시 착지하기 전까지는 절대 재점프 불가
            isGrounded = false; // 점프 적용 즉시 false로 만들어서 같은 프레임에 재감지되는 걸 방지
        }

        FlipSprite();

        state = !isGrounded ? PlayerState.Jump
               : h == 0 ? PlayerState.Idle
               : running ? PlayerState.Run
               : PlayerState.Walk;
    }

    /// <summary>
    /// 대시로 껐던 중력을 현재 모드에 맞는 값으로 되돌린다.
    /// ApplyGravityForMode 와 달리 속도는 건드리지 않는다. (대시 직후 속도를 죽이지 않기 위함)
    /// </summary>
    private void RestoreGravity()
    {
        bool isSide = GameModeManager.Instance == null || GameModeManager.Instance.IsSideView;
        rb.gravityScale = isSide ? sideViewGravityScale : 0f;
    }

    // ---------------- 벽 타기 ----------------
    /// <summary>바라보는 방향 우선으로 좌우에 벽이 있는지 검사. 1 = 오른쪽 벽, -1 = 왼쪽 벽, 0 = 없음.</summary>
    private int DetectWall()
    {
        if (!enableWallCling || wallLayer.value == 0) return 0;

        Vector2 origin = wallCheck != null ? (Vector2)wallCheck.position : (Vector2)transform.position;

        if (HasWallOn(origin, facing)) return facing;
        if (HasWallOn(origin, -facing)) return -facing;
        return 0;
    }

    private bool HasWallOn(Vector2 origin, int dir)
    {
        Vector2 point = origin + Vector2.right * (dir * wallCheckDistance);
        return Physics2D.OverlapCircle(point, wallCheckRadius, wallLayer);
    }

    private void UpdateWallCling(float h)
    {
        if (!enableWallCling || isDodging)
        {
            if (isWallClinging) EndWallCling();
            return;
        }

        int wall = DetectWall();
        int input = Mathf.RoundToInt(h);

        // 이미 붙어 있는 경우: 떨어질 조건을 본다.
        // 붙어 있는 동안은 방향키를 계속 누르고 있을 필요가 없다.
        // 벽 반대쪽 키를 눌러야 떨어진다.
        if (isWallClinging)
        {
            bool pushingAway = input == -wallDirection;
            bool timedOut = maxWallClingTime > 0f && wallClingTimer >= maxWallClingTime;

            if (isGrounded || wall != wallDirection || pushingAway || timedOut) EndWallCling();
            else wallClingTimer += Time.fixedDeltaTime;

            return;
        }

        // ---- 아직 안 붙은 경우 ----
        // 붙을 수 없는 상황이면 접촉 시간을 리셋한다.
        if (isGrounded || wall == 0 ||
            wallJumpLockTimer > 0f ||          // 방금 찬 벽에 곧바로 다시 붙지 않게
            rb.linearVelocity.y > 0.01f)       // 올라가는 중에는 안 붙는다
        {
            wallContactTimer = 0f;
            return;
        }

        wallContactTimer += Time.fixedDeltaTime;

        // 두 가지 경로로 붙는다.
        //  1) 벽 쪽으로 방향키를 누르고 있으면 즉시
        //  2) 방향키를 안 눌러도 벽에 wallContactTimeToCling 초 동안 계속 스치고 있으면 자동
        bool instant = !requireInputTowardWall || input == wall;
        bool byContact = wallContactTimeToCling > 0f && wallContactTimer >= wallContactTimeToCling;

        if (instant || byContact) StartWallCling(wall);
    }

    private void StartWallCling(int wall)
    {
        isWallClinging = true;
        wallDirection = wall;
        wallClingTimer = 0f;
        wallContactTimer = 0f;

        facing = wall;                    // 벽을 바라본다
        rb.linearVelocity = Vector2.zero; // 붙는 순간 낙하 속도를 끊어준다

        SetAnimTrigger(wallGrabTrigger);
    }

    private void EndWallCling()
    {
        isWallClinging = false;
        wallClingTimer = 0f;
        wallContactTimer = 0f;
    }

    private void DoWallJump()
    {
        int away = -wallDirection;
        EndWallCling();

        rb.linearVelocity = new Vector2(away * wallJumpForce.x, wallJumpForce.y);
        facing = away;

        wallJumpLockTimer = wallJumpLockTime;
        hasJumpedSinceGrounded = true;
        state = PlayerState.Jump;

        SetAnimTrigger(wallJumpTrigger);
        OnWallJump?.Invoke();
    }

    // ---------------- 탑뷰 (보스방) ----------------
    private void HandleTopView()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        running = IsRunning();
        float speed = running ? runSpeed : walkSpeed;

        Vector2 dir = new Vector2(h, v);
        if (dir.sqrMagnitude > 1f) dir.Normalize();

        rb.linearVelocity = dir * speed;
        moving = dir.sqrMagnitude > 0.01f;

        if (h != 0) facing = (int)Mathf.Sign(h);
        FlipSprite();

        state = dir.sqrMagnitude < 0.01f ? PlayerState.Idle
               : running ? PlayerState.Run
               : PlayerState.Walk;
    }

    // ---------------- 회피 ----------------
    private void StartDodge()
    {
        bool isSide = GameModeManager.Instance != null && GameModeManager.Instance.IsSideView;

        Vector2 inputDir;
        if (isSide)
        {
            // 사이드뷰: 좌우로만, 입력 없으면 바라보는 방향
            float h = Input.GetAxisRaw("Horizontal");

            if (isWallClinging)
            {
                // 벽에 붙어 있으면 벽을 바라보고 있으므로, 입력이 없을 때 그대로 대시하면 벽에 처박는다.
                // 입력이 없거나 벽 쪽을 누르고 있으면 벽 반대쪽으로 튀어나가게 한다.
                int away = -wallDirection;
                int dir = (h != 0 && Mathf.RoundToInt(h) != wallDirection) ? Mathf.RoundToInt(h) : away;

                inputDir = new Vector2(dir, 0f);
                facing = dir;

                EndWallCling();
                wallJumpLockTimer = wallJumpLockTime; // 대시 끝나고 곧바로 다시 붙지 않게
            }
            else
            {
                inputDir = new Vector2(h != 0 ? h : facing, 0f);
            }
        }
        else
        {
            // 탑뷰: 8방향, 입력 없으면 바라보는 방향
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            inputDir = new Vector2(h, v);
            if (inputDir.sqrMagnitude < 0.01f) inputDir = new Vector2(facing, 0f);
        }

        dodgeDirection = inputDir.normalized;
        isDodging = true;
        IsInvulnerable = true;
        dodgeTimer = dodgeDuration;
        dodgeCooldownTimer = dodgeCooldown;
        state = PlayerState.Dodge;

        // 공중 대시: 중력을 끄고 수직 속도를 지워서 높이를 유지한 채 날아가게 한다.
        // 대시가 끝나면 EndDodge 에서 원래 중력으로 돌려놓는다.
        if (dodgeIgnoresGravity)
        {
            rb.gravityScale = 0f;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        }

        // ── 시각/청각 연출 시작 ──
        if (sprite != null)
        {
            Color c = sprite.color;
            c.a = dodgeAlpha;
            sprite.color = c;
        }

        if (dodgeTrail != null)
            dodgeTrail.emitting = true;

     

        if (audioSource != null && dodgeSfx != null)
            audioSource.PlayOneShot(dodgeSfx);

        if (dodgeParticle != null)
            dodgeParticle.Play();

        CameraDashEffect.Instance?.OnDashStart();

        // 러쉬 이펙트 등 외부 연출에 알린다. (PlayerEffects 가 구독)
        OnDodgeStart?.Invoke();
    }

    private void ApplyDodgeMotion(bool isSide)
    {
        // dodgeTimer는 dodgeDuration에서 0으로 줄어드는 값이므로,
        // progress는 0(시작) -> 1(끝)로 흐르는 진행도.
        float progress = 1f - Mathf.Clamp01(dodgeTimer / dodgeDuration);
        float speedMul = dodgeSpeedCurve.Evaluate(progress);

        if (isSide)
        {
            // 중력을 끈 대시면 y를 0으로 눌러 높이를 유지하고, 아니면 낙하는 중력에 맡긴다.
            float verticalSpeed = dodgeIgnoresGravity ? 0f : rb.linearVelocity.y;
            rb.linearVelocity = new Vector2(dodgeDirection.x * dodgeSpeed * speedMul, verticalSpeed);
        }
        else
        {
            rb.linearVelocity = dodgeDirection * dodgeSpeed * speedMul;
        }
    }

    private void EndDodge()
    {
        isDodging = false;
        IsInvulnerable = false;

        RestoreGravity(); // 여기서부터 다시 떨어지기 시작한다

        // ── 시각 연출 원복 ──
        if (sprite != null)
        {
            Color c = sprite.color;
            c.a = 1f;
            sprite.color = c;
        }

        if (dodgeTrail != null)
            dodgeTrail.emitting = false;

        transform.rotation = Quaternion.identity;

        CameraDashEffect.Instance?.OnDashEnd();

    }

    // ---------------- 유틸 ----------------
    private void FlipSprite()
    {
        if (flipMode == FlipMode.SpriteFlipX)
        {
            // 스케일은 항상 양수로 되돌려 둔다.
            // 음수 스케일이 남아있으면 백페이스 컬링 머티리얼에서 캐릭터가 사라진다.
            Vector3 positive = transform.localScale;
            if (positive.x < 0f)
            {
                positive.x = Mathf.Abs(positive.x);
                transform.localScale = positive;
            }

            if (sprite != null) sprite.flipX = facing < 0;
            return;
        }

        Vector3 s = transform.localScale;
        s.x = Mathf.Abs(s.x) * facing;
        transform.localScale = s;
    }

    /// <summary>
    /// 애니메이터가 실제로 갖고 있는 파라미터 이름을 한 번만 훑어서 기억해둔다.
    /// 컨트롤러에 없는 파라미터는 아예 건드리지 않으므로, 어떤 컨트롤러를 물려도 경고가 나지 않는다.
    /// </summary>
    private void CacheAnimatorParameters()
    {
        animatorParams = new HashSet<string>();

        if (animator == null || animator.runtimeAnimatorController == null) return;

        foreach (AnimatorControllerParameter p in animator.parameters)
            animatorParams.Add(p.name);
    }

    private void SetAnimBool(string paramName, bool value)
    {
        if (animatorParams != null && animatorParams.Contains(paramName))
            animator.SetBool(paramName, value);
    }

    private void SetAnimInt(string paramName, int value)
    {
        if (animatorParams != null && animatorParams.Contains(paramName))
            animator.SetInteger(paramName, value);
    }

    private void SetAnimTrigger(string paramName)
    {
        if (animator == null || string.IsNullOrEmpty(paramName)) return;
        if (animatorParams != null && animatorParams.Contains(paramName))
            animator.SetTrigger(paramName);
    }

    private void UpdateAnimatorState()
    {
        if (animator == null) return;

        // 아래 이름들은 애니메이터에 있는 것만 반영된다. 없는 건 조용히 무시.
        SetAnimInt("State", (int)state);
        SetAnimBool("IsInvulnerable", IsInvulnerable);

        bool isSide = GameModeManager.Instance != null && GameModeManager.Instance.IsSideView;

        bool isWalkingSide = isSide && isGrounded && !isDodging && !running
            && Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0f;
        SetAnimBool("isWalking", isWalkingSide);

        // 사이드뷰에서 공중에 떠있으면(점프/낙하 포함) true
        bool isJumpingSide = isSide && !isGrounded && !isDodging;
        SetAnimBool("isJumping", isJumpingSide);

        bool isRunningSide = isSide && isGrounded && !isDodging && running
            && Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0f;
        SetAnimBool("isRunning", isRunningSide);

        bool isDodgingSide = isSide && isDodging;
        SetAnimBool("isDodging", isDodgingSide);

        SetAnimBool(wallClingBool, isSide && isWallClinging);
    }

    /// <summary>
    /// 모드 전환 직후 위치/속도를 리셋할 때 BossRoomEntrance에서 호출.
    /// </summary>
    public void ResetPhysicsState()
    {
        rb.linearVelocity = Vector2.zero;
        isDodging = false;
        IsInvulnerable = false;
        moving = false;
        RestoreGravity();
        isWallClinging = false;
        wallClingTimer = 0f;
        wallContactTimer = 0f;
        wallJumpLockTimer = 0f;
        dodgeCooldownTimer = 0f;
        jumpRequested = false;
        hasJumpedSinceGrounded = false;

        // 회피 연출이 리셋 시점에 걸려있던 상태로 남지 않도록 원복
        if (sprite != null)
        {
            Color c = sprite.color;
            c.a = 1f;
            sprite.color = c;
        }
        if (dodgeTrail != null)
            dodgeTrail.emitting = false;
        transform.rotation = Quaternion.identity;
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            // 초록 = 지금 바닥으로 인식 중, 빨강 = 공중으로 인식 중.
            // 점프 후에도 계속 초록이면 groundLayer나 groundCheck 위치/반지름 설정을 의심할 것.
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }

        if (!enableWallCling) return;

        // 벽 감지 범위. 노란색 = 지금 붙어있는 쪽.
        Vector3 origin = wallCheck != null ? wallCheck.position : transform.position;
        for (int dir = -1; dir <= 1; dir += 2)
        {
            Gizmos.color = (isWallClinging && wallDirection == dir) ? Color.yellow : Color.cyan;
            Gizmos.DrawWireSphere(origin + Vector3.right * (dir * wallCheckDistance), wallCheckRadius);
        }
    }
}