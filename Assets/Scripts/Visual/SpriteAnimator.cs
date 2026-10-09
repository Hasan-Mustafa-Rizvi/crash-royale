using System;
using CrashRoyale.Data;
using UnityEngine;

namespace CrashRoyale.Visual
{
    /// <summary>Plays <see cref="SpriteAnim"/> flipbooks on a SpriteRenderer and raises an event on the hit frame.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteAnimator : MonoBehaviour
    {
        [SerializeField] private SpriteAnim playOnStart = new SpriteAnim();
        [SerializeField] private bool destroyWhenFinished;
        [SerializeField] private bool useUnscaledTime;

        private SpriteRenderer spriteRenderer;
        private SpriteAnim current;
        private float time;
        private float speed = 1f;
        private int lastRawFrame = -1;
        private bool finished;

        /// <summary>Raised when the current animation reaches its event frame.</summary>
        public event Action EventFrame;

        /// <summary>Raised when a non-looping animation reaches its last frame.</summary>
        public event Action Finished;

        public SpriteAnim Current => current;
        public bool IsFinished => finished;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void Start()
        {
            if (current == null && playOnStart != null && playOnStart.IsValid) Play(playOnStart);
        }

        public void Configure(SpriteAnim anim, bool destroyOnFinish)
        {
            playOnStart = anim;
            destroyWhenFinished = destroyOnFinish;
        }

        public void Play(SpriteAnim anim, bool restart = false, float playbackSpeed = 1f)
        {
            if (anim == null || !anim.IsValid) return;
            speed = Mathf.Max(0.01f, playbackSpeed);
            if (anim == current && !restart && !finished) return;

            current = anim;
            time = 0f;
            lastRawFrame = -1;
            finished = false;
            Apply(0);
        }

        private void Update()
        {
            if (current == null || finished) return;

            var anim = current;
            time += (useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime) * speed;
            int count = anim.frames.Length;
            int raw = Mathf.FloorToInt(time * anim.fps);

            if (anim.loop)
            {
                if (anim.eventFrame >= 0)
                {
                    int cycle = raw / count;
                    int eventAbsolute = cycle * count + anim.eventFrame;
                    bool crossed = lastRawFrame < eventAbsolute && raw >= eventAbsolute;
                    lastRawFrame = raw;
                    if (crossed)
                    {
                        EventFrame?.Invoke();
                        if (current != anim) return;
                    }
                }

                lastRawFrame = raw;
                Apply(raw % count);
                return;
            }

            int frame = Mathf.Min(raw, count - 1);
            bool hitEvent = anim.eventFrame >= 0 && lastRawFrame < anim.eventFrame && raw >= anim.eventFrame;
            lastRawFrame = raw;
            Apply(frame);

            if (hitEvent)
            {
                EventFrame?.Invoke();
                if (current != anim) return;
            }

            if (raw >= count)
            {
                finished = true;
                Finished?.Invoke();
                if (destroyWhenFinished) Destroy(gameObject);
            }
        }

        private void Apply(int frame)
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            spriteRenderer.sprite = current.frames[Mathf.Clamp(frame, 0, current.frames.Length - 1)];
        }
    }
}
