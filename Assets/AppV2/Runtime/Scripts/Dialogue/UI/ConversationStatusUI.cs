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

        [Header("Default cue style")]
        [SerializeField] private Vector2 defaultDesktopCuePosition = Vector2.zero;
        [SerializeField] private Vector2 defaultXrCuePosition = Vector2.zero;
        [SerializeField] private Color defaultCueColor = Color.white;

        private Coroutine _cueRoutine;

        public void ShowSpeakerState()
        {
            SetStatusText("SPEAKER");
        }

        public void ShowListenerState()
        {
            SetStatusText("LISTENER");
        }

        public void ShowChooseSpeakerState()
        {
            SetStatusText("linker Trigger -> nächste Figur\n\n" 
                    + 
                    "rechter Trigger (einmal) -> weiter mit der gewählten Figur\n\n"
                    + 
                    "rechter Trigger (zweimal) -> Scene für bestimmte Rollen überspringen\n\n"
                    + 
                    "Dann sind ALLE Figuren wieder wählbar\n"
                    + 
                    "und können über linken und rechten ausgewählt werden.\n");
        }

        public void ShowPlaybackFullConversationState()
        {
            SetStatusText("Playback full conversation. No recording.");
        }

        public void ShowIdleState()
        {
            SetStatusText("IDLE");
        }

        public void ShowTransitionToSpeaker()
        {
            ShowCue("NOW SPEAK", defaultDesktopCuePosition, defaultXrCuePosition, defaultCueColor);
        }

        public void ShowTransitionToListener()
        {
            ShowCue("NOW LISTEN", defaultDesktopCuePosition, defaultXrCuePosition, defaultCueColor);
        }

        public void ShowCustomCue(string message)
        {
            Debug.Log($"xrCueText null? {xrCueText == null}");
            ShowCue(message, defaultDesktopCuePosition, defaultXrCuePosition, defaultCueColor);
        }

        public void ShowCustomCue(string message, Color color)
        {
            ShowCue(message, defaultDesktopCuePosition, defaultXrCuePosition, color);
        }

        public void ShowCustomCue(string message, Vector2 desktopPosition, Vector2 xrPosition, Color color)
        {
            Debug.Log($"xrCueText null? {xrCueText == null}");
            ShowCue(message, desktopPosition, xrPosition, color);
        }

        public void SetStatusText(string message)
        {
            if (desktopStatusText != null)
                desktopStatusText.text = message;

            if (xrStatusText != null)
                xrStatusText.text = message;
        }

        public void SetStatusText(string message, Color color)
        {
            if (desktopStatusText != null)
            {
                desktopStatusText.text = message;
                desktopStatusText.color = color;
            }

            if (xrStatusText != null)
            {
                xrStatusText.text = message;
                xrStatusText.color = color;
            }
        }

        private void ShowCue(string message, Vector2 desktopPosition, Vector2 xrPosition, Color color)
        {
            if (_cueRoutine != null)
                StopCoroutine(_cueRoutine);

            _cueRoutine = StartCoroutine(
                ShowCueRoutine(message, desktopPosition, xrPosition, color)
            );
        }

        private IEnumerator ShowCueRoutine(
            string message,
            Vector2 desktopPosition,
            Vector2 xrPosition,
            Color color)
        {
            SetCueText(message, true, desktopPosition, xrPosition, color);

            yield return new WaitForSeconds(cueDurationSeconds);

            SetCueText(string.Empty, false, desktopPosition, xrPosition, color);
            _cueRoutine = null;
        }

        private void SetCueText(
            string message,
            bool visible,
            Vector2 desktopPosition,
            Vector2 xrPosition,
            Color color)
        {
            ApplyTextSettings(desktopCueText, message, visible, desktopPosition, color);
            ApplyTextSettings(xrCueText, message, visible, xrPosition, color);
        }

        private void ApplyTextSettings(
            TMP_Text text,
            string message,
            bool visible,
            Vector2 anchoredPosition,
            Color color)
        {
            if (text == null)
                return;

            text.text = message;
            text.color = color;
            text.gameObject.SetActive(visible);

            RectTransform rect = text.GetComponent<RectTransform>();

            if (rect != null)
                rect.anchoredPosition = anchoredPosition;
        }
    }
}