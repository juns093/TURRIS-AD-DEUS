using System;
using UnityEngine;

/// <summary>
/// 게임 전체의 뷰 모드(사이드뷰 / 탑뷰)를 관리하는 싱글턴.
/// PlayerController, CameraModeSwitcher 등이 이 클래스의 이벤트를 구독해서
/// 모드가 바뀔 때마다 자기 동작을 갈아끼운다.
/// </summary>
public class GameModeManager : MonoBehaviour
{
    public enum GameMode
    {
        SideView,   // 일반 사이드 플랫포머
        TopView     // 보스방 (탑뷰)
    }

    public static GameModeManager Instance { get; private set; }

    [Tooltip("게임 시작 시 기본 모드")]
    [SerializeField] private GameMode startMode = GameMode.SideView;

    public GameMode CurrentMode { get; private set; }

    /// <summary>모드가 바뀔 때 발생. 파라미터는 (이전 모드, 새 모드)</summary>
    public event Action<GameMode, GameMode> OnModeChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // 씬을 여러 개로 나눠서 쓰는 구조이므로, 모드 정보가 씬 전환 간에도 유지되도록 반드시 유지시킨다.
        DontDestroyOnLoad(gameObject);

        CurrentMode = startMode;
    }

    /// <summary>
    /// 외부(BossRoomEntrance 등)에서 호출해서 모드를 전환한다.
    /// </summary>
    public void SetMode(GameMode newMode)
    {
        if (newMode == CurrentMode) return;

        GameMode prev = CurrentMode;
        CurrentMode = newMode;
        OnModeChanged?.Invoke(prev, newMode);
    }

    public bool IsSideView => CurrentMode == GameMode.SideView;
    public bool IsTopView => CurrentMode == GameMode.TopView;
}
