using System;
using Eggverse;

/// Walking is a MonoBehaviour, but the maths in it is not. Reproducing the rim clamp here lets
/// the one thing that is easy to get wrong - what happens when you walk into the edge of the
/// world - be checked without an engine.
static class Movement
{
    const float R = 34f, Speed = 13f, Dt = 1f / 60f, Smoothing = 14f, RimFriction = 0.92f;

    /// Holds a direction into the rim for half a second and reports how far along it you get.
    static float SlideDistance(bool projectOntoRim, float degreesOffNormal)
    {
        float hx = (float)Math.Cos(degreesOffNormal * Math.PI / 180.0);
        float hy = (float)Math.Sin(degreesOffNormal * Math.PI / 180.0);
        float px = R * 0.999f, py = 0f, vx = hx * Speed, vy = hy * Speed, travelled = 0f;

        for (int i = 0; i < 30; i++)
        {
            float k = 1f - (float)Math.Exp(-Smoothing * Dt);
            vx += (hx * Speed - vx) * k;
            vy += (hy * Speed - vy) * k;

            float nx = px + vx * Dt, ny = py + vy * Dt;
            float m = (float)Math.Sqrt(nx * nx + ny * ny);
            if (m > R)
            {
                float ox = nx / m, oy = ny / m;
                nx = ox * R; ny = oy * R;
                if (projectOntoRim)
                {
                    float ax = -oy, ay = ox;
                    float d = vx * ax + vy * ay;
                    vx = ax * d * RimFriction; vy = ay * d * RimFriction;
                }
                else { vx *= 0.35f; vy *= 0.35f; }
            }
            travelled += (float)Math.Sqrt((nx - px) * (nx - px) + (ny - py) * (ny - py));
            px = nx; py = ny;
        }
        return travelled;
    }

    /// Simulates a crossing at full throttle from rest, the way a player actually flies it.
    static float RealFlightTime(float distance)
    {
        const float A = 46f, K = 1.4f, Cap = 22f;
        float v = 0f, d = 0f, t = 0f;
        while (d < distance && t < 60f)
        {
            v = Math.Min(Cap, (v + A * Dt) * (float)Math.Exp(-K * Dt));
            d += v * Dt; t += Dt;
        }
        return t;
    }

    public static void Run(Action<bool, string> check)
    {
        // The chart quotes a flight time for every pairing of worlds. It has to be close to what
        // flying it actually costs, or it is decoration - distance over top speed alone is
        // optimistic by a third of a second, which is a fifth of a short hop.
        foreach (var a in PlanetDatabase.All)
            foreach (var b in PlanetDatabase.All)
            {
                if (a.Id == b.Id) continue;
                float gap = Math.Max(0f, (a.SpacePosition - b.SpacePosition).magnitude
                                          - a.SpaceRadius - b.SpaceRadius);
                float quoted = TeoController.FlightSeconds(gap);
                float real = RealFlightTime(gap);
                check(Math.Abs(quoted - real) <= 0.25f,
                      $"the chart's flight time for {a.Name} to {b.Name} is honest " +
                      $"(says {quoted:0.0}s, takes {real:0.0}s)");
            }

        // The old clamp scaled the whole velocity every frame it was against the rim, so it
        // collapsed within a few frames and walking along the edge stalled.
        foreach (float angle in new[] { 20f, 40f, 60f, 80f })
        {
            float stalled = SlideDistance(false, angle);
            float slides = SlideDistance(true, angle);
            Console.WriteLine($"  holding {angle,2:0}deg into the rim: stall {stalled:0.0}u, slide {slides:0.0}u");
            check(slides > stalled,
                  $"sliding beats stalling at {angle:0} degrees ({slides:0.0} against {stalled:0.0})");
        }

        // And it must not become a slingshot: the rim can never add speed.
        float straightOn = SlideDistance(true, 0f);
        check(straightOn < 1f,
              $"walking straight at the rim still stops you ({straightOn:0.00}u)");
        check(RimFriction < 1f, "the rim takes a little speed rather than giving any");
    }
}
