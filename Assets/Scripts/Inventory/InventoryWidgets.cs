// MonoBehaviour 가 아닌 공용 조각들만 모아둔 파일.
// 컴포넌트로 붙이는 스크립트(ItemView / EquipSlotView / StashGridView)는
// 유니티 규칙상 "파일 이름 = 클래스 이름" 이어야 해서 각각 별도 파일로 나뉘어 있다.

using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 레거시 Text 와 TextMeshPro 를 둘 다 받아주는 라벨 참조.
/// 둘 중 쓰는 쪽에만 넣으면 된다. (한글은 레거시 Text + 동적 TTF 가 세팅이 가장 간단하다)
/// </summary>
[System.Serializable]
public class UILabel
{
    [SerializeField] private Text legacy;
    [SerializeField] private TMP_Text tmp;

    public bool IsAssigned => legacy != null || tmp != null;

    public void Set(string value)
    {
        if (legacy != null) legacy.text = value;
        if (tmp != null) tmp.text = value;
    }

    public void SetActive(bool on)
    {
        if (legacy != null) legacy.gameObject.SetActive(on);
        if (tmp != null) tmp.gameObject.SetActive(on);
    }
}

/// <summary>드롭을 받아줄 수 있는 UI 영역.</summary>
public interface IItemDropTarget
{
    bool AcceptDrop(ItemView view);
}
