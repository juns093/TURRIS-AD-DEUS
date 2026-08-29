using UnityEngine;

/// <summary>
/// 전투 수치 계산을 한 곳에 모아둔 정적 클래스.
/// 플레이어/보스/적 어느 쪽이든 피해 계산은 전부 여기를 거치게 한다.
/// (같은 식을 여러 군데 복사해두면 나중에 밸런스 패치할 때 반드시 하나를 빠뜨린다)
///
/// F1  피해 감소 계수    R = 100 / (DEF + 100)
/// F2  플레이어 받는 피해 = max(1, round(적 공격력 × R))
/// F3  보스 받는 피해     = max(1, round(플레이어 공격력 × 치명타 배수))
/// </summary>
public static class CombatCalculator
{
    // ---------------- 밸런스 상수 ----------------
    public const int PlayerMaxHealth = 100;

    public const int BaseAttack = 50;
    public const int MaxAttack = 300;
    public const int MaxAttackPerWeapon = 250;

    public const int BaseDefense = 10;
    public const int MaxDefense = 150;
    public const int MaxDefensePerArmor = 70;

    /// <summary>기본 치명타 확률 (0~1).</summary>
    public const float BaseCritChance = 0.10f;
    /// <summary>치명타 확률 상한. 100%가 되면 치명타가 그냥 상시 버프가 되므로 막아둔다.</summary>
    public const float MaxCritChance = 0.75f;
    /// <summary>기본 치명타 배수. 150% = 1.5</summary>
    public const float BaseCritMultiplier = 1.5f;

    /// <summary>감소 공식의 분모 상수. 값이 클수록 방어력 1점의 가치가 낮아진다.</summary>
    public const float DefenseConstant = 100f;

    // ---------------- F1 ----------------
    /// <summary>
    /// 피해 감소 계수. 1이면 그대로 다 맞고, 0에 가까울수록 단단하다.
    /// 방어력이 음수로 내려가도 0으로 나누지 않도록 클램프해서 쓴다.
    /// </summary>
    public static float DamageReduction(float defense)
    {
        float def = Mathf.Clamp(defense, 0f, MaxDefense);
        return DefenseConstant / (def + DefenseConstant);
    }

    /// <summary>표시용 감소율(%). 방어력 150이면 60을 돌려준다.</summary>
    public static float DamageReductionPercent(float defense)
    {
        return (1f - DamageReduction(defense)) * 100f;
    }

    // ---------------- F2 ----------------
    /// <summary>
    /// 플레이어가 실제로 받는 피해.
    /// 최소 1을 보장한다. 안 그러면 방어력이 높을 때 약한 적에게 완전 무적이 된다.
    /// </summary>
    public static int DamageToPlayer(float enemyAttack, float defense)
    {
        float raw = Mathf.Max(0f, enemyAttack) * DamageReduction(defense);
        return Mathf.Max(1, Mathf.RoundToInt(raw));
    }

    // ---------------- F3 ----------------
    /// <summary>
    /// 보스/적이 받는 피해. 치명타 여부를 바깥에서 이미 정한 경우에 쓴다.
    /// 현재 설계상 보스는 방어 감소를 적용받지 않는다. (난이도는 보스 체력으로 조절)
    /// 나중에 보스에게도 방어력을 주고 싶으면 여기서 DamageReduction을 곱하면 된다.
    /// </summary>
    public static int DamageToBoss(float playerAttack, bool isCritical, float critMultiplier)
    {
        float mul = isCritical ? Mathf.Max(1f, critMultiplier) : 1f;
        float raw = Mathf.Max(0f, playerAttack) * mul;
        return Mathf.Max(1, Mathf.RoundToInt(raw));
    }

    /// <summary>
    /// 치명타 판정까지 여기서 굴린다. 실제 공격 처리에서 쓰는 건 보통 이 함수.
    /// </summary>
    public static int RollDamageToBoss(float playerAttack, float critChance, float critMultiplier, out bool isCritical)
    {
        isCritical = Random.value < Mathf.Clamp(critChance, 0f, MaxCritChance);
        return DamageToBoss(playerAttack, isCritical, critMultiplier);
    }

    // ---------------- 파생 ----------------
    /// <summary>
    /// D1. 유효 체력. 체력이 100 고정이면 결과가 정확히 (방어력 + 100)이 된다.
    /// = 방어력 1점이 EHP 1점. 밸런스 잡을 때 이 값을 기준으로 보는 게 빠르다.
    /// </summary>
    public static float EffectiveHealth(float defense, float health = PlayerMaxHealth)
    {
        return health / DamageReduction(defense);
    }

    /// <summary>D2. 그 적에게 몇 대까지 버티는지.</summary>
    public static float HitsToDie(float enemyAttack, float defense, float health = PlayerMaxHealth)
    {
        int perHit = DamageToPlayer(enemyAttack, defense);
        return perHit <= 0 ? float.PositiveInfinity : health / perHit;
    }

    /// <summary>D3. 치명타까지 포함한 기대 공격력. DPS 비교용.</summary>
    public static float ExpectedAttack(float playerAttack, float critChance, float critMultiplier)
    {
        float c = Mathf.Clamp(critChance, 0f, MaxCritChance);
        return playerAttack * (1f + c * (Mathf.Max(1f, critMultiplier) - 1f));
    }

    /// <summary>보스를 잡는 데 필요한 평균 타수.</summary>
    public static float HitsToKill(float bossHealth, float playerAttack, float critChance, float critMultiplier)
    {
        float dps = ExpectedAttack(playerAttack, critChance, critMultiplier);
        return dps <= 0f ? float.PositiveInfinity : bossHealth / dps;
    }
}
