using UnityEngine;
using  TMPro;
/// <summary>
/// Gắn script này lên Canvas trong EvolutionScene. Quản lý khung chữ kiểu
/// "Congratulations! X đã tiến hóa thành Y!" — ẩn sẵn lúc đầu, EvolutionController
/// sẽ gọi Show()/Hide() đúng lúc animation tiến hóa kết thúc.
/// </summary>
public class EvolutionUI : MonoBehaviour
{
    public static EvolutionUI Instance { get; private set; }

    [Tooltip("GameObject chứa khung chữ (nền + text), sẽ được ẩn/hiện. Để trống sẽ dùng chính GameObject này.")]
    public GameObject panelRoot;

    [Tooltip("Text hiển thị nội dung thông báo (VD: 'Chúc mừng! Dragon Spark đã tiến hóa thành Dragon Fire!').")]
    public TMP_Text messageText;

    void Awake()
    {
        Instance = this;

        if (panelRoot == null)
            panelRoot = gameObject;

        // Ẩn sẵn lúc mới load scene, chỉ hiện ra khi EvolutionController gọi Show().
        panelRoot.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Show(string message)
    {
        if (messageText != null)
            messageText.text = message;
        else
            Debug.LogWarning($"{name}: chưa gán 'Message Text' (TMP_Text) trong Inspector, không hiển thị được nội dung.");
 
        panelRoot.SetActive(true);
    }
    public void Hide()
    {
        panelRoot.SetActive(false);
    }
}

