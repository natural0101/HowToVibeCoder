#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Raylib_cs;

namespace HowToVibeCoder;

/// <summary>
/// Tactile 1998 Soviet elevator junction-box soldering mini-game layered directly over
/// bespoke hand-painted hardware textures (<c>elevator_box_bg.png</c> + <c>soldering_iron_sprite.png</c>)
/// in 1280x720 virtual UI space via <see cref="UiViewport"/>.
/// </summary>
public sealed class ElevatorRepairMinigame
{
    private const float VirtualW = 1280f;
    private const float VirtualH = 720f;
    private const int PadCount = 3;
    private const float OverloadDuration = 0.58f;
    private const float MissRecoveryDuration = 0.22f;
    private const float OpenInputGraceDuration = 0.18f;
    private const float TapeRipDuration = 0.52f;

    // Exact hand-painted soldering_iron_sprite.png (510x512) copper chisel tip normalized pivot
    private const float IronTipPivotNormX = 0.9408f;
    private const float IronTipPivotNormY = 0.9807f;
    private const float IronDrawW = 224f;
    private const float IronDrawH = 225f;
    private const float IronBaseTiltDeg = -24f;

    // Measured pixel coordinates on elevator_box_bg.png (1280x720)
    private static readonly Rectangle NameplateRect = new(285f, 104f, 313f, 66f);
    private static readonly Rectangle StatusBezelRect = new(262f, 197f, 358f, 26f);
    private static readonly Vector2 PilotLampCenter = new(737f, 160f);
    private const float PilotLampRadius = 29f;
    private static readonly Rectangle PilotLabelRect = new(687f, 89f, 100f, 18f);

    private static readonly Vector2 VoltmeterPivot = new(982f, 199f);
    private const float VoltmeterArcRadius = 55f;
    private const float VoltmeterStartDeg = -148f;
    private const float VoltmeterEndDeg = -32f;
    private static readonly Rectangle VoltmeterReadoutRect = new(912f, 213f, 140f, 19f);

    private static readonly Rectangle BrassGrooveRect = new(278f, 562f, 824f, 46f);
    private static readonly Rectangle TrackSlotRect = new(286f, 567f, 808f, 36f);
    private static readonly Rectangle BottomLegendRect = new(210f, 668f, 860f, 20f);

    // Exact centers of the 3 painted recessed copper solder pads on the green Soviet PCB
    private static readonly Vector2[] PadPositions =
    [
        new(472.5f, 381f),
        new(711f, 381f),
        new(951.5f, 383.5f)
    ];

    private static readonly (string Ru, string En)[] PadLabels =
    [
        ("ПРИВОД ШАХТЫ", "SHAFT DRIVE"),
        ("РЕЛЕ ДВЕРЕЙ", "DOOR RELAY"),
        ("КОНТУР 0.1", "CIRCUIT 0.1")
    ];

    private static readonly string[] PadCodes = ["01", "02", "03"];

    // Sweet-spot parameters per pad: (center 0..1, width 28% / 26% / 22%, sweep speed units/sec)
    private static readonly (float Center, float Width, float Speed)[] PadTiming =
    [
        (0.50f, 0.28f, 1.12f),
        (0.57f, 0.26f, 1.36f),
        (0.46f, 0.22f, 1.62f)
    ];

    // Rest center of the weathered "НЕ ТРОГАТЬ" hazard tape across the upper-right rim of Pad 2
    private static readonly Vector2 TapeRestCenter = new(1008f, 375f);

    // Warm 1998 Soviet hardware ink & glow palette
    private static readonly Color EngravedBronzeInk = new(36, 23, 12, 242);
    private static readonly Color EngravedHighlight = new(238, 214, 156, 118);
    private static readonly Color SilkscreenIvory = new(234, 228, 206, 235);
    private static readonly Color BrassBright = new(228, 188, 112, 255);
    private static readonly Color BrassDark = new(82, 58, 28, 255);
    private static readonly Color CopperLit = new(246, 178, 66, 255);
    private static readonly Color DangerRed = new(204, 38, 36, 255);
    private static readonly Color HazardYellow = new(214, 166, 42, 255);
    private static readonly Color HazardCharcoal = new(26, 24, 20, 255);

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

    private Texture2D _bgTex;
    private Texture2D _ironTex;
    private bool _texturesAttempted;

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

    private bool _tapeRipped;
    private bool _tapeAnimActive;
    private float _tapeRipAnim;
    private bool _previewShowcaseMode;

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

        _tapeRipped = false;
        _tapeAnimActive = false;
        _tapeRipAnim = 0f;
        _previewShowcaseMode = false;

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
        _traceGlow[0] = 0.85f; // Live incoming feed on busbar
        _sparks.Clear();
        _smoke.Clear();

        SafePlayAudio(() => AudioMusicSystem.PlayActionClack(0.92f));
    }

    public void Close()
    {
        if (!IsActive) return;
        IsActive = false;
        _phase = Phase.Idle;
        _previewShowcaseMode = false;
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

        // Keep Pad 3 visibly sealed with curling "НЕ ТРОГАТЬ" tape while showcasing golden target track + puck
        _tapeRipped = count >= PadCount;
        _tapeAnimActive = false;
        _tapeRipAnim = 0f;
        _previewShowcaseMode = count == 2;

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
            _ironPressAnim = 0.32f;
            var timing = PadTiming[count];
            _gaugePos = timing.Center + timing.Width * 0.04f;
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

        if (_tapeAnimActive)
        {
            _tapeRipAnim += dt / TapeRipDuration;
            if (_tapeRipAnim >= 1f)
            {
                _tapeRipAnim = 1f;
                _tapeAnimActive = false;
            }
        }

        // Cool freshly soldered beads & energize painted copper traces
        for (int i = 0; i < PadCount; i++)
        {
            if (_padHeat[i] > 0f) _padHeat[i] = MathF.Max(0f, _padHeat[i] - dt * 0.72f);
        }
        for (int i = 0; i < _traceGlow.Length; i++)
        {
            float targetGlow = i <= _solderedCount ? 1f : 0f;
            _traceGlow[i] += (targetGlow - _traceGlow[i]) * MathF.Min(1f, dt * 8f);
        }

        // Smoothly glide soldering iron toward active pad
        int activePadIdx = Math.Clamp(_solderedCount, 0, PadCount - 1);
        Vector2 targetIronPos = PadPositions[activePadIdx];
        if (IsWaitingForTapeRip())
        {
            // Hold iron slightly poised back while waiting for player to peel off "НЕ ТРОГАТЬ" tape
            targetIronPos += new Vector2(-18f, -14f);
        }
        _ironHoverPos = Vector2.Lerp(_ironHoverPos, targetIronPos, MathF.Min(1f, dt * 11f));

        // Spring-damped analog voltmeter needle physics
        float targetVolts = TargetVoltageForCount(_solderedCount);
        float stiffness = _phase == Phase.Overload ? 168f : 90f;
        float damping = _phase == Phase.Overload ? 8.2f : 10.5f;
        _needleVel += (targetVolts - _needleVolts) * stiffness * dt;
        _needleVel *= MathF.Exp(-damping * dt);
        _needleVolts = Math.Clamp(_needleVolts + _needleVel * dt, 0f, 392f);

        // Gentle curling rosin smoke rising from heated copper chisel tip
        _ambientSmokeTimer -= dt;
        if (_ambientSmokeTimer <= 0f)
        {
            _ambientSmokeTimer = 0.09f;
            Vector2 tip = GetIronTipPosition();
            SpawnSmoke(tip + new Vector2(-3f, -4f), 1, dark: false);
        }

        UpdateParticles(dt);

        if (_phase == Phase.Overload)
        {
            _overloadTimer += dt;
            _shakeAmount = MathF.Max(_shakeAmount, 4.2f + 3.4f * MathF.Sin(_overloadTimer * 46f));
            _overloadSparkCooldown -= dt;
            if (_overloadSparkCooldown <= 0f)
            {
                _overloadSparkCooldown = 0.07f;
                SpawnSparks(PadPositions[2], 8, isMiss: false);
                SpawnSmoke(PadPositions[2] + new Vector2(-4f, -8f), 2, dark: false);
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

        bool waitingTape = IsWaitingForTapeRip();

        // Sweep indicator cursor across bottom machined-brass groove only when ready to solder
        var timing = PadTiming[activePadIdx];
        if (!waitingTape && _missRecoveryTimer <= 0f)
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

        // Trigger action on LMB / Space / E
        bool triggerPressed = _openGraceTimer <= 0f && _missRecoveryTimer <= 0f &&
            (Raylib.IsMouseButtonPressed(MouseButton.Left) ||
             Raylib.IsKeyPressed(KeyboardKey.Space) ||
             Raylib.IsKeyPressed(KeyboardKey.E));

        if (triggerPressed)
        {
            _previewShowcaseMode = false;

            if (waitingTape)
            {
                HandleTapeRip();
                return false;
            }

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

        EnsureTexturesLoaded();

        // Deep moody scrim behind the 1280x720 hardware viewport (covers ultrawide sidebars cleanly)
        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), new Color(10, 8, 7, 218));

        UiViewport.Begin();
        try
        {
            float ox = (UiViewport.Width - VirtualW) * 0.5f;
            float oy = (UiViewport.Height - VirtualH) * 0.5f;

            // Physical impact / 380V overload cabinet vibration
            float shakeX = _shakeAmount > 0.05f ? MathF.Sin(_elapsed * 78f) * _shakeAmount : 0f;
            float shakeY = _shakeAmount > 0.05f ? MathF.Cos(_elapsed * 63f) * _shakeAmount * 0.7f : 0f;
            Vector2 origin = new(ox + shakeX, oy + shakeY);

            DrawCabinetBackground(origin);
            DrawNameplateAndStatus(origin, uiFont, titleFont);
            DrawPilotLampOverlay(origin, uiFont);
            DrawAnalogVoltmeterOverlay(origin, uiFont);
            DrawPcbEnergizedTraces(origin);
            DrawSolderPadsAndSilkscreen(origin, uiFont);
            DrawSmokeWisps(origin);
            DrawHazardTape(origin);
            DrawCalibrationTrack(origin, uiFont);
            DrawBottomLegend(origin, uiFont);
            DrawSolderingIron(origin);
            DrawSparks(origin);
        }
        finally
        {
            Raylib.EndMode2D();
        }
    }

    private bool IsWaitingForTapeRip() => _phase == Phase.Soldering && _solderedCount == 2 && !_tapeRipped && !_previewShowcaseMode;

    private void HandleTapeRip()
    {
        _tapeRipped = true;
        _tapeAnimActive = true;
        _tapeRipAnim = 0f;
        _openGraceTimer = 0.20f;
        _shakeAmount = 4.4f;
        _hitFlashTimer = 0.24f;

        _statusRu = "ЛЕНТА СОРВАНА — ПАЯЙ КОНТУР 0.1!";
        _statusEn = "TAPE RIPPED — SOLDER CIRCUIT 0.1!";
        _statusTimer = 1.25f;

        // Arm the final 380V calibration sweep with fair lead-in from the right
        var finalTiming = PadTiming[2];
        _gaugePos = Math.Clamp(finalTiming.Center + 0.40f, 0.72f, 0.90f);
        _gaugeDir = -1f;

        SpawnSmoke(PadPositions[2] + new Vector2(8f, -6f), 5, dark: true);
        SpawnSparks(PadPositions[2] + new Vector2(10f, -6f), 8, isMiss: false);

        SafePlayAudio(() =>
        {
            AudioMusicSystem.PlayActionClack(1.35f);
            AudioMusicSystem.PlayHandGrab();
        });
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
        SpawnSparks(padPos, 26, isMiss: false);
        SpawnSmoke(padPos + new Vector2(-3f, -6f), 9, dark: false);

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
            _shakeAmount = 8.2f;
            _needleVel += 540f;
            SpawnSparks(PadPositions[2], 36, isMiss: false);
            SpawnSmoke(PadPositions[2] + new Vector2(-4f, -8f), 11, dark: false);
            SafePlayAudio(() =>
            {
                AudioMusicSystem.PlayComicZap();
                AudioMusicSystem.PlayAnxietyAlarm();
            });
        }
        else if (_solderedCount == 2 && !_tapeRipped)
        {
            _statusRu = "СОРВИ ЛЕНТУ С КОНТУРА 0.1!";
            _statusEn = "RIP TAPE OFF CIRCUIT 0.1!";
            _statusTimer = 1.35f;
        }
        else
        {
            _statusRu = $"КОНТАКТ {_solderedCount} / {PadCount} СПАЯН — ТОК ПОШЁЛ";
            _statusEn = $"JOINT {_solderedCount} / {PadCount} SOLDERED — POWER UP";
            _statusTimer = 1.15f;

            var nextTiming = PadTiming[_solderedCount];
            _gaugePos = Math.Clamp(nextTiming.Center - 0.42f, 0.08f, 0.28f);
            _gaugeDir = 1f;
        }
    }

    private void HandleSolderMiss(int padIndex)
    {
        _ironPressAnim = 0.65f;
        _missRecoveryTimer = MissRecoveryDuration;
        _missFlashTimer = 0.32f;
        _shakeAmount = 5.2f;
        _statusRu = "ИСКРА МИМО — ЛОВИ ЗОЛОТУЮ ЗОНУ";
        _statusEn = "MISSED — HIT THE GOLDEN ZONE";
        _statusTimer = 0.95f;

        Vector2 padPos = PadPositions[padIndex];
        SpawnSparks(padPos, 14, isMiss: true);
        SpawnSmoke(padPos + new Vector2(-2f, -4f), 6, dark: true);

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
        float idleBob = MathF.Sin(_elapsed * 3.1f) * 2.0f;
        float plunge = MathF.Sin(_ironPressAnim * MathF.PI) * 15f;
        float hoverLift = IsWaitingForTapeRip() ? -22f : -15f;
        return new Vector2(_ironHoverPos.X - 2f + plunge * 0.16f, _ironHoverPos.Y + hoverLift + idleBob + plunge);
    }

    // =========================================================================
    // TEXTURE LOADING (LAZY, HEADLESS-SAFE, BILINEAR FILTERING)
    // =========================================================================
    private void EnsureTexturesLoaded()
    {
        if (_texturesAttempted) return;
        if (!Raylib.IsWindowReady()) return;
        _texturesAttempted = true;

        _bgTex = TryLoadUiTexture("elevator_box_bg.png");
        _ironTex = TryLoadUiTexture("soldering_iron_sprite.png");
    }

    private static Texture2D TryLoadUiTexture(string fileName)
    {
        try
        {
            string? path = ResolveUiAssetPath(fileName);
            if (path is null) return default;
            Texture2D tex = Raylib.LoadTexture(path);
            if (tex.Id != 0)
            {
                Raylib.SetTextureFilter(tex, TextureFilter.Bilinear);
            }
            return tex;
        }
        catch
        {
            return default;
        }
    }

    private static string? ResolveUiAssetPath(string fileName)
    {
        string[] candidates =
        [
            Path.Combine(AppContext.BaseDirectory, "assets", "ui", fileName),
            Path.Combine(Directory.GetCurrentDirectory(), "assets", "ui", fileName),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "assets", "ui", fileName))
        ];

        foreach (string path in candidates)
        {
            if (File.Exists(path)) return path;
        }
        return null;
    }

    // =========================================================================
    // 1. HAND-PAINTED SOVIET ELEVATOR JUNCTION BOX BACKDROP
    // =========================================================================
    private void DrawCabinetBackground(Vector2 o)
    {
        Rectangle dest = new(o.X, o.Y, VirtualW, VirtualH);
        if (_bgTex.Id != 0)
        {
            Rectangle src = new(0f, 0f, _bgTex.Width, _bgTex.Height);
            Raylib.DrawTexturePro(_bgTex, src, dest, Vector2.Zero, 0f, Color.White);
        }
        else
        {
            // Minimal safe headless/missing-asset backdrop so Draw never crashes
            Raylib.DrawRectangleRec(dest, new Color(24, 30, 34, 255));
        }

        // Subtle warm crimson atmospheric bounce across cabinet during 380V overload
        if (_phase == Phase.Overload)
        {
            float pulse = 0.5f + 0.5f * MathF.Sin(_overloadTimer * 48f);
            Raylib.DrawRectangleRec(dest, new Color(210, 36, 24, (int)(26 * pulse)));
        }
    }

    // =========================================================================
    // 2. ENGRAVED BRASS NAMEPLATE & DARK BAKELITE STATUS BEZEL
    // =========================================================================
    private void DrawNameplateAndStatus(Vector2 o, Font uiFont, Font titleFont)
    {
        Rectangle plate = OffsetRect(NameplateRect, o);
        string titleText = Loc.T("ЩИТ ЛИФТА №1 (1998)", "ELEVATOR BOX #1 (1998)");
        DrawEngravedFittedText(titleFont, titleText, plate, 22, 16, EngravedBronzeInk, EngravedHighlight);

        Rectangle statusSlot = OffsetRect(StatusBezelRect, o);

        // Subtle recessed inner glow inside the painted dark Bakelite window on state feedback
        if (_phase == Phase.Overload)
        {
            float pulse = 0.55f + 0.45f * MathF.Sin(_overloadTimer * 44f);
            Raylib.DrawRectangleRec(statusSlot, new Color(185, 32, 24, (int)(70 * pulse)));
        }
        else if (_missFlashTimer > 0f)
        {
            int alpha = (int)(72 * (_missFlashTimer / 0.32f));
            Raylib.DrawRectangleRec(statusSlot, new Color(172, 54, 32, alpha));
        }
        else if (_hitFlashTimer > 0f)
        {
            int alpha = (int)(54 * (_hitFlashTimer / 0.34f));
            Raylib.DrawRectangleRec(statusSlot, new Color(218, 152, 46, alpha));
        }

        string statusLine;
        Color statusInk;
        if (_phase == Phase.Overload)
        {
            statusLine = Loc.T("ПЕРЕГРУЗКА 380В — КОНТУР 0.1!", "380V OVERLOAD — CIRCUIT 0.1!");
            statusInk = new Color(255, 118, 98, 255);
        }
        else if (_statusTimer > 0f && !string.IsNullOrEmpty(_statusRu))
        {
            statusLine = Loc.T(_statusRu, _statusEn);
            statusInk = _missFlashTimer > 0f ? new Color(248, 154, 112, 255) : new Color(255, 214, 116, 255);
        }
        else if (IsWaitingForTapeRip())
        {
            float warnPulse = 0.72f + 0.28f * MathF.Sin(_elapsed * 7.5f);
            statusLine = Loc.T("СОРВИ ЛЕНТУ С КОНТУРА 0.1!", "RIP TAPE OFF CIRCUIT 0.1!");
            statusInk = new Color(255, (int)(178 + 42 * warnPulse), 84, 255);
        }
        else
        {
            statusLine = Loc.T($"КОНТАКТЫ: {_solderedCount} / {PadCount} — СПАЯЙ ЦЕПЬ ЗАПУСКА",
                               $"JOINTS: {_solderedCount} / {PadCount} — SOLDER START CIRCUIT");
            statusInk = new Color(238, 222, 186, 248);
        }

        DrawFittedCenteredText(uiFont, statusLine, statusSlot, 14, 11, statusInk, shadowed: true);
    }

    // =========================================================================
    // 3. LIVE PILOT INDICATOR LAMP GLOW OVER PAINTED KNURLED BRASS BEZEL
    // =========================================================================
    private void DrawPilotLampOverlay(Vector2 o, Font uiFont)
    {
        Vector2 center = o + PilotLampCenter;
        bool overload = _phase == Phase.Overload;
        float pulse = overload
            ? 0.55f + 0.45f * MathF.Sin(_overloadTimer * 48f)
            : 0.78f + 0.22f * MathF.Sin(_elapsed * 3.8f);

        if (overload)
        {
            // Intense pulsing crimson emergency alarm halo & glowing lens core
            Raylib.DrawCircleGradient((int)center.X, (int)center.Y, 92f,
                new Color(255, 36, 24, (int)(135 * pulse)), Color.Blank);
            Raylib.DrawCircleGradient((int)center.X, (int)center.Y, PilotLampRadius + 6f,
                new Color(255, 58, 36, (int)(205 * pulse)), new Color(170, 18, 16, 0));
            Raylib.DrawCircleGradient((int)(center.X - 3f), (int)(center.Y - 4f), 20f,
                new Color(255, 194, 134, (int)(210 * pulse)), Color.Blank);
        }
        else if (_solderedCount == 0)
        {
            // Faint warm standby filament glimmer inside unlit amber glass
            Raylib.DrawCircleGradient((int)center.X, (int)center.Y, 18f,
                new Color(224, 138, 42, (int)(36 * pulse)), Color.Blank);
        }
        else if (_solderedCount == 1)
        {
            // 120V warm honey-amber glow
            Raylib.DrawCircleGradient((int)center.X, (int)center.Y, 58f,
                new Color(246, 168, 46, (int)(66 * pulse)), Color.Blank);
            Raylib.DrawCircleGradient((int)center.X, (int)center.Y, PilotLampRadius,
                new Color(255, 188, 68, (int)(138 * pulse)), Color.Blank);
            Raylib.DrawCircleGradient((int)(center.X - 4f), (int)(center.Y - 5f), 14f,
                new Color(255, 232, 162, (int)(145 * pulse)), Color.Blank);
        }
        else
        {
            // 240V full bright molten-amber pilot lamp radiance
            Raylib.DrawCircleGradient((int)center.X, (int)center.Y, 74f,
                new Color(255, 178, 48, (int)(108 * pulse)), Color.Blank);
            Raylib.DrawCircleGradient((int)center.X, (int)center.Y, PilotLampRadius + 2f,
                new Color(255, 204, 78, (int)(192 * pulse)), Color.Blank);
            Raylib.DrawCircleGradient((int)(center.X - 4f), (int)(center.Y - 5f), 17f,
                new Color(255, 246, 198, (int)(205 * pulse)), Color.Blank);
        }

        // Crisp stenciled label centered cleanly BELOW the knurled brass ring on dark cabinet steel
        string lampLabel = overload ? Loc.T("АВАРИЯ!", "OVERLOAD!") : Loc.T("СЕТЬ", "POWER");
        Color lampInk = overload ? new Color(255, 118, 102, 255) : new Color(204, 196, 178, 224);
        DrawFittedCenteredText(uiFont, lampLabel, OffsetRect(PilotLabelRect, o), 13, 11, lampInk, shadowed: true);
    }

    // =========================================================================
    // 4. ANALOG VOLTMETER DIAL, SPRING NEEDLE & LOWER BAKELITE READOUT
    // =========================================================================
    private void DrawAnalogVoltmeterOverlay(Vector2 o, Font uiFont)
    {
        Vector2 pivot = o + VoltmeterPivot;

        // Safe scale arc (0..260V) & crimson overload zone (260..380V) drawn straight onto painted ivory face
        float dangerStartDeg = VoltageToAngleDeg(260f, VoltmeterStartDeg, VoltmeterEndDeg);
        DrawArcStroke(pivot, VoltmeterArcRadius, 2.0f, VoltmeterStartDeg, dangerStartDeg, 24, EngravedBronzeInk);
        DrawArcStroke(pivot, VoltmeterArcRadius, 4.4f, dangerStartDeg, VoltmeterEndDeg, 16, DangerRed);
        DrawArcStroke(pivot, VoltmeterArcRadius - 6.0f, 1.0f, VoltmeterStartDeg, VoltmeterEndDeg, 28, new Color(102, 82, 54, 145));

        // Major & minor graduation ticks every 20V
        for (int v = 0; v <= 380; v += 20)
        {
            float deg = VoltageToAngleDeg(v, VoltmeterStartDeg, VoltmeterEndDeg);
            float rad = deg * (MathF.PI / 180f);
            bool major = v == 0 || v == 120 || v == 240 || v == 380 || v % 60 == 0;
            float rIn = major ? VoltmeterArcRadius - 8.5f : VoltmeterArcRadius - 4.5f;
            float rOut = VoltmeterArcRadius + 1.5f;
            Vector2 dir = new(MathF.Cos(rad), MathF.Sin(rad));
            Color tickCol = v >= 260 ? DangerRed : EngravedBronzeInk;
            Raylib.DrawLineEx(pivot + dir * rIn, pivot + dir * rOut, major ? 1.7f : 1.0f, tickCol);
        }

        // Crisp vintage ink scale numerals (0, 120, 240, 380) cleanly inside aged-ivory glass
        Font numFont = GameFonts.Get(Typeface.InterfaceBold, 11);
        foreach (int labelV in new[] { 0, 120, 240, 380 })
        {
            float deg = VoltageToAngleDeg(labelV, VoltmeterStartDeg, VoltmeterEndDeg);
            float rad = deg * (MathF.PI / 180f);
            float rNum = VoltmeterArcRadius + 11.5f;
            Vector2 pos = pivot + new Vector2(MathF.Cos(rad) * rNum, MathF.Sin(rad) * rNum);
            string num = labelV.ToString();
            Vector2 sz = GameFonts.Measure(numFont, num, 11);
            Color numCol = labelV >= 260 ? DangerRed : EngravedBronzeInk;
            GameFonts.Draw(numFont, num, new(pos.X - sz.X * 0.5f, pos.Y - sz.Y * 0.5f), 11, numCol, shadowed: false);
        }

        // Subtle stamped meter unit emblem ("В" / "V") inside lower center of ivory dial
        string unitMark = Loc.T("В", "V");
        Font unitFont = GameFonts.Get(Typeface.InterfaceBold, 12);
        Vector2 uSz = GameFonts.Measure(unitFont, unitMark, 12);
        GameFonts.Draw(unitFont, unitMark, new(pivot.X - uSz.X * 0.5f, pivot.Y - 34f - uSz.Y * 0.5f), 12,
            new Color(74, 54, 34, 175), shadowed: false);

        // Vibrating spring-damped crimson pointer needle
        float tremor = _phase == Phase.Overload
            ? MathF.Sin(_elapsed * 74f) * 3.4f + MathF.Cos(_elapsed * 118f) * 1.9f
            : (_solderedCount > 0 ? MathF.Sin(_elapsed * 34f) * 0.9f : MathF.Sin(_elapsed * 12f) * 0.22f);
        float needleDeg = VoltageToAngleDeg(_needleVolts, VoltmeterStartDeg, VoltmeterEndDeg) + tremor;
        float needleRad = needleDeg * (MathF.PI / 180f);
        Vector2 needleDir = new(MathF.Cos(needleRad), MathF.Sin(needleRad));
        Vector2 needlePerp = new(-needleDir.Y, needleDir.X);
        Vector2 needleTip = pivot + needleDir * (VoltmeterArcRadius - 2.5f);

        // Soft cast shadow on aged-ivory dial face
        Raylib.DrawLineEx(pivot + new Vector2(1.8f, 2.0f), needleTip + new Vector2(1.8f, 2.0f), 2.2f, new Color(48, 36, 22, 68));

        // Tapered crimson meter needle
        Color needleCol = _needleVolts >= 255f ? DangerRed : new Color(162, 38, 28, 255);
        DrawTriCCW(pivot - needlePerp * 2.1f, pivot + needlePerp * 2.1f, needleTip, needleCol);
        Raylib.DrawLineEx(pivot, needleTip, 1.3f, needleCol);

        // Tiny machined brass pivot cap
        Raylib.DrawCircleV(pivot + new Vector2(1f, 1.2f), 7.4f, new Color(36, 24, 14, 110));
        Raylib.DrawCircleV(pivot, 7.2f, BrassDark);
        Raylib.DrawCircleV(pivot, 5.4f, BrassBright);
        Raylib.DrawCircleV(pivot, 2.3f, EngravedBronzeInk);

        // Clean stamped voltage readout tucked cleanly inside the dark Bakelite bar BELOW the ivory glass
        int shownVolts = (int)MathF.Round(Math.Clamp(_needleVolts, 0f, 380f));
        string unitStamp = Loc.T($"{shownVolts} В", $"{shownVolts} V");
        Color stampCol = shownVolts >= 260 ? new Color(255, 114, 96, 255) : new Color(238, 224, 190, 245);
        DrawFittedCenteredText(uiFont, unitStamp, OffsetRect(VoltmeterReadoutRect, o), 14, 11, stampCol, shadowed: true);
    }

    // =========================================================================
    // 5. LIVE ENERGIZED COPPER TRACE OVERLAYS ALONG PAINTED PCB PATHS
    // =========================================================================
    private void DrawPcbEnergizedTraces(Vector2 o)
    {
        // Segment 0: Bakelite terminal block right edge -> Pad 0 left copper rim (always live)
        DrawEnergizedTraceSegment(
            [o + new Vector2(347f, 381f), o + new Vector2(452f, 381f)],
            _traceGlow[0], overloadSurge: false, phaseOffset: 0f);
        // Subtle feed glow on top/bottom incoming terminal traces
        DrawEnergizedTraceSegment(
            [o + new Vector2(347f, 368f), o + new Vector2(376f, 368f)],
            _traceGlow[0] * 0.55f, overloadSurge: false, phaseOffset: 0.4f, coreWidth: 2.2f);
        DrawEnergizedTraceSegment(
            [o + new Vector2(347f, 396f), o + new Vector2(376f, 396f)],
            _traceGlow[0] * 0.55f, overloadSurge: false, phaseOffset: 0.8f, coreWidth: 2.2f);

        // Segment 1: Pad 0 right rim -> Pad 1 left rim (energizes when Pad 0 soldered)
        DrawEnergizedTraceSegment(
            [o + new Vector2(493f, 381f), o + new Vector2(691f, 381f)],
            _traceGlow[1], overloadSurge: false, phaseOffset: 1.2f);

        // Segment 2: Pad 1 right rim -> Pad 2 left rim (energizes when Pad 1 soldered)
        DrawEnergizedTraceSegment(
            [o + new Vector2(731f, 381f), o + new Vector2(932f, 383.5f)],
            _traceGlow[2], overloadSurge: false, phaseOffset: 2.4f);

        // Segment 3: Pad 2 upper-right painted diagonal exit trace (surges 380V on overload)
        DrawEnergizedTraceSegment(
            [o + new Vector2(964f, 369f), o + new Vector2(1011f, 319f), o + new Vector2(1058f, 319f)],
            _traceGlow[3], overloadSurge: _phase == Phase.Overload, phaseOffset: 3.6f);
    }

    private void DrawEnergizedTraceSegment(Vector2[] pts, float glow, bool overloadSurge, float phaseOffset, float coreWidth = 3.4f)
    {
        if (glow <= 0.06f || pts.Length < 2) return;

        float pulse = 0.74f + 0.26f * MathF.Sin(_elapsed * (overloadSurge ? 44f : 5.4f) + phaseOffset);
        Color baseCol = overloadSurge
            ? Raylib.ColorLerp(CopperLit, new Color(255, 76, 52, 255), 0.55f + 0.45f * MathF.Sin(_overloadTimer * 48f))
            : CopperLit;

        Color outerHalo = new(baseCol.R, baseCol.G, baseCol.B, (int)(56 * glow * pulse));
        Color midGlow = new(baseCol.R, baseCol.G, baseCol.B, (int)(168 * glow * pulse));
        Color hotCore = new(255, 238, 174, (int)(175 * glow * pulse));

        for (int i = 0; i < pts.Length - 1; i++)
        {
            Raylib.DrawLineEx(pts[i], pts[i + 1], coreWidth + 7.5f, outerHalo);
            Raylib.DrawLineEx(pts[i], pts[i + 1], coreWidth + 1.8f, midGlow);
            Raylib.DrawLineEx(pts[i], pts[i + 1], MathF.Max(1.4f, coreWidth * 0.55f), hotCore);
        }

        // Subtle traveling current shimmer dot along energized trace
        if (glow > 0.65f)
        {
            float travel = (_elapsed * (overloadSurge ? 2.6f : 0.9f) + phaseOffset * 0.23f) % 1f;
            Vector2 p = Vector2.Lerp(pts[0], pts[^1], travel);
            Raylib.DrawCircleGradient((int)p.X, (int)p.Y, 9f, new Color(255, 232, 148, (int)(135 * glow)), Color.Blank);
        }
    }

    // =========================================================================
    // 6. SOLDER PADS (SILKSCREEN STENCILS, TARGET HALOS & GLOSSY TIN BEADS)
    // =========================================================================
    private void DrawSolderPadsAndSilkscreen(Vector2 o, Font uiFont)
    {
        int activePad = _phase == Phase.Soldering ? Math.Clamp(_solderedCount, 0, PadCount - 1) : -1;

        for (int i = 0; i < PadCount; i++)
        {
            Vector2 pos = o + PadPositions[i];
            bool soldered = _padSoldered[i];
            bool active = i == activePad;
            float heat = _padHeat[i];

            // Crisp vintage white PCB silkscreen code cleanly ABOVE the pad (clear of top painted components & tape)
            Color codeCol = soldered || active ? BrassBright : new Color(214, 212, 190, 210);
            DrawFittedCenteredText(uiFont, PadCodes[i], new Rectangle(pos.X - 36f, o.Y + 323f, 72f, 20f), 14, 12, codeCol, shadowed: true);

            if (!soldered)
            {
                if (active)
                {
                    bool needsTapeRip = i == 2 && IsWaitingForTapeRip();
                    float pulse = 0.52f + 0.48f * MathF.Sin(_elapsed * (needsTapeRip ? 8.2f : 6.2f));
                    Color ringCol = needsTapeRip
                        ? new Color(246, 112, 54, (int)(120 + 115 * pulse))
                        : new Color(246, 188, 72, (int)(110 + 120 * pulse));

                    Raylib.DrawCircleGradient((int)pos.X, (int)pos.Y, 34f,
                        new Color(ringCol.R, ringCol.G, ringCol.B, (int)(44 * pulse)), Color.Blank);
                    DrawArcStroke(pos, 25f, 2.2f, 0f, 360f, 36, ringCol);
                }
            }
            else
            {
                // Cooling molten amber radiance right after soldering
                if (heat > 0.04f)
                {
                    Raylib.DrawCircleGradient((int)pos.X, (int)pos.Y, 42f,
                        new Color(255, 168, 56, (int)(140 * heat)), Color.Blank);
                }

                // Translucent amber rosin flux ring around outer copper rim
                Raylib.DrawCircleV(pos, 21.5f, new Color(118, 72, 22, 138));
                // Radial contact shadow under domed solder bead
                Raylib.DrawCircleV(pos + new Vector2(1.5f, 2.2f), 19.0f, new Color(16, 20, 22, 168));
                // Glossy domed liquid-silver tin solder bead (r = 18f)
                Raylib.DrawCircleV(pos, 18.2f, new Color(122, 132, 140, 255));
                Raylib.DrawCircleGradient((int)(pos.X - 3.5f), (int)(pos.Y - 3.5f), 17.2f,
                    new Color(236, 242, 248, 255), new Color(118, 128, 136, 255));
                // Subtle warm brass environmental rim bounce at bottom-right of bead
                DrawArcStroke(pos, 14.5f, 1.8f, 20f, 82f, 10, new Color(214, 182, 128, 110));
                // Crisp wet metallic specular glints on crown
                Raylib.DrawCircleV(pos + new Vector2(-5.5f, -5.5f), 4.6f, new Color(250, 253, 255, 228));
                Raylib.DrawCircleV(pos + new Vector2(-7.8f, -7.8f), 2.0f, Color.White);

                // Warm amber glow inside cooling molten solder center
                if (heat > 0.05f)
                {
                    Raylib.DrawCircleGradient((int)pos.X, (int)pos.Y, 15f,
                        new Color(255, 174, 68, (int)(162 * heat)), Color.Blank);
                }
            }

            // Crisp vintage white PCB silkscreen caption cleanly BELOW the pad (clear of bottom components)
            string label = Loc.T(PadLabels[i].Ru, PadLabels[i].En);
            Color labelColor = i == 2 && _phase == Phase.Overload
                ? new Color(255, 122, 98, 255)
                : (soldered || active ? BrassBright : SilkscreenIvory);
            DrawFittedCenteredText(uiFont, label, new Rectangle(pos.X - 102f, o.Y + 414f, 204f, 22f), 15, 12, labelColor, shadowed: true);
        }
    }

    // =========================================================================
    // 7. INTERACTIVE RIPPABLE HAZARD TAPE ("НЕ ТРОГАТЬ") ON PAD 2
    // =========================================================================
    private void DrawHazardTape(Vector2 o)
    {
        if (_tapeRipped && !_tapeAnimActive) return;

        float ripT = _tapeAnimActive ? Math.Clamp(_tapeRipAnim, 0f, 1f) : 0f;
        float alphaFactor = _tapeAnimActive ? Math.Clamp(1f - MathF.Pow(ripT, 2.4f), 0f, 1f) : 1f;
        if (alphaFactor <= 0.02f) return;

        // Subtle impatient corner flutter when Pad 2 is waiting to be peeled (or in preview showcase)
        bool eager = IsWaitingForTapeRip() || _previewShowcaseMode;
        float flutter = eager ? 0.55f + 0.45f * MathF.Sin(_elapsed * 5.2f) : 0.15f;

        // Parabolic upward-right fling + spin + lift toward camera when ripped off!
        Vector2 flingOffset = _tapeAnimActive
            ? new Vector2(215f * ripT, -165f * ripT + 195f * ripT * ripT)
            : Vector2.Zero;

        Vector2 tapeCenter = o + TapeRestCenter + flingOffset;
        float angleDeg = 22f + (eager ? MathF.Sin(_elapsed * 4.2f) * 1.1f : 0f) + ripT * 96f;
        float scale = 1f + (_tapeAnimActive ? 0.20f * MathF.Sin(ripT * MathF.PI) : 0f);

        const float w = 132f;
        const float h = 23f;
        const float halfW = w * 0.5f;
        const float halfH = h * 0.5f;

        int A(int a) => Math.Clamp((int)(a * alphaFactor), 0, 255);

        Rlgl.PushMatrix();
        Rlgl.Translatef(tapeCenter.X, tapeCenter.Y, 0f);
        Rlgl.Rotatef(angleDeg, 0f, 0f, 1f);
        Rlgl.Scalef(scale, scale, 1f);

        // Cast drop shadow underneath (grows wider as tape curls or lifts away!)
        float shadowDrop = 3.5f + flutter * 2.5f + ripT * 18f;
        Raylib.DrawRectangleRec(new(-halfW + 3f, -halfH + shadowDrop, w, h), new Color(8, 10, 9, A(142)));

        // Base worn hazard yellow masking-tape body
        Rectangle strip = new(-halfW, -halfH, w, h);
        Raylib.DrawRectangleRec(strip, new Color(HazardYellow.R, HazardYellow.G, HazardYellow.B, A(255)));

        // Serrated torn paper edges on left & right ends
        DrawSerratedTapeEnd(-halfW, -halfH, h, facingLeft: true, new Color(HazardYellow.R, HazardYellow.G, HazardYellow.B, A(255)));
        DrawSerratedTapeEnd(halfW, -halfH, h, facingLeft: false, new Color(HazardYellow.R, HazardYellow.G, HazardYellow.B, A(255)));

        // Diagonal matte-charcoal warning stripes
        Color stripeCol = new(HazardCharcoal.R, HazardCharcoal.G, HazardCharcoal.B, A(245));
        for (float sx = -halfW - 6f; sx < halfW + 8f; sx += 17f)
        {
            float x0 = Math.Clamp(sx, -halfW, halfW);
            float x1 = Math.Clamp(sx + 8f, -halfW, halfW);
            float x2 = Math.Clamp(sx + 1f, -halfW, halfW);
            float x3 = Math.Clamp(sx - 7f, -halfW, halfW);
            DrawQuadCCW(
                new(x0, -halfH),
                new(x1, -halfH),
                new(x2, halfH),
                new(x3, halfH),
                stripeCol);
        }

        // Solid worn yellow stencil field in center so "НЕ ТРОГАТЬ" reads crisply at any size
        Rectangle textPanel = new(-46f, -halfH + 2.5f, 92f, h - 5f);
        Raylib.DrawRectangleRec(textPanel, new Color(HazardYellow.R, HazardYellow.G, HazardYellow.B, A(255)));
        Raylib.DrawRectangleLinesEx(textPanel, 1f, new Color(HazardCharcoal.R, HazardCharcoal.G, HazardCharcoal.B, A(220)));

        // Subtle weathering grime along top/bottom adhesive edges
        Raylib.DrawRectangleRec(new(-halfW, -halfH, w, 2.5f), new Color(92, 68, 22, A(70)));
        Raylib.DrawRectangleRec(new(-halfW, halfH - 2.5f, w, 2.5f), new Color(72, 52, 16, A(82)));

        // Curled peeling dog-ear corner on upper-right end (more lifted when ready to rip!)
        float curl = 9f + flutter * 4.5f;
        DrawTriCCW(
            new(halfW - curl, -halfH),
            new(halfW, -halfH),
            new(halfW, -halfH + curl * 0.88f),
            new Color(246, 226, 146, A(255)));
        Raylib.DrawLineEx(
            new(halfW - curl, -halfH),
            new(halfW, -halfH + curl * 0.88f),
            1.1f, new Color(92, 74, 34, A(185)));

        // Crisp dark stenciled warning text
        string warnText = Loc.T("НЕ ТРОГАТЬ", "DO NOT TOUCH");
        Font wf = GameFonts.Get(Typeface.InterfaceBold, 11);
        Vector2 wsz = GameFonts.Measure(wf, warnText, 11);
        GameFonts.Draw(wf, warnText, new(-wsz.X * 0.5f, -wsz.Y * 0.5f), 11,
            new Color(HazardCharcoal.R, HazardCharcoal.G, HazardCharcoal.B, A(255)), shadowed: false);

        Rlgl.PopMatrix();
    }

    private static void DrawSerratedTapeEnd(float edgeX, float topY, float height, bool facingLeft, Color color)
    {
        const int teeth = 5;
        float step = height / teeth;
        float dir = facingLeft ? -1f : 1f;
        for (int i = 0; i < teeth; i++)
        {
            float y0 = topY + i * step;
            float y1 = y0 + step;
            float ym = (y0 + y1) * 0.5f;
            float tipLen = (i % 2 == 0) ? 4.2f : 2.6f;
            DrawTriCCW(new(edgeX, y0), new(edgeX, y1), new(edgeX + dir * tipLen, ym), color);
        }
    }

    // =========================================================================
    // 8. BOTTOM MACHINED-BRASS CALIBRATION TRACK (INSIDE PAINTED DARK GROOVE)
    // =========================================================================
    private void DrawCalibrationTrack(Vector2 o, Font uiFont)
    {
        Rectangle groove = OffsetRect(BrassGrooveRect, o);
        Rectangle slot = OffsetRect(TrackSlotRect, o);

        // Subtle etched brass graduation ticks recessed along the upper & lower inner groove rims
        for (int i = 0; i <= 32; i++)
        {
            float tx = slot.X + slot.Width * (i / 32f);
            bool major = i % 4 == 0;
            float th = major ? 6.0f : 3.5f;
            Color tickCol = major ? new Color(172, 140, 84, 138) : new Color(138, 112, 68, 92);
            Raylib.DrawLineEx(new(tx, groove.Y + 2f), new(tx, groove.Y + 2f + th), major ? 1.3f : 1f, tickCol);
            Raylib.DrawLineEx(new(tx, groove.Y + groove.Height - 2f - th), new(tx, groove.Y + groove.Height - 2f), major ? 1.3f : 1f, tickCol);
        }

        if (_phase == Phase.Overload)
        {
            float surge = 0.55f + 0.45f * MathF.Sin(_overloadTimer * 52f);
            Raylib.DrawRectangleRounded(slot, 0.16f, 4, new Color(224, (int)(54 + 68 * surge), 38, 232));
            Raylib.DrawRectangle((int)(slot.X + 8f), (int)(slot.Y + slot.Height * 0.5f - 2f),
                (int)(slot.Width - 16f), 4, new Color(255, 224, 172, (int)(180 + 70 * surge)));
            return;
        }

        // While Pad 2 is still sealed by hazard tape in live play: pulsing crimson tape-rip prompt inside groove
        if (IsWaitingForTapeRip())
        {
            float pulse = 0.55f + 0.45f * MathF.Sin(_elapsed * 7.2f);
            Raylib.DrawRectangleRounded(slot, 0.14f, 4, new Color(156, 32, 24, (int)(92 + 64 * pulse)));
            Raylib.DrawRectangleRoundedLinesEx(slot, 0.14f, 4, 1.5f, new Color(246, 138, 68, (int)(165 + 80 * pulse)));
            string ripPrompt = Loc.T("СОРВИ ЛЕНТУ «НЕ ТРОГАТЬ» — [ЛКМ] / [ПРОБЕЛ]",
                                     "RIP OFF \"DO NOT TOUCH\" TAPE — [LMB] / [SPACE]");
            DrawFittedCenteredText(uiFont, ripPrompt, slot, 15, 12, new Color(255, 228, 152, 255), shadowed: true);
            return;
        }

        int activePad = Math.Clamp(_solderedCount, 0, PadCount - 1);
        var timing = PadTiming[activePad];
        float sweetStart = timing.Center - timing.Width * 0.5f;
        float sweetEnd = timing.Center + timing.Width * 0.5f;
        bool inSweet = _phase == Phase.Soldering && _gaugePos >= sweetStart && _gaugePos <= sweetEnd && _missRecoveryTimer <= 0f;

        // Glowing molten-gold calibration target window inside dark groove
        float swX = slot.X + sweetStart * slot.Width;
        float swW = timing.Width * slot.Width;
        Rectangle sweetRect = new(swX, slot.Y + 4f, swW, slot.Height - 8f);

        // Soft warm amber spill inside dark track around the golden zone
        Raylib.DrawRectangleRounded(new(sweetRect.X - 6f, sweetRect.Y - 3f, sweetRect.Width + 12f, sweetRect.Height + 6f),
            0.22f, 4, new Color(246, 174, 48, inSweet ? 62 : 34));

        Color sweetTop = inSweet ? new Color(228, 178, 76, 205) : new Color(182, 134, 48, 172);
        Color sweetBot = inSweet ? new Color(156, 94, 24, 212) : new Color(102, 64, 18, 184);
        Raylib.DrawRectangleGradientV((int)sweetRect.X, (int)sweetRect.Y, (int)sweetRect.Width, (int)sweetRect.Height, sweetTop, sweetBot);
        Raylib.DrawLineEx(new(sweetRect.X + 2f, sweetRect.Y + 2f), new(sweetRect.X + sweetRect.Width - 2f, sweetRect.Y + 2f),
            1.2f, new Color(248, 224, 152, inSweet ? 148 : 88));
        Raylib.DrawRectangleLinesEx(sweetRect, 1.3f, inSweet ? new Color(236, 198, 118, 228) : new Color(168, 132, 68, 196));

        // Crisp machined brass calibration notches framing the golden window
        DrawTriCCW(new(swX - 4.5f, groove.Y + 1f), new(swX + 4.5f, groove.Y + 1f), new(swX, groove.Y + 8f), BrassBright);
        DrawTriCCW(new(swX + swW - 4.5f, groove.Y + 1f), new(swX + swW + 4.5f, groove.Y + 1f), new(swX + swW, groove.Y + 8f), BrassBright);
        DrawTriCCW(new(swX - 4.5f, groove.Y + groove.Height - 1f), new(swX + 4.5f, groove.Y + groove.Height - 1f), new(swX, groove.Y + groove.Height - 8f), BrassBright);
        DrawTriCCW(new(swX + swW - 4.5f, groove.Y + groove.Height - 1f), new(swX + swW + 4.5f, groove.Y + groove.Height - 1f), new(swX + swW, groove.Y + groove.Height - 8f), BrassBright);

        // Brief muted rust-red flash on miss recovery
        if (_missFlashTimer > 0f)
        {
            int ma = (int)(128 * (_missFlashTimer / 0.32f));
            Raylib.DrawRectangleRounded(slot, 0.16f, 4, new Color(196, 52, 34, ma));
        }

        // Tactile machined metallic + incandescent indicator slider cursor sweeping smoothly across groove
        float barX = slot.X + _gaugePos * slot.Width;
        float barW = 14f;
        Rectangle slider = new(barX - barW * 0.5f, groove.Y + 2f, barW, groove.Height - 4f);

        // Warm radiant glow behind slider cursor
        Raylib.DrawCircleGradient((int)barX, (int)(groove.Y + groove.Height * 0.5f), inSweet ? 36f : 24f,
            inSweet ? new Color(255, 214, 102, 145) : new Color(238, 128, 48, 92), Color.Blank);

        // Drop shadow inside groove
        Raylib.DrawRectangleRounded(new(slider.X + 2.5f, slider.Y + 2f, slider.Width, slider.Height), 0.28f, 4, new Color(6, 7, 8, 185));

        Color sliderBody = _missRecoveryTimer > 0f
            ? new Color(178, 62, 44, 255)
            : inSweet ? new Color(255, 226, 142, 255) : new Color(234, 134, 54, 255);
        Raylib.DrawRectangleRounded(slider, 0.28f, 4, sliderBody);
        Raylib.DrawRectangleRoundedLinesEx(slider, 0.28f, 4, 1.4f, EngravedBronzeInk);
        // Incandescent vertical hairline core
        Raylib.DrawRectangle((int)(barX - 2f), (int)(slider.Y + 4f), 4, (int)(slider.Height - 8f), new Color(255, 250, 226, 236));
    }

    // =========================================================================
    // 9. BOTTOM STEEL BEZEL CONTROL HINT
    // =========================================================================
    private void DrawBottomLegend(Vector2 o, Font uiFont)
    {
        Rectangle bar = OffsetRect(BottomLegendRect, o);
        string legend;
        Color ink;

        if (_phase == Phase.Overload)
        {
            legend = Loc.T("ЗАМЫКАНИЕ ШАХТЫ — КОНТУР 0.1 ПРОБИТ!", "SHAFT SURGE — CIRCUIT 0.1 BREACHED!");
            ink = new Color(255, 132, 110, 255);
        }
        else if (IsWaitingForTapeRip())
        {
            legend = Loc.T("[ЛКМ] / [ПРОБЕЛ] / [E] — СОРВАТЬ ЛЕНТУ      [ESC] — ЗАКРЫТЬ ЩИТ",
                           "[LMB] / [SPACE] / [E] — RIP OFF TAPE      [ESC] — CLOSE BOX");
            ink = new Color(246, 206, 134, 238);
        }
        else
        {
            legend = Loc.T("[ЛКМ] / [ПРОБЕЛ] / [E] — ПАЯТЬ КОНТАКТ      [ESC] — ЗАКРЫТЬ ЩИТ",
                           "[LMB] / [SPACE] / [E] — SOLDER JOINT      [ESC] — CLOSE BOX");
            ink = new Color(214, 204, 182, 232);
        }

        DrawFittedCenteredText(uiFont, legend, bar, 14, 11, ink, shadowed: true);
    }

    // =========================================================================
    // 10. HAND-PAINTED SOVIET ЭПСН-40 SOLDERING IRON SPRITE
    // =========================================================================
    private void DrawSolderingIron(Vector2 o)
    {
        Vector2 tip = o + GetIronTipPosition();
        float heatPulse = 0.74f + 0.26f * MathF.Sin(_elapsed * 9.5f);
        float plungeTilt = MathF.Sin(_ironPressAnim * MathF.PI) * 3.5f;
        float idleTilt = MathF.Sin(_elapsed * 2.4f) * 1.1f;
        float rotationDeg = IronBaseTiltDeg + plungeTilt + idleTilt;

        // Glowing molten-amber heat bloom around heated copper chisel tip
        Raylib.DrawCircleGradient((int)tip.X, (int)tip.Y, 34f,
            new Color(255, 124, 34, (int)(92 * heatPulse)), Color.Blank);
        Raylib.DrawCircleGradient((int)tip.X, (int)tip.Y, 15f,
            new Color(255, 212, 102, (int)(138 * heatPulse)), Color.Blank);

        if (_ironTex.Id != 0)
        {
            Rectangle src = new(0f, 0f, _ironTex.Width, _ironTex.Height);
            Vector2 pivot = new(IronDrawW * IronTipPivotNormX, IronDrawH * IronTipPivotNormY);

            // Soft layered realistic cast shadow offset (+10, +16) underneath the iron
            Rectangle shadowFar = new(tip.X + 12f, tip.Y + 19f, IronDrawW, IronDrawH);
            Raylib.DrawTexturePro(_ironTex, src, shadowFar, pivot, rotationDeg, new Color(6, 8, 9, 54));
            Rectangle shadowNear = new(tip.X + 10f, tip.Y + 16f, IronDrawW, IronDrawH);
            Raylib.DrawTexturePro(_ironTex, src, shadowNear, pivot, rotationDeg, new Color(6, 8, 9, 112));

            // Full hand-painted birchwood & blued-steel Soviet ЭПСН-40 sprite anchored on exact copper chisel tip
            Rectangle dest = new(tip.X, tip.Y, IronDrawW, IronDrawH);
            Raylib.DrawTexturePro(_ironTex, src, dest, pivot, rotationDeg, Color.White);
        }
        else
        {
            // Minimal safe fallback marker if running headless without GPU textures
            Raylib.DrawCircleV(tip, 6f, CopperLit);
        }
    }

    // =========================================================================
    // 11. ROSIN SMOKE WISPS (BEHIND HAZARD TAPE) & MOLTEN GOLDEN SPARKS
    // =========================================================================
    private void SeedPreviewParticles(Vector2 center, int sparkCount, int smokeCount)
    {
        var detRng = new Random(199801);
        for (int i = 0; i < sparkCount; i++)
        {
            // Fan crisp golden sparks upward-left & around lower-left rim so right-side "НЕ ТРОГАТЬ" tape stays crystal clear
            float angle = i % 4 == 0
                ? MathF.PI * 0.64f + (float)detRng.NextDouble() * MathF.PI * 0.40f
                : -MathF.PI * 1.08f + (float)detRng.NextDouble() * MathF.PI * 0.52f;
            float dist = 6f + (float)detRng.NextDouble() * 32f;
            float speed = 85f + (float)detRng.NextDouble() * 145f;
            Vector2 dir = new(MathF.Cos(angle), MathF.Sin(angle));
            _sparks.Add(new SparkParticle
            {
                Pos = center + dir * dist,
                Vel = dir * speed + new Vector2(-12f, -18f),
                Life = 0.24f + (float)detRng.NextDouble() * 0.28f,
                MaxLife = 0.55f,
                Size = 1.8f + (float)detRng.NextDouble() * 1.7f,
                IsMissSpark = false
            });
        }

        for (int i = 0; i < smokeCount; i++)
        {
            float t = (i + 0.5f) / smokeCount;
            float swirl = MathF.Sin(i * 1.3f) * 8f;
            _smoke.Add(new SmokeWisp
            {
                Pos = center + new Vector2(-12f + swirl - t * 28f, -12f - t * 58f),
                Vel = new Vector2(-16f, -32f),
                Life = (1f - t * 0.65f) * 1.1f,
                MaxLife = 1.2f,
                Radius = 6f + t * 11f,
                DriftPhase = i * 0.9f,
                Dark = false
            });
        }
    }

    private void SpawnSparks(Vector2 pos, int count, bool isMiss)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = -MathF.PI * 1.08f + _rng.NextSingle() * MathF.PI * 1.12f;
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
            s.Radius += 12.5f * dt;
            _smoke[i] = s;
        }
    }

    private void DrawSmokeWisps(Vector2 o)
    {
        foreach (var s in _smoke)
        {
            float t = Math.Clamp(s.Life / s.MaxLife, 0f, 1f);
            int alpha = (int)(MathF.Sin(t * MathF.PI) * (s.Dark ? 88f : 102f));
            Color col = s.Dark
                ? new Color(76, 72, 68, alpha)
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
    // HELPERS & GEOMETRY PRIMITIVES
    // =========================================================================
    private static Rectangle OffsetRect(Rectangle r, Vector2 o) => new(r.X + o.X, r.Y + o.Y, r.Width, r.Height);

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

    private static void DrawFittedCenteredText(Font font, string text, Rectangle rect, int maxSize, int minSize, Color color, bool shadowed)
    {
        int size = maxSize;
        Vector2 sz = GameFonts.Measure(font, text, size);
        while (size > minSize && sz.X > rect.Width - 8f)
        {
            size--;
            sz = GameFonts.Measure(font, text, size);
        }
        Vector2 pos = new(rect.X + (rect.Width - sz.X) * 0.5f, rect.Y + (rect.Height - sz.Y) * 0.5f);
        GameFonts.Draw(font, text, pos, size, color, shadowed);
    }

    private static void DrawEngravedFittedText(Font font, string text, Rectangle rect, int maxSize, int minSize, Color ink, Color embossHighlight)
    {
        int size = maxSize;
        Vector2 sz = GameFonts.Measure(font, text, size);
        while (size > minSize && sz.X > rect.Width - 10f)
        {
            size--;
            sz = GameFonts.Measure(font, text, size);
        }
        Vector2 pos = new(rect.X + (rect.Width - sz.X) * 0.5f, rect.Y + (rect.Height - sz.Y) * 0.5f);
        GameFonts.Draw(font, text, pos + new Vector2(0.8f, 1.0f), size, embossHighlight, shadowed: false);
        GameFonts.Draw(font, text, pos, size, ink, shadowed: false);
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
