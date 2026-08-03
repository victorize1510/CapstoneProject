using UnityEngine;
using UnityEngine.Video;
using UnityEngine.SceneManagement;

public class IntroScene : MonoBehaviour
{
    public VideoPlayer videoPlayer;
    public string nextScene = "DemoScene"; // Tên của scene tiếp theo sau khi video kết thúc

    void Start()
    {
        videoPlayer.loopPointReached += OnVideoEnd; // Đăng ký sự kiện khi video kết thúc
    }


    void OnVideoEnd(VideoPlayer vp)
    {
        SceneManager.LoadScene(nextScene); // Chuyển sang scene tiếp theo
    }
    public void SkipIntro()
    {
        SceneManager.LoadScene(nextScene); // Chuyển sang scene tiếp theo khi người chơi nhấn nút Skip
    }
}

