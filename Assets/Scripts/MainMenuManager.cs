using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Controls Main Menu navigation, scene transitions, and application exit behavior.
public class MainMenuManager : MonoBehaviour
{
    [Header("Main Menu UI References")]
    [Tooltip("Button component used to start the gameplay scene.")]
    public Button playButton;

    [Tooltip("Button component used to quit the application.")]
    public Button exitButton;

    [Header("Scene Settings")]
    [Tooltip("Name of the main gameplay scene to load.")]
    public string gameSceneName = "GameScene";

    private void Start()
    {
        Time.timeScale = 1f;

        EnsureResponsiveCanvasScaler();

        if (playButton != null)
        {
            playButton.onClick.AddListener(PlayGame);
        }

        if (exitButton != null)
        {
            exitButton.onClick.AddListener(QuitGame);
        }
    }

    // Configures the CanvasScaler for responsive screen resolution matching (1920x1080 reference).
    private void EnsureResponsiveCanvasScaler()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = GetComponentInParent<Canvas>();
        }

        if (canvas != null)
        {
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }
    }

    // Loads the main gameplay scene and resets timescale.
    public void PlayGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(gameSceneName);
    }

    // Quits the application or exits Play mode in the Unity Editor.
    public void QuitGame()
    {
        Time.timeScale = 1f;
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
