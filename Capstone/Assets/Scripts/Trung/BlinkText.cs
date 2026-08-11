using TMPro;
using UnityEngine;

public class BlinkText : MonoBehaviour
{
    public TMP_Text targetText;
    public float blinkSpeed = 2f;
    public float minAlpha = 0.25f;
    public float maxAlpha = 1f;

    void Awake()
    {
        if (targetText == null)
            targetText = GetComponent<TMP_Text>();
    }

    void Update()
    {
        if (targetText == null)
            return;

        float alpha = Mathf.Lerp(minAlpha, maxAlpha, Mathf.Abs(Mathf.Sin(Time.time * blinkSpeed)));
        Color color = targetText.color;
        color.a = alpha;
        targetText.color = color;
    }
}
