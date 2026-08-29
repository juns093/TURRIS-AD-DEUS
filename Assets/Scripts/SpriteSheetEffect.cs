using UnityEngine;

/// <summary>
/// 디자이너가 준 스프라이트 시트(연속 프레임)를 그대로 재생하는 가벼운 이펙트 플레이어.
/// Animator/AnimationClip 없이 Sprite 배열만 꽂으면 되므로 이펙트가 늘어나도 세팅이 빠르다.
///
/// 사용법
///  1) 빈 GameObject + SpriteRenderer + 이 스크립트 -> 프리팹으로 저장
///  2) Frames 에 슬라이스한 스프라이트를 순서대로 드래그
///  3) 1회 재생 이펙트면 Loop 끄기, 발밑 먼지처럼 계속 도는 거면 Loop 켜기
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteSheetEffect : MonoBehaviour
{
    [Header("프레임")]
    [Tooltip("스프라이트 시트를 Sprite Mode: Multiple 로 슬라이스한 뒤, 순서대로 드래그해서 넣는다.")]
    [SerializeField] private Sprite[] frames;

    [Tooltip("초당 프레임 수. 12~24 사이가 보통 자연스럽다.")]
    [SerializeField] private float fps = 20f;

    [Tooltip("켜면 무한 반복. 발밑 먼지 루프처럼 계속 도는 이펙트에 사용.")]
    [SerializeField] private bool loop = false;

    [Header("끝났을 때")]
    [Tooltip("켜면 Destroy, 끄면 SetActive(false)로 꺼두고 재사용(오브젝트 풀링). 풀링을 쓰면 꺼두는 쪽이 좋다.")]
    [SerializeField] private bool destroyOnFinish = false;

    [Header("옵션")]
    [Tooltip("재생 시작할 때마다 크기를 살짝 랜덤하게 흔든다. 0이면 사용 안 함.")]
    [SerializeField] private float randomScaleJitter = 0f;

    [Tooltip("재생 시작할 때마다 Z축 회전을 랜덤하게 준다(도). 0이면 사용 안 함.")]
    [SerializeField] private float randomRotation = 0f;

    private SpriteRenderer sr;
    private float timer;
    private int index;
    private bool playing;
    private Vector3 baseScale;

    /// <summary>지금 재생 중인지. 풀에서 놀고 있는 인스턴스를 찾을 때 쓴다.</summary>
    public bool IsPlaying => playing;

    private void Awake()
    {
        CacheRenderer();
        baseScale = transform.localScale;
    }

    private void CacheRenderer()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
    }

    // 풀에서 SetActive(true) 하면 자동으로 처음부터 다시 재생된다.
    private void OnEnable() => Play();

    /// <summary>처음 프레임부터 다시 재생.</summary>
    public void Play()
    {
        CacheRenderer();

        if (frames == null || frames.Length == 0)
        {
            playing = false;
            return;
        }

        if (baseScale == Vector3.zero) baseScale = Vector3.one;

        index = 0;
        timer = 0f;
        playing = true;
        sr.sprite = frames[0];
        sr.enabled = true;

        if (randomScaleJitter > 0f)
            transform.localScale = baseScale * (1f + Random.Range(-randomScaleJitter, randomScaleJitter));

        if (randomRotation > 0f)
            transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(-randomRotation, randomRotation));
    }

    /// <summary>좌우 반전. 플레이어가 바라보는 방향에 맞출 때 사용.</summary>
    public void SetFlip(bool flipX)
    {
        CacheRenderer();
        sr.flipX = flipX;
    }

    /// <summary>플레이어 스프라이트보다 앞/뒤로 그릴지 정한다.</summary>
    public void SetSorting(string layerName, int order)
    {
        CacheRenderer();
        if (!string.IsNullOrEmpty(layerName)) sr.sortingLayerName = layerName;
        sr.sortingOrder = order;
    }

    private void Update()
    {
        if (!playing) return;

        float frameTime = 1f / Mathf.Max(0.01f, fps);
        timer += Time.deltaTime;

        // 프레임 드랍이 나도 재생 속도가 느려지지 않도록 while로 밀린 프레임을 따라잡는다.
        while (timer >= frameTime)
        {
            timer -= frameTime;
            index++;

            if (index >= frames.Length)
            {
                if (loop) index = 0;
                else { Finish(); return; }
            }

            sr.sprite = frames[index];
        }
    }

    /// <summary>루프 이펙트를 바깥에서 끌 때 호출.</summary>
    public void Stop() => Finish();

    private void Finish()
    {
        playing = false;

        if (destroyOnFinish) Destroy(gameObject);
        else gameObject.SetActive(false);
    }
}
