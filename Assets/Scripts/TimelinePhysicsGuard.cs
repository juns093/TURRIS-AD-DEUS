using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// 타임라인(컷신)이 캐릭터의 Transform 을 직접 움직이는 동안, 그 캐릭터의 물리 몸이
/// 혼자 중력을 먹고 속도를 쌓아두는 것을 막는다.
///
/// 왜 필요한가
///   Animation Track 은 매 프레임 Transform 을 자기가 원하는 위치로 덮어쓴다.
///   그래서 화면상 캐릭터는 제자리에 서 있는 것처럼 보이지만, Rigidbody2D 는 그와 별개로
///   계속 아래로 당겨지면서 낙하 속도만 쌓는다. (위치는 매 프레임 되돌려지니 티가 안 난다)
///   컷신이 끝나 Transform 통제가 풀리는 순간, 그동안 쌓인 어마어마한 속도가 한꺼번에
///   적용되면서 캐릭터가 바닥을 뚫고 사라진다.
///
/// 이 스크립트는 컷신이 도는 동안 중력을 꺼두고 수직 속도를 0으로 눌러놓았다가,
/// 끝나는 순간 물리 위치를 실제로 보이는 위치에 맞춰주고 속도를 지운 뒤 중력을 되돌린다.
///
/// 붙이는 법: PlayableDirector 가 있는 오브젝트에 붙이고 Body 에 캐릭터의 Rigidbody2D 를 연결한다.
/// </summary>
[RequireComponent(typeof(PlayableDirector))]
public class TimelinePhysicsGuard : MonoBehaviour
{
    [Header("연결")]
    [Tooltip("컷신 동안 물리를 눌러둘 대상. 비워두면 Player 태그를 가진 오브젝트에서 찾는다.")]
    [SerializeField] private Rigidbody2D body;

    [Header("동작")]
    [Tooltip("좌우 속도까지 0으로 눌러둘지. 컷신에서 캐릭터가 좌우로 움직여야 하면 꺼둘 것.\n" +
             "문제가 되는 건 중력이 쌓이는 수직 속도라서, 보통은 꺼둬도 된다.")]
    [SerializeField] private bool freezeHorizontal;

    [Tooltip("컷신이 끝날 때 물리 위치를 화면에 보이는 Transform 위치로 맞춘다.\n" +
             "이걸 꺼두면 컷신 중 어긋난 위치로 순간이동하듯 튄다.")]
    [SerializeField] private bool syncPositionOnEnd = true;

    private PlayableDirector director;
    private float savedGravityScale;
    private bool guarding;

    private void Awake()
    {
        director = GetComponent<PlayableDirector>();

        if (body == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) body = player.GetComponent<Rigidbody2D>();
        }

        if (body == null)
            Debug.LogWarning("[TimelinePhysicsGuard] Rigidbody2D 를 찾지 못했습니다. Body 를 직접 연결하세요.", this);
    }

    /// <summary>
    /// 재생 상태를 이벤트가 아니라 매 물리 스텝마다 직접 확인한다.
    /// Play On Awake 로 시작하는 타임라인은 이 스크립트가 이벤트를 구독하기 전에
    /// 이미 재생을 시작해버려서, 이벤트만 믿으면 첫 컷신을 놓친다.
    /// </summary>
    private void FixedUpdate()
    {
        if (body == null || director == null) return;

        bool playing = director.state == PlayState.Playing;

        if (playing && !guarding) Begin();
        else if (!playing && guarding) End();

        if (!guarding) return;

        // 중력을 껐어도 다른 스크립트가 속도를 넣을 수 있으니 매 스텝 눌러둔다.
        Vector2 v = body.linearVelocity;
        v.y = 0f;
        if (freezeHorizontal) v.x = 0f;
        body.linearVelocity = v;
    }

    private void Begin()
    {
        guarding = true;
        savedGravityScale = body.gravityScale;

        body.gravityScale = 0f;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
    }

    private void End()
    {
        guarding = false;

        // 씬 언로드나 플레이 종료 때는 OnDisable 이 불리는 시점에 이미 Rigidbody 가 파괴돼 있을 수 있다.
        // FixedUpdate 에는 가드가 있지만 OnDisable 경로에는 없어서 MissingReferenceException 이 났다.
        if (body == null) return;

        // 컷신 동안 어긋나 있던 물리 위치를 실제로 보이는 위치로 데려온다.
        if (syncPositionOnEnd) body.position = body.transform.position;

        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        body.gravityScale = savedGravityScale;
    }

    /// <summary>컷신 도중에 이 오브젝트가 꺼져도 중력이 꺼진 채로 남지 않게 한다.</summary>
    private void OnDisable()
    {
        if (guarding) End();
    }
}
