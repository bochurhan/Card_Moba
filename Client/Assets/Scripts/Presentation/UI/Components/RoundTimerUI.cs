using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CardMoba.Client.GameLogic.RoundFlow;

namespace CardMoba.Client.Presentation.UI.Components
{
    /// <summary>
    /// 回合计时条 UI。
    ///
    /// 职责：
    /// 1. 显示当前阶段名称。
    /// 2. 显示本地倒计时数字和进度条。
    /// 3. 在倒计时归零后显示等待结算提示。
    ///
    /// 本组件只消费 RoundPhaseTimerClient 的数据，不驱动任何游戏逻辑。
    /// </summary>
    public class RoundTimerUI : MonoBehaviour
    {
        [Header("阶段名称")]
        [SerializeField] private TextMeshProUGUI _phaseNameText;

        [Header("倒计时文本")]
        [SerializeField] private TextMeshProUGUI _timerText;

        [Header("进度条")]
        [SerializeField] private Slider _timerBar;
        [SerializeField] private Image _timerBarFill;

        [Header("等待结算遮罩")]
        [SerializeField] private GameObject _waitingOverlay;
        [SerializeField] private TextMeshProUGUI _waitingText;

        [Header("颜色配置")]
        [SerializeField] private Color _colorSafe = new Color(0.2f, 0.8f, 0.3f);
        [SerializeField] private Color _colorWarn = new Color(1.0f, 0.8f, 0.1f);
        [SerializeField] private Color _colorDanger = new Color(0.9f, 0.2f, 0.2f);

        private RoundPhaseTimerClient _timer;
        private float _totalSeconds;
        private bool _isRunning;

        /// <summary>
        /// 绑定计时器数据源。
        /// </summary>
        public void BindTimer(RoundPhaseTimerClient timer)
        {
            if (_timer != null)
            {
                _timer.OnPhaseChangedWithDuration -= HandlePhaseChanged;
                _timer.OnTimerExpired -= HandleTimerExpired;
            }

            _timer = timer;

            if (_timer != null)
            {
                _timer.OnPhaseChangedWithDuration += HandlePhaseChanged;
                _timer.OnTimerExpired += HandleTimerExpired;
            }
        }

        /// <summary>
        /// 显示非计时阶段文案，例如“结算中...”。
        /// </summary>
        public void ShowStaticPhase(string phaseLabel)
        {
            _isRunning = false;

            if (_phaseNameText != null)
                _phaseNameText.text = phaseLabel;

            if (_timerText != null)
                _timerText.text = string.Empty;

            if (_timerBar != null)
                _timerBar.gameObject.SetActive(false);

            SetWaiting(false);
        }

        private void Awake()
        {
            SetWaiting(false);
        }

        private void Update()
        {
            if (!_isRunning || _timer == null)
                return;

            float remaining = Mathf.Max(0f, _timer.RemainingSeconds);

            if (_timerText != null)
                _timerText.text = $"{Mathf.CeilToInt(remaining)}s";

            if (_timerBar != null && _totalSeconds > 0f)
            {
                float ratio = remaining / _totalSeconds;
                _timerBar.value = ratio;
                UpdateBarColor(ratio);
            }
        }

        private void OnDestroy()
        {
            if (_timer != null)
            {
                _timer.OnPhaseChangedWithDuration -= HandlePhaseChanged;
                _timer.OnTimerExpired -= HandleTimerExpired;
            }
        }

        private void HandlePhaseChanged(RoundPhase phase, float totalSeconds)
        {
            _totalSeconds = totalSeconds;
            _isRunning = true;

            if (_phaseNameText != null)
                _phaseNameText.text = GetPhaseDisplayName(phase);

            if (_timerBar != null)
            {
                _timerBar.gameObject.SetActive(true);
                _timerBar.minValue = 0f;
                _timerBar.maxValue = 1f;
                _timerBar.value = 1f;
            }

            UpdateBarColor(1f);
            SetWaiting(false);
        }

        private void HandleTimerExpired()
        {
            _isRunning = false;

            if (_timerText != null)
                _timerText.text = "0s";

            if (_timerBar != null)
                _timerBar.value = 0f;

            UpdateBarColor(0f);
            SetWaiting(true);
        }

        private void UpdateBarColor(float ratio)
        {
            if (_timerBarFill == null)
                return;

            if (ratio > 0.5f)
            {
                _timerBarFill.color = _colorSafe;
            }
            else if (ratio > 0.2f)
            {
                _timerBarFill.color = Color.Lerp(_colorWarn, _colorSafe, (ratio - 0.2f) / 0.3f);
            }
            else
            {
                _timerBarFill.color = Color.Lerp(_colorDanger, _colorWarn, ratio / 0.2f);
            }
        }

        private void SetWaiting(bool waiting)
        {
            if (_waitingOverlay != null)
                _waitingOverlay.SetActive(waiting);

            if (_waitingText != null && waiting)
                _waitingText.text = "等待结算中...";
        }

        private static string GetPhaseDisplayName(RoundPhase phase)
        {
            return phase switch
            {
                RoundPhase.RoundStartSettle => "回合开始",
                RoundPhase.OperationWindow => "操作期",
                RoundPhase.CommandLock => "指令锁定",
                RoundPhase.PlanCardSettle => "定策结算",
                RoundPhase.DeathJudge => "濒死判定",
                RoundPhase.RoundEnd => "回合结束",
                _ => phase.ToString()
            };
        }
    }
}
