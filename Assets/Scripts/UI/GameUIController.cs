using System.Collections;
using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TMG.Survivors
{
    public class GameUIController : MonoBehaviour
    {
        public static GameUIController Instance;

        [SerializeField] private TextMeshProUGUI _gemsCollectedText;
        [SerializeField] private TextMeshProUGUI _entityCounterText;
        [SerializeField] private GameObject _gameOverPanel;
        [SerializeField] private Button _quitButton;
        
        [SerializeField] private GameObject _pausePanel;
        [SerializeField] private Button _pauseResumeButton;
        [SerializeField] private Button _pauseQuitButton;

        private bool _isPaused = false;
        
        private void Awake()
        {
            if (Instance != null)
            {
                Debug.LogWarning("Warning: Multiple instances of GameUIController detected. Destroying new instance", Instance);
                return;
            }

            Instance = this;

            UpdateGemsCollectedText(0);
            EnsureEntityCounterText();
        }

        private void OnEnable()
        {
            _quitButton.onClick.AddListener(OnQuitButton);
            _pauseResumeButton.onClick.AddListener(OnResumeButton);
            _pauseQuitButton.onClick.AddListener(OnQuitButton);
        }

        private void OnDisable()
        {
            _quitButton.onClick.RemoveAllListeners();
            _pauseResumeButton.onClick.RemoveAllListeners();
            _pauseQuitButton.onClick.RemoveAllListeners();
        }

        private void Start()
        {
            _gameOverPanel.SetActive(false);
            _pausePanel.SetActive(false);
        }

        private void Update()
        {
            if(Input.GetKeyDown(KeyCode.Escape))
            {
                ToggleGamePause();
            }
        }

        private void ToggleGamePause()
        {
            _isPaused = !_isPaused;
            _pausePanel.SetActive(_isPaused);
            SetEcsEnabled(!_isPaused);
        }

        private void SetEcsEnabled(bool shouldEnable)
        {
            var defaultWorld = World.DefaultGameObjectInjectionWorld;
            if (defaultWorld == null) return;
            var initializationSystemGroup = defaultWorld.GetExistingSystemManaged<InitializationSystemGroup>();
            initializationSystemGroup.Enabled = shouldEnable;
            
            var simulationSystemGroup = defaultWorld.GetExistingSystemManaged<SimulationSystemGroup>();
            simulationSystemGroup.Enabled = shouldEnable;
        }

        public void UpdateGemsCollectedText(int gemsCollected)
        {
            _gemsCollectedText.text = $"{gemsCollected:N0}";
        }

        public void UpdateEntityCounterText(int enemyCount, int projectileCount, int hazardCount, int pooledProjectileCount)
        {
            EnsureEntityCounterText();
            if (_entityCounterText == null) return;

            _entityCounterText.text =
                $"Enemies: {enemyCount:N0}\n" +
                $"Projectiles: {projectileCount:N0}\n" +
                $"Hazards: {hazardCount:N0}\n" +
                $"Pooled: {pooledProjectileCount:N0}";
        }

        private void EnsureEntityCounterText()
        {
            if (_entityCounterText != null || _gemsCollectedText == null) return;

            var counterObject = new GameObject("EntityCounterText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            var sourceTransform = _gemsCollectedText.rectTransform;
            var rectTransform = counterObject.GetComponent<RectTransform>();
            rectTransform.SetParent(sourceTransform.parent, false);
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.anchoredPosition = new Vector2(24f, -110f);
            rectTransform.sizeDelta = new Vector2(520f, 240f);

            _entityCounterText = counterObject.GetComponent<TextMeshProUGUI>();
            _entityCounterText.font = _gemsCollectedText.font;
            _entityCounterText.fontSize = 42f;
            _entityCounterText.color = Color.white;
            _entityCounterText.alignment = TextAlignmentOptions.TopLeft;
            _entityCounterText.raycastTarget = false;
            _entityCounterText.text = "Enemies: 0\nProjectiles: 0\nHazards: 0\nPooled: 0";
        }

        public void ShowGameOverUI()
        {
            StartCoroutine(ShowGameOverUICoroutine());
        }

        private IEnumerator ShowGameOverUICoroutine()
        {
            yield return new WaitForSeconds(1.5f);

            _gameOverPanel.SetActive(true);
        }
        
        private void OnResumeButton()
        {
            ToggleGamePause();
        }

        private void OnQuitButton()
        {
            SetEcsEnabled(true);
            SceneManager.LoadScene(0);
        }
    }
}
