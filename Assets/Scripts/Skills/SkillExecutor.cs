using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class SkillUsedEvent : UnityEvent<SkillData> { }

/// <summary>
/// 전투 중 실제 스킬 발동을 담당한다. 쿨타임만 여기서 관리하고, 데미지/이펙트 같은
/// 실제 효과는 OnSkillUsed 이벤트에 연결해서 처리한다. (이 시스템이 전투 로직을 몰라도 되게)
/// </summary>
public class SkillExecutor : MonoBehaviour
{
    public static SkillExecutor Instance { get; private set; }

    [Header("연결")]
    [Tooltip("스킬이 실제로 발동될 때 호출된다. 인스펙터에서 이펙트 재생/데미지 처리 함수를 연결한다.")]
    public SkillUsedEvent OnSkillUsed;

    [Tooltip("쿨타임 때문에 발동에 실패했을 때 호출된다.")]
    public SkillUsedEvent OnSkillOnCooldown;

    private readonly Dictionary<string, float> cooldownEndTime = new Dictionary<string, float>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public bool IsOnCooldown(SkillData skill)
    {
        if (skill == null) return false;
        return cooldownEndTime.TryGetValue(skill.skillID, out float end) && Time.time < end;
    }

    /// <summary>0(사용 가능) ~ 1(막 사용함) 사이 값. UI 의 쿨타임 오버레이에 그대로 쓸 수 있다.</summary>
    public float GetCooldownRatio(SkillData skill)
    {
        if (skill == null || skill.cooldown <= 0f) return 0f;
        if (!cooldownEndTime.TryGetValue(skill.skillID, out float end)) return 0f;

        float remain = end - Time.time;
        return remain <= 0f ? 0f : Mathf.Clamp01(remain / skill.cooldown);
    }

    /// <summary>재사용까지 남은 시간(초). 지금 쓸 수 있으면 0. 숫자로 보여줄 때 쓴다.</summary>
    public float GetCooldownRemaining(SkillData skill)
    {
        if (skill == null) return 0f;
        if (!cooldownEndTime.TryGetValue(skill.skillID, out float end)) return 0f;

        return Mathf.Max(0f, end - Time.time);
    }

    /// <summary>쿨타임이 끝났으면 스킬을 발동시키고 true 를 돌려준다.</summary>
    public bool TryUseSkill(SkillData skill)
    {
        if (skill == null) return false;

        if (IsOnCooldown(skill))
        {
            OnSkillOnCooldown?.Invoke(skill);
            return false;
        }

        cooldownEndTime[skill.skillID] = Time.time + skill.cooldown;
        OnSkillUsed?.Invoke(skill);
        return true;
    }
}
