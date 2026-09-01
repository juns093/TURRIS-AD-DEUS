using UnityEngine;

/// <summary>
/// 정해둔 키(기본 E)를 누르면 무기 두 칸을 맞바꾼다.
/// HUD에는 0번 칸만 보여주므로, 바꾸면 "지금 든 무기"가 교체된 것처럼 보인다.
///
/// 붙이는 법: 플레이어나 HUD 등 씬에 하나만 있으면 되는 오브젝트에 붙인다.
/// </summary>
public class WeaponSwapper : MonoBehaviour
{
    [Header("입력")]
    [Tooltip("무기를 바꿔 드는 키.")]
    [SerializeField] private KeyCode swapKey = KeyCode.E;

    [Header("대상")]
    [Tooltip("비워두면 씬에서 PlayerInventory 를 찾는다. 씬이 바뀌어도 다시 찾는다.")]
    [SerializeField] private PlayerInventory inventory;

    [Tooltip("맞바꿀 두 칸의 번호. 무기 1 = 0, 무기 2 = 1")]
    [SerializeField] private int slotA = 0;
    [SerializeField] private int slotB = 1;

    [Header("연출 (선택)")]
    [Tooltip("교체에 성공했을 때 재생할 소리.")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip swapSfx;

    /// <summary>무기를 바꿔 들었을 때. 효과음이나 UI 반짝임 등을 붙일 수 있다.</summary>
    public event System.Action Swapped;

    private void Update()
    {
        EnsureInventory();

        if (!Input.GetKeyDown(swapKey)) return;

        // 인벤토리를 열어 게임이 멈춰 있는 동안에는 바꾸지 않는다.
        // (그때는 인벤토리 화면에서 직접 드래그해서 바꾸는 게 맞다)
        if (InventoryUI.Instance != null && InventoryUI.Instance.IsOpen) return;

        if (inventory == null) return;
        if (!inventory.SwapEquipped(ItemType.Weapon, slotA, slotB)) return;

        if (audioSource != null && swapSfx != null) audioSource.PlayOneShot(swapSfx);
        Swapped?.Invoke();
    }

    private void EnsureInventory()
    {
        if (inventory != null) return;

        inventory = PlayerInventory.Instance != null
            ? PlayerInventory.Instance
            : FindFirstObjectByType<PlayerInventory>();
    }
}
