/// <summary>
/// 씬을 넘어갈 때 "다음 씬의 어느 스폰포인트에 플레이어를 놓을지"를 임시로 들고 있는 정적 클래스.
/// SceneTransitionDoor가 씬 로드 직전에 값을 세팅하고,
/// SceneEntryManager가 새 씬의 Start()에서 이 값을 읽어 소비한 뒤 비운다.
/// </summary>
public static class PendingSpawn
{
    public static string SpawnPointId;
}
