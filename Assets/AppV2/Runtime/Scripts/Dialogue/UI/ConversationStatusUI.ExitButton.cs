using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace AppV2.Runtime.Scripts.Dialogue.UI
{
    public partial class ConversationStatusUI
    {
        [Header("VR: Zurück zum Menü")]
        [SerializeField] private Vector3 exitButtonHeadLocalPosition = new Vector3(0.28f, 0.20f, 0.75f);
        [SerializeField] private float exitButtonSizeMeters = 0.065f;
        private GameObject _exitCanvas;
        private BoxCollider _exitCollider;
        private Image _exitImage;
        private Transform _exitLeftHand;
        private Transform _exitRightHand;
        private XRRayInteractor[] _exitRays;
        private NearFarInteractor[] _exitNearFarRays;
        private Vector3[] _exitRayPoints;

        [Header("Desktop: Zurück zum Menü")]
        [SerializeField] private Vector2 desktopExitButtonMargin = new Vector2(24f, 24f);
        [SerializeField] private float desktopExitButtonSize = 64f;
        private GameObject _desktopExitCanvas;
        private RectTransform _desktopExitButtonRect;
        private Button _desktopExitButton;
        private Action _desktopExitAction;
        private bool _desktopExitMouseDown;
        private int _desktopExitClickFrame = -1;

        public void InitializeDesktopExitButton(Action clicked)
        {
            if (_desktopExitCanvas != null) return;
            _desktopExitAction = clicked;
            _desktopExitCanvas = new GameObject("Desktop Return To Menu", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = _desktopExitCanvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            canvas.targetDisplay = 0;
            var scaler = _desktopExitCanvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var buttonObject = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(_desktopExitCanvas.transform, false);
            var rect = (RectTransform)buttonObject.transform;
            _desktopExitButtonRect = rect;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-desktopExitButtonMargin.x, -desktopExitButtonMargin.y);
            rect.sizeDelta = Vector2.one * desktopExitButtonSize;
            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.8f, 0.03f, 0.03f, 0.95f);
            var button = buttonObject.GetComponent<Button>();
            _desktopExitButton = button;
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(1f, 0.65f, 0.65f, 1f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            button.colors = colors;
            button.onClick.AddListener(InvokeDesktopExit);

            var textObject = new GameObject("Cross", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(buttonObject.transform, false);
            rect = (RectTransform)textObject.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var text = textObject.GetComponent<TextMeshProUGUI>();
            if (desktopStatusText != null) text.font = desktopStatusText.font;
            else if (xrStatusText != null) text.font = xrStatusText.font;
            text.text = "×";
            text.fontSize = desktopExitButtonSize * 0.8f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            _desktopExitCanvas.SetActive(false);
        }

        public void SetDesktopExitButtonVisible(bool visible)
        {
            _desktopExitMouseDown = false;
            if (_desktopExitCanvas != null) _desktopExitCanvas.SetActive(visible);
        }

        private void InvokeDesktopExit()
        {
            if (_desktopExitCanvas == null || !_desktopExitCanvas.activeInHierarchy ||
                _desktopExitClickFrame == Time.frameCount) return;
            _desktopExitClickFrame = Time.frameCount;
            Debug.Log("[ConversationStatusUI] Desktop return-to-menu clicked.", this);
            _desktopExitAction?.Invoke();
        }

        private void ProcessDesktopExitMouse()
        {
            if (_desktopExitCanvas == null || !_desktopExitCanvas.activeInHierarchy ||
                _desktopExitButton == null || !_desktopExitButton.interactable || Mouse.current == null)
            {
                _desktopExitMouseDown = false;
                return;
            }

            // ScreenSpaceOverlay uses no event camera. This path remains usable
            // when XR switches/disables the UI action map or current EventSystem.
            bool hovered = RectTransformUtility.RectangleContainsScreenPoint(
                _desktopExitButtonRect, Mouse.current.position.ReadValue(), null);
            if (Mouse.current.leftButton.wasPressedThisFrame) _desktopExitMouseDown = hovered;
            if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                bool clicked = _desktopExitMouseDown && hovered;
                _desktopExitMouseDown = false;
                if (clicked) InvokeDesktopExit();
            }
        }

        public void InitializeExitButton(Transform head, Transform xrOrigin, Action clicked, Transform leftHand, Transform rightHand)
        {
            if (_exitCanvas != null || head == null) return;
            _exitRays = xrOrigin != null ? xrOrigin.GetComponentsInChildren<XRRayInteractor>(true) : Array.Empty<XRRayInteractor>();
            _exitNearFarRays = xrOrigin != null ? xrOrigin.GetComponentsInChildren<NearFarInteractor>(true) : Array.Empty<NearFarInteractor>();
            _exitLeftHand = leftHand;
            _exitRightHand = rightHand;
            _exitCanvas = new GameObject("VR Return To Menu", typeof(RectTransform), typeof(Canvas), typeof(TrackedDeviceGraphicRaycaster));
            _exitCanvas.transform.SetParent(head, false);
            _exitCanvas.transform.localPosition = exitButtonHeadLocalPosition;
            _exitCanvas.transform.localRotation = Quaternion.identity;
            _exitCanvas.transform.localScale = Vector3.one * (exitButtonSizeMeters / 80f);
            var canvas = _exitCanvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = head.GetComponentInChildren<Camera>();
            canvas.sortingOrder = 1000;
            var rect = (RectTransform)_exitCanvas.transform;
            rect.sizeDelta = new Vector2(80, 80);
            var buttonObject = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button), typeof(BoxCollider));
            buttonObject.transform.SetParent(_exitCanvas.transform, false);
            rect = (RectTransform)buttonObject.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            _exitCollider = buttonObject.GetComponent<BoxCollider>();
            _exitCollider.size = new Vector3(80, 80, 1);
            _exitCollider.isTrigger = true;
            _exitImage = buttonObject.GetComponent<Image>();
            _exitImage.color = new Color(0.8f, 0.03f, 0.03f, 0.95f);
            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = _exitImage;
            button.onClick.AddListener(() => clicked?.Invoke());
            var textObject = new GameObject("Cross", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(buttonObject.transform, false);
            rect = (RectTransform)textObject.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var text = textObject.GetComponent<TextMeshProUGUI>();
            if (xrStatusText != null) text.font = xrStatusText.font;
            text.text = "×"; text.fontSize = 65; text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white; text.raycastTarget = false;
            _exitCanvas.SetActive(false);
        }

        public void SetExitButtonVisible(bool visible)
        {
            if (_exitCanvas != null) _exitCanvas.SetActive(visible);
        }

        public bool IsExitButtonTargeted(Transform leftHand, Transform rightHand)
        {
            _exitLeftHand = leftHand; _exitRightHand = rightHand;
            if (_exitCanvas == null || !_exitCanvas.activeInHierarchy) return false;
            bool activeRay = false;
            if (_exitNearFarRays != null)
            {
                foreach (var ray in _exitNearFarRays)
                {
                    if (ray == null || !ray.isActiveAndEnabled) continue;
                    activeRay = true;
                    if (ray.TryGetCurrentUIRaycastResult(out var hit) && hit.gameObject != null &&
                        hit.gameObject.transform.IsChildOf(_exitCanvas.transform)) return true;
                    var origin = ((IXRRayProvider)ray).GetOrCreateRayOrigin();
                    if (origin != null && _exitCollider.Raycast(new Ray(origin.position, origin.forward), out _, 10f)) return true;
                }
            }
            if (_exitRays != null)
            {
                foreach (var ray in _exitRays)
                {
                    if (ray == null || !ray.isActiveAndEnabled || !ray.GetLinePoints(ref _exitRayPoints, out int count) || count < 2) continue;
                    activeRay = true;
                    for (int i = 1; i < count; i++)
                    {
                        Vector3 segment = _exitRayPoints[i] - _exitRayPoints[i - 1];
                        if (_exitCollider.Raycast(new Ray(_exitRayPoints[i - 1], segment.normalized), out _, segment.magnitude)) return true;
                    }
                }
            }
            // Also usable when teleport rays are disabled by the session settings.
            return !activeRay && (HandTargetsExit(leftHand) || HandTargetsExit(rightHand));
        }

        private bool HandTargetsExit(Transform hand) => hand != null &&
            _exitCollider.Raycast(new Ray(hand.position, hand.forward), out _, 10f);

        private void LateUpdate()
        {
            ProcessDesktopExitMouse();
            if (_exitCanvas == null || !_exitCanvas.activeInHierarchy) return;
            bool hovered = IsExitButtonTargeted(_exitLeftHand, _exitRightHand);
            _exitImage.color = hovered ? new Color(1f, 0.25f, 0.12f, 1f) : new Color(0.8f, 0.03f, 0.03f, 0.95f);
        }

        public void ClearConversationFeedback()
        {
            if (_cueRoutine != null) StopCoroutine(_cueRoutine);
            _cueRoutine = null;
            SetStatusText("");
            SetCueText("", false, defaultDesktopCuePosition, defaultXrCuePosition, defaultCueColor);
        }

        private void OnDestroy()
        {
            if (_exitCanvas != null) Destroy(_exitCanvas);
            if (_desktopExitCanvas != null) Destroy(_desktopExitCanvas);
        }
    }
}
