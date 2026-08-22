using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Manages the heads-up display (HUD), game outcome overlays, pause menu, and urgency animations.
public class UIManager : MonoBehaviour
{
    [Header("HUD References")]
    public TMP_Text scoreText;
    public TMP_Text livesText;
    public TMP_Text timerText;

    [Header("Lives UI Emergency Animation Settings")]
    [Tooltip("Background Image component behind the Lives text display.")]
    public Image livesBgImage;

    [Tooltip("Speed of the flashing animation when lives fall below 3.")]
    public float livesFlashSpeed = 6.0f;

    private Color defaultLivesBgColor = Color.white;
    private Color defaultLivesTextColor = Color.white;
    private bool isDefaultColorsCached = false;
    private bool isFlashingActive = false;
    private int currentLivesTracked = 5;

    [Header("Timer UI Urgency Settings")]
    [Tooltip("Flashing animation speed when remaining time falls under 10 seconds.")]
    public float timerFlashSpeed = 8.0f;

    private Color defaultTimerTextColor = Color.white;
    private float currentRemainingTimeTracked = 60f;
    private bool isTimerFlashingActive = false;

    [Header("Overlay Panels")]
    public GameObject gameOverPanel;
    public TMP_Text gameOverText;
    
    public GameObject levelCompletePanel;
    public TMP_Text levelCompleteText;

    [Header("Pause Menu")]
    [Tooltip("Pause panel overlay object.")]
    public GameObject pausePanel;

    [Tooltip("HUD Pause button to open the pause menu.")]
    public Button pauseButton;

    [Tooltip("Resume button inside the pause panel.")]
    public Button resumeButton;

    [Tooltip("Quit to Main Menu button inside the pause panel.")]
    public Button quitToMainMenuButton;

    [Tooltip("Quit to Desktop button inside the pause panel.")]
    public Button quitToDesktopButton;

    [Tooltip("Name of the Main Menu scene to load.")]
    public string mainMenuSceneName = "MainMenu";

    private bool isPaused = false;

    private static readonly Color ColorWhite = Color.white;
    private static readonly Color ColorMaroon = new Color(0.227f, 0.098f, 0.098f, 1.0f);
    private static readonly Color ColorRed = Color.red;

    private void OnEnable()
    {
        if (GameManager.Instance != null)
        {
            SubscribeToGameManager();
        }
    }

    private void Start()
    {
        EnsureEventSystem();
        EnsureResponsiveCanvasScaler();

        if (GameManager.Instance != null)
        {
            SubscribeToGameManager();
            UpdateScoreUI(GameManager.Instance.CurrentScore);
            UpdateLivesUI(GameManager.Instance.CurrentLives);
            UpdateTimerUI(GameManager.Instance.RemainingTime);
            UpdateGameStateUI(GameManager.Instance.CurrentState);
        }

        if (pauseButton != null)
        {
            pauseButton.onClick.AddListener(TogglePause);
        }
        else
        {
            Debug.LogWarning("[UIManager] 'pauseButton' is not assigned in the Inspector!");
        }

        if (resumeButton != null)
        {
            resumeButton.onClick.AddListener(ResumeGame);
        }
        if (quitToMainMenuButton != null)
        {
            quitToMainMenuButton.onClick.AddListener(QuitToMainMenu);
        }
        if (quitToDesktopButton != null)
        {
            quitToDesktopButton.onClick.AddListener(QuitToDesktop);
        }

        if (livesBgImage == null && livesText != null)
        {
            livesBgImage = livesText.GetComponentInParent<Image>();
            if (livesBgImage == null)
            {
                livesBgImage = livesText.GetComponent<Image>();
            }
            if (livesBgImage != null)
            {
                Debug.Log("[UIManager] Auto-assigned 'livesBgImage' from livesText parent component.");
            }
            else
            {
                Debug.LogWarning("[UIManager] 'livesBgImage' is unassigned and no Image component was found on livesText parent.");
            }
        }

        if (livesBgImage != null)
        {
            defaultLivesBgColor = livesBgImage.color;
        }
        if (livesText != null)
        {
            defaultLivesTextColor = livesText.color;
        }
        if (timerText != null)
        {
            defaultTimerTextColor = timerText.color;
        }
        isDefaultColorsCached = true;

        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }
        Time.timeScale = 1f;
    }

    // Ensures an EventSystem with InputSystemUIInputModule exists in the scene.
    private void EnsureEventSystem()
    {
        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            UnityEngine.EventSystems.EventSystem es = esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            Debug.Log("[UIManager] Auto-created missing EventSystem with InputSystemUIInputModule for UI clicks.");
        }
        else
        {
            var legacyModule = UnityEngine.EventSystems.EventSystem.current.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            var newModule = UnityEngine.EventSystems.EventSystem.current.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

            if (newModule == null)
            {
                if (legacyModule != null)
                {
                    Destroy(legacyModule);
                }
                UnityEngine.EventSystems.EventSystem.current.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                Debug.Log("[UIManager] Upgraded EventSystem to InputSystemUIInputModule for New Input System compatibility.");
            }
        }
    }

    // Configures CanvasScaler for resolution-independent scaling (1920x1080 reference).
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

    private void Update()
    {
        if (Keyboard.current != null && (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.pKey.wasPressedThisFrame))
        {
            TogglePause();
        }

        if (currentLivesTracked < 3)
        {
            isFlashingActive = true;
            AnimateLowLivesFlash();
        }
        else if (isFlashingActive)
        {
            isFlashingActive = false;
            ResetLivesColors();
        }

        if (currentRemainingTimeTracked <= 10f && currentRemainingTimeTracked > 0f)
        {
            isTimerFlashingActive = true;
            AnimateTimerTextFlash();
        }
        else if (isTimerFlashingActive)
        {
            isTimerFlashingActive = false;
            ResetTimerColor();
        }
    }

    // Animates Lives UI color pulsing when health is critically low.
    private void AnimateLowLivesFlash()
    {
        float rawSine = (Mathf.Sin(Time.unscaledTime * livesFlashSpeed) + 1.0f) * 0.5f;
        float t = Mathf.Pow(rawSine, 1.3f);

        Color bgCurrent;
        Color textCurrent;

        if (t <= 0.5f)
        {
            float factor = t / 0.5f;
            bgCurrent = Color.Lerp(ColorWhite, ColorMaroon, factor);
            textCurrent = Color.Lerp(ColorMaroon, ColorWhite, factor);
        }
        else
        {
            float factor = (t - 0.5f) / 0.5f;
            bgCurrent = Color.Lerp(ColorMaroon, ColorRed, factor);
            textCurrent = Color.white;
        }

        if (livesBgImage != null)
        {
            livesBgImage.color = bgCurrent;
        }

        if (livesText != null)
        {
            livesText.color = textCurrent;
        }
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
        {
            UnsubscribeFromGameManager();
        }
    }

    private void SubscribeToGameManager()
    {
        UnsubscribeFromGameManager();

        GameManager.Instance.OnScoreChanged += UpdateScoreUI;
        GameManager.Instance.OnLivesChanged += UpdateLivesUI;
        GameManager.Instance.OnTimerChanged += UpdateTimerUI;
        GameManager.Instance.OnGameStateChanged += UpdateGameStateUI;
    }

    private void UnsubscribeFromGameManager()
    {
        if (GameManager.Instance == null) return;

        GameManager.Instance.OnScoreChanged -= UpdateScoreUI;
        GameManager.Instance.OnLivesChanged -= UpdateLivesUI;
        GameManager.Instance.OnTimerChanged -= UpdateTimerUI;
        GameManager.Instance.OnGameStateChanged -= UpdateGameStateUI;
    }

    private void UpdateScoreUI(int score)
    {
        if (scoreText != null)
        {
            scoreText.text = $"Score\n {score}";
        }
    }

    private void UpdateLivesUI(int lives)
    {
        currentLivesTracked = lives;

        if (livesText != null)
        {
            livesText.text = $"{lives}";
        }

        if (lives >= 3)
        {
            ResetLivesColors();
        }
    }

    private void ResetLivesColors()
    {
        if (isDefaultColorsCached)
        {
            if (livesBgImage != null) livesBgImage.color = defaultLivesBgColor;
            if (livesText != null) livesText.color = defaultLivesTextColor;
        }
    }

    private void UpdateTimerUI(float remainingTime)
    {
        currentRemainingTimeTracked = remainingTime;

        if (timerText != null)
        {
            int seconds = Mathf.CeilToInt(remainingTime);
            timerText.text = $"{seconds}";
        }

        if (remainingTime > 10f)
        {
            ResetTimerColor();
        }
    }

    private void AnimateTimerTextFlash()
    {
        if (timerText == null) return;

        float sine = (Mathf.Sin(Time.unscaledTime * timerFlashSpeed) + 1.0f) * 0.5f;
        timerText.color = Color.Lerp(defaultTimerTextColor, Color.red, sine);
    }

    private void ResetTimerColor()
    {
        if (timerText != null)
        {
            timerText.color = defaultTimerTextColor;
        }
    }

    private Coroutine countdownCoroutine;

    private void UpdateGameStateUI(GameState state)
    {
        if (countdownCoroutine != null)
        {
            StopCoroutine(countdownCoroutine);
            countdownCoroutine = null;
        }

        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (levelCompletePanel != null) levelCompletePanel.SetActive(false);

        switch (state)
        {
            case GameState.Playing:
                ResumeGame();
                break;

            case GameState.GameOver:
                if (gameOverPanel != null)
                {
                    gameOverPanel.SetActive(true);
                }
                if (GameManager.Instance != null && GameManager.Instance.autoRestartOnGameEnd)
                {
                    countdownCoroutine = StartCoroutine(ShowRestartCountdown(gameOverText, "GAME OVER"));
                }
                else if (gameOverText != null)
                {
                    gameOverText.text = $"GAME OVER\nFinal Score: {GameManager.Instance.CurrentScore}";
                }
                break;

            case GameState.LevelComplete:
                if (levelCompletePanel != null)
                {
                    levelCompletePanel.SetActive(true);
                }
                if (GameManager.Instance != null && GameManager.Instance.autoRestartOnGameEnd)
                {
                    countdownCoroutine = StartCoroutine(ShowRestartCountdown(levelCompleteText, "YOU WON CONGRATULATIONS!"));
                }
                else if (levelCompleteText != null)
                {
                    levelCompleteText.text = $"YOU WON CONGRATULATIONS!\nScore: {GameManager.Instance.CurrentScore}";
                }
                break;
        }
    }

    private System.Collections.IEnumerator ShowRestartCountdown(TMP_Text textComponent, string title)
    {
        if (textComponent == null || GameManager.Instance == null) yield break;

        float delay = GameManager.Instance.autoRestartDelay;
        while (delay > 0f)
        {
            int seconds = Mathf.CeilToInt(delay);
            textComponent.text = $"{title}\nScore: {GameManager.Instance.CurrentScore}\nRestarting in {seconds}s...";
            yield return new WaitForSeconds(1.0f);
            delay -= 1.0f;
        }
    }

    // Toggles the game between paused and unpaused states.
    public void TogglePause()
    {
        if (isPaused)
        {
            ResumeGame();
        }
        else
        {
            PauseGame();
        }
    }

    // Pauses time and displays the pause panel.
    public void PauseGame()
    {
        isPaused = true;
        Time.timeScale = 0f;
        if (pausePanel != null)
        {
            pausePanel.SetActive(true);
        }
    }

    // Resumes time and hides the pause panel.
    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f;
        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }
    }

    // Loads the Main Menu scene.
    public void QuitToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    // Quits the application or stops Play mode in editor.
    public void QuitToDesktop()
    {
        Time.timeScale = 1f;
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

}
