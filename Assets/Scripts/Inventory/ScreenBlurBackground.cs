using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 인벤토리를 열 때 그 순간의 화면을 찍어서 블러 처리한 뒤 배경으로 깔아준다.
///
/// 실시간 블러가 아니라 "열린 순간의 정지 화면"을 흐리게 만드는 방식이다.
/// 어차피 인벤토리를 여는 동안은 게임이 멈춰 있으니 결과는 똑같고, 비용은 거의 0이다.
/// </summary>
public class ScreenBlurBackground : MonoBehaviour
{
    [Header("블러")]
    [Tooltip("Nibo/UIBlur 셰이더로 만든 머티리얼. 비워두면 Shader.Find 로 찾고, 그것도 실패하면 다운샘플만으로 흐리게 한다.")]
    [SerializeField] private Material blurMaterial;

    [Tooltip("해상도를 1/N 로 줄여서 블러한다. 클수록 더 흐리고 더 싸다.")]
    [Range(1, 8)] [SerializeField] private int downsample = 4;

    [Tooltip("블러를 몇 번 반복할지. 2~4 정도면 충분하다.")]
    [Range(0, 6)] [SerializeField] private int iterations = 3;

    [Tooltip("반복할수록 커지는 샘플 간격. 1~3 사이.")]
    [SerializeField] private float blurOffset = 1.4f;

    [Header("어둡게")]
    [Tooltip("블러 위에 덧씌울 검은 막의 진하기.")]
    [Range(0f, 1f)] [SerializeField] private float darken = 0.55f;

    [Header("호환")]
    [Tooltip("캡처 결과가 위아래로 뒤집혀 보이면 켠다. (그래픽 API 에 따라 다름)")]
    [SerializeField] private bool flipVertically;

    private RawImage blurImage;
    private Image darkOverlay;
    private RenderTexture captured;
    private RenderTexture blurred;

    /// <summary>UI를 만들 때 InventoryUI 가 불러준다.</summary>
    public void Attach(RawImage target, Image overlay)
    {
        blurImage = target;
        darkOverlay = overlay;

        if (darkOverlay != null)
            darkOverlay.color = new Color(0f, 0f, 0f, darken);

        if (blurMaterial == null)
        {
            Shader s = Shader.Find("Nibo/UIBlur");
            if (s != null) blurMaterial = new Material(s) { hideFlags = HideFlags.HideAndDontSave };
        }
    }

    /// <summary>
    /// 이번 프레임이 다 그려진 뒤 화면을 캡처해서 블러한다.
    /// 반드시 인벤토리 UI를 켜기 "전에" 호출해야 UI가 같이 찍히지 않는다.
    /// </summary>
    public IEnumerator CaptureRoutine()
    {
        yield return new WaitForEndOfFrame();
        Capture();
    }

    public void Capture()
    {
        if (blurImage == null) return;

        int w = Mathf.Max(1, Screen.width);
        int h = Mathf.Max(1, Screen.height);

        EnsureTexture(ref captured, w, h);
        ScreenCapture.CaptureScreenshotIntoRenderTexture(captured);

        int bw = Mathf.Max(1, w / downsample);
        int bh = Mathf.Max(1, h / downsample);
        EnsureTexture(ref blurred, bw, bh);

        // 1) 축소만 해도 이미 상당히 흐려진다 (bilinear)
        Graphics.Blit(captured, blurred);

        // 2) 블러 머티리얼이 있으면 몇 번 더 통과
        if (blurMaterial != null && iterations > 0)
        {
            // 임시 RT 두 장을 핑퐁으로 쓰고, 마지막에 결과를 blurred 로 되돌린다.
            // (blurred 자체를 핑퐁에 끼우면 ReleaseTemporary 가 영구 RT를 반납하려 들어서 터진다)
            RenderTexture a = RenderTexture.GetTemporary(bw, bh, 0, blurred.format);
            RenderTexture b = RenderTexture.GetTemporary(bw, bh, 0, blurred.format);
            a.filterMode = b.filterMode = FilterMode.Bilinear;

            Graphics.Blit(blurred, a);

            for (int i = 0; i < iterations; i++)
            {
                blurMaterial.SetFloat("_Offset", blurOffset * (i + 1));
                Graphics.Blit(a, b, blurMaterial);
                (a, b) = (b, a);
            }

            Graphics.Blit(a, blurred);

            RenderTexture.ReleaseTemporary(a);
            RenderTexture.ReleaseTemporary(b);
        }

        blurImage.texture = blurred;
        blurImage.color = Color.white;

        // ScreenCapture 결과는 그래픽 API 에 따라 상하가 뒤집힌다.
        bool flip = flipVertically ^ SystemInfo.graphicsUVStartsAtTop;
        blurImage.uvRect = flip ? new Rect(0f, 1f, 1f, -1f) : new Rect(0f, 0f, 1f, 1f);
    }

    private static void EnsureTexture(ref RenderTexture rt, int w, int h)
    {
        if (rt != null && rt.width == w && rt.height == h) return;

        if (rt != null) rt.Release();
        rt = new RenderTexture(w, h, 0, RenderTextureFormat.Default)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        rt.Create();
    }

    private void OnDestroy()
    {
        if (captured != null) captured.Release();
        if (blurred != null) blurred.Release();
    }
}
