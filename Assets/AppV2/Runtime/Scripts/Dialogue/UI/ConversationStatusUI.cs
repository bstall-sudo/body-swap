using System.Collections;
using TMPro;
using UnityEngine;

namespace AppV2.Runtime.Scripts.Dialogue.UI
{
    public class ConversationStatusUI : MonoBehaviour
    {
        [Header("Persistent status text")]
        [SerializeField] private TMP_Text desktopStatusText;
        [SerializeField] private TMP_Text xrStatusText;

        [Header("Short transition cue text")]
        [SerializeField] private TMP_Text desktopCueText;
        [SerializeField] private TMP_Text xrCueText;

        [Header("Cue timing")]
        [SerializeField] private float cueDurationSeconds = 1.5f;

        private Coroutine _cueRoutine;

        public void ShowSpeakerState()
        {
            SetStatusText("SPEAKER");
        }

        public void ShowListenerState()
        {
            SetStatusText("LISTENER");
        }

        public void ShowIdleState()
        {
            SetStatusText("IDLE");
        }

        public void ShowTransitionToSpeaker()
        {
            ShowCue("NOW SPEAK");
        }

        public void ShowTransitionToListener()
        {
            ShowCue("NOW LISTEN");
        }

        public void ShowCustomCue(string message)
        {
            ShowCue(message);
        }

        private void SetStatusText(string message)
        {
            if (desktopStatusText != null)
                desktopStatusText.text = message;

            if (xrStatusText != null)
                xrStatusText.text = message;
        }

        private void ShowCue(string message)
        {
            if (_cueRoutine != null)
            {
                StopCoroutine(_cueRoutine);
            }

            _cueRoutine = StartCoroutine(ShowCueRoutine(message));
        }

        private IEnumerator ShowCueRoutine(string message)
        {
            SetCueText(message, true);

            yield return new WaitForSeconds(cueDurationSeconds);

            SetCueText(string.Empty, false);
            _cueRoutine = null;
        }

        private void SetCueText(string message, bool visible)
        {
            if (desktopCueText != null)
            {
                desktopCueText.text = message;
                desktopCueText.gameObject.SetActive(visible);
            }

            if (xrCueText != null)
            {
                xrCueText.text = message;
                xrCueText.gameObject.SetActive(visible);
            }
        }
    }
}