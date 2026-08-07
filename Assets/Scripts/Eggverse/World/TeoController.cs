using System.Collections.Generic;
using UnityEngine;

namespace Eggverse
{
    public enum TeoMovement { Flying, Walking, Frozen }

    /// <summary>Teo himself: drifty thruster flight in space, tighter walking on a planet.</summary>
    public class TeoController : MonoBehaviour
    {
        public TeoMovement Movement = TeoMovement.Frozen;

        [HideInInspector] public float SurfaceRadius = 34f;

        public Vector2 Velocity { get; private set; }
        public float DistanceMovedThisFrame { get; private set; }

        const float FlyAccel = 46f;
        /// <summary>Top speed in space. Public so the chart can turn a distance into a time
        /// rather than keeping its own copy of the number.</summary>
        public const float FlyMaxSpeed = 22f;
        const float FlyDrag = 1.4f;
        const float WalkSpeed = 13f;
        const float WalkSmoothing = 14f;

        SpriteRenderer body;
        Transform visual;
        float bobTimer;

        readonly List<SpriteRenderer> puffPool = new List<SpriteRenderer>();
        readonly List<float> puffLife = new List<float>();
        float puffTimer;
        Sprite puffSprite;

        public void Build()
        {
            visual = new GameObject("Visual").transform;
            visual.SetParent(transform, false);

            var go = new GameObject("Body");
            go.transform.SetParent(visual, false);
            body = go.AddComponent<SpriteRenderer>();
            body.sprite = ProcArt.Teo();
            body.material = ProcArt.SpriteMaterial;
            body.sortingOrder = 20;

            puffSprite = ProcArt.Disc("thrust", new Color(1f, 0.85f, 0.55f, 1f), new Color(1f, 0.4f, 0.1f, 0f), 1.6f, 64, 64f);
        }

        public void Warp(Vector2 position)
        {
            transform.position = new Vector3(position.x, position.y, 0f);
            Velocity = Vector2.zero;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Vector3 before = transform.position;

            switch (Movement)
            {
                case TeoMovement.Flying: TickFlying(dt); break;
                case TeoMovement.Walking: TickWalking(dt); break;
                default: Velocity = Vector2.MoveTowards(Velocity, Vector2.zero, dt * 40f); break;
            }

            DistanceMovedThisFrame = Vector3.Distance(before, transform.position);
            TickVisual(dt);
            TickPuffs(dt);
        }

        void TickFlying(float dt)
        {
            Vector2 input = EggInput.Move;
            Vector2 v = Velocity + input * (FlyAccel * dt);
            v *= Mathf.Exp(-FlyDrag * dt);
            if (v.magnitude > FlyMaxSpeed) v = v.normalized * FlyMaxSpeed;
            Velocity = v;
            transform.position += (Vector3)(v * dt);

            if (input.sqrMagnitude > 0.01f)
            {
                puffTimer -= dt;
                if (puffTimer <= 0f)
                {
                    puffTimer = 0.045f;
                    SpawnPuff(-input.normalized);
                }
            }
        }

        void TickWalking(float dt)
        {
            Vector2 target = EggInput.Move * WalkSpeed;
            Velocity = Vector2.Lerp(Velocity, target, 1f - Mathf.Exp(-WalkSmoothing * dt));
            Vector3 next = transform.position + (Vector3)(Velocity * dt);

            // Stay on the walkable disc.
            Vector2 flat = new Vector2(next.x, next.y);
            if (flat.magnitude > SurfaceRadius)
            {
                flat = flat.normalized * SurfaceRadius;
                Velocity *= 0.35f;
            }
            transform.position = new Vector3(flat.x, flat.y, 0f);
        }

        void TickVisual(float dt)
        {
            if (visual == null) return;
            bobTimer += dt * (Movement == TeoMovement.Flying ? 2.2f : 6.5f);
            float bob = Mathf.Sin(bobTimer) * (Movement == TeoMovement.Flying ? 0.10f : 0.06f);
            float bank = Mathf.Clamp(-Velocity.x * 1.1f, -18f, 18f);
            visual.localPosition = new Vector3(0f, bob, 0f);
            visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(visual.localEulerAngles.z, bank, 1f - Mathf.Exp(-10f * dt)));

            if (body != null && Mathf.Abs(Velocity.x) > 0.4f)
            {
                var s = body.transform.localScale;
                s.x = Mathf.Abs(s.x) * (Velocity.x < 0f ? -1f : 1f);
                body.transform.localScale = s;
            }
        }

        // ---------- thruster puffs ----------

        void SpawnPuff(Vector2 direction)
        {
            int index = -1;
            for (int i = 0; i < puffPool.Count; i++)
            {
                if (puffLife[i] <= 0f) { index = i; break; }
            }

            if (index < 0)
            {
                var go = new GameObject("Puff");
                go.transform.SetParent(transform.parent, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = puffSprite;
                sr.material = ProcArt.SpriteMaterial;
                sr.sortingOrder = 15;
                puffPool.Add(sr);
                puffLife.Add(0f);
                index = puffPool.Count - 1;
            }

            SpriteRenderer puff = puffPool[index];
            puffLife[index] = 0.45f;
            puff.transform.position = transform.position + (Vector3)(direction * 0.55f);
            puff.transform.localScale = Vector3.one * Random.Range(0.35f, 0.55f);
            puff.enabled = true;
        }

        void TickPuffs(float dt)
        {
            for (int i = 0; i < puffPool.Count; i++)
            {
                if (puffLife[i] <= 0f) continue;
                puffLife[i] -= dt;
                float t = Mathf.Clamp01(puffLife[i] / 0.45f);
                var sr = puffPool[i];
                sr.color = new Color(1f, 1f, 1f, t * 0.75f);
                sr.transform.localScale = Vector3.one * Mathf.Lerp(0.15f, 0.6f, t);
                if (puffLife[i] <= 0f) sr.enabled = false;
            }
        }

        public void ClearPuffs()
        {
            for (int i = 0; i < puffPool.Count; i++)
            {
                puffLife[i] = 0f;
                if (puffPool[i] != null) puffPool[i].enabled = false;
            }
        }

    }
}
