using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// 타임라인에 대사를 얹는 전용 트랙.
///
/// 쓰는 법
///   1) Timeline 창에서 + → Dialogue Track 추가
///   2) 왼쪽 바인딩 칸에 TimelineDialogueView 가 붙은 대사창 오브젝트를 드래그
///   3) 트랙 위에서 우클릭 → Add Dialogue Clip 으로 대사를 하나씩 배치
///   4) 클립을 늘리거나 줄여서 그 대사가 떠 있는 시간을 조절
/// </summary>
[TrackColor(0.95f, 0.78f, 0.25f)]
[TrackClipType(typeof(DialogueClip))]
[TrackBindingType(typeof(TimelineDialogueView))]
public class DialogueTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<DialogueMixerBehaviour>.Create(graph, inputCount);
    }
}
