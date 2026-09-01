using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 아이템을 주웠을 때 잠깐 떠서 "방금 주운 것"과 "지금 장착 중인 것"을 나란히 보여주는 알림창.
/// 새로 주운 장비가 지금 쓰는 것보다 나은지 인벤토리를 열지 않고 바로 비교할 수 있게 한다.
///
/// 붙이는 법: HUD 캔버스에 붙이고 아래 칸을 채운다.
/// 플레이어 인벤토리는 비워두면 알아서 찾고, 씬이 바뀌어 새로 생겨도 다시 찾는다.
/// </summary>
public class ItemPickupPopup : MonoBehaviour
{
    /// <summary>아이콘 + 이름 + 능력치 한 줄. 주운 것과 장착 중인 것에 똑같이 쓴다.</summary>
    [System.Serializable]
    public class Row
    {
        [Tooltip("이 줄 전체. 보여줄 게 없으면 통째로 꺼진다.")]
        public GameObject root;
        public Image icon;
        public UILabel name = new UILabel();
        public UILabel stat = new UILabel();

        public void Show(ItemData data)
        {
            bool has = data != null;
            if (root != null) root.SetActive(has);
            if (!has) return;

            if (icon != null)
            {
                icon.sprite = data.icon;
                icon.enabled = data.icon != null;
                icon.preserveAspect = true;
            }

            if (name.IsAssigned) name.Set(data.displayName);
            if (stat.IsAssigned) stat.Set(Describe(data));
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }

        /// <summary>장비면 주요 능력치를, 아니면 종류를 한 줄로.</summary>
        private static string Describe(ItemData d)
        {
            if (d.type == ItemType.Weapon) return "공격력 " + d.attack + Bonus(d);
            if (d.type == ItemType.Armor) return "방어력 " + d.defense + Bonus(d);
            return d.type == ItemType.Consumable ? "소모품" : "재료";
        }

        private static string Bonus(ItemData d)
        {
            string s = "";
            if (d.critChanceBonus != 0f) s += "  치명 +" + (d.critChanceBonus * 100f).ToString("0.#") + "%";
            if (d.critDamageBonus != 0f) s += "  치명피해 +" + (d.critDamageBonus * 100f).ToString("0.#") + "%";
            return s;
        }
    }

    [Header("연결")]
    [Tooltip("알림창 전체. 평소엔 꺼져 있어야 한다.")]
    [SerializeField] private GameObject popupRoot;

    [Tooltip("방금 주운 아이템 줄.")]
    [SerializeField] private Row pickedRow = new Row();

    [Tooltip("여러 개를 한 번에 주웠을 때 개수를 보여줄 라벨. 1개면 숨겨진다.")]
    [SerializeField] private UILabel countLabel = new UILabel();

    [Tooltip("'현재 장착' 머리말. 장착한 게 하나도 없으면 같이 숨겨진다.")]
    [SerializeField] private GameObject equippedHeader;

    [Tooltip("현재 장착 중인 장비 줄들. 무기 2칸 / 방어구 2칸이면 2개면 충분하다.")]
    [SerializeField] private Row[] equippedRows = new Row[2];

    [Tooltip("장착한 게 하나도 없을 때 보여줄 문구.")]
    [SerializeField] private UILabel emptyLabel = new UILabel();

    [Header("동작")]
    [Tooltip("알림창이 떠 있는 시간(초). 게임이 멈춰 있어도 흘러야 해서 실제 시간 기준이다.")]
    [SerializeField] private float showDuration = 3.5f;

    [Tooltip("장비가 아닌 아이템(소모품/재료)을 주웠을 때도 띄울지.")]
    [SerializeField] private bool showForNonEquipment = true;

    private PlayerInventory inventory;
    private float hideAt;

    private void Awake()
    {
        if (popupRoot != null) popupRoot.SetActive(false);
    }

    private void OnDisable() => Unbind();

    private void Update()
    {
        Rebind();

        if (popupRoot != null && popupRoot.activeSelf && Time.unscaledTime >= hideAt)
            popupRoot.SetActive(false);
    }

    /// <summary>플레이어가 씬을 넘어가며 새로 생기기도 해서, 바뀌었으면 다시 구독한다.</summary>
    private void Rebind()
    {
        PlayerInventory found = PlayerInventory.Instance != null
            ? PlayerInventory.Instance
            : FindFirstObjectByType<PlayerInventory>();

        if (found == inventory) return;

        Unbind();
        inventory = found;
        if (inventory != null) inventory.ItemPickedUp += HandlePickup;
    }

    private void Unbind()
    {
        if (inventory == null) return;
        inventory.ItemPickedUp -= HandlePickup;
        inventory = null;
    }

    private void HandlePickup(ItemData data, int count)
    {
        if (data == null) return;
        if (!data.IsEquippable && !showForNonEquipment) return;

        if (popupRoot != null) popupRoot.SetActive(true);
        hideAt = Time.unscaledTime + showDuration;

        pickedRow.Show(data);

        if (countLabel.IsAssigned)
        {
            countLabel.SetActive(count > 1);
            if (count > 1) countLabel.Set("x" + count);
        }

        ShowEquipped(data.type);
    }

    /// <summary>주운 것과 같은 종류의 장착칸을 훑어서 지금 뭘 끼고 있는지 보여준다.</summary>
    private void ShowEquipped(ItemType type)
    {
        bool comparable = type == ItemType.Weapon || type == ItemType.Armor;
        int shown = 0;

        if (comparable && inventory != null)
        {
            ItemInstance[] slots = type == ItemType.Weapon ? inventory.Weapons : inventory.Armors;

            for (int i = 0; i < equippedRows.Length; i++)
            {
                ItemInstance equipped = slots != null && i < slots.Length ? slots[i] : null;

                if (equipped != null && equipped.data != null)
                {
                    equippedRows[i].Show(equipped.data);
                    shown++;
                }
                else
                {
                    equippedRows[i].Hide();
                }
            }
        }
        else
        {
            foreach (Row r in equippedRows) r.Hide();
        }

        if (equippedHeader != null) equippedHeader.SetActive(comparable);

        if (emptyLabel.IsAssigned)
        {
            bool showEmpty = comparable && shown == 0;
            emptyLabel.SetActive(showEmpty);
            if (showEmpty) emptyLabel.Set("장착한 장비 없음");
        }
    }
}
