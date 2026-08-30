using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// 타임라인에 올리는 대사 한 덩어리. 클립의 길이가 곧 그 대사가 떠 있는 시간이다.
/// Dialogue Track 에서 우클릭 → Add Dialogue Clip 으로 만든다.
/// </summary>
[System.Serializable]
public class DialogueClip : PlayableAsset, ITimelineClipAsset
{
    [Tooltip("말하는 사람 이름. 비워두면 이름칸이 숨겨진다.")]
    public string speaker = "";

    [TextArea(2, 5)]
    [Tooltip("화면에 띄울 대사.")]
    public string text = "";

    [Header("타자기 효과")]
    [Tooltip("켜면 글자가 하나씩 찍히듯 나타난다. 끄면 클립이 시작되는 순간 전부 보인다.")]
    public bool typewriter = true;

    [Tooltip("클립 길이 중 몇 %에 걸쳐 글자를 다 찍을지.\n" +
             "0.5 면 클립 앞쪽 절반 동안 다 찍고, 남은 절반은 완성된 문장을 보여준다.")]
    [Range(0.05f, 1f)] public float typewriterPortion = 0.5f;

    /// <summary>클립끼리 겹쳐서 블렌딩하는 기능은 쓰지 않는다. 대사는 하나씩 또렷하게 나오는 게 맞다.</summary>
    public ClipCaps clipCaps => ClipCaps.None;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        ScriptPlayable<DialogueBehaviour> playable = ScriptPlayable<DialogueBehaviour>.Create(graph);

        DialogueBehaviour behaviour = playable.GetBehaviour();
        behaviour.speaker = speaker;
        behaviour.text = text;
        behaviour.typewriter = typewriter;
        behaviour.typewriterPortion = typewriterPortion;

        return playable;
    }
}

/// <summary>
/// 클립 하나가 들고 있는 대사 데이터.
/// 실제로 화면에 그리는 일은 트랙 믹서(DialogueMixerBehaviour)가 모아서 처리한다.
/// </summary>
public class DialogueBehaviour : PlayableBehaviour
{
    public string speaker;
    public string text;
    public bool typewriter;
    public float typewriterPortion = 0.5f;

    /// <summary>
    /// 지금 시점에 몇 글자까지 보여줄지 잘라서 돌려준다.
    ///
    /// 코루틴으로 한 글자씩 찍지 않고 재생 위치로 계산하는 이유:
    /// 타임라인은 앞뒤로 스크럽할 수 있어서, 시간에 비례해 잘라내야
    /// 에디터에서 슬라이더를 뒤로 끌었을 때도 글자 수가 정확히 되감긴다.
    /// </summary>
    public string Reveal(double time, double duration)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        if (!typewriter || duration <= 0d) return text;

        float window = Mathf.Max(0.0001f, typewriterPortion);
        double progress = time / (duration * window);

        if (progress >= 1d) return text;
        if (progress <= 0d) return string.Empty;

        int shown = Mathf.Clamp(Mathf.FloorToInt((float)progress * text.Length), 0, text.Length);
        return text.Substring(0, shown);
    }
}
