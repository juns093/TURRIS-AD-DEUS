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

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // 이미 다른 씬에서부터 살아남은 플레이어가 있으므로, 이번에 씬에 딸려 들어온 쪽을 없앤다.
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
