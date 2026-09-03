using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// HUD 캔버스처럼 "씬이 바뀌어도 계속 떠 있어야 하는 UI" 루트에 붙인다.
/// <see cref="PersistentPlayer"/> 와 같은 방식이지만, UI 는 종류가 여러 개라
/// id 별로 하나씩만 살아남게 관리한다.
///
/// 하는 일
///   - 처음 만들어진 것만 살려서 DontDestroyOnLoad 로 넘긴다.
///   - 나중에 로드된 씬에 같은 id 의 UI 가 또 들어 있으면 (그 씬을 에디터에서 바로 열어
///     테스트하려고 넣어둔 사본 등) 그쪽을 없애서 HUD 가 두 겹으로 겹치는 것을 막는다.
///   - 씬에 EventSystem 이 없으면 하나 만들어준다. UI 가 씬을 넘어 살아남아도
///     클릭/드래그를 받으려면 EventSystem 이 씬마다 있어야 한다.
///
/// 주의: 반드시 하이어라키 최상위 오브젝트에 붙인다. (DontDestroyOnLoad 는 루트에만 걸린다)
/// </summary>
[DisallowMultipleComponent]
public class PersistentUI : MonoBehaviour
{
    [Header("싱글톤")]
    [Tooltip("이 UI 를 구분하는 이름. 같은 id 는 게임 전체에 하나만 남는다.\n" +
             "비워두면 오브젝트 이름을 그대로 쓴다.")]
    [SerializeField] private string id = "";

    [Header("EventSystem")]
    [Tooltip("켜면 씬에 EventSystem 이 없을 때 자동으로 하나 만든다.\n" +
             "이걸 꺼두면 EventSystem 이 없는 씬에서는 UI 클릭/드래그가 전부 먹통이 된다.\n" +
             "만들어진 EventSystem 은 그 씬 것이라 씬이 바뀌면 같이 사라진다. " +
             "그래서 원래 EventSystem 이 있는 씬과 겹칠 일이 없다.")]
    [SerializeField] private bool ensureEventSystem = true;

    private static readonly Dictionary<string, PersistentUI> Alive = new Dictionary<string, PersistentUI>();

    /// <summary>id 로 살아있는 UI 를 찾는다. 없으면 null.</summary>
    public static PersistentUI Find(string id)
    {
        PersistentUI found;
        return Alive.TryGetValue(id, out found) ? found : null;
    }

    public string Id { get { return string.IsNullOrEmpty(id) ? gameObject.name : id; } }

    // 살아남은 쪽인지. 중복이라 지워지는 쪽이 OnDestroy 에서 남의 등록을 지우면 안 된다.
    private bool registered;

    private void Awake()
    {
        string key = Id;

        PersistentUI existing;
        if (Alive.TryGetValue(key, out existing) && existing != null && existing != this)
        {
            // 이미 이전 씬에서 넘어온 것이 있다. 이번 씬에 딸려 들어온 사본을 없앤다.
            Destroy(gameObject);
            return;
        }

        if (transform.parent != null)
        {
            Debug.LogWarning("[PersistentUI] '" + name + "' 은(는) 씬을 넘어 유지하려면 최상위 오브젝트여야 합니다. 부모에서 분리합니다.", this);
            transform.SetParent(null, true);
        }

        Alive[key] = this;
        registered = true;

        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void Start()
    {
        // sceneLoaded 는 "처음부터 열려 있던 씬"에는 안 불린다. 그 몫을 여기서 챙긴다.
        if (registered && ensureEventSystem) EnsureEventSystem();
    }

    private void OnDestroy()
    {
        if (!registered) return;

        SceneManager.sceneLoaded -= HandleSceneLoaded;

        PersistentUI current;
        if (Alive.TryGetValue(Id, out current) && current == this) Alive.Remove(Id);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (ensureEventSystem) EnsureEventSystem();
    }

    /// <summary>
    /// 씬에 EventSystem 이 하나도 없으면 만든다. 이미 있으면 아무것도 하지 않는다.
    /// DontDestroyOnLoad 로 넘기지 않는 이유: 그렇게 하면 자기 EventSystem 을 들고 있는 씬으로
    /// 넘어갔을 때 두 개가 되어 Unity 가 경고를 뱉고 한쪽을 꺼버린다.
    /// </summary>
    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        if (FindFirstObjectByType<EventSystem>() != null) return;

        GameObject go = new GameObject("EventSystem", typeof(EventSystem));

        // 입력 모듈은 프로젝트가 쓰는 입력 방식에 맞춰야 한다.
        // Input System 패키지가 깔려 있는데 StandaloneInputModule 을 붙이면 그대로 에러가 난다.
        System.Type inputSystemModule = System.Type.GetType(
            "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");

        if (inputSystemModule != null) go.AddComponent(inputSystemModule);
        else go.AddComponent<StandaloneInputModule>();

        Debug.Log("[PersistentUI] 이 씬에 EventSystem 이 없어서 하나 만들었습니다.", go);
    }
}
