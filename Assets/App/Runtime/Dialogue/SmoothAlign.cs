using UnityEngine;

namespace App.Runtime.Dialogue
{
    public class SmoothAlign
    {
        private Vector3 _fromPos, _toPos;
        private float _fromYaw, _toYaw;
        private float _t;
        private float _dur;
        private bool _active;

        public bool Active => _active;

        public void StartAlign(Vector3 fromPos, float fromYaw, Vector3 toPos, float toYaw, float durationSec)
        {
            _fromPos = fromPos;
            _fromYaw = fromYaw;
            _toPos = toPos;
            _toYaw = toYaw;
            _dur = Mathf.Max(0.001f, durationSec);
            _t = 0f;
            _active = true;
        }

        public void Tick(float dt)
        {
            if (!_active) return;
            _t += dt;
            if (_t >= _dur) { _t = _dur; _active = false; }
        }

        public void GetCurrent(out Vector3 pos, out float yaw)
        {
            float a = Mathf.Clamp01(_t / _dur);
            // weich (smoothstep)
            a = a * a * (3f - 2f * a);

            pos = Vector3.Lerp(_fromPos, _toPos, a);
            yaw = Mathf.LerpAngle(_fromYaw, _toYaw, a);
        }
    }
}
