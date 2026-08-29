using UnityEngine;

public enum ItemType
{
    Weapon,      // 무기 슬롯 2칸 중 하나에 장착
    Armor,       // 방어구 슬롯 2칸 중 하나에 장착
    Consumable,  // 물약 등. 장착 불가, 겹쳐 쌓임
    Misc         // 재료/잡템
}

/// <summary>
/// 아이템 원본 데이터. Project 창에서 우클릭 → Create → Nibo → Item 으로 만든다.
/// 런타임에 실제로 들고 다니는 건 ItemData 가 아니라 ItemInstance(개수/위치를 가진 사본)다.
/// </summary>
[CreateAssetMenu(fileName = "NewItem", menuName = "Nibo/Item", order = 0)]
public class ItemData : ScriptableObject
{
    [Header("기본")]
    [Tooltip("저장/불러오기에서 아이템을 찾는 키. 에셋 이름과 달라도 되지만 절대 겹치면 안 된다.")]
    public string id = "item_id";
    public string displayName = "이름 없는 아이템";
    [TextArea(2, 4)] public string description;
    public ItemType type = ItemType.Misc;

    [Header("아이콘")]
    [Tooltip("비워두면 UI가 이름이 적힌 색깔 상자로 대신 그린다. 아트 없이도 테스트 가능.")]
    public Sprite icon;
    [Tooltip("아이콘이 없을 때 쓰는 상자 색. 아이콘이 있으면 곱해지는 색으로 쓰인다.")]
    public Color tint = Color.white;

    [Header("아이콘 표시 (칸 차지에는 영향 없음)")]
    [Tooltip("아이콘을 돌려서 그릴 각도(도). 칼을 비스듬히 눕혀 보여주고 싶을 때 쓴다.\n" +
             "격자에서 몇 칸을 차지하는지는 아래 Grid Width/Height 가 정하고, 이 값은 그림만 돌린다.")]
    [Range(-180f, 180f)] public float iconAngle;

    [Tooltip("아이콘 크기 배율. 돌렸더니 칸 밖으로 삐져나오면 줄인다.")]
    [Range(0.1f, 3f)] public float iconScale = 1f;

    [Tooltip("아이콘 위치 미세조정(픽셀). 돌리고 나서 중심이 어긋나 보일 때 쓴다.")]
    public Vector2 iconOffset;

    [Header("장착 슬롯에서의 표시 (선택)")]
    [Tooltip("켜면 장착 슬롯에서 아래 값(그림/각도/크기/위치)을 따로 쓴다.\n" +
             "격자에서는 크게, 슬롯에서는 작게 같은 식으로 나누고 싶으면 켤 것.\n" +
             "끄면 격자에서 쓰는 위의 아이콘 설정을 슬롯에서도 그대로 쓴다.")]
    public bool useSeparateSlotDisplay = true;

    [Tooltip("장착 슬롯 전용 그림. 비우면 위의 Icon 을 그대로 쓴다.\n" +
             "격자에서는 길게 눕힌 그림, 슬롯에서는 정사각형 아이콘 같은 식으로 나눠 쓸 때.")]
    public Sprite slotIcon;

    [Range(-180f, 180f)] public float slotIconAngle;
    [Range(0.1f, 3f)] public float slotIconScale = 1f;
    public Vector2 slotIconOffset;

    [Header("인벤토리 칸 차지 크기")]
    [Tooltip("가로 칸 수. 무기는 5, 물약은 2가 기본.")]
    [Min(1)] public int gridWidth = 1;
    [Tooltip("세로 칸 수. 무기는 2, 물약은 2가 기본.")]
    [Min(1)] public int gridHeight = 1;

    [Tooltip("한 칸에 겹쳐 쌓을 수 있는 최대 개수. 물약은 16, 장비는 1.")]
    [Min(1)] public int maxStack = 1;

    [Tooltip("인벤토리에서 90도 회전을 허용할지. 정사각형 아이템은 꺼두는 게 깔끔하다.")]
    public bool allowRotation = true;

    [Header("장착 시 스탯 (Weapon / Armor 만)")]
    [Tooltip("무기당 최대 250. CombatCalculator.MaxAttackPerWeapon 참고.")]
    public int attack;
    [Tooltip("방어구당 최대 70. CombatCalculator.MaxDefensePerArmor 참고.")]
    public int defense;
    [Tooltip("치명타 확률 보너스. 0.05 = +5%p")]
    public float critChanceBonus;
    [Tooltip("치명타 배수 보너스. 0.2 = +20%p (1.5 -> 1.7)")]
    public float critDamageBonus;

    public bool IsEquippable => type == ItemType.Weapon || type == ItemType.Armor;

    // ---- 표시용 값 고르기 ----
    // 같은 아이템이라도 격자에 놓였을 때와 장착 슬롯에 끼워졌을 때 다르게 보여줄 수 있다.
    private bool UseSlot(bool inEquipSlot) => inEquipSlot && useSeparateSlotDisplay;

    public Sprite GetIcon(bool inEquipSlot) => UseSlot(inEquipSlot) && slotIcon != null ? slotIcon : icon;
    public float GetIconAngle(bool inEquipSlot) => UseSlot(inEquipSlot) ? slotIconAngle : iconAngle;
    public float GetIconScale(bool inEquipSlot) => UseSlot(inEquipSlot) ? slotIconScale : iconScale;
    public Vector2 GetIconOffset(bool inEquipSlot) => UseSlot(inEquipSlot) ? slotIconOffset : iconOffset;

    /// <summary>인스펙터에서 값을 잘못 넣었을 때 바로 잡아준다.</summary>
    private void OnValidate()
    {
        gridWidth = Mathf.Max(1, gridWidth);
        gridHeight = Mathf.Max(1, gridHeight);
        maxStack = Mathf.Max(1, maxStack);

        // 장비는 겹쳐 쌓이면 안 된다. 스탯 계산이 꼬인다.
        if (IsEquippable) maxStack = 1;

        if (type == ItemType.Weapon)
            attack = Mathf.Clamp(attack, 0, CombatCalculator.MaxAttackPerWeapon);
        if (type == ItemType.Armor)
            defense = Mathf.Clamp(defense, 0, CombatCalculator.MaxDefensePerArmor);

        if (gridWidth == gridHeight) allowRotation = false;

        iconScale = Mathf.Clamp(iconScale, 0.1f, 3f);
        if (iconScale <= 0f) iconScale = 1f;

        slotIconScale = Mathf.Clamp(slotIconScale, 0.1f, 3f);
        if (slotIconScale <= 0f) slotIconScale = 1f;
    }
}

/// <summary>
/// 실제로 인벤토리에 들어있는 한 덩어리. 같은 ItemData 라도 개수와 위치가 다르면 다른 인스턴스다.
/// </summary>
[System.Serializable]
public class ItemInstance
{
    public ItemData data;
    public int count = 1;

    /// <summary>격자에서의 좌상단 칸 좌표. 장착 중이거나 어디에도 없으면 -1.</summary>
    public int gridX = -1;
    public int gridY = -1;

    /// <summary>90도 돌려서 넣었는지.</summary>
    public bool rotated;

    public ItemInstance(ItemData data, int count = 1)
    {
        this.data = data;
        this.count = Mathf.Clamp(count, 1, data != null ? data.maxStack : 1);
    }

    public int Width => rotated ? data.gridHeight : data.gridWidth;
    public int Height => rotated ? data.gridWidth : data.gridHeight;

    public bool IsFull => data == null || count >= data.maxStack;
    public int FreeSpace => data == null ? 0 : Mathf.Max(0, data.maxStack - count);

    /// <summary>같은 종류라서 합칠 수 있는지.</summary>
    public bool CanStackWith(ItemInstance other)
    {
        return other != null && data != null && other.data == data && data.maxStack > 1 && !IsFull;
    }

    /// <summary>other 를 이 더미에 부어넣고, 넘겨받은 개수를 돌려준다.</summary>
    public int Absorb(ItemInstance other)
    {
        if (!CanStackWith(other)) return 0;
        int moved = Mathf.Min(FreeSpace, other.count);
        count += moved;
        other.count -= moved;
        return moved;
    }

    public ItemInstance Clone()
    {
        return new ItemInstance(data, count) { rotated = rotated, gridX = gridX, gridY = gridY };
    }
}
