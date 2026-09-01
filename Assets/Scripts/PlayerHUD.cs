using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면에 체력 / 스테미나 / 회피 쿨타임을 띄우는 HUD.
///
/// 값을 이벤트로 받지 않고 매 프레임 읽어오는 이유:
/// PlayerState 의 currentHP 가 public 필드라 어느 스크립트든 직접 대입할 수 있고,
/// 그런 변경은 이벤트가 발생하지 않는다. 매 프레임 읽으면 누가 어떻게 바꾸든 항상 맞는다.
/// 게이지 두어 개 갱신하는 비용은 무시할 수준이다.
///
/// 붙이는 법: CanvasPlayer 같은 HUD 캔버스에 붙이고 아래 칸을 채운다.
/// 플레이어는 비워두면 씬에서 자동으로 찾는다. (씬을 넘어가도 다시 찾는다)
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

    [Header("스테미나")]
    [SerializeField] private Image staminaFill;
    [SerializeField] private UILabel staminaLabel = new UILabel();

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

    [Header("표시")]
    [Tooltip("숫자를 '현재/최대' 로 보여줄지. 끄면 현재 값만 보여준다.")]
    [SerializeField] private bool showMaxInLabel = true;

    private void Awake()
    {
        if (staminaFill != null) staminaNormalColor = staminaFill.color;
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

        if (healthFill != null) healthFill.fillAmount = cur / max;
        if (healthLabel.IsAssigned) healthLabel.Set(Format(cur, max));
    }

    private void UpdateStamina()
    {
        if (playerState == null) return;

        float max = Mathf.Max(1f, playerState.MaxStamina);
        float cur = Mathf.Clamp(playerState.CurrentStamina, 0f, max);

        if (staminaFill != null)
        {
            staminaFill.fillAmount = cur / max;

            // 회피에 필요한 만큼 없으면 색으로 알려준다
            bool canDodge = playerController == null
                         || playerController.DodgeStaminaCost <= 0f
                         || cur >= playerController.DodgeStaminaCost;
            staminaFill.color = canDodge ? staminaNormalColor : staminaLowColor;
        }

        if (staminaLabel.IsAssigned) staminaLabel.Set(Format(cur, max));
    }

    private void UpdateDodgeCooldown()
    {
        if (playerController == null) return;

        float remaining = playerController.DodgeCooldownRemaining;
        bool onCooldown = remaining > 0f;

        if (dodgeCooldownRoot != null) dodgeCooldownRoot.SetActive(onCooldown);

        if (dodgeCooldownFill != null)
        {
            float duration = Mathf.Max(0.0001f, playerController.DodgeCooldownDuration);
            dodgeCooldownFill.fillAmount = Mathf.Clamp01(remaining / duration);
        }

        if (dodgeCooldownLabel.IsAssigned)
        {
            dodgeCooldownLabel.SetActive(onCooldown);
            // 0.04초 남았는데 "0.0" 이라고 쓰면 이미 끝난 것처럼 보인다. 올림해서 0.1 이상을 유지한다.
            if (onCooldown)
                dodgeCooldownLabel.Set((Mathf.Ceil(remaining * 10f) / 10f).ToString("0.0"));
        }
    }

    private string Format(float cur, float max)
    {
        return showMaxInLabel
            ? Mathf.CeilToInt(cur) + " / " + Mathf.CeilToInt(max)
            : Mathf.CeilToInt(cur).ToString();
    }
}
