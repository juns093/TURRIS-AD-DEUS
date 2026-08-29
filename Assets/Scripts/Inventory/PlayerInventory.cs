using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어의 장비 슬롯 + 보관함을 들고 있는 본체. 플레이어 오브젝트에 붙인다.
/// UI는 이 클래스를 읽고 쓰기만 하고, 규칙 판단은 전부 여기서 한다.
/// </summary>
public class PlayerInventory : MonoBehaviour
{
    /// <summary>
    /// 어디서든 PlayerInventory.Instance 로 접근한다.
    /// 플레이어 오브젝트가 PersistentPlayer 로 씬을 넘어 살아남으므로 인벤토리도 함께 유지된다.
    /// </summary>
    public static PlayerInventory Instance { get; private set; }

    [Header("보관함 크기 (칸)")]
    [SerializeField] private int stashWidth = 10;
    [SerializeField] private int stashHeight = 12;

    [Header("장착 슬롯 수")]
    [SerializeField] private int weaponSlots = 2;
    [SerializeField] private int armorSlots = 2;

    [Header("시작 지급 아이템")]
    [Tooltip("게임 시작할 때 보관함에 넣어줄 것들. 나무 지팡이는 여기에.")]
    [SerializeField] private List<ItemData> startingItems = new List<ItemData>();

    [Tooltip("켜면 시작 아이템 중 장비는 자동으로 장착까지 해준다.")]
    [SerializeField] private bool autoEquipStartingGear = true;

    [Header("줍기")]
    [Tooltip("켜면 장비를 주웠을 때 해당 종류의 빈 슬롯이 있으면 곧바로 장착한다.\n빈 슬롯이 없으면 교체하지 않고 그냥 보관함으로 들어간다.")]
    [SerializeField] private bool autoEquipOnPickup = true;

    // ---- 상태 ----
    public InventoryGrid Stash { get; private set; }
    public ItemInstance[] Weapons { get; private set; }
    public ItemInstance[] Armors { get; private set; }

    /// <summary>장비가 바뀌어서 스탯이 갱신됐을 때.</summary>
    public event System.Action StatsChanged;
    /// <summary>장착 슬롯 내용이 바뀌었을 때. UI 갱신용.</summary>
    public event System.Action EquipmentChanged;

    /// <summary>아이템을 실제로 획득했을 때. (아이템, 실제로 들어간 개수) — 획득 알림 UI 용.</summary>
    public event System.Action<ItemData, int> ItemPickedUp;

    // ---- 최종 스탯 ----
    public int Attack { get; private set; }
    public int Defense { get; private set; }
    public float CritChance { get; private set; }
    public float CritMultiplier { get; private set; }
    public int MaxHealth => CombatCalculator.PlayerMaxHealth;

    private void Awake()
    {
        Stash = new InventoryGrid(stashWidth, stashHeight);
        Weapons = new ItemInstance[Mathf.Max(1, weaponSlots)];
        Armors = new ItemInstance[Mathf.Max(1, armorSlots)];
        Recalculate();

        // 먼저 살아남은 플레이어의 인벤토리를 정본으로 둔다.
        // (PersistentPlayer 가 중복 플레이어를 지우지만, Awake 순서를 믿지 않고 여기서도 막는다)
        if (Instance == null) Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        // 정본이 아닌 복제 플레이어라면 시작 아이템을 또 지급하지 않는다.
        if (Instance != this) return;

        foreach (ItemData data in startingItems)
        {
            if (data == null) continue;

            ItemInstance inst = new ItemInstance(data);

            if (autoEquipStartingGear && data.IsEquippable && TryEquipToFreeSlot(inst))
                continue;

            if (!Stash.TryAdd(inst))
                Debug.LogWarning($"[PlayerInventory] 보관함이 꽉 차서 시작 아이템 '{data.displayName}'을(를) 넣지 못했습니다.", this);
        }
    }

    // ---------------- 획득 ----------------
    /// <summary>아이템을 주웠을 때. 전부 넣었으면 true.</summary>
    public bool Pickup(ItemData data, int count = 1)
    {
        if (data == null) return false;
        return Pickup(new ItemInstance(data, count));
    }

    /// <summary>
    /// 아이템을 인벤토리에 넣는다.
    ///  1) 장비이고 같은 종류의 빈 슬롯이 있으면 바로 장착 (autoEquipOnPickup)
    ///  2) 아니면 보관함으로. 물약처럼 쌓이는 건 기존 더미에 먼저 부어진다.
    /// </summary>
    /// <returns>
    /// 전부 넣었으면 true. 자리가 모자라면 false이고, 이때 item.count 에는 못 넣고 남은 개수가 남는다.
    /// (줍기 오브젝트가 남은 수량만큼 계속 바닥에 남아있게 하려고 이렇게 돌려준다)
    /// </returns>
    public bool Pickup(ItemInstance item)
    {
        if (item == null || item.data == null) return false;

        int before = item.count;

        // 빈 슬롯이 있을 때만 자동 장착. 이미 차 있으면 멋대로 갈아끼우지 않는다.
        if (autoEquipOnPickup && item.data.IsEquippable && HasFreeSlot(item.data.type))
        {
            if (TryEquipToFreeSlot(item))
            {
                ItemPickedUp?.Invoke(item.data, before);
                return true;
            }
        }

        bool all = Stash.TryAdd(item);
        int taken = all ? before : before - item.count;

        if (taken > 0) ItemPickedUp?.Invoke(item.data, taken);
        return all;
    }

    /// <summary>그 종류의 장착 슬롯 중 비어있는 게 있는지.</summary>
    public bool HasFreeSlot(ItemType type)
    {
        ItemInstance[] slots = SlotArrayFor(type);
        if (slots == null) return false;

        foreach (ItemInstance s in slots)
            if (s == null) return true;

        return false;
    }

    // ---------------- 장착 ----------------
    private ItemInstance[] SlotArrayFor(ItemType type)
    {
        if (type == ItemType.Weapon) return Weapons;
        if (type == ItemType.Armor) return Armors;
        return null;
    }

    public bool CanEquip(ItemInstance item, ItemType slotType)
    {
        return item != null && item.data != null && item.data.IsEquippable && item.data.type == slotType;
    }

    /// <summary>비어있는 슬롯 아무 데나 장착. 자리가 없으면 false.</summary>
    public bool TryEquipToFreeSlot(ItemInstance item)
    {
        if (item == null || item.data == null || !item.data.IsEquippable) return false;

        ItemInstance[] slots = SlotArrayFor(item.data.type);
        if (slots == null) return false;

        for (int i = 0; i < slots.Length; i++)
            if (slots[i] == null)
                return Equip(item, item.data.type, i);

        // 빈 슬롯이 없으면 0번과 교체
        return Equip(item, item.data.type, 0);
    }

    /// <summary>
    /// 지정한 슬롯에 장착한다. 원래 그 슬롯에 있던 건 보관함으로 돌려보낸다.
    /// 보관함에 자리가 없으면 장착을 취소하고 false 를 돌려준다. (아이템이 증발하지 않게)
    /// </summary>
    public bool Equip(ItemInstance item, ItemType slotType, int slotIndex)
    {
        if (!CanEquip(item, slotType)) return false;

        ItemInstance[] slots = SlotArrayFor(slotType);
        if (slots == null || slotIndex < 0 || slotIndex >= slots.Length) return false;

        ItemInstance previous = slots[slotIndex];
        if (previous == item) return true;

        // 다른 슬롯에 이미 끼워져 있던 걸 옮기는 경우
        int fromSlot = System.Array.IndexOf(slots, item);
        if (fromSlot >= 0) slots[fromSlot] = null;

        bool wasInStash = Stash.Contains(item);
        if (wasInStash) Stash.Remove(item);

        // 원래 끼어있던 장비를 보관함으로
        if (previous != null && !Stash.TryAdd(previous))
        {
            // 롤백: 아무것도 안 한 상태로 되돌린다
            if (fromSlot >= 0) slots[fromSlot] = item;
            else if (wasInStash) Stash.TryAdd(item);

            Debug.LogWarning("[PlayerInventory] 보관함에 자리가 없어 장비를 교체할 수 없습니다.", this);
            return false;
        }

        slots[slotIndex] = item;
        item.gridX = -1;
        item.gridY = -1;

        Recalculate();
        EquipmentChanged?.Invoke();
        return true;
    }

    /// <summary>장착 해제해서 보관함으로. 자리가 없으면 그대로 둔다.</summary>
    public bool Unequip(ItemType slotType, int slotIndex)
    {
        ItemInstance[] slots = SlotArrayFor(slotType);
        if (slots == null || slotIndex < 0 || slotIndex >= slots.Length) return false;

        ItemInstance item = slots[slotIndex];
        if (item == null) return false;

        if (!Stash.TryAdd(item))
        {
            Debug.LogWarning("[PlayerInventory] 보관함이 꽉 차서 장비를 벗을 수 없습니다.", this);
            return false;
        }

        slots[slotIndex] = null;
        Recalculate();
        EquipmentChanged?.Invoke();
        return true;
    }

    public ItemInstance GetEquipped(ItemType slotType, int slotIndex)
    {
        ItemInstance[] slots = SlotArrayFor(slotType);
        if (slots == null || slotIndex < 0 || slotIndex >= slots.Length) return null;
        return slots[slotIndex];
    }

    /// <summary>슬롯에서 아이템을 떼어내되 보관함에 넣지는 않는다. 드래그로 격자에 직접 놓을 때 사용.</summary>
    public ItemInstance DetachFromSlot(ItemType slotType, int slotIndex)
    {
        ItemInstance[] slots = SlotArrayFor(slotType);
        if (slots == null || slotIndex < 0 || slotIndex >= slots.Length) return null;

        ItemInstance item = slots[slotIndex];
        if (item == null) return null;

        slots[slotIndex] = null;
        Recalculate();
        EquipmentChanged?.Invoke();
        return item;
    }

    // ---------------- 스탯 집계 ----------------
    public void Recalculate()
    {
        int atk = CombatCalculator.BaseAttack;
        int def = CombatCalculator.BaseDefense;
        float crit = CombatCalculator.BaseCritChance;
        float critMul = CombatCalculator.BaseCritMultiplier;

        if (Weapons != null)
        {
            foreach (ItemInstance w in Weapons)
            {
                if (w == null || w.data == null) continue;
                atk += Mathf.Clamp(w.data.attack, 0, CombatCalculator.MaxAttackPerWeapon);
                def += w.data.defense;
                crit += w.data.critChanceBonus;
                critMul += w.data.critDamageBonus;
            }
        }

        if (Armors != null)
        {
            foreach (ItemInstance a in Armors)
            {
                if (a == null || a.data == null) continue;
                def += Mathf.Clamp(a.data.defense, 0, CombatCalculator.MaxDefensePerArmor);
                atk += a.data.attack;
                crit += a.data.critChanceBonus;
                critMul += a.data.critDamageBonus;
            }
        }

        Attack = Mathf.Clamp(atk, 0, CombatCalculator.MaxAttack);
        Defense = Mathf.Clamp(def, 0, CombatCalculator.MaxDefense);
        CritChance = Mathf.Clamp(crit, 0f, CombatCalculator.MaxCritChance);
        CritMultiplier = Mathf.Max(1f, critMul);

        StatsChanged?.Invoke();
    }

    /// <summary>지금 스탯이 어떻게 계산됐는지 한 줄로. 밸런스 확인할 때 Console 에서 보기 좋다.</summary>
    public string DescribeStats()
    {
        int weaponAtk = 0;
        foreach (ItemInstance w in Weapons)
            if (w != null && w.data != null) weaponAtk += w.data.attack;

        int armorDef = 0;
        foreach (ItemInstance a in Armors)
            if (a != null && a.data != null) armorDef += a.data.defense;

        return $"공격력 {Attack} (기본 {CombatCalculator.BaseAttack} + 무기 {weaponAtk})   " +
               $"방어력 {Defense} (기본 {CombatCalculator.BaseDefense} + 방어구 {armorDef})   " +
               $"치명타 {CritChance * 100f:0.#}% x{CritMultiplier:0.##}";
    }

    [ContextMenu("스탯 계산 내역 출력")]
    private void LogStats() => Debug.Log($"[PlayerInventory] {DescribeStats()}", this);

    // ---------------- 전투에서 쓰는 입구 ----------------
    /// <summary>적에게 맞았을 때 실제로 깎일 체력.</summary>
    public int CalculateIncomingDamage(float enemyAttack)
    {
        return CombatCalculator.DamageToPlayer(enemyAttack, Defense);
    }

    /// <summary>보스를 때렸을 때 줄 피해. 치명타 판정까지 여기서 굴린다.</summary>
    public int CalculateOutgoingDamage(out bool isCritical)
    {
        return CombatCalculator.RollDamageToBoss(Attack, CritChance, CritMultiplier, out isCritical);
    }
}
