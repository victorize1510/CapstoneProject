using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class LoadingScreen : MonoBehaviour
{
    public Slider loadingBar;
    public TextMeshProUGUI progressText;
    public string sceneToLoad = "MainMenu";

    [Header("Tùy chỉnh tốc độ chạy mượt")]
    public float fillSpeed = 0.5f; // càng nhỏ càng chạy chậm/mượt

    private float targetProgress = 0f;

    void Start()
    {
        loadingBar.value = 0f;
        StartCoroutine(LoadSceneAsync());
    }

    void Update()
    {
        // Cho thanh loading chạy mượt tới targetProgress thay vì nhảy đột ngột
        if (loadingBar.value < targetProgress)
        {
            loadingBar.value = Mathf.MoveTowards(loadingBar.value, targetProgress, fillSpeed * Time.deltaTime);

            if (progressText != null)
                progressText.text = "Loading... " + Mathf.RoundToInt(loadingBar.value * 100f) + "%";
        }
    }

    System.Collections.IEnumerator LoadSceneAsync()
    {
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneToLoad);
        operation.allowSceneActivation = false;

        while (!operation.isDone)
        {
            float progress = Mathf.Clamp01(operation.progress / 0.9f);
            targetProgress = progress;

            // Khi load thật sự xong (90%) VÀ thanh hiển thị cũng đã đuổi kịp tới gần 100%
            if (operation.progress >= 0.9f && loadingBar.value >= 0.99f)
            {
                targetProgress = 1f;
                loadingBar.value = 1f;

                if (progressText != null)
                    progressText.text = "Loading... 100%";

                yield return new WaitForSeconds(0.3f);
                operation.allowSceneActivation = true;
            }

            yield return null;
        }
    }
}