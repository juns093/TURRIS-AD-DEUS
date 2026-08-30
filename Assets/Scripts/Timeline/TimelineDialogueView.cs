using UnityEngine;

/// <summary>
/// 타임라인 대사 트랙이 글자를 뿌려 넣을 대상. 대사창 UI에 붙이고 트랙에 연결한다.
///
/// 붙이는 법
///   DialogueBox            ... 대사창 전체를 담은 오브젝트 + 이 스크립트
///     └ Speaker            ... Text/TMP  -> Speaker Label 에 연결 (없어도 됨)
///     └ Body               ... Text/TMP  -> Body Label 에 연결
///
/// 이 오브젝트를 타임라인의 Dialogue Track 에 드래그해서 바인딩하면 된다.
/// 대사가 없는 구간에서는 자동으로 숨겨진다.
/// </summary>
public class TimelineDialogueView : MonoBehaviour
{
    [Header("연결")]
    [Tooltip("대사창 전체. 비워두면 이 오브젝트 자신을 켜고 끈다.\n" +
             "이 스크립트가 붙은 오브젝트를 그대로 넣으면 스스로 꺼져서 다시 못 켜지니 비워둘 것.")]
    [SerializeField] private GameObject boxRoot;

    [Tooltip("말하는 사람 이름. 레거시 Text 와 TextMeshPro 둘 다 받는다. 안 쓰면 비워둔다.")]
    [SerializeField] private UILabel speakerLabel = new UILabel();

    [Tooltip("대사 본문. 레거시 Text 와 TextMeshPro 둘 다 받는다.")]
    [SerializeField] private UILabel bodyLabel = new UILabel();

    [Header("동작")]
    [Tooltip("에디터에서 타임라인을 스크럽할 때도 대사가 보이게 할지. 보통 켜두면 미리보기가 편하다.")]
    [SerializeField] private bool previewInEditor = true;

    public bool PreviewInEditor => previewInEditor;

    /// <summary>대사창을 켜고 내용을 채운다. 트랙 믹서가 매 프레임 불러준다.</summary>
    public void Show(string speaker, string body)
    {
        SetVisible(true);

        if (speakerLabel.IsAssigned)
        {
            speakerLabel.Set(speaker);
            speakerLabel.SetActive(!string.IsNullOrEmpty(speaker));
        }

        if (bodyLabel.IsAssigned) bodyLabel.Set(body);
    }

    /// <summary>대사 구간이 아닐 때. 트랙 믹서가 불러준다.</summary>
    public void Hide()
    {
        SetVisible(false);
    }

    private void SetVisible(bool on)
    {
        GameObject target = boxRoot != null ? boxRoot : gameObject;

        // 자기 자신을 끄면 다음 프레임에 되살릴 방법이 없다. 그 경우엔 라벨만 비운다.
        if (target == gameObject && boxRoot == null)
        {
            if (!on)
            {
                if (speakerLabel.IsAssigned) speakerLabel.Set(string.Empty);
                if (bodyLabel.IsAssigned) bodyLabel.Set(string.Empty);
            }
            return;
        }

        if (target.activeSelf != on) target.SetActive(on);
    }
}
