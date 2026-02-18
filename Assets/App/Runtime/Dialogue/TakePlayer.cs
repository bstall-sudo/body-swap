using UnityEngine;

namespace App.Runtime.Dialogue
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
        private bool _playing;

        public bool IsPlaying => _playing;

        public TakePlayer(Transform actorRoot, Transform head, Transform left, Transform right, AudioSource audio)
        {
            _actorRoot = actorRoot;
            _headT = head; _leftT = left; _rightT = right;
            _audio = audio;
        }

        public void Begin(TakeData take)
        {
            
            _take = take;
            _i = 0;
            _playing = take != null && take.Frames.Count > 0;

            if (!_playing) return;

            _dspStart = AudioSettings.dspTime + 0.05; // kleine Vorlaufzeit

            if (_audio != null && take.AudioClip != null)
            {
                _audio.clip = take.AudioClip;
                _audio.Play();
               
                
              
            }
        }

        public void Tick()
        {
            if (!_playing || _take == null) return;

            if (_audio == null || _audio.clip == null || !_audio.isPlaying)
                return;

            float t = (float)_audio.timeSamples / _audio.clip.frequency;

            var frames = _take.Frames;
            if (frames.Count == 0) { _playing = false; return; }

            // Wenn wir schon am Ende sind
            if (_i >= frames.Count - 1)
            {
                ApplyFrame(frames[^1]);
                _playing = false;
                return;
            }

            // _i so weit vorschieben, bis frames[_i] <= t <= frames[_i+1]
            while (_i < frames.Count - 2 && frames[_i + 1].T < t)
                _i++;

            var a = frames[_i];
            var b = frames[_i + 1];

            float dt = b.T - a.T;
            float u = (dt > 0.0001f) ? Mathf.Clamp01((t - a.T) / dt) : 0f;

            // Body interpolieren (Pos lerp, Yaw lerpAngle)
            Vector3 bodyPos = Vector3.Lerp(a.Body.Pos, b.Body.Pos, u);
            float bodyYaw = Mathf.LerpAngle(a.Body.YawDeg, b.Body.YawDeg, u);

            _actorRoot.localPosition = bodyPos;
            _actorRoot.localRotation = Quaternion.Euler(0f, bodyYaw, 0f);

            // Head/Hands interpolieren (Pos lerp, Rot slerp)
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


        public void Stop()
        {
            _playing = false;
            if (_audio != null && _audio.isPlaying) _audio.Stop();
        }
    }
}
