using UnityEngine;

/// <summary>
/// Player 루트 오브젝트에 붙인다.
/// 씬이 바뀌어도 플레이어 오브젝트 자체는 파괴되지 않고 유지되며,
/// 혹시 새로 로드된 씬에 에디터에서 미리 배치해둔 "테스트용 플레이어"가 있다면
/// (씬을 그 씬에서 바로 열어서 테스트할 때를 위함) 그쪽을 파괴해서 중복을 막는다.
/// </summary>
public class PersistentPlayer : MonoBehaviour
{
    public static PersistentPlayer Instance { get; private set; }

    /// <summary>
    /// 이번 씬에 미리 놓여 있다가 중복이라 지워진 플레이어의 위치.
    ///
    /// 스폰포인트가 없는 씬에서 살아남은 플레이어를 어디에 둘지 정하는 데 쓴다.
    /// 이게 없으면 이전 씬의 좌표를 그대로 들고 와서 맵 밖에 떨어진다.
    /// (에디터에서 씬에 손으로 배치해둔 그 자리가 곧 그 씬의 기본 시작 위치라는 뜻)
    /// </summary>
    private static Vector3? sceneSpawnHint;

    /// <summary>기본 시작 위치를 한 번 꺼내 쓰고 비운다. 다음 씬으로 새어나가지 않게 한다.</summary>
    public static Vector3? ConsumeSceneSpawnHint()
    {
        Vector3? v = sceneSpawnHint;
        sceneSpawnHint = null;
        return v;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // 이미 다른 씬에서부터 살아남은 플레이어가 있으므로, 이번에 씬에 딸려 들어온 쪽을 없앤다.
            // 사라지기 전에 "이 씬에서는 여기가 시작 자리였다"는 것만 남겨둔다.
            sceneSpawnHint = transform.position;
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
