using UnityEngine;

/// <summary>
/// 배경 레이어를 카메라가 실제로 보는 범위에 맞춰 늘려준다.
///
/// 왜 필요한가
///   직교 카메라는 orthographicSize 로 "세로"만 정하고 가로는 세로 x 화면비로 결정된다.
///   그래서 16:9 를 기준으로 배경을 깔아두면, 울트라와이드(21:9, 32:9) 같은 더 넓은 화면에서는
///   가로로 더 많이 보이면서 배경 바깥의 빈 공간이 그대로 드러난다.
///   (예: 1920x1080 에서 24유닛이 보이던 것이 3440x1440 에서는 35.8유닛까지 보인다)
///
///   해상도는 플레이어 모니터마다 다르므로 에디터에서 미리 맞춰둘 수 없다.
///   실행 중에 카메라가 지금 얼마나 보고 있는지를 읽어서 그때그때 늘리는 수밖에 없다.
///
/// 하는 일
///   - 카메라 가시 범위보다 배경이 작으면 그만큼 키운다. (반대로 줄이지는 않는다)
///   - 늘어난 만큼 텍스처 타일링을 같이 올려서 그림이 옆으로 늘어나 보이지 않게 한다.
///   - 해상도가 바뀌는 순간에만 다시 계산한다. (창 크기 조절, 전체화면 전환 등)
///
/// 붙이는 법: 배경 레이어(Quad + MeshRenderer)에 BackGroundScroll 과 같이 붙인다.
/// 위치는 건드리지 않으므로 BackGroundScroll 의 패럴랙스와 충돌하지 않는다.
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
[DisallowMultipleComponent]
public class BackgroundFitToCamera : MonoBehaviour
{
    [Header("기준 카메라")]
    [Tooltip("비워두면 Camera.main 을 자동으로 쓴다.")]
    [SerializeField] private Camera targetCamera;

    [Header("얼마나 덮을지")]
    [Tooltip("카메라 가시 범위보다 이 비율만큼 더 크게 만든다.\n" +
             "1.1 이면 10% 여유. 카메라가 흔들리거나 살짝 앞서 나갈 때 가장자리가 비지 않게 해준다.")]
    [Range(1f, 2f)] [SerializeField] private float coverMargin = 1.15f;

    [Tooltip("가로를 화면에 맞출지. 울트라와이드 대응은 이쪽이다.")]
    [SerializeField] private bool fitWidth = true;

    [Tooltip("세로를 화면에 맞출지. 4:3 처럼 세로로 긴 화면에서 위아래가 비는 걸 막는다.")]
    [SerializeField] private bool fitHeight = true;

    [Tooltip("켜면 원래 배치해둔 크기보다 작아지지는 않는다.\n" +
             "배경은 커도 문제없지만 작으면 바깥이 보이므로 보통 켜둔다.")]
    [SerializeField] private bool neverShrink = true;

    [Header("텍스처")]
    [Tooltip("가로로 늘린 만큼 텍스처 타일링(x)도 같이 올린다. 그림이 옆으로 늘어나는 걸 막는다.\n" +
             "배경이 좌우로 반복되는 그림이 아니면 꺼둘 것.")]
    [SerializeField] private bool tileHorizontally = true;

    [Tooltip("세로로 늘린 만큼 텍스처 타일링(y)도 같이 올린다.\n" +
             "하늘처럼 위아래로 반복되면 안 되는 그림은 꺼두면 늘어나기만 한다.")]
    [SerializeField] private bool tileVertically = false;

    [Header("적용 범위")]
    [Tooltip("켜면 사이드뷰에서만 크기를 맞춘다. 탑뷰에서는 배치해둔 그대로 둔다.")]
    [SerializeField] private bool sideViewOnly = true;

    private MeshRenderer meshRenderer;
    private Material mat;

    private Vector3 baseScale;      // 에디터에서 배치해둔 원래 크기
    private Vector2 baseTiling;     // 원래 타일링
    private Vector2 meshSize = Vector2.one;

    private int lastScreenW = -1;
    private int lastScreenH = -1;
    private float lastOrthoSize = -1f;

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();

        // BackGroundScroll 도 같은 인스턴스 머티리얼을 쓴다. (.material 은 두 번 불러도 같은 것을 돌려준다)
        // 여기서는 타일링만 건드리고 저쪽은 오프셋만 건드리므로 서로 간섭하지 않는다.
        mat = meshRenderer.material;

        baseScale = transform.localScale;
        baseTiling = mat != null ? mat.mainTextureScale : Vector2.one;

        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            Vector3 b = mf.sharedMesh.bounds.size;
            // 두께가 0인 Quad 라도 x, y 는 항상 있다. 0으로 나누는 것만 막는다.
            meshSize = new Vector2(
                Mathf.Approximately(b.x, 0f) ? 1f : b.x,
                Mathf.Approximately(b.y, 0f) ? 1f : b.y);
        }

        if (targetCamera == null) targetCamera = Camera.main;
    }

    private void OnEnable()
    {
        // 씬이 바뀌면서 카메라가 새로 생겼을 수 있다. 다음 LateUpdate 에서 다시 잡게 한다.
        lastScreenW = -1;
    }

    /// <summary>
    /// LateUpdate 에서 확인하는 이유:
    /// PixelPerfectCamera 와 Cinemachine 이 orthographicSize 를 늦게 덮어쓴다.
    /// Update 에서 읽으면 한 프레임 전 값이라 화면비가 어긋난 채로 계산된다.
    /// </summary>
    private void LateUpdate()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null) return;
        }

        if (sideViewOnly && GameModeManager.Instance != null && !GameModeManager.Instance.IsSideView)
            return;

        // 해상도나 카메라 줌이 바뀐 순간에만 다시 계산한다. 매 프레임 스케일을 쓰면 낭비다.
        if (Screen.width == lastScreenW && Screen.height == lastScreenH
            && Mathf.Approximately(targetCamera.orthographicSize, lastOrthoSize))
            return;

        lastScreenW = Screen.width;
        lastScreenH = Screen.height;
        lastOrthoSize = targetCamera.orthographicSize;

        Fit();
    }

    private void Fit()
    {
        if (!targetCamera.orthographic) return;

        float viewH = targetCamera.orthographicSize * 2f;
        float viewW = viewH * targetCamera.aspect;

        Vector3 scale = transform.localScale;

        if (fitWidth)
        {
            float needed = viewW * coverMargin / meshSize.x;
            scale.x = neverShrink ? Mathf.Max(baseScale.x, needed) : needed;
        }

        if (fitHeight)
        {
            float needed = viewH * coverMargin / meshSize.y;
            scale.y = neverShrink ? Mathf.Max(baseScale.y, needed) : needed;
        }

        transform.localScale = scale;

        if (mat == null) return;

        // 늘린 배율만큼 타일링을 올려야 원래 그림 크기(픽셀 밀도)가 유지된다.
        // 안 그러면 배경이 가로로 쭉 늘어나 흐릿해진다.
        Vector2 tiling = mat.mainTextureScale;

        if (tileHorizontally && !Mathf.Approximately(baseScale.x, 0f))
            tiling.x = baseTiling.x * (scale.x / baseScale.x);

        if (tileVertically && !Mathf.Approximately(baseScale.y, 0f))
            tiling.y = baseTiling.y * (scale.y / baseScale.y);

        mat.mainTextureScale = tiling;
    }
}
