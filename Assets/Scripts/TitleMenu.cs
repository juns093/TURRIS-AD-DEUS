using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 타이틀 화면의 버튼 동작. Play 는 게임을 시작하고 Quit 은 게임을 끈다.
///
/// 붙이는 법: 타이틀 캔버스에 붙이고, 버튼의 OnClick 에
///   Play -> TitleMenu.PlayGame
///   Quit -> TitleMenu.QuitGame
/// 을 연결한다.
/// </summary>
public class TitleMenu : MonoBehaviour
{
    [Header("시작할 씬")]
    [Tooltip("Play 를 눌렀을 때 넘어갈 씬 이름. Build Settings 에 등록돼 있어야 한다.")]
    [SerializeField] private string playSceneName = "tutorialScene";

    private void Start()
    {
        // 인벤토리를 열어둔 채로 타이틀에 돌아오는 등, 시간이 멈춘 상태로 넘어오는 경우가 있다.
        // 그대로 두면 버튼은 눌리는데 다음 씬이 통째로 정지한 채 시작한다.
        Time.timeScale = 1f;

        // 타이틀에서는 항상 커서가 보여야 한다.
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    /// <summary>Play 버튼. 게임 씬으로 넘어간다.</summary>
    public void PlayGame()
    {
        if (string.IsNullOrEmpty(playSceneName))
        {
            Debug.LogError("[TitleMenu] 시작할 씬 이름이 비어 있습니다.", this);
            return;
        }

        if (!IsSceneInBuild(playSceneName))
        {
            Debug.LogError($"[TitleMenu] '{playSceneName}' 씬이 빌드 목록에 없습니다. " +
                           "File > Build Settings 의 씬 목록에 추가하세요.", this);
            return;
        }

        // 이전 플레이에서 넘어온 스폰 정보가 남아 있으면 엉뚱한 자리에서 시작한다.
        PendingSpawn.SpawnPointId = null;

        SceneManager.LoadScene(playSceneName);
    }

    /// <summary>
    /// 씬 이름이 빌드 목록에 있는지 확인한다.
    ///
    /// Application.CanStreamedLevelBeLoaded 를 쓰면 안 된다.
    /// 그건 빌드된 플레이어 데이터만 보기 때문에 에디터에서는 항상 false 를 돌려주고,
    /// 그대로 믿으면 에디터에서 Play 버튼이 아예 동작하지 않는다.
    /// 빌드 목록을 직접 훑는 이 방식은 에디터와 빌드 양쪽에서 똑같이 동작한다.
    /// </summary>
    private static bool IsSceneInBuild(string sceneName)
    {
        int count = SceneManager.sceneCountInBuildSettings;

        for (int i = 0; i < count; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            if (string.IsNullOrEmpty(path)) continue;

            // "Assets/Scenes/tutorialScene.unity" 에서 파일 이름만 떼어내 비교한다.
            int slash = path.LastIndexOf('/');
            int dot = path.LastIndexOf('.');
            if (dot <= slash) continue;

            string name = path.Substring(slash + 1, dot - slash - 1);
            if (string.Equals(name, sceneName, System.StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    /// <summary>Quit 버튼. 게임을 종료한다.</summary>
    public void QuitGame()
    {
        // 에디터에서는 Application.Quit 이 아무 일도 하지 않는다.
        // 그대로 두면 "버튼이 안 먹는다"고 오해하기 쉬워서 플레이 모드를 직접 꺼준다.
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
