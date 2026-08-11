using UnityEngine;
using UnityEngine.InputSystem;
public class ClickToStartManager : MonoBehaviour
{
    public GameObject clickAnywhereText;
    public CanvasGroup buttonsGroup;
    public float fadeInDuration = 1.5f;

    [Header("Đường cong tùy chỉnh tốc độ fade")]
    public AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private bool hasClicked = false;

    void Update()
    {
        if (!hasClicked && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            hasClicked = true;
            StartSequence();
        }
    }

    void StartSequence()
    {
        clickAnywhereText.SetActive(false);
        StartCoroutine(FadeInButtons());
    }

    System.Collections.IEnumerator FadeInButtons()
    {
        float elapsed = 0f;
        buttonsGroup.interactable = false;
        buttonsGroup.blocksRaycasts = true;

        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeInDuration);

            // Đánh giá đường cong tại t để ra alpha - bạn tự vẽ tay đường cong này
            buttonsGroup.alpha = fadeCurve.Evaluate(t);

            yield return null;
        }

        buttonsGroup.alpha = 1f;
        buttonsGroup.interactable = true;
    }
}
