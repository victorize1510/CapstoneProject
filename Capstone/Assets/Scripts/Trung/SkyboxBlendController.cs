using UnityEngine;

public class SkyboxBlendController : MonoBehaviour
{
    public Material skyboxMaterial; // Mat_SkyboxBlend

    [Header("4 texture panorama tương ứng")]
    public Texture nightTex;
    public Texture sunriseTex;
    public Texture dayTex;
    public Texture sunsetTex;

    private DayNightCycle dayNightCycle;

    void Start()
    {
        dayNightCycle = GetComponent<DayNightCycle>();
    }

    void Update()
    {
        UpdateSkyboxBlend(dayNightCycle.timeOfDay);
    }

    void UpdateSkyboxBlend(float t)
    {
        // Chia 1 ngày thành 4 đoạn, mỗi đoạn blend giữa 2 texture liền kề
        Texture texA, texB;
        float blend;

        if (t < 0.25f) // Đêm -> Bình minh (0 - 0.25)
        {
            texA = nightTex; texB = sunriseTex;
            blend = Mathf.InverseLerp(0f, 0.25f, t);
        }
        else if (t < 0.5f) // Bình minh -> Ngày (0.25 - 0.5)
        {
            texA = sunriseTex; texB = dayTex;
            blend = Mathf.InverseLerp(0.25f, 0.5f, t);
        }
        else if (t < 0.75f) // Ngày -> Hoàng hôn (0.5 - 0.75)
        {
            texA = dayTex; texB = sunsetTex;
            blend = Mathf.InverseLerp(0.5f, 0.75f, t);
        }
        else // Hoàng hôn -> Đêm (0.75 - 1.0)
        {
            texA = sunsetTex; texB = nightTex;
            blend = Mathf.InverseLerp(0.75f, 1f, t);
        }

        skyboxMaterial.SetTexture("_Texture1", texA);
        skyboxMaterial.SetTexture("_Texture2", texB);
        skyboxMaterial.SetFloat("_Blend", blend);
        
        //tính Exposure động theo giờ, giống logic sunHeight đã làm
        float sunHeight = Mathf.Sin(t * Mathf.PI * 2f - Mathf.PI / 2f);
        float exposure = Mathf.Lerp(0.3f, 1.2f, (sunHeight + 1f) / 2f); 
        // Giữa trưa: sunHeight = 1 -> exposure cao (sáng)
        // Nửa đêm: sunHeight = -1 -> exposure thấp (tối)

        skyboxMaterial.SetFloat("_Exposure", exposure);
        }
}