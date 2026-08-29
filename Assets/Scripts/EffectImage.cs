using UnityEngine;

/// <summary>
/// 이미지 딱 한 장짜리 이펙트.
/// 스프라이트 시트 애니메이션 없이, 스폰 -> 수명 동안 페이드/확대/이동 -> 자동 종료 만 담당한다.
///
/// 수명 관리는 전부 이 스크립트 안에서 끝난다.
///  - age 가 lifetime 에 도달하면 스스로 Finish()
///  - Finish() 에서 OnFinished 이벤트를 쏘므로, 풀(PlayerEffects)이 그걸 듣고 회수한다
///  - 풀을 안 쓰고 그냥 Instantiate 해서 던져놔도 destroyOnFinish 를 켜두면 알아서 Destroy 된다
///
/// 프리팹 만드는 법: 빈 GameObject + SpriteRenderer(이펙트 이미지 넣기) + 이 스크립트
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class EffectImage : MonoBehaviour
{
    [Header("이미지")]
    [Tooltip("비워두면 SpriteRenderer에 이미 들어있는 스프라이트를 그대로 쓴다.")]
    [SerializeField] private Sprite overrideSprite;

    [Header("수명 (초)")]
    [Tooltip("스폰부터 사라질 때까지 걸리는 총 시간.")]
    [SerializeField] private float lifetime = 0.35f;

    [Tooltip("나타나는 데 걸리는 시간. 0이면 즉시 최대 알파로 뜬다.")]
    [SerializeField] private float fadeInTime = 0.04f;

    [Tooltip("사라지는 데 걸리는 시간. lifetime 끝에서 이 시간만큼 남았을 때부터 투명해진다.")]
    [SerializeField] private float fadeOutTime = 0.18f;

    [Tooltip("가장 진할 때의 알파.")]
    [SerializeField, Range(0f, 1f)] private float maxAlpha = 1f;

    [Header("살아있는 동안")]
    [Tooltip("초당 이동량. y를 양수로 주면 먼지가 위로 떠오르는 느낌이 난다. x는 바라보는 방향 기준.")]
    [SerializeField] private Vector2 drift = new Vector2(-0.3f, 0.4f);

    [Tooltip("수명 진행도(0~1)에 따른 크기 배율. 작게 시작해서 커지며 사라지는 게 기본값.")]
    [SerializeField]
    private AnimationCurve scaleOverLife = new AnimationCurve(
        new Keyframe(0f, 0.8f),
        new Keyframe(1f, 1.35f)
    );

    [Header("스폰할 때 랜덤")]
    [Tooltip("크기를 이 비율만큼 랜덤하게 흔든다. 0.12면 ±12%. 같은 이미지가 반복돼도 덜 지루해진다.")]
    [SerializeField, Range(0f, 0.5f)] private float scaleJitter = 0.12f;

    [Tooltip("Z축 회전을 ±이 각도만큼 랜덤하게 준다.")]
    [SerializeField] private float rotationJitter = 20f;

    [Header("끝났을 때")]
    [Tooltip("켜면 Destroy, 끄면 SetActive(false)로 꺼서 재사용. 풀을 쓰는 PlayerEffects와 함께 쓸 땐 꺼둘 것.")]
    [SerializeField] private bool destroyOnFinish = false;

    /// <summary>수명이 다해 종료됐을 때 1회 발생. 풀이 이걸 듣고 인스턴스를 회수한다.</summary>
    public event System.Action<EffectImage> OnFinished;

    /// <summary>지금 화면에 떠 있는 중인지.</summary>
    public bool IsAlive => alive;

    private SpriteRenderer sr;
    private Vector3 baseScale;
    private float age;
    private float spawnScaleMul = 1f;
    private bool alive;

    private bool initialized;

    private void Awake()
    {
        // 풀이 Spawn 전에 수동으로 부를 수도 있어서, 두 번 돌아도 baseScale이 오염되지 않게 막는다.
        if (initialized) return;
        initialized = true;

        sr = GetComponent<SpriteRenderer>();
        baseScale = transform.localScale;
        if (baseScale == Vector3.zero) baseScale = Vector3.one;

        if (overrideSprite != null) sr.sprite = overrideSprite;

        alive = false;
    }

    /// <summary>
    /// 지정한 위치에 이펙트를 띄운다. 이 시점부터 수명 카운트가 시작된다.
    /// </summary>
    public void Spawn(Vector3 position, bool flipX)
    {
        if (sr == null) Awake();

        transform.position = position;
        transform.rotation = rotationJitter > 0f
            ? Quaternion.Euler(0f, 0f, Random.Range(-rotationJitter, rotationJitter))
            : Quaternion.identity;

        spawnScaleMul = scaleJitter > 0f
            ? 1f + Random.Range(-scaleJitter, scaleJitter)
            : 1f;

        sr.flipX = flipX;

        age = 0f;
        alive = true;

        ApplyFrame();                  // 첫 프레임부터 올바른 크기/알파로 보이게
        gameObject.SetActive(true);
    }

    /// <summary>플레이어 스프라이트보다 앞/뒤 어디에 그릴지.</summary>
    public void SetSorting(string layerName, int order)
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (!string.IsNullOrEmpty(layerName)) sr.sortingLayerName = layerName;
        sr.sortingOrder = order;
    }

    private void Update()
    {
        if (!alive) return;

        age += Time.deltaTime;

        if (age >= lifetime)
        {
            Finish();
            return;
        }

        // 이동은 프레임 단위 누적
        if (drift != Vector2.zero)
        {
            float dirX = sr != null && sr.flipX ? -1f : 1f;
            transform.position += new Vector3(drift.x * dirX, drift.y, 0f) * Time.deltaTime;
        }

        ApplyFrame();
    }

    /// <summary>현재 나이에 맞는 크기와 알파를 계산해서 적용.</summary>
    private void ApplyFrame()
    {
        float life = Mathf.Max(0.0001f, lifetime);
        float t = Mathf.Clamp01(age / life);

        transform.localScale = baseScale * (spawnScaleMul * scaleOverLife.Evaluate(t));

        float a = maxAlpha;

        // 나타나는 구간
        if (fadeInTime > 0f && age < fadeInTime)
            a *= age / fadeInTime;

        // 사라지는 구간
        float remain = life - age;
        if (fadeOutTime > 0f && remain < fadeOutTime)
            a *= Mathf.Max(0f, remain) / fadeOutTime;

        Color c = sr.color;
        c.a = a;
        sr.color = c;
    }

    /// <summary>수명을 기다리지 않고 바로 끝내고 싶을 때.</summary>
    public void Kill() => Finish();

    private void Finish()
    {
        if (!alive) return;
        alive = false;

        OnFinished?.Invoke(this); // 풀이 먼저 회수하도록 알린 뒤에 끈다

        if (destroyOnFinish) Destroy(gameObject);
        else gameObject.SetActive(false);
    }

    // 씬 전환 등으로 강제로 꺼질 때도 풀이 카운트를 잃지 않도록
    private void OnDisable()
    {
        if (!alive) return;
        alive = false;
        OnFinished?.Invoke(this);
    }
}
