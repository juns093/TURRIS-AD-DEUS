using UnityEngine;
using UnityEngine.UI;

public class TextFade : MonoBehaviour
{
    public Text text;
    public float fadeSpeed = 2f;
    public float visibleAlpha = 1f;
    public float hiddenAlpha = 0f;

    private bool playerInRange = false;

    private void Start()
    {
        if (text == null)
            text = GetComponent<Text>();

        SetAlpha(hiddenAlpha);  // 시작할 때는 숨겨진 상태
    }

    private void Update()
    { 
       float targetAlpha = playerInRange ? visibleAlpha : hiddenAlpha;
        Color currentColor = text.color;
        float newAlpha = Mathf.Lerp(currentColor.a, targetAlpha, Time.deltaTime * fadeSpeed);
        text.color = new Color(currentColor.r, currentColor.g, currentColor.b, newAlpha);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
        }
    }

    private void SetAlpha(float alpha)
    {
        Color c = text.color;
        text.color = new Color(c.r, c.g, c.b, alpha);
    }
}   
