#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;

namespace HowToVibeCoder;

/// <summary>
/// Tactile 1990s Soviet/Art-Deco open steel elevator junction box soldering mini-game.
/// Rendered as a physical electrical cabinet in 1280x720 virtual UI space via <see cref="UiViewport"/>.
/// </summary>
public sealed class ElevatorRepairMinigame
{
    private const float VirtualW = 1280f;
    private const float VirtualH = 720f;
    private const int PadCount = 3;
    private const float OverloadDuration = 0.55f;
    private const float MissRecoveryDuration = 0.22f;
    private const float OpenInputGraceDuration = 0.18f;

    private enum Phase
    {
        Idle = 0,
        Soldering = 1,
        Overload = 2,
        Completed = 3
    }

    private struct SparkParticle
    {
        public Vector2 Pos;
        public Vector2 Vel;
        public float Life;
        public float MaxLife;
        public float Size;
        public bool IsMissSpark;
    }

    private struct SmokeWisp
    {
        public Vector2 Pos;
        public Vector2 Vel;
        public float Life;
        public float MaxLife;
        public float Radius;
        public float DriftPhase;
        public bool Dark;
    }

    // Pad geometry in 1280x720 virtual space
    private static readonly Vector2[] PadPositions =
    [
        new(368f, 374f),
        new(640f, 374f),
        new(912f, 374f)
    ];

    private static readonly (string Ru, string En)[] PadLabels =
    [
        ("ПРИВОД ШАХТЫ", "SHAFT DRIVE"),
        ("РЕЛЕ ДВЕРЕЙ", "DOOR RELAY"),
        ("КОНТУР 0.1", "CIRCUIT 0.1")
    ];

    private static readonly string[] PadCodes = ["01", "02", "03"];

    // Sweet-spot parameters per pad: (center 0..1, width 0..1, sweep speed units/sec)
    private static readonly (float Center, float Width, float Speed)[] PadTiming =
    [
        (0.50f, 0.28f, 1.12f),
        (0.57f, 0.28f, 1.36f),
        (0.46f, 0.22f, 1.62f)
    ];

    // Physical material palette (1990s Soviet steel, aged brass, vintage emerald-olive PCB, warm ivory)
    private static readonly Color SteelDark = new(24, 28, 31, 255);
    private static readonly Color SteelMid = new(44, 50, 54, 255);
    private static readonly Color SteelLight = new(74, 82, 87, 255);
    private static readonly Color SteelHighlight = new(118, 127, 132, 165);
    private static readonly Color CavityDark = new(13, 16, 17, 255);
    private static readonly Color BrassBright = new(218, 179, 108, 255);
    private static readonly Color BrassMid = new(168, 132, 72, 255);
    private static readonly Color BrassDark = new(86, 63, 32, 255);
    private static readonly Color EngravedInk = new(34, 23, 13, 255);
    private static readonly Color PcbTop = new(27, 62, 46, 255);
    private static readonly Color PcbBottom = new(17, 42, 31, 255);
    private static readonly Color CopperDim = new(126, 92, 50, 255);
    private static readonly Color CopperLit = new(238, 172, 68, 255);
    private static readonly Color Silkscreen = new(226, 220, 202, 235);
    private static readonly Color IvoryFace = new(239, 229, 204, 255);
    private static readonly Color HazardYellow = new(218, 168, 36, 255);
    private static readonly Color HazardBlack = new(26, 24, 22, 255);
    private static readonly Color DangerRed = new(198, 38, 42, 255);

    private readonly bool[] _padSoldered = new bool[PadCount];
    private readonly float[] _padHeat = new float[PadCount];
    private readonly float[] _traceGlow = new float[PadCount + 1];
    private readonly List<SparkParticle> _sparks = new(128);
    private readonly List<SmokeWisp> _smoke = new(96);
    private readonly Random _rng = new(1998);

    private Phase _phase = Phase.Idle;
    private int _solderedCount;
    private float _elapsed;
    private float _openGraceTimer;
    private float _missRecoveryTimer;
    private float _missFlashTimer;
    private float _hitFlashTimer;
    private float _overloadTimer;
    private float _overloadSparkCooldown;
    private float _ambientSmokeTimer;

    private float _gaugePos = 0.16f;
    private float _gaugeDir = 1f;

    private Vector2 _ironHoverPos = PadPositions[0];
    private float _ironPressAnim;

    private float _needleVolts;
    private float _needleVel;
    private float _shakeAmount;

    private string _statusRu = "";
    private string _statusEn = "";
    private float _statusTimer;

    public bool IsActive { get; private set; }

    public void Open()
    {
        IsActive = true;
        _phase = Phase.Soldering;
        _solderedCount = 0;
        _elapsed = 0f;
        _openGraceTimer = OpenInputGraceDuration;
        _missRecoveryTimer = 0f;
        _missFlashTimer = 0f;
        _hitFlashTimer = 0f;
        _overloadTimer = 0f;
        _overloadSparkCooldown = 0f;
        _ambientSmokeTimer = 0f;
        _gaugePos = 0.14f;
        _gaugeDir = 1f;
        _ironHoverPos = PadPositions[0];
        _ironPressAnim = 0f;
        _needleVolts = 0f;
        _needleVel = 0f;
        _shakeAmount = 0f;
        _statusRu = "";
        _statusEn = "";
        _statusTimer = 0f;

        Array.Clear(_padSoldered);
        Array.Clear(_padHeat);
        Array.Clear(_traceGlow);
        _traceGlow[0] = 0.65f; // Live power present on incoming busbar
        _sparks.Clear();
        _smoke.Clear();

        SafePlayAudio(() => AudioMusicSystem.PlayActionClack(0.92f));
    }

    public void Close()
    {
        if (!IsActive) return;
        IsActive = false;
        _phase = Phase.Idle;
        _sparks.Clear();
        _smoke.Clear();
        _shakeAmount = 0f;
        SafePlayAudio(() => AudioMusicSystem.PlayActionClack(0.76f));
    }

    /// <summary>
    /// Deterministic mid-solder showcase state for --autotest screenshot capture (73_elevator_repair_minigame.png).
    /// </summary>
    public void SetPreviewStateForScreenshot(int solderedCount)
    {
        int count = Math.Clamp(solderedCount, 0, PadCount);
        IsActive = true;
        _solderedCount = count;
        _elapsed = 2.4f;
        _openGraceTimer = 0f;
        _missRecoveryTimer = 0f;
        _missFlashTimer = 0f;
        _hitFlashTimer = count > 0 ? 0.35f : 0f;
        _shakeAmount = 0f;
        _statusRu = "";
        _statusEn = "";
        _statusTimer = 0f;

        Array.Clear(_padSoldered);
        Array.Clear(_padHeat);
        Array.Clear(_traceGlow);
        _traceGlow[0] = 1f;
        for (int i = 0; i < count; i++)
        {
            _padSoldered[i] = true;
            _padHeat[i] = i == count - 1 ? 0.42f : 0.10f;
            _traceGlow[i + 1] = 1f;
        }

        _needleVolts = TargetVoltageForCount(count);
        _needleVel = 0f;

        _sparks.Clear();
        _smoke.Clear();

        if (count >= PadCount)
        {
            _phase = Phase.Overload;
            _overloadTimer = OverloadDuration * 0.55f;
            _ironHoverPos = PadPositions[PadCount - 1];
            _ironPressAnim = 0.45f;
            _gaugePos = 0.50f;
            SeedPreviewParticles(PadPositions[PadCount - 1], 24, 10);
        }
        else
        {
            _phase = Phase.Soldering;
            _overloadTimer = 0f;
            _ironHoverPos = PadPositions[count];
            _ironPressAnim = 0.36f;
            var timing = PadTiming[count];
            _gaugePos = timing.Center + timing.Width * 0.08f;
            _gaugeDir = 1f;
            SeedPreviewParticles(PadPositions[count], 18, 9);
        }
    }

    public bool Update(float dt)
    {
        if (!IsActive) return false;

        dt = Math.Clamp(dt, 0f, 0.05f);
        _elapsed += dt;

        if (_openGraceTimer > 0f) _openGraceTimer = MathF.Max(0f, _openGraceTimer - dt);
        if (_missRecoveryTimer > 0f) _missRecoveryTimer = MathF.Max(0f, _missRecoveryTimer - dt);
        if (_missFlashTimer > 0f) _missFlashTimer = MathF.Max(0f, _missFlashTimer - dt);
        if (_hitFlashTimer > 0f) _hitFlashTimer = MathF.Max(0f, _hitFlashTimer - dt);
        if (_statusTimer > 0f) _statusTimer = MathF.Max(0f, _statusTimer - dt);
        if (_ironPressAnim > 0f) _ironPressAnim = MathF.Max(0f, _ironPressAnim - dt * 3.8f);
        if (_shakeAmount > 0f) _shakeAmount = MathF.Max(0f, _shakeAmount - dt * 22f);

        // Smoothly heat/cool solder pads and light copper traces
        for (int i = 0; i < PadCount; i++)
        {
            if (_padHeat[i] > 0f) _padHeat[i] = MathF.Max(0f, _padHeat[i] - dt * 0.75f);
        }
        for (int i = 0; i < _traceGlow.Length; i++)
        {
            float targetGlow = i <= _solderedCount ? 1f : 0f;
            _traceGlow[i] += (targetGlow - _traceGlow[i]) * MathF.Min(1f, dt * 8f);
        }

        // Smoothly glide soldering iron toward active pad
        int activePadIdx = Math.Clamp(_solderedCount, 0, PadCount - 1);
        Vector2 targetIronPos = PadPositions[activePadIdx];
        _ironHoverPos = Vector2.Lerp(_ironHoverPos, targetIronPos, MathF.Min(1f, dt * 11f));

        // Spring-damped voltmeter needle physics + subtle 50Hz AC micro-tremor
        float targetVolts = TargetVoltageForCount(_solderedCount);
        float stiffness = _phase == Phase.Overload ? 165f : 88f;
        float damping = _phase == Phase.Overload ? 8.5f : 10.5f;
        _needleVel += (targetVolts - _needleVolts) * stiffness * dt;
        _needleVel *= MathF.Exp(-damping * dt);
        _needleVolts = Math.Clamp(_needleVolts + _needleVel * dt, 0f, 392f);

        // Ambient rosin smoke rising continuously from hot soldering iron tip
        _ambientSmokeTimer -= dt;
        if (_ambientSmokeTimer <= 0f)
        {
            _ambientSmokeTimer = 0.085f;
            Vector2 tip = GetIronTipPosition();
            SpawnSmoke(tip + new Vector2(2f, -3f), 1, dark: false);
        }

        UpdateParticles(dt);

        if (_phase == Phase.Overload)
        {
            _overloadTimer += dt;
            _shakeAmount = MathF.Max(_shakeAmount, 3.8f + 3.2f * MathF.Sin(_overloadTimer * 44f));
            _overloadSparkCooldown -= dt;
            if (_overloadSparkCooldown <= 0f)
            {
                _overloadSparkCooldown = 0.075f;
                SpawnSparks(PadPositions[2], 7, isMiss: false);
                SpawnSmoke(PadPositions[2] + new Vector2(0f, -8f), 2, dark: false);
            }

            if (_overloadTimer >= OverloadDuration)
            {
                IsActive = false;
                _phase = Phase.Completed;
                return true;
            }
            return false;
        }

        if (_phase != Phase.Soldering) return false;

        // Clean cancel on ESC without consuming keycard or soldering iron
        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            Close();
            return false;
        }

        // Oscillate glowing copper heat-bar across brass rail
        var timing = PadTiming[activePadIdx];
        if (_missRecoveryTimer <= 0f)
        {
            _gaugePos += _gaugeDir * timing.Speed * dt;
            if (_gaugePos >= 1f)
            {
                _gaugePos = 1f;
                _gaugeDir = -1f;
            }
            else if (_gaugePos <= 0f)
            {
                _gaugePos = 0f;
                _gaugeDir = 1f;
            }
        }

        // Trigger solder attempt on LMB / Space / E
        bool triggerPressed = _openGraceTimer <= 0f && _missRecoveryTimer <= 0f &&
            (Raylib.IsMouseButtonPressed(MouseButton.Left) ||
             Raylib.IsKeyPressed(KeyboardKey.Space) ||
             Raylib.IsKeyPressed(KeyboardKey.E));

        if (triggerPressed)
        {
            float sweetStart = timing.Center - timing.Width * 0.5f;
            float sweetEnd = timing.Center + timing.Width * 0.5f;
            bool hit = _gaugePos >= sweetStart && _gaugePos <= sweetEnd;

            if (hit)
            {
                HandleSolderHit(activePadIdx);
            }
            else
            {
                HandleSolderMiss(activePadIdx);
            }
        }

        return false;
    }

    public void Draw(Font uiFont, Font titleFont)
    {
        if (!IsActive) return;

        // Full-screen moody scrim behind the open steel cabinet
        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), new Color(8, 10, 12, 198));

        UiViewport.Begin();
        try
        {
            float ox = (UiViewport.Width - VirtualW) * 0.5f;
            float oy = (UiViewport.Height - VirtualH) * 0.5f;

            // Subtle physical impact / overload vibration
            float shakeX = _shakeAmount > 0.05f ? MathF.Sin(_elapsed * 78f) * _shakeAmount : 0f;
            float shakeY = _shakeAmount > 0.05f ? MathF.Cos(_elapsed * 63f) * _shakeAmount * 0.7f : 0f;
            Vector2 origin = new(ox + shakeX, oy + shakeY);

            DrawCabinetHousing(origin);
            DrawNameplateAndStatus(origin, uiFont, titleFont);
            DrawPilotLamp(origin, uiFont);
            DrawAnalogVoltmeter(origin, uiFont);
            DrawFiberglassPcb(origin, uiFont);
            DrawSmokeWisps(origin);
            DrawHazardTape(origin + PadPositions[2], uiFont);
            DrawCalibrationRail(origin);
            DrawBottomLegend(origin, uiFont);
            DrawSolderingIron(origin);
            DrawSparks(origin);
        }
        finally
        {
            Raylib.EndMode2D();
        }
    }

    private void HandleSolderHit(int padIndex)
    {
        _ironPressAnim = 1f;
        _padSoldered[padIndex] = true;
        _padHeat[padIndex] = 1f;
        _hitFlashTimer = 0.34f;
        _solderedCount++;
        _needleVel += 320f;

        Vector2 padPos = PadPositions[padIndex];
        SpawnSparks(padPos, 24, isMiss: false);
        SpawnSmoke(padPos + new Vector2(0f, -6f), 9, dark: false);

        SafePlayAudio(() =>
        {
            AudioMusicSystem.PlaySolderSizzle();
            AudioMusicSystem.PlayHitmarker();
        });

        if (_solderedCount >= PadCount)
        {
            _phase = Phase.Overload;
            _overloadTimer = 0f;
            _overloadSparkCooldown = 0.06f;
            _shakeAmount = 7.5f;
            _needleVel += 520f;
            SpawnSparks(PadPositions[2], 34, isMiss: false);
            SpawnSmoke(PadPositions[2] + new Vector2(0f, -8f), 10, dark: false);
            SafePlayAudio(() =>
            {
                AudioMusicSystem.PlayComicZap();
                AudioMusicSystem.PlayAnxietyAlarm();
            });
        }
        else
        {
            _statusRu = $"КОНТАКТ {_solderedCount} / {PadCount} СПАЯН · ТОК ПОШЁЛ";
            _statusEn = $"JOINT {_solderedCount} / {PadCount} SOLDERED · POWER UP";
            _statusTimer = 1.15f;

            // Place cursor with fair lead-in toward the next pad's sweet spot
            var nextTiming = PadTiming[_solderedCount];
            if (_solderedCount == 1)
            {
                _gaugePos = Math.Clamp(nextTiming.Center - 0.42f, 0.08f, 0.28f);
                _gaugeDir = 1f;
            }
            else
            {
                _gaugePos = Math.Clamp(nextTiming.Center + 0.42f, 0.72f, 0.92f);
                _gaugeDir = -1f;
            }
        }
    }

    private void HandleSolderMiss(int padIndex)
    {
        _ironPressAnim = 0.65f;
        _missRecoveryTimer = MissRecoveryDuration;
        _missFlashTimer = 0.32f;
        _shakeAmount = 5.2f;
        _statusRu = "ИСКРА МИМО · ЛОВИ ЗОЛОТУЮ ЗОНУ";
        _statusEn = "MISSED · HIT THE GOLDEN ZONE";
        _statusTimer = 0.95f;

        Vector2 padPos = PadPositions[padIndex];
        SpawnSparks(padPos, 13, isMiss: true);
        SpawnSmoke(padPos + new Vector2(0f, -4f), 5, dark: true);

        SafePlayAudio(() =>
        {
            AudioMusicSystem.PlayActionClack(0.56f);
            AudioMusicSystem.PlayComicZap();
        });
    }

    private static float TargetVoltageForCount(int solderedCount) => solderedCount switch
    {
        <= 0 => 0f,
        1 => 120f,
        2 => 240f,
        _ => 380f
    };

    private Vector2 GetIronTipPosition()
    {
        float idleBob = MathF.Sin(_elapsed * 3.1f) * 2.2f;
        float plunge = MathF.Sin(_ironPressAnim * MathF.PI) * 19f;
        return new Vector2(_ironHoverPos.X - 3f + plunge * 0.25f, _ironHoverPos.Y - 20f + idleBob + plunge);
    }

    private void SeedPreviewParticles(Vector2 center, int sparkCount, int smokeCount)
    {
        var detRng = new Random(199801);
        for (int i = 0; i < sparkCount; i++)
        {
            // Fan sparks upward-left & around the lower rim so the right-side "НЕ ТРОГАТЬ" tape stays 100% crisp
            float angle = i % 4 == 0
                ? MathF.PI * 0.62f + (float)detRng.NextDouble() * MathF.PI * 0.42f
                : -MathF.PI * 1.08f + (float)detRng.NextDouble() * MathF.PI * 0.56f;
            float dist = 5f + (float)detRng.NextDouble() * 34f;
            float speed = 85f + (float)detRng.NextDouble() * 150f;
            Vector2 dir = new(MathF.Cos(angle), MathF.Sin(angle));
            _sparks.Add(new SparkParticle
            {
                Pos = center + dir * dist,
                Vel = dir * speed + new Vector2(-12f, -18f),
                Life = 0.24f + (float)detRng.NextDouble() * 0.28f,
                MaxLife = 0.55f,
                Size = 1.9f + (float)detRng.NextDouble() * 1.7f,
                IsMissSpark = false
            });
        }

        for (int i = 0; i < smokeCount; i++)
        {
            float t = (i + 0.5f) / smokeCount;
            float swirl = MathF.Sin(i * 1.3f) * 9f;
            // Drift rosin smoke cleanly upward-left away from the hazard warning tape
            _smoke.Add(new SmokeWisp
            {
                Pos = center + new Vector2(-10f + swirl - t * 28f, -12f - t * 62f),
                Vel = new Vector2(-16f, -32f),
                Life = (1f - t * 0.65f) * 1.1f,
                MaxLife = 1.2f,
                Radius = 6f + t * 12f,
                DriftPhase = i * 0.9f,
                Dark = false
            });
        }
    }

    private void SpawnSparks(Vector2 pos, int count, bool isMiss)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = -MathF.PI * 1.05f + _rng.NextSingle() * MathF.PI * 1.10f;
            float speed = (isMiss ? 110f : 145f) + _rng.NextSingle() * 210f;
            float life = 0.20f + _rng.NextSingle() * 0.34f;
            _sparks.Add(new SparkParticle
            {
                Pos = pos + new Vector2((_rng.NextSingle() - 0.5f) * 8f, (_rng.NextSingle() - 0.5f) * 8f),
                Vel = new Vector2(MathF.Cos(angle) * speed, MathF.Sin(angle) * speed - 30f),
                Life = life,
                MaxLife = life,
                Size = 1.8f + _rng.NextSingle() * 2.1f,
                IsMissSpark = isMiss
            });
        }
    }

    private void SpawnSmoke(Vector2 pos, int count, bool dark)
    {
        for (int i = 0; i < count; i++)
        {
            float life = 0.75f + _rng.NextSingle() * 0.75f;
            _smoke.Add(new SmokeWisp
            {
                Pos = pos + new Vector2(-4f + (_rng.NextSingle() - 0.5f) * 8f, (_rng.NextSingle() - 0.5f) * 6f),
                Vel = new Vector2(-15f + (_rng.NextSingle() - 0.5f) * 14f, -26f - _rng.NextSingle() * 24f),
                Life = life,
                MaxLife = life,
                Radius = 5f + _rng.NextSingle() * 5f,
                DriftPhase = _rng.NextSingle() * MathF.Tau,
                Dark = dark
            });
        }
    }

    private void UpdateParticles(float dt)
    {
        for (int i = _sparks.Count - 1; i >= 0; i--)
        {
            var p = _sparks[i];
            p.Life -= dt;
            if (p.Life <= 0f)
            {
                _sparks.RemoveAt(i);
                continue;
            }
            p.Vel.Y += 460f * dt;
            p.Vel *= MathF.Exp(-2.2f * dt);
            p.Pos += p.Vel * dt;
            _sparks[i] = p;
        }

        for (int i = _smoke.Count - 1; i >= 0; i--)
        {
            var s = _smoke[i];
            s.Life -= dt;
            if (s.Life <= 0f)
            {
                _smoke.RemoveAt(i);
                continue;
            }
            s.Pos.X += (s.Vel.X + MathF.Sin(_elapsed * 3.6f + s.DriftPhase) * 14f) * dt;
            s.Pos.Y += s.Vel.Y * dt;
            s.Radius += 13f * dt;
            _smoke[i] = s;
        }
    }

    // =========================================================================
    // 1. OPEN BRUSHED-STEEL ELECTRICAL CABINET HOUSING
    // =========================================================================
    private void DrawCabinetHousing(Vector2 o)
    {
        Rectangle cab = new(o.X + 150f, o.Y + 64f, 980f, 592f);

        // Deep physical drop shadow on wall behind cabinet
        Raylib.DrawRectangleRounded(new(cab.X + 12f, cab.Y + 16f, cab.Width, cab.Height), 0.025f, 8, new Color(4, 5, 6, 175));
        Raylib.DrawRectangleRounded(new(cab.X + 6f, cab.Y + 8f, cab.Width, cab.Height), 0.025f, 8, new Color(6, 7, 9, 145));

        // Swung-open heavy steel cabinet door slab peeking cleanly on the left edge
        Rectangle openDoor = new(o.X + 104f, o.Y + 72f, 44f, 576f);
        Raylib.DrawRectangleRec(new(openDoor.X + 4f, openDoor.Y + 8f, openDoor.Width, openDoor.Height), new Color(4, 5, 6, 150));
        Raylib.DrawRectangleGradientH((int)openDoor.X, (int)openDoor.Y, (int)openDoor.Width, (int)openDoor.Height,
            new Color(22, 26, 29, 255), new Color(48, 55, 60, 255));
        Raylib.DrawRectangleLinesEx(openDoor, 1.5f, new Color(76, 84, 90, 210));
        // Subtle recessed inner door stiffening rib
        Raylib.DrawRectangle((int)(openDoor.X + 12f), (int)(openDoor.Y + 26f), 16, (int)(openDoor.Height - 52f), new Color(18, 22, 24, 210));

        // Main rolled-steel cabinet body
        Raylib.DrawRectangleRounded(cab, 0.022f, 8, SteelMid);
        Raylib.DrawRectangleGradientV((int)cab.X + 4, (int)cab.Y + 4, (int)cab.Width - 8, (int)cab.Height - 8,
            new Color(52, 58, 63, 255), new Color(28, 32, 36, 255));

        // Fine brushed-steel horizontal grain across cabinet face
        for (int y = (int)cab.Y + 8; y < (int)(cab.Y + cab.Height - 8); y += 5)
        {
            int alpha = (int)((y * 13) % 14 + 4);
            Raylib.DrawLine((int)cab.X + 8, y, (int)(cab.X + cab.Width - 8), y, new Color(178, 188, 194, alpha));
        }

        // Top-left metallic bevel highlight & outer rim shadow
        Raylib.DrawLineEx(new(cab.X + 10f, cab.Y + 3f), new(cab.X + cab.Width - 10f, cab.Y + 3f), 2f, SteelHighlight);
        Raylib.DrawRectangleRoundedLinesEx(cab, 0.022f, 8, 2.5f, new Color(86, 94, 100, 235));

        // Rubber weatherproof gasket & dark inner frame lip
        Rectangle innerRim = new(cab.X + 20f, cab.Y + 18f, cab.Width - 40f, cab.Height - 36f);
        Raylib.DrawRectangleRoundedLinesEx(innerRim, 0.016f, 6, 3.5f, new Color(16, 18, 20, 235));
        Raylib.DrawRectangleRoundedLinesEx(new(innerRim.X + 3f, innerRim.Y + 3f, innerRim.Width - 6f, innerRim.Height - 6f),
            0.014f, 6, 1.2f, new Color(90, 98, 103, 120));

        // Two heavy barrel hinges with brass pins on the left cabinet wall
        foreach (float hy in new[] { 184f, 512f })
        {
            Rectangle barrel = new(o.X + 139f, o.Y + hy - 26f, 20f, 52f);
            Raylib.DrawRectangleRec(new(barrel.X + 2f, barrel.Y + 3f, barrel.Width, barrel.Height), new Color(5, 6, 7, 175));
            Raylib.DrawRectangleGradientH((int)barrel.X, (int)barrel.Y, (int)barrel.Width, (int)barrel.Height,
                new Color(34, 38, 42, 255), new Color(84, 92, 97, 255));
            Raylib.DrawRectangleLinesEx(barrel, 1.2f, new Color(18, 20, 22, 240));
            // Knuckle split grooves
            Raylib.DrawLineEx(new(barrel.X, barrel.Y + 17f), new(barrel.X + barrel.Width, barrel.Y + 17f), 1.5f, SteelDark);
            Raylib.DrawLineEx(new(barrel.X, barrel.Y + 35f), new(barrel.X + barrel.Width, barrel.Y + 35f), 1.5f, SteelDark);
            // Warm brass hinge pin caps top & bottom
            Raylib.DrawRectangle((int)(barrel.X + 4f), (int)(barrel.Y - 5f), 12, 5, BrassMid);
            Raylib.DrawRectangle((int)(barrel.X + 4f), (int)(barrel.Y + barrel.Height), 12, 5, BrassDark);
        }

        // Right-edge stamped steel latch catch plate
        Rectangle latch = new(cab.X + cab.Width - 6f, o.Y + 332f, 16f, 56f);
        Raylib.DrawRectangleRounded(latch, 0.25f, 4, SteelLight);
        Raylib.DrawRectangleRoundedLinesEx(latch, 0.25f, 4, 1.2f, SteelDark);
        Raylib.DrawCircleV(new(latch.X + 8f, latch.Y + 28f), 4f, CavityDark);

        // 4 Machined Steel Corner Rivets (clear of all plates & text)
        DrawSteelRivet(new(o.X + 170f, o.Y + 84f), 6.5f);
        DrawSteelRivet(new(o.X + 1110f, o.Y + 84f), 6.5f);
        DrawSteelRivet(new(o.X + 170f, o.Y + 636f), 6.5f);
        DrawSteelRivet(new(o.X + 1110f, o.Y + 636f), 6.5f);
    }

    // =========================================================================
    // 2. WARM ENGRAVED BRASS NAMEPLATE & STATUS STRIP
    // =========================================================================
    private void DrawNameplateAndStatus(Vector2 o, Font uiFont, Font titleFont)
    {
        // Riveted warm brass cabinet nameplate
        Rectangle plate = new(o.X + 198f, o.Y + 96f, 444f, 64f);
        Raylib.DrawRectangleRec(new(plate.X + 3f, plate.Y + 4f, plate.Width, plate.Height), new Color(8, 9, 10, 185));
        Raylib.DrawRectangleGradientV((int)plate.X, (int)plate.Y, (int)plate.Width, (int)plate.Height,
            new Color(224, 186, 114, 255), new Color(156, 118, 60, 255));
        Raylib.DrawRectangleLinesEx(plate, 2f, BrassDark);
        Raylib.DrawRectangleLinesEx(new(plate.X + 5f, plate.Y + 5f, plate.Width - 10f, plate.Height - 10f), 1.1f, new Color(72, 50, 22, 185));

        DrawBrassScrew(new(plate.X + 17f, plate.Y + plate.Height * 0.5f), 4.6f);
        DrawBrassScrew(new(plate.X + plate.Width - 17f, plate.Y + plate.Height * 0.5f), 4.6f);

        string titleText = Loc.T("ЩИТ ЛИФТА №1 · 1998", "ELEVATOR BOX #1 · 1998");
        DrawCenteredText(titleFont, titleText, plate, 24, EngravedInk, shadowed: false);

        // Riveted dark anodized-metal status placard matching the brass nameplate above
        Rectangle statusPlate = new(o.X + 198f, o.Y + 172f, 444f, 40f);
        Raylib.DrawRectangleRec(new(statusPlate.X + 2f, statusPlate.Y + 3f, statusPlate.Width, statusPlate.Height), new Color(6, 8, 9, 175));
        Raylib.DrawRectangleGradientV((int)statusPlate.X, (int)statusPlate.Y, (int)statusPlate.Width, (int)statusPlate.Height,
            new Color(34, 40, 44, 250), new Color(16, 20, 22, 252));
        Color statusBorder = _phase == Phase.Overload ? DangerRed
            : _missFlashTimer > 0f ? new Color(196, 84, 52, 240)
            : _hitFlashTimer > 0f ? CopperLit
            : BrassMid;
        Raylib.DrawRectangleLinesEx(statusPlate, 1.5f, statusBorder);
        Raylib.DrawRectangleLinesEx(new(statusPlate.X + 4f, statusPlate.Y + 4f, statusPlate.Width - 8f, statusPlate.Height - 8f),
            1f, new Color(138, 112, 68, 95));
        Raylib.DrawLineEx(new(statusPlate.X + 5f, statusPlate.Y + 2f), new(statusPlate.X + statusPlate.Width - 5f, statusPlate.Y + 2f),
            1f, new Color(188, 176, 148, 55));

        DrawBrassScrew(new(statusPlate.X + 17f, statusPlate.Y + statusPlate.Height * 0.5f), 3.6f);
        DrawBrassScrew(new(statusPlate.X + statusPlate.Width - 17f, statusPlate.Y + statusPlate.Height * 0.5f), 3.6f);

        string statusLine;
        Color statusInk;
        if (_phase == Phase.Overload)
        {
            statusLine = Loc.T("ПЕРЕГРУЗКА КОНТУРА 0.1 · 380В!", "CIRCUIT 0.1 OVERLOAD · 380V!");
            statusInk = new Color(255, 118, 102, 255);
        }
        else if (_statusTimer > 0f && !string.IsNullOrEmpty(_statusRu))
        {
            statusLine = Loc.T(_statusRu, _statusEn);
            statusInk = _missFlashTimer > 0f ? new Color(242, 146, 112, 255) : BrassBright;
        }
        else
        {
            statusLine = Loc.T($"КОНТАКТЫ: {_solderedCount} / {PadCount}   ·   СПАЯЙ ЦЕПЬ ЗАПУСКА",
                               $"JOINTS: {_solderedCount} / {PadCount}   ·   SOLDER START CIRCUIT");
            statusInk = Silkscreen;
        }
        DrawCenteredText(uiFont, statusLine, statusPlate, 15, statusInk, shadowed: true);
    }

    // =========================================================================
    // 3. BAKELITE PILOT INDICATOR LAMP
    // =========================================================================
    private void DrawPilotLamp(Vector2 o, Font uiFont)
    {
        Vector2 center = new(o.X + 754f, o.Y + 148f);
        bool overload = _phase == Phase.Overload;
        float pulse = overload
            ? 0.55f + 0.45f * MathF.Sin(_overloadTimer * 48f)
            : 0.45f + 0.18f * MathF.Sin(_elapsed * 3.5f);

        // Soft bloom halo behind glass dome
        if (overload)
        {
            Raylib.DrawCircleGradient((int)center.X, (int)center.Y, 56f,
                new Color(255, 42, 34, (int)(135 * pulse)), Color.Blank);
        }
        else if (_solderedCount > 0)
        {
            Raylib.DrawCircleGradient((int)center.X, (int)center.Y, 44f,
                new Color(245, 178, 58, (int)((48 + _solderedCount * 22) * (0.7f + 0.3f * pulse))), Color.Blank);
        }

        // Steel mounting collar + knurled brass bezel
        Raylib.DrawCircleV(center + new Vector2(2f, 3f), 30f, new Color(6, 7, 8, 175));
        Raylib.DrawCircleV(center, 29f, new Color(36, 40, 44, 255));
        Raylib.DrawCircleV(center, 25f, BrassMid);
        Raylib.DrawCircleV(center, 22f, BrassDark);

        // Faceted glass dome lens
        Color lensOuter, lensCore;
        if (overload)
        {
            lensOuter = new Color(178, 20, 24, 255);
            lensCore = new Color(255, (int)(96 + 90 * pulse), 74, 255);
        }
        else if (_solderedCount == 0)
        {
            lensOuter = new Color(58, 36, 20, 255);
            lensCore = new Color(118, 72, 34, 255);
        }
        else if (_solderedCount == 1)
        {
            lensOuter = new Color(138, 86, 24, 255);
            lensCore = new Color(235, 168, 62, 255);
        }
        else
        {
            lensOuter = new Color(172, 102, 22, 255);
            lensCore = new Color(255, 204, 86, 255);
        }

        Raylib.DrawCircleV(center, 19f, lensOuter);
        Raylib.DrawCircleGradient((int)(center.X - 3f), (int)(center.Y - 3f), 17f, lensCore, lensOuter);
        // Glass specular crescent
        Raylib.DrawCircleV(center + new Vector2(-6f, -6f), 4.5f, new Color(255, 250, 232, 165));
        Raylib.DrawCircleV(center + new Vector2(-2f, -9f), 2.2f, new Color(255, 255, 248, 130));

        // Stamped metal plate caption under lamp
        string lampLabel = overload ? Loc.T("АВАРИЯ!", "OVERLOAD!") : Loc.T("СЕТЬ", "POWER");
        Color lampInk = overload ? new Color(255, 110, 96, 255) : new Color(188, 180, 164, 220);
        DrawCenteredText(uiFont, lampLabel, new Rectangle(center.X - 54f, center.Y + 34f, 108f, 22f), 13, lampInk, shadowed: true);
    }

    // =========================================================================
    // 4. TOP-RIGHT SOVIET ANALOG PANEL VOLTMETER (0..380В)
    // =========================================================================
    private void DrawAnalogVoltmeter(Vector2 o, Font uiFont)
    {
        Rectangle housing = new(o.X + 868f, o.Y + 86f, 214f, 144f);

        // Cast shadow & matte black Bakelite flange
        Raylib.DrawRectangleRounded(new(housing.X + 4f, housing.Y + 5f, housing.Width, housing.Height), 0.08f, 8, new Color(5, 6, 7, 185));
        Raylib.DrawRectangleRounded(housing, 0.08f, 8, new Color(26, 26, 28, 255));
        Raylib.DrawRectangleRoundedLinesEx(housing, 0.08f, 8, 2f, new Color(76, 76, 80, 220));

        // 4 miniature corner bezel screws (outside the ivory meter face & readout)
        DrawBrassScrew(new(housing.X + 10f, housing.Y + 10f), 3.2f);
        DrawBrassScrew(new(housing.X + housing.Width - 10f, housing.Y + 10f), 3.2f);
        DrawBrassScrew(new(housing.X + 10f, housing.Y + housing.Height - 10f), 3.2f);
        DrawBrassScrew(new(housing.X + housing.Width - 10f, housing.Y + housing.Height - 10f), 3.2f);

        // Brass inner frame & warm ivory dial window (upper section; needle sweeps strictly inside)
        Rectangle face = new(housing.X + 16f, housing.Y + 12f, housing.Width - 32f, 94f);
        Raylib.DrawRectangleRounded(new(face.X - 2f, face.Y - 2f, face.Width + 4f, face.Height + 4f), 0.06f, 6, BrassMid);
        Raylib.DrawRectangleRounded(face, 0.05f, 6, IvoryFace);
        Raylib.DrawRectangleGradientV((int)face.X + 2, (int)face.Y + 2, (int)face.Width - 4, 22,
            new Color(142, 128, 102, 72), Color.Blank);
        Raylib.DrawRectangleRoundedLinesEx(face, 0.05f, 6, 1.4f, BrassDark);

        // Scale geometry: sweep -150 deg (0V) -> -30 deg (380V)
        Vector2 pivot = new(face.X + face.Width * 0.5f, face.Y + face.Height - 9f);
        const float arcRadius = 58f;
        const float startDeg = -150f;
        const float endDeg = -30f;

        // Safe scale arc & crimson danger zone above 260V
        float dangerStartDeg = VoltageToAngleDeg(260f, startDeg, endDeg);
        DrawArcStroke(pivot, arcRadius, 2.2f, startDeg, dangerStartDeg, 24, EngravedInk);
        DrawArcStroke(pivot, arcRadius, 5.0f, dangerStartDeg, endDeg, 16, DangerRed);
        DrawArcStroke(pivot, arcRadius - 6.5f, 1.0f, startDeg, endDeg, 28, new Color(110, 96, 76, 180));

        // Major & minor graduation ticks
        for (int v = 0; v <= 380; v += 20)
        {
            float deg = VoltageToAngleDeg(v, startDeg, endDeg);
            float rad = deg * (MathF.PI / 180f);
            bool major = v == 0 || v == 120 || v == 240 || v == 380 || v % 60 == 0;
            float rIn = major ? arcRadius - 9.0f : arcRadius - 4.8f;
            float rOut = arcRadius + 1.5f;
            Vector2 dir = new(MathF.Cos(rad), MathF.Sin(rad));
            Color tickCol = v >= 260 ? DangerRed : EngravedInk;
            Raylib.DrawLineEx(pivot + dir * rIn, pivot + dir * rOut, major ? 1.8f : 1.0f, tickCol);
        }

        // Crisp scale numerals (0, 120, 240, 380)
        foreach (int labelV in new[] { 0, 120, 240, 380 })
        {
            float deg = VoltageToAngleDeg(labelV, startDeg, endDeg);
            float rad = deg * (MathF.PI / 180f);
            float rNum = arcRadius + 13.0f;
            Vector2 pos = pivot + new Vector2(MathF.Cos(rad) * rNum, MathF.Sin(rad) * rNum);
            string num = labelV.ToString();
            Font nf = GameFonts.Get(Typeface.InterfaceBold, 11);
            Vector2 sz = GameFonts.Measure(nf, num, 11);
            Color numCol = labelV >= 260 ? DangerRed : EngravedInk;
            GameFonts.Draw(nf, num, new(pos.X - sz.X * 0.5f, pos.Y - sz.Y * 0.5f), 11, numCol, shadowed: false);
        }

        // Trembling brass/crimson pointer needle
        float tremor = _phase == Phase.Overload
            ? MathF.Sin(_elapsed * 72f) * 3.4f + MathF.Cos(_elapsed * 115f) * 1.8f
            : (_solderedCount > 0 ? MathF.Sin(_elapsed * 34f) * 0.9f : MathF.Sin(_elapsed * 12f) * 0.25f);
        float needleDeg = VoltageToAngleDeg(_needleVolts, startDeg, endDeg) + tremor;
        float needleRad = needleDeg * (MathF.PI / 180f);
        Vector2 needleDir = new(MathF.Cos(needleRad), MathF.Sin(needleRad));
        Vector2 needlePerp = new(-needleDir.Y, needleDir.X);
        Vector2 needleTip = pivot + needleDir * (arcRadius - 3f);

        // Needle cast shadow on ivory face
        Raylib.DrawLineEx(pivot + new Vector2(2f, 2f), needleTip + new Vector2(2f, 2f), 2.4f, new Color(40, 32, 22, 65));
        // Tapered brass/crimson needle body
        Color needleCol = _needleVolts >= 255f ? DangerRed : new Color(152, 42, 32, 255);
        DrawTriCCW(pivot - needlePerp * 2.2f, pivot + needlePerp * 2.2f, needleTip, needleCol);
        Raylib.DrawLineEx(pivot, needleTip, 1.4f, needleCol);

        // Brass pivot dome cap
        Raylib.DrawCircleV(pivot, 9.2f, BrassDark);
        Raylib.DrawCircleV(pivot, 7.2f, BrassBright);
        Raylib.DrawCircleV(pivot, 3.0f, EngravedInk);

        // Subtle curved glass sheen on upper-left of dial
        Raylib.DrawLineEx(new(face.X + 10f, face.Y + 7f), new(face.X + 74f, face.Y + 7f), 2f, new Color(255, 255, 255, 78));

        // Dedicated recessed brass-rimmed readout window cleanly BELOW the pivot (needle can NEVER cross digits!)
        int shownVolts = (int)MathF.Round(Math.Clamp(_needleVolts, 0f, 380f));
        Rectangle readout = new(housing.X + 62f, housing.Y + 113f, 90f, 22f);
        Raylib.DrawRectangleRounded(readout, 0.22f, 4, new Color(16, 18, 20, 255));
        Raylib.DrawRectangleRoundedLinesEx(readout, 0.22f, 4, 1.2f, shownVolts >= 260 ? DangerRed : BrassMid);
        string unitStamp = Loc.T($"{shownVolts} В", $"{shownVolts} V");
        Color stampCol = shownVolts >= 260 ? new Color(255, 108, 92, 255) : IvoryFace;
        DrawCenteredText(uiFont, unitStamp, readout, 14, stampCol, shadowed: false);
    }

    // =========================================================================
    // 5. VINTAGE EMERALD-OLIVE FIBERGLASS PCB, TRACES, 3 PADS & HAZARD TAPE
    // =========================================================================
    private void DrawFiberglassPcb(Vector2 o, Font uiFont)
    {
        Rectangle cavity = new(o.X + 190f, o.Y + 234f, 900f, 282f);
        Rectangle pcb = new(o.X + 198f, o.Y + 240f, 884f, 270f);

        // Recessed dark interior cavity behind PCB
        Raylib.DrawRectangleRounded(cavity, 0.015f, 6, CavityDark);
        Raylib.DrawRectangleRoundedLinesEx(cavity, 0.015f, 6, 1.5f, new Color(68, 76, 80, 145));

        // Emerald-olive vintage fiberglass board (текстолит)
        Raylib.DrawRectangleRec(new(pcb.X + 3f, pcb.Y + 4f, pcb.Width, pcb.Height), new Color(4, 7, 6, 185));
        Raylib.DrawRectangleGradientV((int)pcb.X, (int)pcb.Y, (int)pcb.Width, (int)pcb.Height, PcbTop, PcbBottom);

        // Subtle fiberglass weave cross-hatch texture
        for (int x = (int)pcb.X + 12; x < (int)(pcb.X + pcb.Width); x += 16)
            Raylib.DrawLine(x, (int)pcb.Y + 2, x, (int)(pcb.Y + pcb.Height - 2), new Color(180, 210, 180, 7));
        for (int y = (int)pcb.Y + 12; y < (int)(pcb.Y + pcb.Height); y += 16)
            Raylib.DrawLine((int)pcb.X + 2, y, (int)(pcb.X + pcb.Width - 2), y, new Color(180, 210, 180, 7));

        // Chamfered PCB edge & inner silkscreen border frame
        Raylib.DrawRectangleLinesEx(pcb, 2f, new Color(56, 98, 72, 210));
        Raylib.DrawRectangleLinesEx(new(pcb.X + 8f, pcb.Y + 8f, pcb.Width - 16f, pcb.Height - 16f), 1f, new Color(196, 192, 168, 52));

        // 4 Brass PCB corner mounting standoffs
        DrawBrassScrew(new(pcb.X + 18f, pcb.Y + 18f), 4.4f);
        DrawBrassScrew(new(pcb.X + pcb.Width - 18f, pcb.Y + 18f), 4.4f);
        DrawBrassScrew(new(pcb.X + 18f, pcb.Y + pcb.Height - 18f), 4.4f);
        DrawBrassScrew(new(pcb.X + pcb.Width - 18f, pcb.Y + pcb.Height - 18f), 4.4f);

        // Silkscreen board code in quiet upper-left margin
        Font microFont = GameFonts.Get(Typeface.InterfaceBold, 12);
        GameFonts.Draw(microFont, Loc.T("ПЛАТА УЛ-98М · 380В", "BOARD UL-98M · 380V"),
            new(pcb.X + 34f, pcb.Y + 13f), 12, new Color(188, 198, 174, 155), shadowed: false);

        // Authentic passive vintage components in upper & lower quiet bands
        DrawVintagePcbComponents(o, pcb);

        // Left braided cloth power wires entering Bakelite terminal block
        DrawLeftPowerHarness(o);

        // Etched golden-copper circuit traces connecting terminal -> pads -> voltmeter bus
        DrawCopperTraces(o);

        // 3 Burnt Circular Solder Pads + Silkscreen Labels
        int activePad = _phase == Phase.Soldering ? Math.Clamp(_solderedCount, 0, PadCount - 1) : -1;
        for (int i = 0; i < PadCount; i++)
        {
            Vector2 p = o + PadPositions[i];
            DrawSolderPad(p, i, _padSoldered[i], i == activePad, _padHeat[i], uiFont);
        }
    }

    private void DrawVintagePcbComponents(Vector2 o, Rectangle pcb)
    {
        // Top margin Soviet electrolytic cans & resistors kept cleanly LEFT of Pad 3 soldering iron silhouette!
        DrawAxialResistor(new(o.X + 402f, o.Y + 268f), 46f, new Color(182, 44, 38, 255), new Color(214, 166, 48, 255));
        DrawElectrolyticCap(new(o.X + 474f, o.Y + 270f), 13f);
        DrawElectrolyticCap(new(o.X + 510f, o.Y + 270f), 11f);
        DrawAxialResistor(new(o.X + 580f, o.Y + 268f), 46f, new Color(46, 124, 68, 255), new Color(214, 166, 48, 255));

        // Bottom margin small ceramic disc caps & carbon resistors
        DrawAxialResistor(new(o.X + 494f, o.Y + 484f), 54f, new Color(186, 98, 34, 255), new Color(196, 154, 54, 255));
        DrawAxialResistor(new(o.X + 768f, o.Y + 484f), 54f, new Color(172, 38, 42, 255), new Color(218, 174, 68, 255));
        Raylib.DrawCircleV(new(o.X + 296f, o.Y + 482f), 7f, new Color(188, 92, 42, 235));
        Raylib.DrawCircleLines((int)(o.X + 296f), (int)(o.Y + 482f), 7f, new Color(82, 38, 18, 220));
        Raylib.DrawCircleV(new(o.X + 318f, o.Y + 482f), 6f, new Color(188, 92, 42, 235));
        Raylib.DrawCircleLines((int)(o.X + 318f), (int)(o.Y + 482f), 6f, new Color(82, 38, 18, 220));

        // Decorative copper trace vias along quiet board corners
        foreach (var v in new Vector2[]
        {
            new(o.X + 474f, o.Y + 296f), new(o.X + 510f, o.Y + 296f),
            new(o.X + 580f, o.Y + 294f), new(o.X + 494f, o.Y + 462f),
            new(o.X + 768f, o.Y + 462f), new(pcb.X + pcb.Width - 46f, o.Y + 474f)
        })
        {
            Raylib.DrawCircleV(v, 4.2f, CopperDim);
            Raylib.DrawCircleV(v, 1.8f, CavityDark);
        }
    }

    private static void DrawElectrolyticCap(Vector2 center, float r)
    {
        Raylib.DrawCircleV(center + new Vector2(2f, 3f), r, new Color(6, 12, 9, 155));
        Raylib.DrawCircleV(center, r, new Color(46, 68, 86, 255));
        Raylib.DrawCircleV(center, r - 2.4f, new Color(148, 158, 164, 255));
        Raylib.DrawLineEx(center + new Vector2(-r * 0.45f, 0f), center + new Vector2(r * 0.45f, 0f), 1.2f, new Color(76, 84, 88, 200));
        Raylib.DrawLineEx(center + new Vector2(0f, -r * 0.45f), center + new Vector2(0f, r * 0.45f), 1.2f, new Color(76, 84, 88, 200));
    }

    private static void DrawAxialResistor(Vector2 center, float width, Color band1, Color band2)
    {
        float half = width * 0.5f;
        Raylib.DrawLineEx(new(center.X - half - 10f, center.Y), new(center.X + half + 10f, center.Y), 2f, new Color(172, 178, 180, 210));
        Raylib.DrawCircleV(new(center.X - half - 10f, center.Y), 3.4f, CopperDim);
        Raylib.DrawCircleV(new(center.X + half + 10f, center.Y), 3.4f, CopperDim);
        Rectangle body = new(center.X - half, center.Y - 6f, width, 12f);
        Raylib.DrawRectangleRounded(new(body.X + 1f, body.Y + 2f, body.Width, body.Height), 0.45f, 4, new Color(6, 10, 8, 145));
        Raylib.DrawRectangleRounded(body, 0.45f, 4, new Color(196, 166, 118, 255));
        Raylib.DrawRectangle((int)(center.X - half + 8f), (int)body.Y + 1, 4, 10, band1);
        Raylib.DrawRectangle((int)(center.X - 2f), (int)body.Y + 1, 4, 10, band2);
        Raylib.DrawRectangle((int)(center.X + half - 12f), (int)body.Y + 1, 4, 10, BrassBright);
    }

    private void DrawLeftPowerHarness(Vector2 o)
    {
        // 3 cloth-insulated power cables entering from left cabinet grommet into Bakelite terminal block
        Color[] wireColors =
        [
            new(146, 54, 42, 255),
            new(172, 134, 52, 255),
            new(74, 82, 92, 255)
        ];

        for (int i = 0; i < 3; i++)
        {
            float wy = o.Y + 348f + i * 26f;
            Vector2 a = new(o.X + 168f, wy + (i - 1) * 8f);
            Vector2 b = new(o.X + 224f, wy);
            Raylib.DrawLineEx(a + new Vector2(0f, 3f), b + new Vector2(0f, 3f), 7f, new Color(6, 8, 9, 145));
            Raylib.DrawLineEx(a, b, 6.2f, wireColors[i]);
            Raylib.DrawLineEx(a + new Vector2(0f, -1.5f), b + new Vector2(0f, -1.5f), 1.6f, new Color(255, 244, 224, 65));
        }

        // Rubber wall grommet
        Raylib.DrawRectangleRounded(new(o.X + 166f, o.Y + 332f, 14f, 84f), 0.35f, 4, new Color(22, 24, 26, 255));

        // 3-screw Bakelite terminal strip on left edge of PCB
        Rectangle term = new(o.X + 220f, o.Y + 328f, 38f, 92f);
        Raylib.DrawRectangleRec(new(term.X + 3f, term.Y + 3f, term.Width, term.Height), new Color(6, 10, 8, 165));
        Raylib.DrawRectangleRounded(term, 0.12f, 4, new Color(34, 28, 24, 255));
        Raylib.DrawRectangleRoundedLinesEx(term, 0.12f, 4, 1.4f, BrassDark);
        for (int i = 0; i < 3; i++)
        {
            float ty = o.Y + 348f + i * 26f;
            DrawBrassScrew(new(term.X + 19f, ty), 5.2f);
        }
    }

    private void DrawCopperTraces(Vector2 o)
    {
        // Segment 0: Terminal block -> Pad 1 (always powered)
        Color c0 = TraceColor(_traceGlow[0]);
        DrawTracePolyline([
            o + new Vector2(258f, 374f),
            o + new Vector2(338f, 374f)
        ], 6f, c0, _traceGlow[0]);

        // Auxiliary chamfered bus traces from terminal upper/lower screws
        DrawTracePolyline([
            o + new Vector2(258f, 348f),
            o + new Vector2(292f, 348f),
            o + new Vector2(314f, 370f),
            o + new Vector2(338f, 370f)
        ], 3f, c0, _traceGlow[0] * 0.7f);
        DrawTracePolyline([
            o + new Vector2(258f, 400f),
            o + new Vector2(292f, 400f),
            o + new Vector2(314f, 378f),
            o + new Vector2(338f, 378f)
        ], 3f, c0, _traceGlow[0] * 0.7f);

        // Segment 1: Pad 1 -> Pad 2 (chamfered parallel dual PCB traces)
        Color c1 = TraceColor(_traceGlow[1]);
        DrawTracePolyline([
            o + new Vector2(398f, 374f),
            o + new Vector2(610f, 374f)
        ], 6f, c1, _traceGlow[1]);
        DrawTracePolyline([
            o + new Vector2(400f, 362f),
            o + new Vector2(452f, 362f),
            o + new Vector2(474f, 340f),
            o + new Vector2(544f, 340f),
            o + new Vector2(566f, 362f),
            o + new Vector2(610f, 362f)
        ], 3.2f, c1, _traceGlow[1] * 0.85f);

        // Segment 2: Pad 2 -> Pad 3
        Color c2 = TraceColor(_traceGlow[2]);
        DrawTracePolyline([
            o + new Vector2(670f, 374f),
            o + new Vector2(882f, 374f)
        ], 6f, c2, _traceGlow[2]);
        DrawTracePolyline([
            o + new Vector2(670f, 386f),
            o + new Vector2(722f, 386f),
            o + new Vector2(744f, 408f),
            o + new Vector2(814f, 408f),
            o + new Vector2(836f, 386f),
            o + new Vector2(882f, 386f)
        ], 3.2f, c2, _traceGlow[2] * 0.85f);

        // Segment 3: Pad 3 -> Right collector bus heading up into Voltmeter!
        Color c3 = _phase == Phase.Overload
            ? Raylib.ColorLerp(CopperLit, new Color(255, 76, 56, 255), 0.5f + 0.5f * MathF.Sin(_overloadTimer * 44f))
            : TraceColor(_traceGlow[3]);
        DrawTracePolyline([
            o + new Vector2(942f, 374f),
            o + new Vector2(1018f, 374f),
            o + new Vector2(1038f, 354f),
            o + new Vector2(1038f, 240f)
        ], 6f, c3, _traceGlow[3]);
        DrawTracePolyline([
            o + new Vector2(942f, 386f),
            o + new Vector2(1024f, 386f),
            o + new Vector2(1048f, 362f),
            o + new Vector2(1048f, 240f)
        ], 3.2f, c3, _traceGlow[3] * 0.85f);
    }

    private Color TraceColor(float glow) => Raylib.ColorLerp(CopperDim, CopperLit, Math.Clamp(glow, 0f, 1f));

    private void DrawTracePolyline(Vector2[] pts, float width, Color color, float glow)
    {
        if (glow > 0.15f)
        {
            float pulse = 0.72f + 0.28f * MathF.Sin(_elapsed * 5.2f);
            Color halo = new(color.R, color.G, color.B, (int)(52 * glow * pulse));
            for (int i = 0; i < pts.Length - 1; i++)
                Raylib.DrawLineEx(pts[i], pts[i + 1], width + 6f, halo);
        }
        for (int i = 0; i < pts.Length - 1; i++)
        {
            Raylib.DrawLineEx(pts[i], pts[i + 1], width, color);
            Raylib.DrawCircleV(pts[i + 1], width * 0.48f, color);
        }
    }

    private void DrawSolderPad(Vector2 pos, int index, bool soldered, bool active, float heat, Font uiFont)
    {
        // Pad code stencil cleanly above pad (generous breathing room above Pad 3 hazard tape)
        DrawCenteredText(uiFont, PadCodes[index], new Rectangle(pos.X - 36f, pos.Y - 84f, 72f, 20f), 14,
            soldered || active ? BrassBright : new Color(170, 180, 160, 165), shadowed: false);

        if (!soldered)
        {
            // Scorched carbon soot aura around burnt pad
            Raylib.DrawCircleGradient((int)pos.X, (int)pos.Y, 44f, new Color(12, 10, 9, 175), Color.Blank);
            Raylib.DrawCircleGradient((int)(pos.X + 4f), (int)(pos.Y - 3f), 36f, new Color(24, 16, 12, 125), Color.Blank);

            // Subtle breathing focus ring on currently active pad
            if (active)
            {
                float pulse = 0.55f + 0.45f * MathF.Sin(_elapsed * 6.5f);
                DrawArcStroke(pos, 38f, 2.2f, 0f, 360f, 40, new Color(238, 182, 76, (int)(110 + 115 * pulse)));
            }

            // Oxidized dark copper annular pad & hollow through-hole
            Raylib.DrawCircleV(pos, 30f, new Color(76, 52, 30, 255));
            Raylib.DrawCircleV(pos, 26f, new Color(118, 78, 42, 255));
            Raylib.DrawCircleV(pos, 18f, new Color(46, 32, 22, 255));
            Raylib.DrawCircleV(pos, 11f, CavityDark);
            // Exposed tarnished lead pin in center
            Raylib.DrawCircleV(pos, 4.5f, new Color(154, 144, 122, 255));
        }
        else
        {
            // Warm glow halo right after soldering (cools cleanly to metallic silver-tin)
            if (heat > 0.04f)
            {
                Raylib.DrawCircleGradient((int)pos.X, (int)pos.Y, 48f,
                    new Color(255, 164, 54, (int)(135 * heat)), Color.Blank);
            }

            // Amber rosin flux perimeter ring
            Raylib.DrawCircleV(pos, 32f, new Color(96, 62, 22, 175));
            // Outer copper pad rim peeking cleanly around solder dome
            Raylib.DrawCircleV(pos, 29f, CopperLit);
            // Molten-to-cooled silver-tin solder dome
            Raylib.DrawCircleV(pos + new Vector2(1.5f, 2.5f), 25f, new Color(28, 34, 38, 160));
            Raylib.DrawCircleV(pos, 25f, new Color(138, 148, 154, 255));
            Raylib.DrawCircleGradient((int)(pos.X - 4f), (int)(pos.Y - 4f), 23f,
                new Color(224, 232, 238, 255), new Color(128, 138, 145, 255));
            // Wet metallic specular glints on dome crown
            Raylib.DrawCircleV(pos + new Vector2(-7f, -7f), 6.2f, new Color(248, 252, 255, 220));
            Raylib.DrawCircleV(pos + new Vector2(-10f, -10f), 2.6f, Color.White);
            // Subtle cooling amber tint inside solder center when fresh
            if (heat > 0.05f)
            {
                Raylib.DrawCircleGradient((int)pos.X, (int)pos.Y, 19f,
                    new Color(255, 172, 68, (int)(155 * heat)), Color.Blank);
            }
        }

        // Crisp silkscreen label centered cleanly below pad
        string label = Loc.T(PadLabels[index].Ru, PadLabels[index].En);
        Color labelColor = index == 2 && _phase == Phase.Overload
            ? new Color(255, 118, 98, 255)
            : (soldered || active ? BrassBright : Silkscreen);
        Font lf = GameFonts.Get(Typeface.InterfaceBold, 17);
        Vector2 lsz = GameFonts.Measure(lf, label, 17);
        GameFonts.Draw(lf, label, new(pos.X - lsz.X * 0.5f, pos.Y + 46f), 17, labelColor, shadowed: true);
    }

    private void DrawHazardTape(Vector2 pad3Center, Font uiFont)
    {
        // Crosses upper-right rim of Pad 3 ("КОНТУР 0.1") — 100% clear of "03" above, iron tip/sparks left, and "КОНТУР 0.1" below!
        float overloadPeel = _phase == Phase.Overload ? Math.Clamp(_overloadTimer / OverloadDuration, 0f, 1f) : 0f;
        float shakeDeg = _phase == Phase.Overload ? MathF.Sin(_overloadTimer * 68f) * 3.8f : 0f;
        float angleDeg = 24f + overloadPeel * 11f + shakeDeg;
        Vector2 tapeCenter = pad3Center + new Vector2(66f + overloadPeel * 8f, -14f - overloadPeel * 10f);

        const float w = 148f;
        const float h = 24f;

        Rlgl.PushMatrix();
        Rlgl.Translatef(tapeCenter.X, tapeCenter.Y, 0f);
        Rlgl.Rotatef(angleDeg, 0f, 0f, 1f);

        // Cast drop shadow (grows wider on right edge as tape peels!)
        float peelLift = 3.5f + overloadPeel * 9f;
        Raylib.DrawRectangleRec(new(-w * 0.5f + 3f, -h * 0.5f + peelLift, w, h), new Color(6, 9, 8, 145));

        // Base worn hazard yellow tape body
        Rectangle strip = new(-w * 0.5f, -h * 0.5f, w, h);
        Raylib.DrawRectangleRec(strip, HazardYellow);

        // Diagonal black warning chevrons along left & right ends
        for (float sx = -w * 0.5f - 8f; sx < w * 0.5f + 8f; sx += 18f)
        {
            float x0 = Math.Clamp(sx, -w * 0.5f, w * 0.5f);
            float x1 = Math.Clamp(sx + 9f, -w * 0.5f, w * 0.5f);
            float x2 = Math.Clamp(sx + 2f, -w * 0.5f, w * 0.5f);
            float x3 = Math.Clamp(sx - 7f, -w * 0.5f, w * 0.5f);
            DrawQuadCCW(
                new(x0, -h * 0.5f),
                new(x1, -h * 0.5f),
                new(x2, h * 0.5f),
                new(x3, h * 0.5f),
                HazardBlack);
        }

        // Solid worn yellow stencil field in middle so "НЕ ТРОГАТЬ" reads crisply at any size
        Rectangle textPanel = new(-48f, -h * 0.5f + 3f, 96f, h - 6f);
        Raylib.DrawRectangleRec(textPanel, HazardYellow);
        Raylib.DrawRectangleLinesEx(textPanel, 1f, HazardBlack);

        // Subtle curled peeling right corner highlight
        DrawTriCCW(
            new(w * 0.5f - 12f, -h * 0.5f),
            new(w * 0.5f, -h * 0.5f),
            new(w * 0.5f, -h * 0.5f + 11f),
            new Color(246, 224, 134, 255));

        Raylib.DrawRectangleLinesEx(strip, 1f, new Color(46, 38, 18, 210));

        // Crisp dark stenciled warning text
        string warnText = Loc.T("НЕ ТРОГАТЬ", "DO NOT TOUCH");
        Font wf = GameFonts.Get(Typeface.InterfaceBold, 12);
        Vector2 wsz = GameFonts.Measure(wf, warnText, 12);
        GameFonts.Draw(wf, warnText, new(-wsz.X * 0.5f, -wsz.Y * 0.5f), 12, HazardBlack, shadowed: false);

        Rlgl.PopMatrix();
    }

    // =========================================================================
    // 6. TACTILE BRASS CALIBRATION RAIL (TIMING SWEET-SPOT GAUGE)
    // =========================================================================
    private void DrawCalibrationRail(Vector2 o)
    {
        Rectangle housing = new(o.X + 228f, o.Y + 530f, 824f, 60f);
        Rectangle slot = new(o.X + 252f, o.Y + 547f, 776f, 26f);

        // Brushed-brass calibration housing
        Raylib.DrawRectangleRounded(new(housing.X + 3f, housing.Y + 4f, housing.Width, housing.Height), 0.14f, 6, new Color(6, 7, 8, 175));
        Raylib.DrawRectangleRounded(housing, 0.14f, 6, BrassMid);
        Raylib.DrawRectangleGradientV((int)housing.X + 4, (int)housing.Y + 2, (int)housing.Width - 8, (int)housing.Height - 4,
            new Color(214, 176, 108, 255), new Color(138, 102, 52, 255));
        Raylib.DrawRectangleRoundedLinesEx(housing, 0.14f, 6, 1.8f, BrassDark);

        DrawBrassScrew(new(housing.X + 12f, housing.Y + housing.Height * 0.5f), 4.2f);
        DrawBrassScrew(new(housing.X + housing.Width - 12f, housing.Y + housing.Height * 0.5f), 4.2f);

        // Etched brass graduation ticks along top rim
        for (int i = 0; i <= 32; i++)
        {
            float tx = slot.X + slot.Width * (i / 32f);
            float th = (i % 4 == 0) ? 7f : 4f;
            Raylib.DrawLineEx(new(tx, housing.Y + 5f), new(tx, housing.Y + 5f + th), i % 4 == 0 ? 1.4f : 1f, EngravedInk);
        }

        // Milled dark steel track slot
        Raylib.DrawRectangleRounded(slot, 0.25f, 4, new Color(15, 18, 20, 255));
        Raylib.DrawRectangleRoundedLinesEx(slot, 0.25f, 4, 1.4f, new Color(64, 50, 28, 235));

        int activePad = Math.Clamp(_solderedCount, 0, PadCount - 1);
        var timing = PadTiming[activePad];
        float sweetStart = timing.Center - timing.Width * 0.5f;
        float sweetEnd = timing.Center + timing.Width * 0.5f;
        bool inSweet = _phase == Phase.Soldering && _gaugePos >= sweetStart && _gaugePos <= sweetEnd && _missRecoveryTimer <= 0f;

        if (_phase == Phase.Overload)
        {
            float surge = 0.6f + 0.4f * MathF.Sin(_overloadTimer * 52f);
            Raylib.DrawRectangleRounded(new(slot.X + 3f, slot.Y + 3f, slot.Width - 6f, slot.Height - 6f), 0.2f, 4,
                new Color(224, (int)(62 + 64 * surge), 44, 240));
            return;
        }

        // Warm golden sweet-spot window inside rail
        float swX = slot.X + sweetStart * slot.Width;
        float swW = timing.Width * slot.Width;
        Rectangle sweetRect = new(swX, slot.Y + 3f, swW, slot.Height - 6f);
        Color sweetTop = inSweet ? new Color(255, 224, 122, 245) : new Color(216, 166, 68, 215);
        Color sweetBot = inSweet ? new Color(226, 152, 48, 245) : new Color(154, 108, 38, 215);
        Raylib.DrawRectangleGradientV((int)sweetRect.X, (int)sweetRect.Y, (int)sweetRect.Width, (int)sweetRect.Height, sweetTop, sweetBot);
        Raylib.DrawRectangleLinesEx(sweetRect, 1.3f, inSweet ? new Color(255, 242, 184, 255) : BrassBright);

        // Brass bracket pointers marking exact sweet-spot boundaries
        DrawTriCCW(new(swX - 4f, slot.Y - 3f), new(swX + 4f, slot.Y - 3f), new(swX, slot.Y + 3f), EngravedInk);
        DrawTriCCW(new(swX + swW - 4f, slot.Y - 3f), new(swX + swW + 4f, slot.Y - 3f), new(swX + swW, slot.Y + 3f), EngravedInk);

        // Brief muted rust-red flash on miss recovery
        if (_missFlashTimer > 0f)
        {
            int ma = (int)(130 * (_missFlashTimer / 0.32f));
            Raylib.DrawRectangleRounded(slot, 0.25f, 4, new Color(194, 52, 36, ma));
        }

        // Oscillating glowing copper heat-bar indicator
        float barX = slot.X + _gaugePos * slot.Width;
        float barW = 14f;
        Rectangle heatBar = new(barX - barW * 0.5f, slot.Y - 6f, barW, slot.Height + 12f);

        // Soft warm heat glow behind moving copper bar
        Raylib.DrawCircleGradient((int)barX, (int)(slot.Y + slot.Height * 0.5f), inSweet ? 32f : 22f,
            inSweet ? new Color(255, 206, 92, 130) : new Color(236, 118, 42, 85), Color.Blank);

        Raylib.DrawRectangleRounded(new(heatBar.X + 2f, heatBar.Y + 3f, heatBar.Width, heatBar.Height), 0.3f, 4, new Color(8, 8, 8, 175));
        Color barBody = _missRecoveryTimer > 0f
            ? new Color(176, 62, 46, 255)
            : inSweet ? new Color(255, 224, 138, 255) : new Color(232, 128, 52, 255);
        Raylib.DrawRectangleRounded(heatBar, 0.3f, 4, barBody);
        Raylib.DrawRectangleRoundedLinesEx(heatBar, 0.3f, 4, 1.4f, EngravedInk);
        // Incandescent vertical hairline core
        Raylib.DrawRectangle((int)(barX - 2f), (int)(heatBar.Y + 4f), 4, (int)(heatBar.Height - 8f), new Color(255, 248, 220, 230));
    }

    // =========================================================================
    // 7. BOTTOM STAMPED CONTROL STRIP
    // =========================================================================
    private void DrawBottomLegend(Vector2 o, Font uiFont)
    {
        Rectangle bar = new(o.X + 210f, o.Y + 604f, 860f, 34f);
        string legend = _phase == Phase.Overload
            ? Loc.T("ЗАМЫКАНИЕ ШАХТЫ · КОНТУР 0.1 ПРОБИТ!", "SHAFT SURGE · CIRCUIT 0.1 BREACHED!")
            : Loc.T("[ЛКМ] / [ПРОБЕЛ] / [E] — ПАЯТЬ КОНТАКТ     ·     [ESC] — ЗАКРЫТЬ ЩИТ",
                    "[LMB] / [SPACE] / [E] — SOLDER JOINT     ·     [ESC] — CLOSE BOX");
        Color ink = _phase == Phase.Overload ? new Color(255, 132, 112, 255) : new Color(206, 198, 180, 230);
        DrawCenteredText(uiFont, legend, bar, 15, ink, shadowed: true);
    }

    // =========================================================================
    // 8. LIVE 2D ILLUSTRATED HEATED SOLDERING IRON (SOVIET EPSN-40 STYLE)
    // =========================================================================
    private void DrawSolderingIron(Vector2 o)
    {
        Vector2 tip = o + GetIronTipPosition();
        float heatPulse = 0.72f + 0.28f * MathF.Sin(_elapsed * 9.5f);

        // Glowing cherry-orange heat bloom around tip
        Raylib.DrawCircleGradient((int)tip.X, (int)tip.Y, 34f,
            new Color(255, 118, 32, (int)(92 * heatPulse)), Color.Blank);
        Raylib.DrawCircleGradient((int)tip.X, (int)tip.Y, 16f,
            new Color(255, 206, 98, (int)(130 * heatPulse)), Color.Blank);

        // Local coordinates: (0,0) = chisel copper tip; +X extends along shaft to wooden handle.
        // Rotate -146 deg so handle extends cleanly up-left (never blocking Voltmeter, Hazard Tape, or Labels!).
        Rlgl.PushMatrix();
        Rlgl.Translatef(tip.X, tip.Y, 0f);
        Rlgl.Rotatef(-146f, 0f, 0f, 1f);

        // Subtle cast drop shadow beneath the entire soldering iron
        Raylib.DrawRectangleRounded(new(4f, 7f, 204f, 18f), 0.4f, 4, new Color(5, 8, 7, 105));

        // 1. Heated conical copper chisel tip (0 .. 34)
        DrawTriCCW(new(0f, 0f), new(34f, -6.5f), new(34f, 6.5f), new Color(216, 78, 30, 255));
        DrawTriCCW(new(0f, 0f), new(22f, -4.4f), new(22f, 4.4f), new Color(255, 148, 44, 255));
        DrawTriCCW(new(0f, 0f), new(10f, -2.2f), new(10f, 2.2f), new Color(255, 232, 142, 255));

        // 2. Ventilated steel heating barrel with heat-blued tempering (32 .. 106)
        Rectangle barrel = new(32f, -6.8f, 74f, 13.6f);
        Raylib.DrawRectangleGradientV((int)barrel.X, (int)barrel.Y, (int)barrel.Width, (int)barrel.Height,
            new Color(166, 174, 180, 255), new Color(68, 74, 80, 255));
        // Heat-blued tint near hot copper end
        Raylib.DrawRectangle((int)barrel.X, (int)barrel.Y + 1, 20, 12, new Color(96, 72, 118, 95));
        Raylib.DrawRectangleLinesEx(barrel, 1.1f, new Color(28, 32, 36, 230));
        // Dark heat ventilation slots along steel barrel
        for (int s = 0; s < 4; s++)
        {
            Raylib.DrawRectangleRounded(new(54f + s * 11f, -3.2f, 6f, 6.4f), 0.4f, 4, new Color(24, 26, 28, 225));
        }

        // 3. Crimped brass ferrule collar (104 .. 118)
        Rectangle ferrule = new(104f, -9.5f, 14f, 19f);
        Raylib.DrawRectangleGradientV((int)ferrule.X, (int)ferrule.Y, (int)ferrule.Width, (int)ferrule.Height,
            BrassBright, BrassDark);
        Raylib.DrawRectangleLinesEx(ferrule, 1.1f, EngravedInk);

        // 4. Turned lacquered walnut/birch wooden handle (117 .. 214)
        Rectangle handle = new(117f, -12.5f, 96f, 25f);
        Raylib.DrawRectangleRounded(handle, 0.38f, 6, new Color(138, 84, 44, 255));
        Raylib.DrawRectangleGradientV((int)handle.X + 3, (int)handle.Y + 2, (int)handle.Width - 6, (int)handle.Height - 4,
            new Color(178, 118, 66, 255), new Color(84, 48, 24, 255));
        Raylib.DrawRectangleRoundedLinesEx(handle, 0.38f, 6, 1.3f, EngravedInk);

        // Ergonomic turned grip grooves in wooden handle
        for (int g = 0; g < 3; g++)
        {
            float gx = 132f + g * 10f;
            Raylib.DrawLineEx(new(gx, -11f), new(gx, 11f), 1.8f, new Color(64, 34, 16, 195));
        }

        // Iconic 1990s coiled blue Soviet electrical tape wrap near rear of handle!
        Rectangle blueTape = new(176f, -12.8f, 22f, 25.6f);
        Raylib.DrawRectangleRec(blueTape, new Color(36, 98, 192, 255));
        Raylib.DrawRectangleGradientV((int)blueTape.X, (int)blueTape.Y, (int)blueTape.Width, (int)blueTape.Height,
            new Color(74, 142, 232, 210), new Color(22, 62, 134, 230));
        Raylib.DrawLineEx(new(183f, -12f), new(186f, 12f), 1.1f, new Color(18, 46, 102, 165));
        Raylib.DrawLineEx(new(191f, -12f), new(194f, 12f), 1.1f, new Color(18, 46, 102, 165));

        // Rubber cord strain-relief boot (212 .. 228)
        Raylib.DrawRectangleRounded(new(211f, -5.5f, 16f, 11f), 0.35f, 4, new Color(28, 30, 32, 255));

        Rlgl.PopMatrix();
    }

    // =========================================================================
    // 9. ROSIN SMOKE WISPS (BEHIND HAZARD TAPE) & MOLTEN GOLDEN SPARKS
    // =========================================================================
    private void DrawSmokeWisps(Vector2 o)
    {
        foreach (var s in _smoke)
        {
            float t = Math.Clamp(s.Life / s.MaxLife, 0f, 1f);
            int alpha = (int)(MathF.Sin(t * MathF.PI) * (s.Dark ? 95f : 112f));
            Color col = s.Dark
                ? new Color(76, 74, 72, alpha)
                : new Color(224, 218, 202, alpha);
            Vector2 p = o + s.Pos;
            Raylib.DrawCircleGradient((int)p.X, (int)p.Y, s.Radius, col, Color.Blank);
        }
    }

    private void DrawSparks(Vector2 o)
    {
        foreach (var sp in _sparks)
        {
            float t = Math.Clamp(sp.Life / sp.MaxLife, 0f, 1f);
            Vector2 head = o + sp.Pos;
            Vector2 tail = head - sp.Vel * 0.032f;
            Color outer = sp.IsMissSpark
                ? new Color(255, 126, 62, (int)(240 * t))
                : new Color(255, 196, 64, (int)(255 * t));
            Color core = sp.IsMissSpark
                ? new Color(220, 236, 255, (int)(230 * t))
                : new Color(255, 250, 214, (int)(255 * t));
            Raylib.DrawLineEx(tail, head, sp.Size * 1.4f, outer);
            Raylib.DrawCircleV(head, sp.Size * 0.9f, core);
        }
    }

    // =========================================================================
    // DRAWING PRIMITIVES & HARDWARE HELPERS
    // =========================================================================
    private static float VoltageToAngleDeg(float volts, float startDeg, float endDeg)
    {
        float frac = Math.Clamp(volts / 380f, 0f, 1.03f);
        return startDeg + (endDeg - startDeg) * frac;
    }

    private static void DrawArcStroke(Vector2 center, float radius, float thickness, float startDeg, float endDeg, int segments, Color color)
    {
        if (segments < 1) return;
        float step = (endDeg - startDeg) / segments;
        for (int i = 0; i < segments; i++)
        {
            float a0 = (startDeg + i * step) * (MathF.PI / 180f);
            float a1 = (startDeg + (i + 1) * step) * (MathF.PI / 180f);
            Vector2 p0 = center + new Vector2(MathF.Cos(a0) * radius, MathF.Sin(a0) * radius);
            Vector2 p1 = center + new Vector2(MathF.Cos(a1) * radius, MathF.Sin(a1) * radius);
            Raylib.DrawLineEx(p0, p1, thickness, color);
        }
    }

    private static void DrawSteelRivet(Vector2 center, float radius)
    {
        Raylib.DrawCircleV(center + new Vector2(1.5f, 2f), radius + 1.5f, new Color(8, 10, 11, 165));
        Raylib.DrawCircleV(center, radius + 1.2f, SteelDark);
        Raylib.DrawCircleV(center, radius, SteelLight);
        Raylib.DrawCircleV(center + new Vector2(-radius * 0.3f, -radius * 0.3f), radius * 0.36f, new Color(188, 198, 204, 190));
    }

    private static void DrawBrassScrew(Vector2 center, float radius)
    {
        Raylib.DrawCircleV(center + new Vector2(0.8f, 1.2f), radius + 0.8f, new Color(12, 10, 8, 150));
        Raylib.DrawCircleV(center, radius, BrassDark);
        Raylib.DrawCircleV(center, radius - 0.9f, BrassBright);
        float d = radius * 0.62f;
        Raylib.DrawLineEx(center + new Vector2(-d, -d * 0.55f), center + new Vector2(d, d * 0.55f), 1.3f, EngravedInk);
    }

    private static void DrawCenteredText(Font font, string text, Rectangle rect, int size, Color color, bool shadowed)
    {
        Vector2 sz = GameFonts.Measure(font, text, size);
        Vector2 pos = new(rect.X + (rect.Width - sz.X) * 0.5f, rect.Y + (rect.Height - sz.Y) * 0.5f);
        GameFonts.Draw(font, text, pos, size, color, shadowed);
    }

    private static void DrawTriCCW(Vector2 a, Vector2 b, Vector2 c, Color color)
    {
        // In screen coordinates (+Y down), CCW winding has negative 2D cross product.
        float cross = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        if (cross > 0f)
            Raylib.DrawTriangle(a, c, b, color);
        else
            Raylib.DrawTriangle(a, b, c, color);
    }

    private static void DrawQuadCCW(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
    {
        DrawTriCCW(a, b, c, color);
        DrawTriCCW(a, c, d, color);
    }

    private static void SafePlayAudio(Action play)
    {
        try
        {
            play();
        }
        catch
        {
            // Keep gameplay resilient when audio device is headless or muted.
        }
    }
}
