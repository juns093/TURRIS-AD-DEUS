using System;
using UnityEngine;

/// <summary>
/// 회피(Dodge) 등에서 사용할 스테미나 관리 전용 컴포넌트.
/// PlayerController는 이 컴포넌트에 "소비 가능한지"만 물어보고,
/// 실제 회복/소비/딜레이 로직은 전부 여기서 처리한다.
///
/// 사용법:
///  1) Player 오브젝트에 이 스크립트를 붙인다 (PlayerController와 같은 오브젝트).
///  2) PlayerController에서 dodge 시작 전에
///       if (stamina.TryConsume(dodgeStaminaCost)) { StartDodge(); }
///     식으로 사용.
/// </summary>
public class PlayerState : MonoBehaviour
{
    [Header("스테미나")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float regenPerSecond = 20f;

    [Tooltip("스테미나를 소비한 직후, 다시 회복이 시작되기까지 대기하는 시간(초)")]
    [SerializeField] private float regenDelay = 0.5f;

    [Header("체력")]
    public float maxHP = 100f;
    public float currentHP = 100f;
    public float MaxStamina => maxStamina;
    public float CurrentStamina { get; private set; }

    /// <summary>(현재 스테미나, 최대 스테미나). 스테미나바 UI 등에서 구독.</summary>
    public event Action<float, float> OnStaminaChanged;

    private float regenDelayTimer;

    private void Awake()
    {
        CurrentStamina = maxStamina;
    }

    private void Update()
    {
        if (regenDelayTimer > 0f)
        {
            regenDelayTimer -= Time.deltaTime;
            return;
        }

        if (CurrentStamina < maxStamina)
        {
            float prev = CurrentStamina;
            CurrentStamina = Mathf.Min(maxStamina, CurrentStamina + regenPerSecond * Time.deltaTime);
            if (!Mathf.Approximately(prev, CurrentStamina))
                OnStaminaChanged?.Invoke(CurrentStamina, maxStamina);
        }
    }

    /// <summary>소비하지 않고 "지금 이 비용을 감당할 수 있는지"만 확인한다.</summary>
    public bool HasEnough(float cost) => CurrentStamina >= cost;

    /// <summary>
    /// cost만큼 소비를 시도한다. 스테미나가 부족하면 아무것도 소비하지 않고 false를 반환한다.
    /// 성공 시 회복 딜레이 타이머를 리셋한다.
    /// </summary>
    public bool TryConsume(float cost)
    {
        if (cost <= 0f) return true;
        if (CurrentStamina < cost) return false;

        CurrentStamina -= cost;
        regenDelayTimer = regenDelay;
        OnStaminaChanged?.Invoke(CurrentStamina, maxStamina);
        return true;
    }

    /// <summary>씬 재시작, 체크포인트 부활 등에서 호출해서 가득 채운다.</summary>
    public void ResetStamina()
    {
        regenDelayTimer = 0f;
        CurrentStamina = maxStamina;
        OnStaminaChanged?.Invoke(CurrentStamina, maxStamina);
    }
}