using CrashRoyale.Battle;
using CrashRoyale.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CrashRoyale.UI
{
    public class PauseMenu : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button quitButton;

        public bool IsOpen => panel.activeSelf;

        private void Awake()
        {
            panel.SetActive(false);
            resumeButton.onClick.AddListener(Close);
            restartButton.onClick.AddListener(() => SceneFlow.Load(SceneFlow.Battle));
            quitButton.onClick.AddListener(() => SceneFlow.Load(SceneFlow.MainMenu));
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            var battle = BattleManager.Instance;
            if (battle == null || battle.State == BattleState.Ended) return;

            panel.SetActive(true);
            UITween.Pop(panel.transform, 0.85f, 1.03f, 0.2f);
            battle.SetPaused(true);
        }

        public void Close()
        {
            panel.SetActive(false);
            var battle = BattleManager.Instance;
            if (battle != null) battle.SetPaused(false);
        }
    }
}
