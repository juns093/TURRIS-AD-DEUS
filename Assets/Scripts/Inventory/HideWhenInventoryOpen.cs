using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 인벤토리가 열려 있는 동안 숨길 UI 루트에 붙인다. (HUD 캔버스, 스킬바, 미니맵 등)
///
/// 왜 필요한가
///   인벤토리 배경은 "열린 순간의 화면"을 찍어서 흐리게 깐다. 그래서 HUD 가 켜진 채로 찍히면
///   흐려진 HUD 가 배경에 그대로 남아 지저분해 보인다.
///   이 스크립트를 붙여두면 화면을 찍기 "전에" 먼저 꺼지므로 배경에 아예 안 찍히고,
///   인벤토리를 닫으면 다시 켜진다.
///
/// 끄는 방식
///   GameObject.SetActive 는 쓰지 않는다. 꺼지는 순간 OnDisable 로 등록이 풀려서
///   다시 켜줄 방법이 없어지기 때문이다. 대신 CanvasGroup 의 알파를 0으로 만들거나
///   Canvas 컴포넌트를 꺼서 "그리기만" 멈춘다. 스크립트는 계속 돌아간다.
///
/// 붙이는 곳: 숨기고 싶은 UI 의 루트 (예: CanvasPlayer)
/// </summary>
[DisallowMultipleComponent]
public class HideWhenInventoryOpen : MonoBehaviour
{
    public enum HideMode
    {
        /// <summary>CanvasGroup 알파를 0으로. 자식 캔버스까지 확실히 같이 숨는다. (권장)</summary>
        CanvasGroup,

        /// <summary>Canvas 컴포넌트를 끈다. 가장 싸지만 이 오브젝트에 Canvas 가 있어야 한다.</summary>
        DisableCanvas,
    }

    [Tooltip("숨기는 방식. 잘 모르겠으면 CanvasGroup 그대로 두면 된다.")]
    [SerializeField] private HideMode hideMode = HideMode.CanvasGroup;

    [Tooltip("숨어 있는 동안에도 클릭을 계속 받게 할지. 보통은 꺼둔다.")]
    [SerializeField] private bool keepRaycastsWhileHidden = false;

    private static readonly List<HideWhenInventoryOpen> Registered = new List<HideWhenInventoryOpen>();

    private CanvasGroup group;
    private Canvas canvas;

    private float savedAlpha = 1f;
    private bool savedBlocksRaycasts = true;
    private bool savedInteractable = true;
    private bool hidden;

    /// <summary>등록된 UI 를 한꺼번에 켜거나 끈다. InventoryUI 가 불러준다.</summary>
    public static void SetAllVisible(bool visible)
    {
        // 순회 도중 씬 언로드 등으로 목록이 바뀔 수 있으니 뒤에서부터 돈다.
        for (int i = Registered.Count - 1; i >= 0; i--)
        {
            HideWhenInventoryOpen entry = Registered[i];

            if (entry == null)
            {
                Registered.RemoveAt(i);
                continue;
            }

            entry.SetVisible(visible);
        }
    }

    private void Awake()
    {
        canvas = GetComponent<Canvas>();

        if (hideMode == HideMode.DisableCanvas && canvas == null)
        {
            Debug.LogWarning($"[HideWhenInventoryOpen] '{name}' 에 Canvas 가 없어서 CanvasGroup 방식으로 바꿉니다.", this);
            hideMode = HideMode.CanvasGroup;
        }

        if (hideMode == HideMode.CanvasGroup)
        {
            group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void OnEnable()
    {
        if (!Registered.Contains(this)) Registered.Add(this);

        // 인벤토리가 이미 열려 있는데 씬이 바뀌면서 새 HUD 가 올라온 경우.
        // 그대로 두면 흐린 배경 위에 선명한 HUD 가 겹쳐 보인다.
        bool inventoryOpen = InventoryUI.Instance != null && InventoryUI.Instance.OtherUIHidden;
        SetVisible(!inventoryOpen);
    }

    private void OnDisable()
    {
        Registered.Remove(this);

        // 등록이 풀린 뒤에 숨은 채로 남으면 다시 켜줄 사람이 없다. 원상복구하고 나간다.
        SetVisible(true);
    }

    private void SetVisible(bool visible)
    {
        if (hidden == !visible) return;

        if (!visible) Save();

        switch (hideMode)
        {
            case HideMode.DisableCanvas:
                if (canvas != null) canvas.enabled = visible;
                break;

            default:
                if (group != null)
                {
                    group.alpha = visible ? savedAlpha : 0f;
                    group.blocksRaycasts = visible || keepRaycastsWhileHidden ? savedBlocksRaycasts : false;
                    group.interactable = visible || keepRaycastsWhileHidden ? savedInteractable : false;
                }
                break;
        }

        hidden = !visible;
    }

    /// <summary>숨기기 직전의 값을 기억해 둔다. 원래 반투명하게 쓰던 UI 를 1로 되돌리지 않기 위해서다.</summary>
    private void Save()
    {
        if (group == null) return;

        savedAlpha = group.alpha;
        savedBlocksRaycasts = group.blocksRaycasts;
        savedInteractable = group.interactable;
    }
}
