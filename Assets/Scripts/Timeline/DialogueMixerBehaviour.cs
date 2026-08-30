using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// 대사 트랙 전체를 맡아서, 매 프레임 "지금 재생 중인 클립"을 찾아 대사창에 뿌린다.
/// 클립마다 따로 그리지 않고 여기서 한 번에 처리해야, 클립과 클립 사이 빈 구간에서
/// 대사창을 확실히 닫을 수 있다.
/// </summary>
public class DialogueMixerBehaviour : PlayableBehaviour
{
    // 재생이 끝난 뒤 대사창을 닫으려면 대상이 필요한데,
    // 그때는 playerData 를 받을 수 없어서 마지막으로 본 것을 기억해둔다.
    private TimelineDialogueView view;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        view = playerData as TimelineDialogueView;
        if (view == null) return;

        // 에디터에서 스크럽 중인데 미리보기를 껐다면 화면을 건드리지 않는다.
        if (!Application.isPlaying && !view.PreviewInEditor)
        {
            view.Hide();
            return;
        }

        // 가장 비중이 큰 클립 하나만 고른다. 대사는 겹쳐 섞이면 안 되고 하나만 또렷해야 한다.
        int count = playable.GetInputCount();
        int best = -1;
        float bestWeight = 0f;

        for (int i = 0; i < count; i++)
        {
            float weight = playable.GetInputWeight(i);
            if (weight > bestWeight)
            {
                bestWeight = weight;
                best = i;
            }
        }

        if (best < 0)
        {
            view.Hide(); // 클립이 없는 빈 구간
            return;
        }

        ScriptPlayable<DialogueBehaviour> clip = (ScriptPlayable<DialogueBehaviour>)playable.GetInput(best);
        DialogueBehaviour data = clip.GetBehaviour();

        if (data == null || string.IsNullOrEmpty(data.text))
        {
            view.Hide();
            return;
        }

        view.Show(data.speaker, data.Reveal(clip.GetTime(), clip.GetDuration()));
    }

    /// <summary>
    /// 타임라인이 끝나서 그래프가 정리될 때 대사창을 닫는다.
    /// 이걸 빼면 마지막 대사가 화면에 그대로 남는다.
    /// (스크럽 중에는 호출되지 않으므로 에디터 미리보기가 깜빡이지 않는다)
    /// </summary>
    public override void OnPlayableDestroy(Playable playable)
    {
        if (view != null) view.Hide();
        view = null;
    }
}
