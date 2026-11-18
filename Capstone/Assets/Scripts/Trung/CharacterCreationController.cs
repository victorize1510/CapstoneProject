using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CharacterCreationController : MonoBehaviour
{
    [Header("Khung đặt tên (ẩn tới khi chọn giới tính)")]
    public TMP_InputField nameInputField;

    [Header("Chọn giới tính")]
    public Button maleButton;
    public Button femaleButton;
    [Range(0f, 1f)] public float dimmedAlpha = 0.4f;

    [Header("Nút tạo nhân vật")]
    public Button createCharaterButton;
    public TextMeshProUGUI errorText;

    [Header("Popup xác nhận")]
    public GameObject confirmPopup;
    public TextMeshProUGUI confirmText;
    public Button acceptButton;
    public Button cancelButton;

    [Header("Scene tiếp theo")]
    public string gameplaySceneName = "GameplayScene";

    string selectedGender;
    Image maleImage;
    Image femaleImage;

    void Awake()
    {
        if (maleButton != null) maleImage = maleButton.GetComponent<Image>();
        if (femaleButton != null) femaleImage = femaleButton.GetComponent<Image>();

        if (nameInputField != null) nameInputField.gameObject.SetActive(false);
        if (confirmPopup != null) confirmPopup.SetActive(false);
        if (errorText != null) errorText.gameObject.SetActive(false);

        if (maleButton != null) maleButton.onClick.AddListener(() => SelectGender("Male"));
        if (femaleButton != null) femaleButton.onClick.AddListener(() => SelectGender("Female"));
        if (createCharaterButton != null) createCharaterButton.onClick.AddListener(OnCreateClicked);
        if (acceptButton != null) acceptButton.onClick.AddListener(OnAcceptConfirm);
        if (cancelButton != null) cancelButton.onClick.AddListener(OnCancelConfirm);
    }

    void SelectGender(string gender)
    {
        selectedGender = gender;

        SetImageAlpha(maleImage, gender == "Male" ? 1f : dimmedAlpha);
        SetImageAlpha(femaleImage, gender == "Female" ? 1f : dimmedAlpha);

        if (nameInputField != null) nameInputField.gameObject.SetActive(true);
        if (errorText != null) errorText.gameObject.SetActive(false);
    }

    void SetImageAlpha(Image img, float alpha)
    {
        if (img == null) return;
        Color c = img.color;
        c.a = alpha;
        img.color = c;
    }

    void OnCreateClicked()
    {
        if (string.IsNullOrEmpty(selectedGender))
        {
            ShowError("Vui lòng chọn giới tính trước.");
            return;
        }

        string typedName = nameInputField != null ? nameInputField.text.Trim() : "";
        if (string.IsNullOrEmpty(typedName))
        {
            ShowError("Vui lòng nhập tên nhân vật.");
            return;
        }

        string genderLabel = selectedGender == "Male" ? "Nam" : "Nữ";
        if (confirmText != null)
            confirmText.text = $"Bạn muốn chọn nhân vật {genderLabel} tên \"{typedName}\"?";

        if (confirmPopup != null) confirmPopup.SetActive(true);
    }

    void ShowError(string msg)
    {
        if (errorText == null) return;
        errorText.text = msg;
        errorText.gameObject.SetActive(true);
    }

    void OnCancelConfirm()
    {
        if (confirmPopup != null) confirmPopup.SetActive(false);
        if (nameInputField != null) nameInputField.gameObject.SetActive(false);

        selectedGender = null;
        SetImageAlpha(maleImage, 1f);
        SetImageAlpha(femaleImage, 1f);
    }

    void OnAcceptConfirm()
    {
        string typedName = nameInputField != null ? nameInputField.text.Trim() : "";

        CharacterSelectionData.Gender = selectedGender;
        CharacterSelectionData.PlayerName = typedName;

        if (string.IsNullOrWhiteSpace(gameplaySceneName))
        {
            Debug.LogWarning($"{name}: chưa gán Gameplay Scene Name.");
            return;
        }
        SceneManager.LoadScene(gameplaySceneName);
    }
}

public static class CharacterSelectionData
{
    public static string Gender;
    public static string PlayerName;
}