using UnityEngine;

/// <summary>
/// 플레이어의 실제 이동 속도(Rigidbody2D.linearVelocity)에 맞춰 배경 텍스처를 좌우로 스크롤하고,
/// 카메라의 수직(Y) 이동을 이 레이어가 따라갈지 말지를 레이어별로 토글할 수 있다.
///
/// [좌우 스크롤]
/// 걷기/달리기 속도 차이, 정지, 방향 전환까지 전부 플레이어의 실제 물리 속도를 그대로
/// 반영하기 때문에 "지금 걷는 중인지"를 따로 판정할 필요가 없다.
///
/// [수직 추적 토글]
/// 점프해서 카메라가 위로 움직여도, 건물/땅처럼 "제자리에 있어야 하는" 배경은
/// followCameraVertically를 꺼두면 원래 높이에 그대로 고정된다.
/// 반대로 하늘/먼 산처럼 원근감을 살리고 싶은 레이어는 켜두고
/// verticalParallaxFactor로 카메라를 얼마나 따라갈지(0~1) 조절하면 된다.
///
/// 사용법:
///  1) 배경 레이어마다 오브젝트에 이 스크립트를 붙인다 (레이어 개수만큼 여러 개 사용 가능)
///  2) meshRenderer: 이 레이어의 MeshRenderer 연결
///  3) playerRb: Player 오브젝트의 Rigidbody2D 연결
///  4) cameraTransform: 씬의 메인 카메라(또는 CinemachineBrain이 붙은 카메라) 연결. 비워두면 Camera.main 자동 사용
///  5) 레이어 성격에 맞게 parallaxFactor(좌우), followCameraVertically + verticalParallaxFactor(상하) 조절
/// </summary>
public class BackGroundScroll : MonoBehaviour
{
    [Header("스크롤 대상")]
    [SerializeField] private MeshRenderer meshRenderer;

    [Header("따라갈 플레이어")]
    [Tooltip("Player 오브젝트의 Rigidbody2D. 여기서 실제 이동 속도를 읽어온다.")]
    [SerializeField] private Rigidbody2D playerRb;

    [Tooltip("켜면 Rigidbody 속도 대신 플레이어가 실제로 이동한 거리로 스크롤을 계산한다.\n" +
             "컷신(타임라인)은 물리가 아니라 Transform 을 직접 움직이기 때문에 Rigidbody 속도가 0이다.\n" +
             "그래서 이걸 꺼두면 컷신 동안 배경이 멈춰 보인다. 평소 조작에서는 두 방식의 결과가 같다.")]
    [SerializeField] private bool useActualMovement = true;

    [Tooltip("한 프레임에 이만큼(월드 유닛) 넘게 움직였으면 순간이동으로 보고 무시한다.\n" +
             "씬 이동이나 리스폰으로 좌표가 확 바뀔 때 배경이 튀는 것을 막는다.")]
    [SerializeField] private float teleportThreshold = 5f;

    [Header("좌우 원근감 (패럴랙스)")]
    [Tooltip("1 = 플레이어와 같은 속도로 스크롤 (가장 앞쪽 레이어).\n0에 가까울수록 멀리 있는 배경처럼 느리게 스크롤 (하늘, 먼 산 등)")]
    [Range(0f, 1f)]
    [SerializeField] private float parallaxFactor = 0.5f;

    [Tooltip("플레이어 속도(유닛/초)를 텍스처 오프셋 단위로 바꿔주는 배율. 값이 작을수록 전체적으로 느려짐")]
    [SerializeField] private float speedToScrollRatio = 0.05f;

    [Tooltip("체크하면 좌우 스크롤 방향이 반대로 뒤집힘")]
    [SerializeField] private bool invertDirection = false;

    [Header("카메라 추적 (공통)")]
    [Tooltip("비워두면 Camera.main을 자동으로 사용")]
    [SerializeField] private Transform cameraTransform;

    [Header("수평(X) 카메라 추적")]
    [Tooltip("켜면 카메라가 좌우로 움직일 때(플레이어를 따라갈 때) 이 레이어도 같이 움직여서 화면 밖으로 안 벗어남.\n끄면 카메라가 아무리 옆으로 가도 이 레이어는 처음 배치된 X 위치에 그대로 고정됨.")]
    [SerializeField] private bool followCameraHorizontally = true;

    [Tooltip("카메라 X 이동량 중 몇 %를 따라갈지. 1 = 카메라와 완전히 같이 움직임(화면 밖으로 안 나감), 0.3 = 살짝만 따라가서 원근감 표현(먼 배경일수록 작게)")]
    [Range(0f, 1f)]
    [SerializeField] private float horizontalParallaxFactor = 1f;

    [Header("수직(Y) 카메라 추적")]
    [Tooltip("켜면 카메라가 위/아래로 움직일 때 이 레이어도 같이 움직임 (하늘 등).\n끄면 카메라가 점프 등으로 움직여도 이 레이어는 원래 높이에 고정됨 (건물, 땅 등).")]
    [SerializeField] private bool followCameraVertically = false;

    [Tooltip("카메라 Y 이동량 중 몇 %를 따라갈지. 1 = 카메라와 완전히 같이 움직임, 0.3 = 살짝만 따라가서 원근감 표현")]
    [Range(0f, 1f)]
    [SerializeField] private float verticalParallaxFactor = 1f;

    private Material mat;
    private float initialX;
    private float initialY;
    private float initialCameraX;
    private float initialCameraY;
    private bool hasCameraRef;

    private float lastPlayerX;
    private bool hasLastPlayerX;

    private void Awake()
    {
        if (meshRenderer != null)
            mat = meshRenderer.material; // 인스턴스화해서 같은 머티리얼을 쓰는 다른 오브젝트에 영향 안 주게 함

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        initialX = transform.position.x;
        initialY = transform.position.y;

        if (cameraTransform != null)
        {
            initialCameraX = cameraTransform.position.x;
            initialCameraY = cameraTransform.position.y;
            hasCameraRef = true;
        }
    }

    private void Update()
    {
        // ── 좌우 스크롤 (텍스처 UV) ──
        if (mat == null || playerRb == null) return;

        // 이번 프레임에 플레이어가 실제로 움직인 거리.
        // 원래는 속도 x 시간으로 구했는데, 그러면 컷신처럼 물리를 안 쓰고 Transform 만
        // 움직이는 구간에서 속도가 0으로 잡혀 배경이 멈춰버린다.
        // 위치 변화량을 직접 재면 어느 쪽으로 움직였든 똑같이 반응한다.
        float moved;

        if (useActualMovement)
        {
            float x = playerRb.transform.position.x;

            if (!hasLastPlayerX)
            {
                lastPlayerX = x;
                hasLastPlayerX = true;
                return; // 첫 프레임은 기준만 잡고 넘어간다
            }

            moved = x - lastPlayerX;
            lastPlayerX = x;

            // 순간이동은 이동으로 치지 않는다
            if (Mathf.Abs(moved) > teleportThreshold) return;
        }
        else
        {
            moved = playerRb.linearVelocity.x * Time.deltaTime;
        }

        float direction = invertDirection ? -1f : 1f;
        float scrollAmount = moved * parallaxFactor * speedToScrollRatio * direction;

        Vector2 offset = mat.mainTextureOffset;
        offset.x += scrollAmount;
        mat.mainTextureOffset = offset;
    }

    private void LateUpdate()
    {
        // 카메라가 이번 프레임에 다 움직인 뒤(Cinemachine은 LateUpdate에서 카메라를 움직임)
        // 그 값을 읽어서 따라갈지 말지 결정. followCameraVertically가 꺼져있으면
        // 아예 원래 높이(initialY)에 그대로 고정.
        if (!hasCameraRef) return;

        Vector3 pos = transform.position;

        if (followCameraHorizontally)
        {
            float camDeltaX = cameraTransform.position.x - initialCameraX;
            pos.x = initialX + camDeltaX * horizontalParallaxFactor;
        }
        else
        {
            pos.x = initialX;
        }

        if (followCameraVertically)
        {
            float camDeltaY = cameraTransform.position.y - initialCameraY;
            pos.y = initialY + camDeltaY * verticalParallaxFactor;
        }
        else
        {
            pos.y = initialY;
        }

        transform.position = pos;
    }
}