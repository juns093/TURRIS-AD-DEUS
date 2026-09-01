using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 지금 장착 중인 장비를 HUD에 항상 띄워두는 표시판.
/// 인벤토리를 열지 않아도 무기/방어구에 뭘 끼고 있는지 한눈에 보인다.
///
/// 값을 이벤트로 받지 않고 매 프레임 읽는 이유:
/// 장착은 UI 드래그, 줍기 자동장착, 스크립트 호출 등 여러 경로로 바뀌는데
/// 그중 일부는 이벤트를 안 거친다. 매 프레임 읽으면 어느 경로로 바뀌든 항상 맞는다.
/// 슬롯 몇 개 갱신하는 비용은 무시할 수준이다.
///
/// 붙이는 법: HUD 캔버스에 붙이고 Slots 에 칸을 등록한다.
/// </summary>
public class EquippedDisplay : MonoBehaviour
{
    /// <summary>표시판의 칸 하나. 어느 장착칸을 비출지 지정한다.</summary>
    [System.Serializable]
    public class Slot
    {
        [Tooltip("이 칸이 비출 장비 종류.")]
        public ItemType type = ItemType.Weapon;

        [Tooltip("같은 종류 안에서 몇 번째 칸인지. 무기 1 = 0, 무기 2 = 1")]
        public int index;

        [Tooltip("장착한 아이템 그림이 들어갈 Image.")]
        public Image icon;

        [Tooltip("비어 있을 때만 보여줄 것(실루엣, 흐린 배경 등). 없어도 된다.")]
        public GameObject emptyMark;
    }

    [Header("연결")]
    [Tooltip("비워두면 PlayerInventory 를 씬에서 찾는다. 씬이 바뀌어도 다시 찾는다.")]
    [SerializeField] private PlayerInventory inventory;

    [Tooltip("표시할 칸들. 무기 0/1, 방어구 0/1 식으로 등록한다.")]
    [SerializeField] private Slot[] slots;

    [Header("표시")]
    [Tooltip("비어 있는 칸의 아이콘 투명도. 0이면 아예 안 보인다.")]
    [Range(0f, 1f)] [SerializeField] private float emptyIconAlpha = 0f;

    private void Update()
    {
        EnsureInventory();
        Refresh();
    }

    private void EnsureInventory()
    {
        if (inventory != null) return;

        inventory = PlayerInventory.Instance != null
            ? PlayerInventory.Instance
            : FindFirstObjectByType<PlayerInventory>();
    }

    private void Refresh()
    {
        if (slots == null) return;

        foreach (Slot s in slots)
        {
            if (s == null) continue;

            ItemData data = GetEquippedData(s.type, s.index);
            bool has = data != null && data.icon != null;

            if (s.icon != null)
            {
                s.icon.sprite = has ? data.icon : null;
                s.icon.preserveAspect = true;

                // enabled 를 끄지 않고 알파로 처리한다.
                // 껐다 켜면 레이아웃이 흔들리는 경우가 있어서 자리는 유지하는 편이 안전하다.
                Color c = s.icon.color;
                c.a = has ? 1f : emptyIconAlpha;
                s.icon.color = c;
            }

            if (s.emptyMark != null) s.emptyMark.SetActive(!has);
        }
    }

    private ItemData GetEquippedData(ItemType type, int index)
    {
        if (inventory == null) return null;

        ItemInstance inst = inventory.GetEquipped(type, index);
        return inst != null ? inst.data : null;
    }
}
