using System.Collections;
using UnityEngine;

/// <summary>
/// 검은 화면 CanvasGroup을 페이드 인/아웃해서 모드 전환을 부드럽게 가려주는 용도.
/// 씬에 Canvas(Screen Space - Overlay) 하나 만들고 화면 전체를 덮는 검은 Image + 이 스크립트를 붙인 뒤,
/// BossRoomEntrance의 screenFader 필드에 연결한다. (없어도 즉시 전환으로 동작함)
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class ScreenFader : MonoBehaviour
{
    private CanvasGroup group;

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;   
    }

    public IEnumerator FadeOut(float duration)
    {
        yield return Fade(0f, 1f, duration);
        group.blocksRaycasts = true;
    }

    public IEnumerator FadeIn(float duration)
    {
        group.blocksRaycasts = false;
        yield return Fade(1f, 0f, duration);
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            group.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        group.alpha = to;
    }
}
