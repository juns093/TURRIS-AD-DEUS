using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 함정 앞에 깔아두는 튜토리얼 트리거.
/// 플레이어가 닿으면 시간을 느리게 늦추고 "대시로 피하세요" 안내를 띄운 뒤,
/// 실제로 대시를 쓰면 시간을 원래 속도로 돌려놓는다.
///
/// 튜토리얼 씬에서만 나오게 하는 법
///   이 스크립트가 붙은 오브젝트를 튜토리얼 씬에만 두면 된다.
///   씬에 속한 평범한 오브젝트라서 다른 씬으로 넘어가면 같이 사라진다.
///   (혹시 프리팹을 여기저기 복사해 둘 예정이면 아래 Restrict To Scene 에 씬 이름을 적어두면
///    그 씬이 아닐 때 스스로 꺼진다)
///
/// 붙이는 법
///   빈 게임오브젝트 + Collider2D (Is Trigger 체크) + 이 스크립트
///   함정보다 조금 앞에 놓아서, 플레이어가 함정에 닿기 전에 이 구역을 먼저 지나가게 한다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class TutorialDashPrompt : MonoBehaviour
{
    [Header("느려지는 정도")]
    [Tooltip("트리거에 닿았을 때의 시간 배속. 0.15 면 평소의 15% 속도로 흐른다.")]
    [Range(0.02f, 1f)] [SerializeField] private float slowTimeScale = 0.15f;

    [Tooltip("느려지고 다시 빨라질 때 걸리는 시간(초). 게임이 멈춰 있어도 흘러야 하므로 실제 시간 기준이다.\n" +
             "0으로 두면 뚝 끊기듯 즉시 바뀐다.")]
    [SerializeField] private float rampDuration = 0.12f;

    [Tooltip("느린 동안 물리 계산 간격도 같이 줄여서 움직임을 부드럽게 만든다. 보통 켜두면 된다.")]
    [SerializeField] private bool scaleFixedTimestep = true;

    [Header("안내 문구")]
    [Tooltip("안내를 담은 오브젝트. 평소엔 꺼두고, 트리거에 닿았을 때만 켜진다.")]
    [SerializeField] private GameObject promptRoot;

    [Tooltip("문구를 넣을 라벨. 레거시 Text 와 TextMeshPro 둘 다 받는다. 비워두면 문구는 건드리지 않는다.")]
    [SerializeField] private UILabel promptLabel = new UILabel();

    [TextArea(1, 3)]
    [SerializeField] private string promptMessage = "Q 를 눌러 대시로 피하세요!";

    [Header("동작")]
    [Tooltip("한 번만 작동시킬지. 끄면 다시 지나갈 때마다 또 느려진다.")]
    [SerializeField] private bool triggerOnce = true;

    [Tooltip("대시를 안 해도 이 시간(실제 초)이 지나면 스스로 원래 속도로 돌아간다.\n" +
             "대시가 쿨타임이라 못 쓰는 상황 등에서 느린 채로 갇히지 않게 하는 안전장치다. 0 이하면 사용 안 함.")]
    [SerializeField] private float maxSlowDuration = 6f;

    [Tooltip("비워두면 어느 씬에서든 작동한다. 씬 이름을 적으면 그 씬이 아닐 때 스스로 꺼진다.")]
    [SerializeField] private string restrictToScene = "";

    [Header("알림 (선택)")]
    [Tooltip("느려지기 시작할 때. 효과음이나 화면 연출을 연결한다.")]
    [SerializeField] private UnityEngine.Events.UnityEvent onSlowStarted;

    [Tooltip("대시로 빠져나와 원래 속도로 돌아왔을 때.")]
    [SerializeField] private UnityEngine.Events.UnityEvent onSlowEnded;

    private PlayerController tracked;
    private Coroutine routine;
    private float defaultFixedDeltaTime;
    private bool slowing;   // 지금 이 트리거가 시간을 늦춰둔 상태인지
    private bool consumed;  // 한 번 쓰고 끝난 트리거인지

    private void Awake()
    {
        defaultFixedDeltaTime = Time.fixedDeltaTime;

        if (!string.IsNullOrEmpty(restrictToScene) &&
            SceneManager.GetActiveScene().name != restrictToScene)
        {
            enabled = false;
            return;
        }

        GetComponent<Collider2D>().isTrigger = true;
        HidePrompt();
    }

    /// <summary>
    /// 꺼지거나 파괴될 때는 무조건 시간을 되돌린다.
    /// 이걸 빼먹으면 느린 상태에서 씬을 옮겼을 때 게임 전체가 느린 채로 남는다.
    /// </summary>
    private void OnDisable()
    {
        if (!slowing) return;

        Unsubscribe();
        RestoreTimeNow();
        HidePrompt();
        slowing = false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (slowing) return;
        if (consumed && triggerOnce) return;

        // 플레이어 콜라이더가 자식에 붙어 있을 수도 있어서 부모까지 훑는다.
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null) return;

        Begin(player);
    }

    private void Begin(PlayerController player)
    {
        tracked = player;
        slowing = true;
        consumed = true;

        tracked.OnDodgeStart += HandleDash;

        ShowPrompt();
        onSlowStarted?.Invoke();

        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(SlowRoutine());
    }

    /// <summary>대시를 쓰는 순간 PlayerController 가 불러준다.</summary>
    private void HandleDash()
    {
        if (!slowing) return;
        End();
    }

    private void End()
    {
        Unsubscribe();
        slowing = false;

        HidePrompt();

        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(RampRoutine(Time.timeScale, 1f, true));
    }

    private void Unsubscribe()
    {
        if (tracked == null) return;
        tracked.OnDodgeStart -= HandleDash;
        tracked = null;
    }

    /// <summary>느리게 만든 뒤, 대시가 없더라도 안전시간이 지나면 스스로 풀어준다.</summary>
    private IEnumerator SlowRoutine()
    {
        yield return RampRoutine(Time.timeScale, slowTimeScale, false);

        if (maxSlowDuration <= 0f) yield break;

        float waited = 0f;
        while (slowing && waited < maxSlowDuration)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!slowing) yield break; // 그 사이에 대시로 이미 풀렸다

        Unsubscribe();
        slowing = false;
        HidePrompt();
        yield return RampRoutine(Time.timeScale, 1f, true);
    }

    /// <summary>
    /// 시간 배속을 from 에서 to 로 부드럽게 옮긴다.
    /// 시간이 느려진 상태에서도 연출 자체는 일정하게 흘러야 하므로 unscaled 시간을 쓴다.
    /// </summary>
    private IEnumerator RampRoutine(float from, float to, bool isRestoring)
    {
        // 인벤토리가 열려 있으면 게임을 멈춰둔 주인이 따로 있다.
        // 그 위에 우리가 배속을 덮어쓰면 인벤토리를 닫을 때 엉뚱한 값으로 되돌아가므로 기다린다.
        while (IsPausedByInventory()) yield return null;

        if (rampDuration > 0f)
        {
            float t = 0f;
            while (t < rampDuration)
            {
                t += Time.unscaledDeltaTime;
                ApplyTimeScale(Mathf.Lerp(from, to, t / rampDuration));
                yield return null;
            }
        }

        ApplyTimeScale(to);
        routine = null;

        if (isRestoring) onSlowEnded?.Invoke();
    }

    private static bool IsPausedByInventory()
    {
        return InventoryUI.Instance != null && InventoryUI.Instance.IsOpen;
    }

    private void ApplyTimeScale(float value)
    {
        Time.timeScale = value;

        if (scaleFixedTimestep)
            Time.fixedDeltaTime = defaultFixedDeltaTime * Mathf.Max(0.01f, value);
    }

    /// <summary>코루틴을 기다리지 않고 즉시 원래 속도로. 오브젝트가 꺼질 때 쓴다.</summary>
    private void RestoreTimeNow()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        // 인벤토리가 멈춰둔 상태라면 배속은 인벤토리가 알아서 되돌린다. 물리 간격만 원복한다.
        if (!IsPausedByInventory()) Time.timeScale = 1f;
        Time.fixedDeltaTime = defaultFixedDeltaTime;
    }

    private void ShowPrompt()
    {
        if (promptRoot != null) promptRoot.SetActive(true);
        if (promptLabel.IsAssigned) promptLabel.Set(promptMessage);
    }

    private void HidePrompt()
    {
        if (promptRoot != null) promptRoot.SetActive(false);
    }

    private void OnDrawGizmos()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col == null) return;

        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.35f);
        Bounds b = col.bounds;
        Gizmos.DrawCube(b.center, b.size);
    }
}
