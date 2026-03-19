using UnityEngine;


namespace AppV2.Runtime.Scripts.Dialogue.Persistence
{
    public class TakePlayer
    {
        private readonly Transform _actorRoot;
        private readonly Transform _headT;
        private readonly Transform _leftT;
        private readonly Transform _rightT;
        private readonly AudioSource _audio;

        private TakeData _take;
        private int _i;
        private double _dspStart;
        public bool _playing;

        private float _fallbackStartTime;

        public bool IsPlaying => _playing;

        public TakePlayer(Transform actorRoot, Transform head, Transform left, Transform right, AudioSource audio)
        {
            _actorRoot = actorRoot;
            _headT = head; _leftT = left; _rightT = right;
            _audio = audio;
        }

        public void Begin(TakeData take)
        {
            Stop();

            _take = take;
            _i = 0;
            _playing = take != null && take.Frames != null && take.Frames.Count > 0;

            if (!_playing) return;

            if (_audio != null && take.AudioClip != null)
            {
                _audio.clip = take.AudioClip;

                _dspStart = AudioSettings.dspTime + 0.05;
                _audio.PlayScheduled(_dspStart);
            }
            else
            {
                _fallbackStartTime = Time.time;
                _dspStart = AudioSettings.dspTime;
            }

            UnityEngine.Debug.Log(
                $"TakePlayer.Begin called. Frames: {take?.Frames?.Count}, AudioClip: {take?.AudioClip != null}"
            );
        }

        public void Tick()
        {
            if (!_playing || _take == null)
                return;

            if (_audio == null)
            {
                UnityEngine.Debug.LogError("TakePlayer.Tick: AudioSource is null.");
                Stop();
                return;
            }

            if (_audio.clip == null)
            {
                UnityEngine.Debug.LogError("TakePlayer.Tick: Audio clip is null.");
                Stop();
                return;
            }

            if (!_audio.isPlaying)
            {
                Stop();
                return;
            }

            float t = (float)_audio.timeSamples / _audio.clip.frequency - 0.03f; //kleiner Versatz

            var frames = _take.Frames;
            if (frames == null || frames.Count == 0)
            {
                Stop();
                return;
            }

            if (_i >= frames.Count - 1)
            {
                ApplyFrame(frames[^1]);
                Stop();
                return;
            }

            while (_i < frames.Count - 2 && frames[_i + 1].T < t)
                _i++;

            var a = frames[_i];
            var b = frames[_i + 1];

            float dt = b.T - a.T;
            float u = (dt > 0.0001f) ? Mathf.Clamp01((t - a.T) / dt) : 0f;

            Vector3 bodyPos = Vector3.Lerp(a.Body.Pos, b.Body.Pos, u);
            float bodyYaw = Mathf.LerpAngle(a.Body.YawDeg, b.Body.YawDeg, u);

            _actorRoot.localPosition = bodyPos;
            _actorRoot.localRotation = Quaternion.Euler(0f, bodyYaw, 0f);

            if (_headT)
            {
                _headT.localPosition = Vector3.Lerp(a.Head.Pos, b.Head.Pos, u);
                _headT.localRotation = Quaternion.Slerp(a.Head.Rot, b.Head.Rot, u);
            }

            if (_leftT)
            {
                _leftT.localPosition = Vector3.Lerp(a.Left.Pos, b.Left.Pos, u);
                _leftT.localRotation = Quaternion.Slerp(a.Left.Rot, b.Left.Rot, u);
            }

            if (_rightT)
            {
                _rightT.localPosition = Vector3.Lerp(a.Right.Pos, b.Right.Pos, u);
                _rightT.localRotation = Quaternion.Slerp(a.Right.Rot, b.Right.Rot, u);
            }
        }

        private void ApplyFrame(Frame f)
        {
            _actorRoot.localPosition = f.Body.Pos;
            _actorRoot.localRotation = Quaternion.Euler(0f, f.Body.YawDeg, 0f);

            if (_headT) { _headT.localPosition = f.Head.Pos; _headT.localRotation = f.Head.Rot; }
            if (_leftT) { _leftT.localPosition = f.Left.Pos; _leftT.localRotation = f.Left.Rot; }
            if (_rightT) { _rightT.localPosition = f.Right.Pos; _rightT.localRotation = f.Right.Rot; }
        }

        //Stoppen und RAM leeren.
        public void Stop()
        {
            _playing = false;

            if (_audio != null)
            {
                if (_audio.isPlaying)
                {
                    _audio.Stop();
                }

                if (_audio.clip != null)
                {
                    AudioClip clipToDestroy = _audio.clip;
                    _audio.clip = null;
                    UnityEngine.Object.Destroy(clipToDestroy);
                }
            }

            _take = null;
            _i = 0;
            _fallbackStartTime = 0f;
            _dspStart = 0.0;
        }
    }
}
