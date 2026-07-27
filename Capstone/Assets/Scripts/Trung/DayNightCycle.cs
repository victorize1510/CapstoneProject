using UnityEngine;

public class DayNightCycle : MonoBehaviour
{
    public Light sunLight;
    public Light moonLight;
    public float dayLengthInMinutes = 2f;

    [Range(0, 1)]
    public float timeOfDay = 0.25f;

    [Header("Độ sáng theo giờ")]
    public AnimationCurve sunIntensityCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public float maxSunIntensity = 1.5f;
    public float maxMoonIntensity = 0.3f;
    [Header("Kéo GameObject hình ảnh vào đây")]
    public GameObject sunVisual;   // Kéo SunVisual vào
    public GameObject moonVisual;  // Kéo Sphere vào

    void Update()
    {
        timeOfDay += Time.deltaTime / (dayLengthInMinutes * 60f);
        if (timeOfDay >= 1f) timeOfDay = 0f;

        UpdateSunRotation();
        UpdateLightIntensity();
    }

    void UpdateSunRotation()
    {
        float sunAngle = timeOfDay * 360f - 90f;
        sunLight.transform.rotation = Quaternion.Euler(sunAngle, 170f, 0f);
        moonLight.transform.rotation = Quaternion.Euler(sunAngle + 180f, 170f, 0f);

        bool isDaytime = sunLight.transform.eulerAngles.x < 180f;
        sunLight.enabled = isDaytime;
        moonLight.enabled = !isDaytime;

        // THÊM: đồng bộ ẩn/hiện hình ảnh theo cùng điều kiện
        if (sunVisual != null) sunVisual.SetActive(isDaytime);
        if (moonVisual != null) moonVisual.SetActive(!isDaytime);
    }

    void UpdateLightIntensity()
    {
        // Đánh giá curve tại thời điểm hiện tại để ra hệ số sáng (0 -> 1)
        float sunFactor = sunIntensityCurve.Evaluate(timeOfDay);
        sunLight.intensity = sunFactor * maxSunIntensity;

        // Mặt trăng sáng ngược lại với mặt trời (khi mặt trời tối thì trăng sáng)
        float moonFactor = 1f - sunFactor;
        moonLight.intensity = moonFactor * maxMoonIntensity;
    }
}