using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면에 체력 / 스테미나 / 회피 쿨타임을 띄우는 HUD.
/// 스테미나는 Image(Filled) 로도, Slider 로도 보여줄 수 있다.
///
/// 값을 이벤트로 받지 않고 매 프레임 읽어오는 이유:
/// PlayerState 의 currentHP 가 public 필드라 어느 스크립트든 직접 대입할 수 있고,
/// 그런 변경은 이벤트가 발생하지 않는다. 매 프레임 읽으면 누가 어떻게 바꾸든 항상 맞는다.
///
/// 게이지는 값이 "바뀐 순간에만" 트윈을 건다. 매 프레임 새 트윈을 만들면 금방 무거워지고
/// 서로 덮어써서 오히려 뚝뚝 끊긴다.
///
/// 붙이는 법: CanvasPlayer 같은 HUD 캔버스에 붙이고 아래 칸을 채운다.
/// </summary>
public class PlayerHUD : MonoBehaviour
{
    [Header("대상 (비워두면 자동 탐색)")]
    [SerializeField] private PlayerState playerState;
    [SerializeField] private PlayerController playerController;

    [Header("체력")]
    [Tooltip("Image Type = Filled 로 두고 Fill Method 를 Horizontal 로 맞춘다.")]
    [SerializeField] private Image healthFill;
    [SerializeField] private UILabel healthLabel = new UILabel();

    [Tooltip("체력이 줄었을 때 늦게 따라오며 '방금 깎인 양'을 보여줄 잔상 게이지.\n" +
             "메인 게이지 뒤(먼저 그려지도록 형제 순서를 앞)에 두고 색만 다르게 한다. 없어도 된다.")]
    [SerializeField] private Image healthGhostFill;

    [Header("스테미나")]
    [Tooltip("스테미나를 Slider 로 보여줄 때 연결한다. 대시하면 값이 쭉 내려갔다가 리젠으로 다시 찬다.\n" +
             "연결하면 아래 Fill 이미지의 fillAmount 대신 이쪽 value(0~1) 로 게이지를 움직인다.\n" +
             "Interactable 은 꺼두고 Transition 은 None 으로 둔다.")]
    [SerializeField] private Slider staminaSlider;

    [Tooltip("Slider 를 연결했다면 이 이미지는 게이지 값이 아니라 '색' 에만 쓰인다. Slider 의 Fill 이미지를 넣는다.")]
    [SerializeField] private Image staminaFill;
    [SerializeField] private UILabel staminaLabel = new UILabel();

    [Tooltip("스테미나용 잔상 게이지. 없어도 된다.")]
    [SerializeField] private Image staminaGhostFill;

    [Tooltip("회피에 쓸 스테미나가 모자랄 때 게이지에 입힐 색. 지금 회피가 되는지 한눈에 보인다.")]
    [SerializeField] private Color staminaLowColor = new Color(0.75f, 0.25f, 0.2f, 1f);
    private Color staminaNormalColor = Color.white;

    [Header("회피 쿨타임")]
    [Tooltip("남은 쿨타임 비율(1 -> 0)로 채워지는 이미지. 쿨타임이 없으면 숨겨진다.")]
    [SerializeField] private Image dodgeCooldownFill;
    [Tooltip("남은 시간을 소수 첫째 자리까지 보여줄 라벨.")]
    [SerializeField] private UILabel dodgeCooldownLabel = new UILabel();
    [Tooltip("쿨타임일 때만 켜둘 묶음. 비워두면 생략된다.")]
    [SerializeField] private GameObject dodgeCooldownRoot;

    [Tooltip("회피가 다시 준비됐을 때 톡 튀게 할 대상. 보통 대시 아이콘. 비워두면 생략된다.")]
    [SerializeField] private RectTransform dodgeIcon;

    [Header("연출")]
    [Tooltip("끄면 예전처럼 값이 즉시 반영된다.")]
    [SerializeField] private bool animateBars = true;

    [Tooltip("게이지가 새 값까지 흘러가는 시간(초).")]
    [Range(0.02f, 1f)] [SerializeField] private float fillDuration = 0.22f;

    [SerializeField] private Ease fillEase = Ease.OutQuad;

    [Tooltip("잔상 게이지가 따라오기 전에 멈춰 있는 시간(초). 이 사이에 '얼마나 깎였는지'가 보인다.")]
    [Range(0f, 1.5f)] [SerializeField] private float ghostDelay = 0.35f;

    [Tooltip("잔상 게이지가 따라오는 데 걸리는 시간(초).")]
    [Range(0.05f, 2f)] [SerializeField] private float ghostDuration = 0.45f;

    [Tooltip("값이 줄어들 때 게이지를 살짝 튕겨준다. 0이면 안 튕긴다.")]
    [Range(0f, 0.3f)] [SerializeField] private float punchStrength = 0.12f;

    [Header("표시")]
    [Tooltip("숫자를 '현재/최대' 로 보여줄지. 끄면 현재 값만 보여준다.")]
    [SerializeField] private bool showMaxInLabel = true;

    // 지금 화면에 반영된 목표값. 이게 바뀔 때만 새 트윈을 건다.
    private float healthTarget = -1f;
    private float staminaTarget = -1f;
    private bool dodgeWasOnCooldown;

    private Tween healthTween, healthGhostTween, healthPunch;
    private Tween staminaTween, staminaGhostTween, staminaPunch;
    private Tween dodgeReadyPunch;

    private void Awake()
    {
        if (staminaFill != null) staminaNormalColor = staminaFill.color;

        if (staminaSlider != null)
        {
            // 보여주기용이라 클릭/드래그로 값이 끌려가면 안 된다. 항상 0~1 비율로만 쓴다.
            staminaSlider.interactable = false;
            staminaSlider.wholeNumbers = false;
            staminaSlider.minValue = 0f;
            staminaSlider.maxValue = 1f;
        }
    }

    /// <summary>HUD가 꺼지거나 씬이 바뀔 때 돌던 트윈이 사라진 대상을 건드리지 않게 정리한다.</summary>
    private void OnDisable()
    {
        KillAll();
    }

    private void OnDestroy()
    {
        KillAll();
    }

    private void KillAll()
    {
        healthTween?.Kill(); healthGhostTween?.Kill(); healthPunch?.Kill();
        staminaTween?.Kill(); staminaGhostTween?.Kill(); staminaPunch?.Kill();
        dodgeReadyPunch?.Kill();
    }

    private void Update()
    {
        EnsureTarget();

        UpdateHealth();
        UpdateStamina();
        UpdateDodgeCooldown();
    }

    /// <summary>플레이어는 씬을 넘어가며 새로 생기기도 해서, 놓쳤으면 다시 찾는다.</summary>
    private void EnsureTarget()
    {
        if (playerState == null)
            playerState = FindFirstObjectByType<PlayerState>();

        if (playerController == null)
            playerController = playerState != null
                ? playerState.GetComponent<PlayerController>()
                : FindFirstObjectByType<PlayerController>();
    }

    private void UpdateHealth()
    {
        if (playerState == null) return;

        float max = Mathf.Max(1f, playerState.maxHP);
        float cur = Mathf.Clamp(playerState.currentHP, 0f, max);
        float target = cur / max;

        ApplyBar(null, healthFill, healthGhostFill, target, ref healthTarget,
                 ref healthTween, ref healthGhostTween, ref healthPunch);

        if (healthLabel.IsAssigned) healthLabel.Set(Format(cur, max));
    }

    private void UpdateStamina()
    {
        if (playerState == null) return;

        float max = Mathf.Max(1f, playerState.MaxStamina);
        float cur = Mathf.Clamp(playerState.CurrentStamina, 0f, max);
        float target = cur / max;

        ApplyBar(staminaSlider, staminaFill, staminaGhostFill, target, ref staminaTarget,
                 ref staminaTween, ref staminaGhostTween, ref staminaPunch);

        if (staminaFill != null)
        {
            // 회피에 필요한 만큼 없으면 색으로 알려준다
            bool canDodge = playerController == null
                         || playerController.DodgeStaminaCost <= 0f
                         || cur >= playerController.DodgeStaminaCost;
            staminaFill.color = canDodge ? staminaNormalColor : staminaLowColor;
        }

        if (staminaLabel.IsAssigned) staminaLabel.Set(Format(cur, max));
    }

    /// <summary>
    /// 게이지 하나를 새 값으로 옮긴다.
    /// slider 가 연결돼 있으면 그쪽 value 를, 아니면 main 이미지의 fillAmount 를 움직인다.
    /// 줄어들 때는 메인이 먼저 내려가고 잔상이 늦게 따라와서 "방금 이만큼 깎였다"가 보인다.
    /// 늘어날 때는 잔상이 먼저 올라가서 메인이 그 위를 덮으며 차오르는 느낌을 준다.
    /// </summary>
    private void ApplyBar(Slider slider, Image main, Image ghost, float target, ref float lastTarget,
                          ref Tween mainTween, ref Tween ghostTween, ref Tween punch)
    {
        if (slider == null && main == null) return;

        // 값이 사실상 그대로면 아무것도 하지 않는다. (매 프레임 트윈 생성을 막는 핵심)
        if (lastTarget >= 0f && Mathf.Abs(target - lastTarget) < 0.0005f) return;

        bool decreased = lastTarget >= 0f && target < lastTarget;
        bool first = lastTarget < 0f;
        lastTarget = target;

        if (!animateBars || first)
        {
            mainTween?.Kill();
            ghostTween?.Kill();
            SetBarValue(slider, main, target);
            if (ghost != null) ghost.fillAmount = target;
            return;
        }

        mainTween?.Kill();
        mainTween = slider != null
            ? slider.DOValue(target, fillDuration).SetEase(fillEase).SetUpdate(true)
            : main.DOFillAmount(target, fillDuration).SetEase(fillEase).SetUpdate(true);

        if (ghost != null)
        {
            ghostTween?.Kill();

            if (decreased)
            {
                // 잠깐 멈췄다가 천천히 따라온다
                ghostTween = ghost.DOFillAmount(target, ghostDuration)
                                  .SetDelay(ghostDelay).SetEase(Ease.InQuad).SetUpdate(true);
            }
            else
            {
                // 회복은 잔상이 앞서가야 메인이 그 위를 덮으며 차오른다
                ghostTween = ghost.DOFillAmount(target, fillDuration * 0.6f)
                                  .SetEase(Ease.OutQuad).SetUpdate(true);
            }
        }

        if (decreased && punchStrength > 0f)
        {
            // Slider 를 쓰면 Slider 자체를, 이미지만 쓰면 게이지를 감싼 틀을 흔든다.
            RectTransform t = slider != null
                ? slider.transform as RectTransform
                : main.transform.parent as RectTransform;
            if (t != null)
            {
                punch?.Kill();
                t.localScale = Vector3.one;
                punch = t.DOPunchScale(new Vector3(0f, punchStrength, 0f), 0.25f, 8, 0.6f).SetUpdate(true);
            }
        }
    }

    /// <summary>트윈 없이 게이지를 그 값에 바로 꽂는다.</summary>
    private static void SetBarValue(Slider slider, Image main, float value)
    {
        if (slider != null) slider.SetValueWithoutNotify(value);
        else if (main != null) main.fillAmount = value;
    }

    private void UpdateDodgeCooldown()
    {
        if (playerController == null) return;

        float remaining = playerController.DodgeCooldownRemaining;
        bool onCooldown = remaining > 0f;

        if (dodgeCooldownRoot != null) dodgeCooldownRoot.SetActive(onCooldown);

        // 쿨타임은 매 프레임 연속으로 줄어드는 값이라 그 자체가 이미 부드럽다.
        // 여기에 트윈을 얹으면 목표가 계속 바뀌면서 오히려 뚝뚝 끊긴다.
        float dur = Mathf.Max(0.0001f, playerController.DodgeCooldownDuration);

        if (dodgeCooldownFill != null)
            dodgeCooldownFill.fillAmount = Mathf.Clamp01(remaining / dur);

        if (dodgeCooldownLabel.IsAssigned)
        {
            dodgeCooldownLabel.SetActive(onCooldown);
            // 0.04초 남았는데 "0.0" 이라고 쓰면 이미 끝난 것처럼 보인다. 올림해서 0.1 이상을 유지한다.
            if (onCooldown)
                dodgeCooldownLabel.Set((Mathf.Ceil(remaining * 10f) / 10f).ToString("0.0"));
        }

        // 쿨타임이 막 끝난 순간에만 톡 튀겨서 "이제 쓸 수 있다"를 알린다
        if (dodgeWasOnCooldown && !onCooldown && animateBars && dodgeIcon != null)
        {
            dodgeReadyPunch?.Kill();
            dodgeIcon.localScale = Vector3.one;
            dodgeReadyPunch = dodgeIcon.DOPunchScale(Vector3.one * 0.18f, 0.32f, 9, 0.7f).SetUpdate(true);
        }

        dodgeWasOnCooldown = onCooldown;
    }

    private string Format(float cur, float max)
    {
        return showMaxInLabel
            ? Mathf.CeilToInt(cur) + " / " + Mathf.CeilToInt(max)
            : Mathf.CeilToInt(cur).ToString();
    }
}
