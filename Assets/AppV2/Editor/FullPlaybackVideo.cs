using System;
using System.Collections.Generic;
using System.IO;
using AppV2.Runtime.Scripts.Dialogue;
using TMPro;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
internal sealed class FullPlaybackVideo : IDisposable
{
    private static FullPlaybackVideo _active;
    private static readonly Color PanelColor = new Color(0.05f, 0.08f, 0.12f, 0.96f);
    private static readonly Color RecordColor = new Color(0.70f, 0.12f, 0.12f, 1f);
    private const float FilmHorizontalFov = 55f;
    private const float FilmCameraBackOffset = 2f;
    private const float FramingReferenceDistance = 3f;
    private const string VideoExclusionTag = "excludeFromVideoRendering";
    private const string XrVisualLayer = "XR Film Excluded";
    private const float ViewfinderDistance = 0.8f;
    private const float ViewfinderScale = 0.00085f;

    private readonly FlowController _flow;
    private readonly string _directory;
    private readonly List<Renderer> _visualRenderers = new List<Renderer>();
    private readonly Dictionary<GameObject, int> _originalLayers = new Dictionary<GameObject, int>();
    private ContinuousMoveProvider _moveProvider;
    private XRInputValueReader<Vector2> _originalMoveInput;
    private InputAction _playbackMove;
    private int _xrVisualLayer;
    private GameObject _panel;
    private Camera _source;
    private Camera _filmCamera;
    private RenderTexture _target;
    private RawImage _preview;
    private RectTransform _frame;
    private Button _recordButton;
    private Button _aspectButton;
    private Image _frameImage;
    private Image[] _viewfinder;
    private RectTransform _controls;
    private TextMeshProUGUI _aspectLabel;
    private TextMeshProUGUI _recordLabel;
    private TextMeshProUGUI _status;
    private RecorderController _recorder;
    private RecorderControllerSettings _settings;
    private MovieRecorderSettings _movie;
    private InputAction _xrClick;
    private string _output;
    private bool _fourByThree;

    static FullPlaybackVideo()
    {
        FlowController.FullPlaybackVideoAvailabilityChanged += OnPlaybackChanged;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingPlayMode) Close();
        };
        AssemblyReloadEvents.beforeAssemblyReload += Close;
    }

    private static void OnPlaybackChanged(FlowController flow, bool available)
    {
        if (!available)
        {
            if (_active?._flow == flow) Close();
            return;
        }

        Close();
        var stage = flow.Stage;
        Camera source = stage.UseXR && stage.XrHead != null
            ? stage.XrHead.GetComponentInChildren<Camera>()
            : Camera.main;
        string directory = stage.GetFullPlaybackVideoDirectory();
        if (source == null || !Directory.Exists(directory))
        {
            Debug.LogWarning("Videoaufnahme braucht eine aktive Kamera und einen Wiedergabeordner.", flow);
            return;
        }

        _active = new FullPlaybackVideo(flow, directory);
        try
        {
            _active.CreateCamera(source);
            _active.CreatePanel(source, stage.UseXR);
            _active.ApplyAspect();
            if (stage.UseXR) _active.BindPlaybackMovement(stage.XrOrigin);
        }
        catch (Exception exception)
        {
            Close();
            Debug.LogException(exception, flow);
        }
    }

    private static void Close()
    {
        var active = _active;
        _active = null;
        active?.Dispose();
    }

    private FullPlaybackVideo(FlowController flow, string directory)
    {
        _flow = flow;
        _directory = directory;
    }

    private void CreateCamera(Camera source)
    {
        _source = source;
        var cameraObject = new GameObject("Playback Film Camera", typeof(Camera));
        cameraObject.transform.SetParent(source.transform, false);
        if (_flow.Stage.UseXR)
            cameraObject.transform.localPosition = Vector3.back * FilmCameraBackOffset;
        _filmCamera = cameraObject.GetComponent<Camera>();
        _filmCamera.CopyFrom(source);
        _filmCamera.stereoTargetEye = StereoTargetEyeMask.None;
        _xrVisualLayer = LayerMask.NameToLayer(XrVisualLayer);
        _filmCamera.cullingMask &= ~LayerMask.GetMask("UI", XrVisualLayer);
        _filmCamera.rect = new Rect(0f, 0f, 1f, 1f);
        _filmCamera.depth = source.depth - 1f;
        _filmCamera.usePhysicalProperties = false;
        _filmCamera.ResetProjectionMatrix();
        _filmCamera.ResetWorldToCameraMatrix();

        var filmData = _filmCamera.GetUniversalAdditionalCameraData();
        filmData.allowXRRendering = false;
        if (source.TryGetComponent<UniversalAdditionalCameraData>(out var sourceData))
        {
            filmData.renderPostProcessing = sourceData.renderPostProcessing;
            filmData.volumeLayerMask = sourceData.volumeLayerMask;
            filmData.volumeTrigger = sourceData.volumeTrigger;
        }
        RenderPipelineManager.beginCameraRendering += BeginFilmRender;
        RenderPipelineManager.endCameraRendering += EndFilmRender;
    }

    private void CreatePanel(Camera source, bool xr)
    {
        _panel = new GameObject("Playback Video Controls", typeof(RectTransform), typeof(Canvas));
        _panel.layer = LayerMask.NameToLayer("UI");
        var canvas = _panel.GetComponent<Canvas>();
        canvas.sortingOrder = 1001;
        var canvasRect = (RectTransform)_panel.transform;
        if (xr)
        {
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = source;
            canvasRect.SetParent(source.transform, false);
            canvasRect.localPosition = Vector3.forward * ViewfinderDistance;
            canvasRect.localScale = Vector3.one * ViewfinderScale;
            _viewfinder = new Image[4];
            for (int i = 0; i < _viewfinder.Length; i++)
            {
                _viewfinder[i] = Add<Image>(canvasRect, "Viewfinder Edge", Vector2.zero, Vector2.zero);
                _viewfinder[i].raycastTarget = false;
            }
        }
        else
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _panel.AddComponent<GraphicRaycaster>();
        }

        var body = Add<Image>(canvasRect, "Video Panel", Vector2.zero, new Vector2(310f, xr ? 62f : 198f));
        _controls = body.rectTransform;
        if (!xr)
        {
            body.rectTransform.anchorMin = body.rectTransform.anchorMax = new Vector2(1f, 0f);
            body.rectTransform.anchoredPosition = new Vector2(-175f, 119f);
        }
        body.color = PanelColor;
        body.raycastTarget = false;
        if (!xr)
        {
            _frameImage = Add<Image>(_controls, "Preview Frame", new Vector2(0f, 25f), new Vector2(228f, 130f));
            _frameImage.color = Color.white;
            _frameImage.raycastTarget = false;
            _frame = _frameImage.rectTransform;
            _preview = Add<RawImage>(_frame, "Film Preview", Vector2.zero, new Vector2(224f, 126f));
            _preview.raycastTarget = false;
        }

        float buttonY = xr ? 7f : -62f;
        _recordButton = AddButton(_controls, new Vector2(-41f, buttonY), 185f, "Video starten", ToggleRecording, out _recordLabel);
        _aspectButton = AddButton(_controls, new Vector2(96f, buttonY), 74f, "16:9", ToggleAspect, out _aspectLabel);
        _status = AddLabel(_controls, "Status", new Vector2(0f, buttonY - 25f), new Vector2(300f, 19f), "", 14f);
        if (xr)
        {
            // The playback scene may use the desktop input module or disable teleport rays.
            _xrClick = new InputAction("Playback Video Click", InputActionType.Button);
            _xrClick.AddBinding("<XRController>{LeftHand}/triggerPressed");
            _xrClick.AddBinding("<XRController>{RightHand}/triggerPressed");
            _xrClick.performed += OnXrClick;
            _xrClick.Enable();
        }
    }

    private void BindPlaybackMovement(Transform origin)
    {
        if (origin == null) return;
        _moveProvider = origin.GetComponentInChildren<ContinuousMoveProvider>();
        if (_moveProvider == null) return;

        var moveInput = _moveProvider.leftHandMoveInput;
        var action = moveInput.inputActionReference?.action;
        if (action == null ||
            moveInput.inputSourceMode != XRInputValueReader.InputSourceMode.InputActionReference) return;

        // Keep the configured stick binding independent of the interactor's shared action toggles.
        _originalMoveInput = moveInput;
        _playbackMove = action.Clone();
        _playbackMove.Enable();
        _moveProvider.leftHandMoveInput = new XRInputValueReader<Vector2>(
            "Playback Left Hand Move", XRInputValueReader.InputSourceMode.InputAction)
        {
            inputAction = _playbackMove
        };
    }

    private void OnXrClick(InputAction.CallbackContext context)
    {
        var stage = _flow.Stage;
        var controller = context.control.device as UnityEngine.InputSystem.XR.XRController;
        if (controller == null) return;
        Transform hand = controller == UnityEngine.InputSystem.XR.XRController.leftHand
            ? stage.XrLeftHand
            : stage.XrRightHand;
        if (hand == null) return;

        var ray = new Ray(hand.position, hand.forward);
        if (TargetsButton(ray, _recordButton)) _recordButton.onClick.Invoke();
        else if (TargetsButton(ray, _aspectButton)) _aspectButton.onClick.Invoke();
    }

    private static bool TargetsButton(Ray ray, Button button)
    {
        if (!button.interactable) return false;
        var rect = (RectTransform)button.transform;
        var plane = new Plane(rect.forward, rect.position);
        if (!plane.Raycast(ray, out float distance)) return false;
        Vector3 point = rect.InverseTransformPoint(ray.GetPoint(distance));
        return rect.rect.Contains(new Vector2(point.x, point.y));
    }

    private Button AddButton(RectTransform parent, Vector2 center, float width, string title, Action clicked,
        out TextMeshProUGUI label)
    {
        var size = new Vector2(width, 36f);
        var image = Add<Image>(parent, title, center, size);
        image.color = RecordColor;
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        // Head-locked controls should only act on a pointer click.
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(() => clicked());
        label = AddLabel(image.rectTransform, "Label", Vector2.zero, size, title, 21f);
        return button;
    }

    private TextMeshProUGUI AddLabel(RectTransform parent, string name, Vector2 center, Vector2 size, string title, float fontSize)
    {
        var label = Add<TextMeshProUGUI>(parent, name, center, size);
        label.text = title;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }

    private static T Add<T>(RectTransform parent, string name, Vector2 center, Vector2 size) where T : Graphic
    {
        var item = new GameObject(name, typeof(RectTransform), typeof(T));
        // Keep the whole panel out of the mono camera's world-space render.
        item.layer = parent.gameObject.layer;
        var rect = (RectTransform)item.transform;
        rect.SetParent(parent, false);
        rect.anchoredPosition = center;
        rect.sizeDelta = size;
        return item.GetComponent<T>();
    }

    private void ToggleAspect()
    {
        if (_recorder != null) return;
        _fourByThree = !_fourByThree;
        ApplyAspect();
    }

    private void ApplyAspect()
    {
        int width = _fourByThree ? 1440 : 1920;
        const int height = 1080;
        float aspect = (float)width / height;
        var previous = _target;
        _target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
        {
            name = "Playback Film Render Texture",
            antiAliasing = 1
        };
        _target.Create();
        _filmCamera.targetTexture = _target;
        _filmCamera.aspect = aspect;
        float halfWidth = Mathf.Tan(FilmHorizontalFov * Mathf.Deg2Rad / 2f);
        _filmCamera.fieldOfView = 2f * Mathf.Atan(halfWidth / aspect) * Mathf.Rad2Deg;
        if (_preview != null)
        {
            _preview.texture = _target;
            _preview.rectTransform.sizeDelta = new Vector2(126f * aspect, 126f);
            _frame.sizeDelta = _preview.rectTransform.sizeDelta + Vector2.one * 4f;
        }
        _aspectLabel.text = _fourByThree ? "4:3" : "16:9";
        ReleaseTexture(previous);
    }

    private void BeginFilmRender(ScriptableRenderContext context, Camera camera)
    {
        if (camera != _filmCamera || _source == null || _target == null) return;

        if (_viewfinder != null)
        {
            Rect visible = ProjectionBounds(_source.projectionMatrix);
            if (_source.stereoEnabled)
            {
                Rect left = ProjectionBounds(_source.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left));
                Rect right = ProjectionBounds(_source.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right));
                Rect overlap = Rect.MinMaxRect(Mathf.Max(left.xMin, right.xMin), Mathf.Max(left.yMin, right.yMin),
                    Mathf.Min(left.xMax, right.xMax), Mathf.Min(left.yMax, right.yMax));
                if (overlap.width > 0f && overlap.height > 0f) visible = overlap;
            }

            Rect crop = ProjectionBounds(camera.projectionMatrix);
            // Project the film frustum onto a plane at a typical subject distance.
            float scale = (FramingReferenceDistance + FilmCameraBackOffset) / FramingReferenceDistance;
            crop = Rect.MinMaxRect(crop.xMin * scale, crop.yMin * scale, crop.xMax * scale, crop.yMax * scale);
            PositionViewfinder(visible, crop);
        }

        var origin = _flow.Stage.XrOrigin;
        if (_xrVisualLayer < 0) return;

        if (_flow.Stage.UseXR && origin != null) ExcludeVisualsForFilm(origin.gameObject);
        foreach (var taggedRoot in GameObject.FindGameObjectsWithTag(VideoExclusionTag))
            ExcludeVisualsForFilm(taggedRoot);
    }

    private void ExcludeVisualsForFilm(GameObject root)
    {
        root.GetComponentsInChildren(false, _visualRenderers);
        foreach (var renderer in _visualRenderers)
        {
            var visual = renderer.gameObject;
            if (visual.layer == _xrVisualLayer) continue;
            _originalLayers.Add(visual, visual.layer);
            visual.layer = _xrVisualLayer;
        }
    }

    private void EndFilmRender(ScriptableRenderContext context, Camera camera)
    {
        if (camera == _filmCamera) RestoreVisualLayers();
    }

    private void RestoreVisualLayers()
    {
        foreach (var entry in _originalLayers)
        {
            if (entry.Key != null) entry.Key.layer = entry.Value;
        }
        _originalLayers.Clear();
    }

    private static Rect ProjectionBounds(Matrix4x4 projection)
    {
        return Rect.MinMaxRect((projection.m02 - 1f) / projection.m00, (projection.m12 - 1f) / projection.m11,
            (projection.m02 + 1f) / projection.m00, (projection.m12 + 1f) / projection.m11);
    }

    private void PositionViewfinder(Rect visible, Rect crop)
    {
        float units = ViewfinderDistance / ViewfinderScale;
        const float stroke = 2f;
        Color color = _recorder == null ? Color.white : RecordColor;
        SetEdge(0, new Vector2(crop.center.x * units, crop.yMax * units + stroke), new Vector2(crop.width * units, stroke));
        SetEdge(1, new Vector2(crop.center.x * units, crop.yMin * units - stroke), new Vector2(crop.width * units, stroke));
        SetEdge(2, new Vector2(crop.xMin * units - stroke, crop.center.y * units), new Vector2(stroke, crop.height * units));
        SetEdge(3, new Vector2(crop.xMax * units + stroke, crop.center.y * units), new Vector2(stroke, crop.height * units));

        float panelHalfHeight = _controls.rect.height / 2f;
        float desiredY = crop.yMin * units - panelHalfHeight - stroke * 2f;
        // Keep the buttons reachable when the film frame reaches the bottom of the headset view.
        float minimumY = visible.yMin * units + panelHalfHeight + stroke * 2f;
        _controls.localPosition = new Vector3(crop.center.x * units, Mathf.Max(desiredY, minimumY), 0f);

        void SetEdge(int index, Vector2 position, Vector2 size)
        {
            var edge = _viewfinder[index];
            edge.rectTransform.localPosition = position;
            edge.rectTransform.sizeDelta = size;
            edge.color = color;
        }
    }

    private void ToggleRecording()
    {
        if (_recorder != null)
        {
            StopRecording();
            return;
        }

        try
        {
            if (!Directory.Exists(_directory))
                throw new DirectoryNotFoundException(_directory);

            string name = $"Video_{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}";
            string output = Path.Combine(_directory, name);
            for (int suffix = 2; File.Exists(output + ".mp4"); suffix++)
                output = Path.Combine(_directory, $"{name}_{suffix}");

            _settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            _settings.SetRecordModeToManual();
            // Preserve real-time pacing while the player operates the camera in VR.
            _settings.FrameRatePlayback = FrameRatePlayback.Variable;
            _settings.CapFrameRate = false;
            _settings.ExitPlayMode = false;

            _movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            _movie.Enabled = true;
#if UNITY_EDITOR_WIN
            _movie.EncoderSettings = new MonitoredPlaybackEncoderSettings();
#else
            _movie.EncoderSettings = new CoreEncoderSettings { Codec = CoreEncoderSettings.OutputCodec.MP4 };
#endif
            _movie.ImageInputSettings = new RenderTextureInputSettings { RenderTexture = _target };
            _movie.CaptureAudio = true;
            _movie.OutputFile = output;
            _settings.AddRecorderSettings(_movie);

            _recorder = new RecorderController(_settings);
            _recorder.PrepareRecording();
            if (!_recorder.StartRecording()) throw new InvalidOperationException("Unity Recorder konnte nicht starten.");
            _output = output + ".mp4";
            if (_frameImage != null) _frameImage.color = RecordColor;
            _recordLabel.text = "Video stoppen";
            _aspectButton.interactable = false;
            _status.text = "Aufnahme läuft";
        }
        catch (Exception exception)
        {
            StopRecording();
            _status.text = "Aufnahme konnte nicht starten";
            Debug.LogException(exception, _flow);
        }
    }

    private void StopRecording()
    {
        try
        {
            _recorder?.StopRecording();
            if (_output != null && File.Exists(_output))
            {
                if (_status != null) _status.text = "Video gespeichert";
                Debug.Log($"Video gespeichert: {_output}", _flow);
            }
            else if (_output != null)
            {
                if (_status != null) _status.text = "Keine Videodatei erzeugt";
                Debug.LogWarning($"Keine Videodatei erzeugt: {_output}", _flow);
            }
        }
        catch (Exception exception)
        {
            if (_status != null) _status.text = "Video konnte nicht gespeichert werden";
            Debug.LogException(exception, _flow);
        }
        finally
        {
            _recorder = null;
            _output = null;
            Object.Destroy(_settings);
            Object.Destroy(_movie);
            _settings = null;
            _movie = null;
            if (_recordLabel != null) _recordLabel.text = "Video starten";
            if (_aspectButton != null) _aspectButton.interactable = true;
            if (_frameImage != null) _frameImage.color = Color.white;
        }
    }

    private static void ReleaseTexture(RenderTexture texture)
    {
        if (texture == null) return;
        texture.Release();
        Object.Destroy(texture);
    }

    public void Dispose()
    {
        RenderPipelineManager.beginCameraRendering -= BeginFilmRender;
        RenderPipelineManager.endCameraRendering -= EndFilmRender;
        RestoreVisualLayers();
        if (_moveProvider != null && _originalMoveInput != null)
            _moveProvider.leftHandMoveInput = _originalMoveInput;
        _playbackMove?.Dispose();
        if (_xrClick != null)
        {
            _xrClick.performed -= OnXrClick;
            _xrClick.Dispose();
        }
        StopRecording();
        if (_filmCamera != null)
        {
            _filmCamera.enabled = false;
            _filmCamera.targetTexture = null;
            Object.Destroy(_filmCamera.gameObject);
        }
        if (_panel != null)
        {
            _panel.SetActive(false);
            Object.Destroy(_panel);
        }
        ReleaseTexture(_target);
    }
}
