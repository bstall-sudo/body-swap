using UnityEngine;
using UnityEngine.InputSystem;
using System.Diagnostics;
using System.Collections;

using AppV2.Runtime.Scripts.Input;
using AppV2.Runtime.Scripts.Dialogue.Persistence;

namespace AppV2.Runtime.Scripts.Dialogue.Persistence
{
    public class TakeRecorder
    {
        private readonly Transform _stageRoot;     // TakeRoot
        private readonly Transform _roleRoot;     // ActorA oder ActorB
        private readonly string _roleId;
        private readonly int _roleIndex;
        private readonly IInputTransformsProvider _input;    // XRInputProvider (Anchor schon gesetzt)
        private readonly int _micDeviceIndex;
        private int _sampleRate;

        private bool _waitingForMic;
        private string _device;
        private int _micStartSamplePos;

        // f�r das Rebase, damit die neue Aufnahme so aufgenommen wird, dass es keinen "Sprung" gibt vom Ende der letzten Aufnahme
        private bool _hasDesiredStart = false;
        private Vector3 _desiredStartPos;
        private float _desiredStartYaw;


        //f�r die Rebase-Berechnung
        private bool _hasRebase = false;
        private Vector3 _rebasePos;
        private float _rebaseYawDeg;


        private double _dspStart;
        private bool _recording;

        //das ist wichtig damit die Aufnahmen wieder auf 1,1,1 skaliert werden vor dem Abspeichern, auch wenn man eine kleinere Rolle spielt.
        private float _roleScale = 1f;

        public TakeData Current { get; private set; }

        // für das Rebase, damit die neue Aufnahme so aufgenommen wird, dass es keinen "Sprung" gibt vom Ende der letzten Aufnahme
        public void SetDesiredStartPose(Vector3 pos, float yawDeg)
        {
            _desiredStartPos = pos;
            _desiredStartYaw = yawDeg;   //rotation, wo die Figur am Ende der letzten Aufnahme stand.
            _hasDesiredStart = true;
        }

        public void SetRoleScale(float roleScale)
        {
            _roleScale = Mathf.Max(0.0001f, roleScale);
        }


        public TakeRecorder(Transform stageRoot, Transform roleRoot, string roleId, int roleIndex, IInputTransformsProvider input,
                            int micDeviceIndex = 0, int sampleRate = 48000)
        {
            _stageRoot = stageRoot;
            _roleRoot = roleRoot;
            _roleId = roleId;
            _roleIndex = roleIndex;
            _input = input;
            _micDeviceIndex = micDeviceIndex;
            _sampleRate = sampleRate;
        }

        private static string PickMicDevice()
        {
            var devices = Microphone.devices;
            if (devices == null || devices.Length == 0) return null;

            // Bevorzugt Headset-Mics (Vive/HTC), sonst erstes Ger�t
            foreach (var d in devices)
            {
                var low = d.ToLowerInvariant();
                if (low.Contains("vive") || low.Contains("htc") || low.Contains("vr"))
                    return d;
            }
            return devices[0];
        }

        private static int PickSupportedSampleRate(string device, int preferred = 48000)
        {
            Microphone.GetDeviceCaps(device, out int minHz, out int maxHz);

            // Manche Treiber geben 0/0 zur�ck -> dann nimm Unity OutputSampleRate
            if (minHz == 0 && maxHz == 0)
                return AudioSettings.outputSampleRate;

            // Preferred innerhalb Caps?
            if (preferred >= minHz && preferred <= maxHz)
                return preferred;

            // Sonst nimm maxHz (oder minHz) als sicheren Wert
            return maxHz > 0 ? maxHz : minHz;
        }


        public void Begin()


        {
            //verstehe ich nicht...
            _hasRebase = false;

            //UnityEngine.Debug.Log("Mic devices: " + string.Join(" | ", Microphone.devices));

            Current = new TakeData();
            _recording = true;
            _device = PickMicDevice();
            if (string.IsNullOrEmpty(_device))
            {
                UnityEngine.Debug.LogWarning("No microphone device found.");
                Current.AudioClip = null;
                _waitingForMic = false;
                _dspStart = AudioSettings.dspTime;
                return;
            }

            _sampleRate = PickSupportedSampleRate(_device, 48000);

            //UnityEngine.Debug.Log($"Using mic: '{_device}' @ {_sampleRate} Hz");
            //UnityEngine.Debug.Log("Mic devices: " + string.Join(" | ", Microphone.devices));

            Current.AudioClip = Microphone.Start(_device, false, 300, _sampleRate);
            _waitingForMic = true;
            _micStartSamplePos = 0;

        }


        public void Tick(float embodimentDeltaY)

        {
            if (_waitingForMic && !string.IsNullOrEmpty(_device))
            {
                int pos = Microphone.GetPosition(_device);
                if (pos > 0)
                {
                    //UnityEngine.Debug.Log("Mic started, first pos: " + pos);
                    // Ab jetzt ist Aufnahme wirklich gestartet
                    _micStartSamplePos = pos;
                    _dspStart = AudioSettings.dspTime;
                    _waitingForMic = false;
                }
                else
                {
                    return; // noch keine validen Samples -> auch keine Pose-Frames
                }
            }

            if (!_recording || Current == null) return;

            int micPos = Microphone.GetPosition(_device);
            int total = Current.AudioClip.samples;

            // samples since start (mit wrap)
            int sampleCount = micPos - _micStartSamplePos;
            if (sampleCount < 0) sampleCount += total;

            float t = (float)sampleCount / _sampleRate;

            // Live Posen vom Input (die kommen bei XR bei uns schon Anchor-local = stage-local) "out var" heisst, die out-Variablen werden direkt dort deklariert. 
            // if (!Methode()) return; -> wenn methode fehlschl�gt, zur�ckkehren, sonst weiter
            if (!_input.TryGetHeadPose(out var headP_stageLocal, out var headR_stageLocal)) return;
            _input.TryGetLeftHandPose(out var leftP_stageLocal, out var leftR_stageLocal);
            _input.TryGetRightHandPose(out var rightP_stageLocal, out var rightR_stageLocal);

            //f�r das Rebase yawDeg => _desiredStartYaw (rotation)
            static Quaternion YawRot(float yawDeg) => Quaternion.Euler(0f, yawDeg, 0f);

            // BodyPose aus Head ableiten: Bodenposition + yaw
            Vector3 bodyPos = headP_stageLocal; bodyPos.y = 0f;
            float yaw = headR_stageLocal.eulerAngles.y;

            // Rebase einmalig berechnen: measuredStart -> desiredStart
            if (_hasDesiredStart && !_hasRebase)
            {
                float deltaYaw = Mathf.DeltaAngle(yaw, _desiredStartYaw); // desired - measured (als shortest angle)
                Quaternion qDelta = YawRot(deltaYaw);

                // rebasePos so, dass: desired = rebasePos + qDelta * measured
                _rebasePos = _desiredStartPos - (qDelta * bodyPos);
                _rebaseYawDeg = deltaYaw;
                _hasRebase = true;
            }

            if (_hasRebase)
            {
                Quaternion qDelta = Quaternion.Euler(0f, _rebaseYawDeg, 0f);

                // Body
                bodyPos = _rebasePos + (qDelta * bodyPos);
                yaw = Mathf.Repeat(yaw + _rebaseYawDeg, 360f);

                // Head / Hands POS
                headP_stageLocal = _rebasePos + (qDelta * headP_stageLocal);
                leftP_stageLocal = _rebasePos + (qDelta * leftP_stageLocal);
                rightP_stageLocal = _rebasePos + (qDelta * rightP_stageLocal);

                // Head / Hands ROT
                headR_stageLocal = qDelta * headR_stageLocal;
                leftR_stageLocal = qDelta * leftR_stageLocal;
                rightR_stageLocal = qDelta * rightR_stageLocal;
            }

            //optional normalisieren, falls yaw �ber 360 Grad
            yaw = Mathf.Repeat(yaw, 360f);

            // roleRoot entsprechend setzen (stage-local)
            _roleRoot.localPosition = bodyPos;
            _roleRoot.localRotation = Quaternion.Euler(0f, yaw, 0f);

            // Head/Hands relativ zum roleRoot speichern:
            // stage-local -> actor-local
            var actorRot = _roleRoot.localRotation;
            var invActorRot = Quaternion.Inverse(actorRot);

            
            PoseSample ToActorLocal(Vector3 pStage, Quaternion rStage)
            {
                Vector3 delta = pStage - bodyPos;
                var pLocal = invActorRot * delta;
                pLocal.y -= embodimentDeltaY;
                var rLocal = invActorRot * rStage;
                return new PoseSample { Pos = pLocal, Rot = rLocal };
            }
            

            /*
            PoseSample ToActorLocalNeutral(Vector3 pStage, Quaternion rStage)
            {
                Vector3 delta = pStage - bodyPos;
                Vector3 pLocalEmbodied = invActorRot * delta;

                // WICHTIG:
                // Für die gespeicherten Daten wieder neutralisieren
                Vector3 pLocalNeutral = pLocalEmbodied / _roleScale;

                Quaternion rLocal = invActorRot * rStage;
                return new PoseSample { Pos = pLocalNeutral, Rot = rLocal };
            }
            */

            var f = new Frame
            {
                T = t,
                Body = new BodyPose { Pos = bodyPos, YawDeg = yaw },
                Head = ToActorLocal(headP_stageLocal, headR_stageLocal),
                Left = ToActorLocal(leftP_stageLocal, leftR_stageLocal),
                Right = ToActorLocal(rightP_stageLocal, rightR_stageLocal),
            };

            Current.Frames.Add(f);
            Current.DurationSec = t;
        }

        public (AudioClip clip, int startSample, int sampleCount, int channels, int sampleRate)? EndAndGetTrimInfo()
        {
            if (!_recording) return null;
            _recording = false;

            if (string.IsNullOrEmpty(_device) || Current?.AudioClip == null)
                return null;

            int endPos = Microphone.GetPosition(_device);
            int channels = Current.AudioClip.channels;

            if (Microphone.IsRecording(_device))
                Microphone.End(_device);

            if (endPos <= 0) return null;

            int totalSamples = Current.AudioClip.samples;

            int sampleCount = endPos - _micStartSamplePos;
            if (sampleCount < 0) sampleCount += totalSamples;

            if (sampleCount <= 0 || sampleCount > totalSamples) return null;

            //UnityEngine.Debug.Log("Chosen Mic device is: " + _device);

            // Wichtig: hier KEIN GetData, nur Info zur�ckgeben
            return (Current.AudioClip, _micStartSamplePos, sampleCount, channels, _sampleRate);
        }

        public static AudioClip TrimMicClip(AudioClip source, int startSample, int sampleCount, int channels, int sampleRate)
        {
            float[] data = new float[sampleCount * channels];
            int totalSamples = source.samples;

            if (startSample + sampleCount <= totalSamples)
            {
                source.GetData(data, startSample);
            }
            else
            {
                int firstPart = totalSamples - startSample;
                float[] data1 = new float[firstPart * channels];
                float[] data2 = new float[(sampleCount - firstPart) * channels];

                source.GetData(data1, startSample);
                source.GetData(data2, 0);

                System.Array.Copy(data1, 0, data, 0, data1.Length);
                System.Array.Copy(data2, 0, data, data1.Length, data2.Length);
            }

            var trimmed = AudioClip.Create("TakeAudioTrimmed", sampleCount, channels, sampleRate, false);
            trimmed.SetData(data, 0);
            return trimmed;
        }



    }
}
