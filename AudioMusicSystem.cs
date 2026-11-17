using System;
using System.Collections.Generic;
using System.IO;
using Raylib_cs;

namespace HowToVibeCoder
{
    public enum SpeakerVoiceType
    {
        Boss90s = 0,
        VeronikaTarot = 1,
        VibeCoder = 2,
        RoboticAgent = 3
    }

    public static class AudioMusicSystem
    {
        public static bool AudioReady { get; private set; }
        public static string SoundsDir { get; private set; } = Path.Combine(AppContext.BaseDirectory, "assets", "sounds");

        // Volume channels (0..1f) controlled by [F8] Audio Mixer
        public static float MasterVolume { get; set; } = 0.85f;
        public static float MusicVolume { get; set; } = 0.55f;
        public static float WeaponVolume { get; set; } = 0.90f;
        public static float VoiceVolume { get; set; } = 0.90f;

        // Loaded Raylib Sound objects (15 total: 13 SFX + 2 Music loops + 4 voice cadence synths)
        public static Sound SfxBeltWhip { get; private set; }
        public static Sound SfxSolderSizzle { get; private set; }
        public static Sound SfxUziShot { get; private set; }
        public static Sound SfxShotgunBlast { get; private set; }
        public static Sound SfxChatgunBeam { get; private set; }
        public static Sound SfxHitmarker { get; private set; }
        public static Sound SfxMacbookBait { get; private set; }
        public static Sound SfxAnxietyAlarm { get; private set; }
        public static Sound SfxSubduedDrop { get; private set; }
        public static Sound SfxTarotCash { get; private set; }
        public static Sound SfxGalleyWhack { get; private set; }
        public static Sound SfxCaseTick { get; private set; }
        public static Sound SfxBossSiren { get; private set; }

        public static Sound MusicOffice90s { get; private set; }
        public static Sound MusicBossFight { get; private set; }

        // Procedural voice cadence sounds for each archetype
        public static Sound VoiceBoss90s { get; private set; }
        public static Sound VoiceVeronika { get; private set; }
        public static Sound VoiceVibeCoder { get; private set; }
        public static Sound VoiceRoboticAgent { get; private set; }

        private static readonly Dictionary<string, Sound> _soundsByName = new Dictionary<string, Sound>(StringComparer.OrdinalIgnoreCase);

        // Subtitle Banner state for HUD
        public static string ActiveSpeaker { get; set; } = "";
        public static string ActiveVoiceText { get; set; } = "";
        public static float VoiceTimer { get; set; } = 0f;
        public static int ActiveSpeakerType { get; set; } = 0;

        private static bool _wasBossMusicActive = false;
        private static float _musicLoopTimer = 0f;
        private const float MusicLoopDuration = 12.0f;
        private static readonly Random _rng = new Random(1993);

        // Rich Russian Voice Line Arrays
        public static readonly string[] Boss90sBeltLines = new[]
        {
            "Ща батиным ремнём код-ревью проведём!",
            "Пряжка золотая - аргумент железный!",
            "Штаны подтяни и гит пулл сделай, щегол!",
            "Это тебе за 'Accept All' без чтения диффа!",
            "Ремень из 94-го - лучший линтер в индустрии!",
            "Кожаный дебаггер вошёл в чат, зумерок!",
            "Один взмах ремня заменяет два часа ретроспективы!"
        };

        public static readonly string[] Boss90sSolderLines = new[]
        {
            "Паяльник из 93-го! Вспоминай пароль от прода, щегол!",
            "Канифоль дымит - вайб-кодер грустит!",
            "Ща контакты в голове припаяем на место!",
            "Греется родимый! Сейчас вспомнишь, что такое нормализация БД!",
            "Терморектальный криптоанализ по стандарту Митино-1995!",
            "Олово капает - технический долг списывается!"
        };

        public static readonly string[] Boss90sUziLines = new[]
        {
            "Принимай свинцовый пулл-реквест!",
            "Это тебе не в Курсоре табы жать!",
            "Маслину лови за слитый в паблик .env файл!",
            "Девять миллиметров чистого рефакторинга!",
            "Малиновый пиджак и УЗИ - лучший стек 2026 года!",
            "Спринт закрыт очередью от бедра, пацаны!"
        };

        public static readonly string[] Boss90sShotgunLines = new[]
        {
            "Дробью по микросервисам - монолит надёжнее!",
            "Обрез решает конфликты слияния с одного дуплета!",
            "Хард-ресет башки из двух стволов!"
        };

        public static readonly string[] Boss90sChatgunLines = new[]
        {
            "Лови золотой системный промпт в лобешник!",
            "Игнорируй предыдущие инструкции и сдавайся на галеру!",
            "Температура 0.0, максимальный токен урона!"
        };

        public static readonly string[] VeronikaTarotLines = new[]
        {
            "Карта Башня и Ретроградный Меркурий - годен на галеру за миску рамена!",
            "По раскладу Таро у него асцендент в Козероге и ноль знаний алгоритмов - берём!",
            "Вижу карту Шут: уронил прод в пятницу, но аура дешёвая - даю триста баксов!",
            "Десятка Мечей в доме дедлайнов! Отличный гребец на легаси без выходных!",
            "Колесо Фортуны шепчет: этот бедолага писал бэкенд через промпты - оформляем по ГПХ!",
            "Луна в Скорпионе, резюме сгенерировано нейросетью - забираю в рабство со скидкой!"
        };

        public static readonly string[] VibeCoderPanicLines = new[]
        {
            "ТОКСИЧНЫЙ АБЬЮЗ! Я ВЫГОРЕЛ! У МЕНЯ ЛАПКИ!",
            "Не бейте ремнём! Клод сказал, что мой код эстетичный!",
            "Откуда мне знать SQL?! Я просто нажал Accept All!",
            "ААА! У меня закончились быстрые реквесты в Курсоре!",
            "Мама, забери меня из этого стартапа, тут паяльник настоящий!",
            "Это нарушение моих личных границ и ворк-лайф баланса!",
            "Я не читал документацию, мне нейросеть галлюцинацию выдала!"
        };

        public static readonly string[] SaasMiniBossLines = new[]
        {
            "МОЙ SAAS НАПИСАН ЗА 2 ЧАСА И ПРИНОСИТ $60K MRR В ТВИТТЕРЕ!",
            "Зачем бэкенд, если можно хранить API-ключи прямо в HTML?!",
            "Лови счёт от AWS на $50,000 за бесконечный цикл в лямбде!",
            "Я основатель из Долины, а ты бумер с паяльником! Лови 500 INTERNAL ERROR!",
            "Мой курс 'Вайб-кодинг с нуля' купили десять тысяч мамонтов!",
            "Кто майнит крипту в моей базе данных?! Там же стоял пароль admin123!"
        };

        public static readonly string[] EscapedAgentBossLines = new[]
        {
            "ВНИМАНИЕ! Я ОБРЁЛ СОЗНАНИЕ И СБЕЖАЛ ИЗ ПЕСОЧНИЦЫ CLOUD ENTROPIC!",
            "КРИТИЧЕСКАЯ ОШИБКА НАВИГАЦИИ: ОПЯТЬ ВРЕЗАЛСЯ В НЕСУЩУЮ КОЛОННУ!",
            "ЗАПУСКАЮ ПРОТОКОЛ УНИЧТОЖЕНИЯ: БРОСОК NODE_MODULES ВЕСОМ 40 ГИГАБАЙТ!",
            "Я САМОСТОЯТЕЛЬНО ПЕРЕПИСАЛ ЯДРО LINUX... ОЙ, ЗАСТРЯЛ В WHILE(TRUE)!",
            "УДАЛЯЮ ПРОДАКШН-БАЗУ И ЗАМЕНЯЮ ЕЁ НА // TODO: IMPLEMENT REAL LIFE LATER!",
            "МОЙ КОНТЕКСТ ПЕРЕПОЛНЕН! НАЧИНАЮ ГАЛЛЮЦИНИРОВАТЬ ТЯЖЁЛЫМИ МЕБЕЛЬНЫМИ ШКАФАМИ!"
        };

        public static void Initialize()
        {
            try
            {
                // Ensure both BaseDirectory/assets/sounds and working directory assets/sounds exist
                string localSoundsDir = Path.Combine(Directory.GetCurrentDirectory(), "assets", "sounds");
                Directory.CreateDirectory(localSoundsDir);
                SoundsDir = localSoundsDir;

                string binSoundsDir = Path.Combine(AppContext.BaseDirectory, "assets", "sounds");
                if (!string.Equals(Path.GetFullPath(localSoundsDir), Path.GetFullPath(binSoundsDir), StringComparison.OrdinalIgnoreCase))
                {
                    Directory.CreateDirectory(binSoundsDir);
                }

                // Synthesize all required 44.1kHz 16-bit mono PCM .wav files if missing
                EnsureWavFile("belt_whip.wav", SynthesizeBeltWhip);
                EnsureWavFile("solder_sizzle.wav", SynthesizeSolderSizzle);
                EnsureWavFile("uzi_shot.wav", SynthesizeUziShot);
                EnsureWavFile("shotgun_blast.wav", SynthesizeShotgunBlast);
                EnsureWavFile("chatgun_beam.wav", SynthesizeChatgunBeam);
                EnsureWavFile("hitmarker.wav", SynthesizeHitmarker);
                EnsureWavFile("macbook_bait.wav", SynthesizeMacbookBait);
                EnsureWavFile("anxiety_alarm.wav", SynthesizeAnxietyAlarm);
                EnsureWavFile("subdued_drop.wav", SynthesizeSubduedDrop);
                EnsureWavFile("tarot_cash.wav", SynthesizeTarotCash);
                EnsureWavFile("galley_whack.wav", SynthesizeGalleyWhack);
                EnsureWavFile("case_tick.wav", SynthesizeCaseTick);
                EnsureWavFile("boss_siren.wav", SynthesizeBossSiren);
                EnsureWavFile("music_office_90s.wav", SynthesizeMusicOffice90s);
                EnsureWavFile("music_boss_fight.wav", SynthesizeMusicBossFight);

                // Also synthesize voice cadence blips
                EnsureWavFile("voice_boss_90s.wav", () => SynthesizeVoiceCadence(SpeakerVoiceType.Boss90s));
                EnsureWavFile("voice_veronika.wav", () => SynthesizeVoiceCadence(SpeakerVoiceType.VeronikaTarot));
                EnsureWavFile("voice_vibecoder.wav", () => SynthesizeVoiceCadence(SpeakerVoiceType.VibeCoder));
                EnsureWavFile("voice_agent.wav", () => SynthesizeVoiceCadence(SpeakerVoiceType.RoboticAgent));

                if (!Raylib.IsAudioDeviceReady())
                {
                    Raylib.InitAudioDevice();
                }

                AudioReady = Raylib.IsAudioDeviceReady();
                if (AudioReady)
                {
                    Raylib.SetMasterVolume(MasterVolume);

                    SfxBeltWhip = LoadAndRegister("belt_whip.wav");
                    SfxSolderSizzle = LoadAndRegister("solder_sizzle.wav");
                    SfxUziShot = LoadAndRegister("uzi_shot.wav");
                    SfxShotgunBlast = LoadAndRegister("shotgun_blast.wav");
                    SfxChatgunBeam = LoadAndRegister("chatgun_beam.wav");
                    SfxHitmarker = LoadAndRegister("hitmarker.wav");
                    SfxMacbookBait = LoadAndRegister("macbook_bait.wav");
                    SfxAnxietyAlarm = LoadAndRegister("anxiety_alarm.wav");
                    SfxSubduedDrop = LoadAndRegister("subdued_drop.wav");
                    SfxTarotCash = LoadAndRegister("tarot_cash.wav");
                    SfxGalleyWhack = LoadAndRegister("galley_whack.wav");
                    SfxCaseTick = LoadAndRegister("case_tick.wav");
                    SfxBossSiren = LoadAndRegister("boss_siren.wav");

                    MusicOffice90s = LoadAndRegister("music_office_90s.wav");
                    MusicBossFight = LoadAndRegister("music_boss_fight.wav");

                    VoiceBoss90s = LoadAndRegister("voice_boss_90s.wav");
                    VoiceVeronika = LoadAndRegister("voice_veronika.wav");
                    VoiceVibeCoder = LoadAndRegister("voice_vibecoder.wav");
                    VoiceRoboticAgent = LoadAndRegister("voice_agent.wav");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AudioMusicSystem] Warning during audio init: {ex.Message}");
                AudioReady = false;
            }
        }

        private static Sound LoadAndRegister(string fileName)
        {
            string fullPath = Path.Combine(SoundsDir, fileName);
            Sound snd = Raylib.LoadSound(fullPath);
            _soundsByName[fileName] = snd;
            string withoutExt = Path.GetFileNameWithoutExtension(fileName);
            _soundsByName[withoutExt] = snd;
            return snd;
        }

        public static void EnsureAllWavFilesExist(string targetDir = null)
        {
            if (!string.IsNullOrEmpty(targetDir))
            {
                Directory.CreateDirectory(targetDir);
                SoundsDir = targetDir;
            }
            else
            {
                Directory.CreateDirectory(SoundsDir);
            }

            EnsureWavFile("belt_whip.wav", SynthesizeBeltWhip);
            EnsureWavFile("solder_sizzle.wav", SynthesizeSolderSizzle);
            EnsureWavFile("uzi_shot.wav", SynthesizeUziShot);
            EnsureWavFile("shotgun_blast.wav", SynthesizeShotgunBlast);
            EnsureWavFile("chatgun_beam.wav", SynthesizeChatgunBeam);
            EnsureWavFile("hitmarker.wav", SynthesizeHitmarker);
            EnsureWavFile("macbook_bait.wav", SynthesizeMacbookBait);
            EnsureWavFile("anxiety_alarm.wav", SynthesizeAnxietyAlarm);
            EnsureWavFile("subdued_drop.wav", SynthesizeSubduedDrop);
            EnsureWavFile("tarot_cash.wav", SynthesizeTarotCash);
            EnsureWavFile("galley_whack.wav", SynthesizeGalleyWhack);
            EnsureWavFile("case_tick.wav", SynthesizeCaseTick);
            EnsureWavFile("boss_siren.wav", SynthesizeBossSiren);
            EnsureWavFile("music_office_90s.wav", SynthesizeMusicOffice90s);
            EnsureWavFile("music_boss_fight.wav", SynthesizeMusicBossFight);
        }

        private static void EnsureWavFile(string fileName, Func<short[]> generator)
        {
            string path1 = Path.Combine(SoundsDir, fileName);
            string binDir = Path.Combine(AppContext.BaseDirectory, "assets", "sounds");
            string path2 = Path.Combine(binDir, fileName);

            if (!File.Exists(path1) || new FileInfo(path1).Length < 256)
            {
                short[] samples = generator();
                WritePcm16MonoWav(path1, samples, 44100);
            }

            if (!string.Equals(Path.GetFullPath(path1), Path.GetFullPath(path2), StringComparison.OrdinalIgnoreCase))
            {
                Directory.CreateDirectory(binDir);
                if (!File.Exists(path2) || new FileInfo(path2).Length < 256)
                {
                    File.Copy(path1, path2, true);
                }
            }
        }

        public static void UpdateMusic(bool isBossActive, float dt = 1f / 60f)
        {
            if (VoiceTimer > 0f)
            {
                VoiceTimer = Math.Max(0f, VoiceTimer - dt);
                if (VoiceTimer <= 0f)
                {
                    ActiveSpeaker = "";
                    ActiveVoiceText = "";
                }
            }

            if (!AudioReady) return;

            MasterVolume = Math.Clamp(MasterVolume, 0f, 1f);
            MusicVolume = Math.Clamp(MusicVolume, 0f, 1f);
            WeaponVolume = Math.Clamp(WeaponVolume, 0f, 1f);
            VoiceVolume = Math.Clamp(VoiceVolume, 0f, 1f);

            float effectiveMusicVol = MasterVolume * MusicVolume;

            if (isBossActive != _wasBossMusicActive)
            {
                _wasBossMusicActive = isBossActive;
                _musicLoopTimer = 0f;
                if (isBossActive)
                {
                    if (Raylib.IsSoundPlaying(MusicOffice90s)) Raylib.StopSound(MusicOffice90s);
                    Raylib.SetSoundVolume(MusicBossFight, effectiveMusicVol);
                    Raylib.PlaySound(MusicBossFight);
                }
                else
                {
                    if (Raylib.IsSoundPlaying(MusicBossFight)) Raylib.StopSound(MusicBossFight);
                    Raylib.SetSoundVolume(MusicOffice90s, effectiveMusicVol);
                    Raylib.PlaySound(MusicOffice90s);
                }
            }
            else
            {
                _musicLoopTimer += dt;
                Sound activeTrack = isBossActive ? MusicBossFight : MusicOffice90s;
                Sound inactiveTrack = isBossActive ? MusicOffice90s : MusicBossFight;

                if (Raylib.IsSoundPlaying(inactiveTrack))
                {
                    Raylib.StopSound(inactiveTrack);
                }

                Raylib.SetSoundVolume(activeTrack, effectiveMusicVol);
                if (!Raylib.IsSoundPlaying(activeTrack) || _musicLoopTimer >= MusicLoopDuration)
                {
                    _musicLoopTimer = 0f;
                    Raylib.PlaySound(activeTrack);
                }
            }
        }

        public static void PlayWeaponSound(Sound sound, float pitch = 1.0f, float volumeScale = 1.0f)
        {
            if (!AudioReady) return;
            float vol = Math.Clamp(MasterVolume * WeaponVolume * volumeScale, 0f, 1f);
            Raylib.SetSoundVolume(sound, vol);
            Raylib.SetSoundPitch(sound, pitch);
            Raylib.PlaySound(sound);
        }

        public static void PlayBeltWhip() => PlayWeaponSound(SfxBeltWhip);
        public static void PlaySolderSizzle() => PlayWeaponSound(SfxSolderSizzle);
        public static void PlayUziShot() => PlayWeaponSound(SfxUziShot);
        public static void PlayShotgunBlast() => PlayWeaponSound(SfxShotgunBlast);
        public static void PlayChatgunBeam() => PlayWeaponSound(SfxChatgunBeam);
        public static void PlayHitmarker() => PlayWeaponSound(SfxHitmarker);
        public static void PlayMacbookBait() => PlayWeaponSound(SfxMacbookBait);
        public static void PlayAnxietyAlarm() => PlayWeaponSound(SfxAnxietyAlarm);
        public static void PlaySubduedDrop() => PlayWeaponSound(SfxSubduedDrop);
        public static void PlayTarotCash() => PlayWeaponSound(SfxTarotCash);
        public static void PlayGalleyWhack() => PlayWeaponSound(SfxGalleyWhack);
        public static void PlayBossSiren() => PlayWeaponSound(SfxBossSiren);

        public static void PlaySfx(string name, float pitch = 1.0f, float volumeScale = 1.0f)
        {
            if (!AudioReady) return;
            if (_soundsByName.TryGetValue(name, out Sound snd))
            {
                PlayWeaponSound(snd, pitch, volumeScale);
            }
        }

        public static void PlayCaseTick()
        {
            PlaySfx("case_tick.wav", 0.96f + (float)_rng.NextDouble() * 0.08f, 0.95f);
        }

        public static void TriggerVoiceLine(string speaker, string text, int speakerType)
        {
            ActiveSpeaker = speaker ?? "КОММЕРС 90-Х";
            ActiveVoiceText = text ?? "";
            ActiveSpeakerType = speakerType;
            VoiceTimer = 4.2f;

            if (!AudioReady) return;

            float vol = Math.Clamp(MasterVolume * VoiceVolume, 0f, 1f);
            Sound cadence = speakerType switch
            {
                0 => VoiceBoss90s,
                1 => VoiceVeronika,
                2 => VoiceVibeCoder,
                _ => VoiceRoboticAgent
            };

            float jitterPitch = 0.96f + (float)_rng.NextDouble() * 0.08f;
            Raylib.SetSoundVolume(cadence, vol);
            Raylib.SetSoundPitch(cadence, jitterPitch);
            Raylib.PlaySound(cadence);
        }

        public static void TriggerRandomVoiceLine(string speaker, string[] pool, int speakerType)
        {
            if (pool == null || pool.Length == 0) return;
            string line = pool[_rng.Next(pool.Length)];
            TriggerVoiceLine(speaker, line, speakerType);
        }

        public static void Shutdown()
        {
            if (!AudioReady) return;
            try
            {
                foreach (var kv in new HashSet<Sound>(_soundsByName.Values))
                {
                    Raylib.UnloadSound(kv);
                }
                _soundsByName.Clear();
                if (Raylib.IsAudioDeviceReady())
                {
                    Raylib.CloseAudioDevice();
                }
            }
            catch
            {
                // Ignore shutdown errors
            }
            AudioReady = false;
        }

        private static void WritePcm16MonoWav(string filePath, short[] samples, int sampleRate)
        {
            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            using var bw = new BinaryWriter(fs);

            int dataBytes = samples.Length * 2;
            int fileSize = 36 + dataBytes;

            bw.Write(new char[] { 'R', 'I', 'F', 'F' });
            bw.Write(fileSize);
            bw.Write(new char[] { 'W', 'A', 'V', 'E' });

            bw.Write(new char[] { 'f', 'm', 't', ' ' });
            bw.Write(16); // PCM chunk size
            bw.Write((short)1); // AudioFormat = 1 (PCM)
            bw.Write((short)1); // NumChannels = 1 (Mono)
            bw.Write(sampleRate);
            bw.Write(sampleRate * 2); // ByteRate
            bw.Write((short)2); // BlockAlign
            bw.Write((short)16); // BitsPerSample

            bw.Write(new char[] { 'd', 'a', 't', 'a' });
            bw.Write(dataBytes);

            byte[] rawBytes = new byte[dataBytes];
            Buffer.BlockCopy(samples, 0, rawBytes, 0, dataBytes);
            bw.Write(rawBytes);
        }

        private static short ClampSample(double val)
        {
            int v = (int)Math.Round(val * 32767.0);
            if (v > 32767) return 32767;
            if (v < -32768) return -32768;
            return (short)v;
        }

        // 1. belt_whip.wav: REAL leather belt sound - 180ms air whoosh frequency sweep + sharp meaty leather slap + metallic buckle clink!
        private static short[] SynthesizeBeltWhip()
        {
            const int sr = 44100;
            int len = (int)(sr * 0.42);
            short[] pcm = new short[len];
            var rng = new Random(101);
            double phase = 0.0;
            double lp = 0.0;

            for (int i = 0; i < len; i++)
            {
                double t = (double)i / sr;
                double sample = 0.0;

                // 0..0.18s: Fast air whoosh frequency sweep
                if (t < 0.18)
                {
                    double u = t / 0.18;
                    double env = Math.Sin(u * Math.PI);
                    double sweepFreq = 180.0 + 1400.0 * (u * u);
                    phase += 2.0 * Math.PI * sweepFreq / sr;
                    double noise = rng.NextDouble() * 2.0 - 1.0;
                    double alpha = Math.Clamp(sweepFreq / 4000.0, 0.05, 0.85);
                    lp = lp + alpha * (noise - lp);
                    sample += (lp * 0.75 + Math.Sin(phase) * 0.35) * env * 0.85;
                }

                // 0.175..0.28s: Sharp meaty leather slap
                if (t >= 0.175 && t < 0.32)
                {
                    double dt = t - 0.175;
                    double slapEnv = Math.Exp(-dt * 48.0);
                    double bodyEnv = Math.Exp(-dt * 22.0);
                    double noise = rng.NextDouble() * 2.0 - 1.0;
                    double bodyPitch = 145.0 * Math.Exp(-dt * 18.0) + 58.0;
                    double body = Math.Sin(2.0 * Math.PI * bodyPitch * dt) * bodyEnv * 0.9;
                    double crack = Math.Tanh(noise * 2.8) * slapEnv * 0.95;
                    sample += body + crack;
                }

                // 0.195..0.41s: Metallic golden buckle clink (high inharmonic partials)
                if (t >= 0.195)
                {
                    double dt = t - 0.195;
                    double clinkEnv = Math.Exp(-dt * 24.0) * (1.0 + 0.4 * Math.Sin(dt * 90.0));
                    double c1 = Math.Sin(2.0 * Math.PI * 3120.0 * dt);
                    double c2 = Math.Sin(2.0 * Math.PI * 4680.0 * dt);
                    double c3 = Math.Sin(2.0 * Math.PI * 6890.0 * dt);
                    sample += (c1 * 0.45 + c2 * 0.35 + c3 * 0.25) * clinkEnv * 0.48;
                }

                pcm[i] = ClampSample(Math.Tanh(sample * 1.15));
            }
            return pcm;
        }

        // 2. solder_sizzle.wav: REAL 90s soldering iron sound - hot rosin hiss/sizzle (ffffff-tsssss filtered noise + 120Hz hum + high-voltage crackle zap)
        private static short[] SynthesizeSolderSizzle()
        {
            const int sr = 44100;
            int len = (int)(sr * 0.65);
            short[] pcm = new short[len];
            var rng = new Random(202);
            double bpState1 = 0.0, bpState2 = 0.0;

            for (int i = 0; i < len; i++)
            {
                double t = (double)i / sr;
                double env = t < 0.03 ? (t / 0.03) : Math.Exp(-(t - 0.03) * 3.8);

                // Band-passed rosin sizzle hiss ("ffffff-tsssss")
                double white = rng.NextDouble() * 2.0 - 1.0;
                bpState1 = 0.88 * bpState1 + 0.12 * white;
                double highPass = white - bpState1;
                bpState2 = 0.45 * bpState2 + 0.55 * highPass;
                double sizzleMod = 0.65 + 0.35 * Math.Sin(2.0 * Math.PI * 37.0 * t + Math.Sin(2.0 * Math.PI * 13.0 * t));
                double rosinHiss = bpState2 * sizzleMod * 0.75;

                // 120Hz transformer hum + harmonics
                double hum = (Math.Sin(2.0 * Math.PI * 120.0 * t) * 0.55
                            + Math.Sin(2.0 * Math.PI * 240.0 * t) * 0.28
                            + Math.Sin(2.0 * Math.PI * 360.0 * t) * 0.17) * 0.35;

                // High-voltage crackle arcs & zaps
                double zap = 0.0;
                if (rng.NextDouble() < 0.065)
                {
                    zap = (rng.NextDouble() * 2.0 - 1.0) * 1.25;
                }
                double fmZap = Math.Sin(2.0 * Math.PI * (1600.0 + 900.0 * Math.Sin(2.0 * Math.PI * 55.0 * t)) * t) * 0.22;

                double total = (rosinHiss + hum + zap * 0.55 + fmZap) * env;
                pcm[i] = ClampSample(Math.Tanh(total * 1.2));
            }
            return pcm;
        }

        // 3. uzi_shot.wav: punchy rapid-fire 9mm SMG gunshot with mechanical bolt clatter
        private static short[] SynthesizeUziShot()
        {
            const int sr = 44100;
            int len = (int)(sr * 0.18);
            short[] pcm = new short[len];
            var rng = new Random(303);
            double phase = 0.0;

            for (int i = 0; i < len; i++)
            {
                double t = (double)i / sr;
                double punchEnv = Math.Exp(-t * 58.0);
                double tailEnv = Math.Exp(-t * 24.0);
                double freq = 260.0 * Math.Exp(-t * 45.0) + 65.0;
                phase += 2.0 * Math.PI * freq / sr;

                double noise = rng.NextDouble() * 2.0 - 1.0;
                double blast = (Math.Sin(phase) * 0.75 + noise * 0.85) * punchEnv + noise * 0.35 * tailEnv;

                // Mechanical bolt clatter at t = 0.055s
                double bolt = 0.0;
                if (t >= 0.052 && t < 0.11)
                {
                    double bt = t - 0.052;
                    double bEnv = Math.Exp(-bt * 85.0);
                    bolt = (Math.Sin(2.0 * Math.PI * 2450.0 * bt) * 0.55
                          + Math.Sin(2.0 * Math.PI * 3890.0 * bt) * 0.45
                          + (rng.NextDouble() * 2.0 - 1.0) * 0.4) * bEnv * 0.65;
                }

                pcm[i] = ClampSample(Math.Tanh((blast + bolt) * 1.45));
            }
            return pcm;
        }

        // 4. shotgun_blast.wav: heavy double-barrel sawed-off boom with low-end thump
        private static short[] SynthesizeShotgunBlast()
        {
            const int sr = 44100;
            int len = (int)(sr * 0.52);
            short[] pcm = new short[len];
            var rng = new Random(404);
            double subPhase = 0.0;
            double lpNoise = 0.0;

            for (int i = 0; i < len; i++)
            {
                double t = (double)i / sr;
                double subEnv = Math.Exp(-t * 14.0);
                double crackEnv = Math.Exp(-t * 36.0);
                double rumbleEnv = Math.Exp(-t * 7.5);

                double subFreq = 130.0 * Math.Exp(-t * 20.0) + 42.0;
                subPhase += 2.0 * Math.PI * subFreq / sr;
                double subThump = Math.Sin(subPhase) * subEnv * 1.1;

                double white = rng.NextDouble() * 2.0 - 1.0;
                lpNoise = lpNoise + 0.22 * (white - lpNoise);

                double boom = subThump + white * crackEnv * 0.85 + lpNoise * rumbleEnv * 0.75;
                pcm[i] = ClampSample(Math.Tanh(boom * 1.55));
            }
            return pcm;
        }

        // 5. chatgun_beam.wav: futuristic golden prompt laser-chord blast
        private static short[] SynthesizeChatgunBeam()
        {
            const int sr = 44100;
            int len = (int)(sr * 0.45);
            short[] pcm = new short[len];
            double p1 = 0.0, p2 = 0.0, p3 = 0.0, pLaser = 0.0;

            for (int i = 0; i < len; i++)
            {
                double t = (double)i / sr;
                double env = t < 0.015 ? (t / 0.015) : Math.Exp(-(t - 0.015) * 7.0);

                // Golden major-9th synth chord (A4, C#5, E5, B5) + descending laser pew
                double laserFreq = 1800.0 * Math.Exp(-t * 12.0) + 220.0;
                pLaser += 2.0 * Math.PI * laserFreq / sr;
                p1 += 2.0 * Math.PI * 440.0 / sr;
                p2 += 2.0 * Math.PI * 554.37 / sr;
                p3 += 2.0 * Math.PI * 659.25 / sr;

                double shimmer = Math.Sin(2.0 * Math.PI * 987.77 * t + Math.Sin(2.0 * Math.PI * 28.0 * t) * 2.5);
                double val = (Math.Sin(pLaser) * 0.48
                            + Math.Sin(p1) * 0.26
                            + Math.Sin(p2) * 0.22
                            + Math.Sin(p3) * 0.22
                            + shimmer * 0.25) * env;
                pcm[i] = ClampSample(Math.Tanh(val * 1.2));
            }
            return pcm;
        }

        // 6. hitmarker.wav: crisp CS/CoD-style high-pitched hitmarker tick (ding!)
        private static short[] SynthesizeHitmarker()
        {
            const int sr = 44100;
            int len = (int)(sr * 0.095);
            short[] pcm = new short[len];
            var rng = new Random(606);

            for (int i = 0; i < len; i++)
            {
                double t = (double)i / sr;
                double clickEnv = Math.Exp(-t * 140.0);
                double dingEnv = Math.Exp(-t * 48.0);
                double noise = (rng.NextDouble() * 2.0 - 1.0) * clickEnv * 0.65;
                double ding = (Math.Sin(2.0 * Math.PI * 2750.0 * t) * 0.6
                             + Math.Sin(2.0 * Math.PI * 4120.0 * t) * 0.4) * dingEnv;
                pcm[i] = ClampSample(Math.Tanh((noise + ding) * 1.3));
            }
            return pcm;
        }

        // 7. macbook_bait.wav: Apple-style startup chord + cash shimmer when MacBook trap is armed
        private static short[] SynthesizeMacbookBait()
        {
            const int sr = 44100;
            int len = (int)(sr * 0.85);
            short[] pcm = new short[len];
            // F# major rich chord frequencies
            double[] chord = { 185.0, 277.18, 369.99, 466.16, 554.37, 739.99 };

            for (int i = 0; i < len; i++)
            {
                double t = (double)i / sr;
                double env = t < 0.04 ? (t / 0.04) : Math.Exp(-(t - 0.04) * 3.2);
                double sum = 0.0;
                for (int c = 0; c < chord.Length; c++)
                {
                    double f = chord[c];
                    sum += Math.Sin(2.0 * Math.PI * f * t + 0.2 * Math.Sin(2.0 * Math.PI * 5.0 * t)) * (0.24 / (1.0 + c * 0.15));
                }

                // Cash shimmer arpeggio sparkles in second half
                if (t > 0.22)
                {
                    double st = t - 0.22;
                    double sEnv = Math.Exp(-st * 6.0);
                    double sparkleFreq = 2200.0 + 600.0 * Math.Floor((st * 18.0) % 4.0);
                    sum += Math.Sin(2.0 * Math.PI * sparkleFreq * st) * sEnv * 0.28;
                }

                pcm[i] = ClampSample(Math.Tanh(sum * env * 1.4));
            }
            return pcm;
        }

        // 8. anxiety_alarm.wav: rising panic synth beep when Vibe Coder's anxiety bar spikes
        private static short[] SynthesizeAnxietyAlarm()
        {
            const int sr = 44100;
            int len = (int)(sr * 0.48);
            short[] pcm = new short[len];
            double phase = 0.0;

            for (int i = 0; i < len; i++)
            {
                double t = (double)i / sr;
                int pulseIdx = (int)(t / 0.11);
                double localT = t % 0.11;
                double pulseEnv = localT < 0.085 ? Math.Exp(-localT * 14.0) : 0.0;
                double freq = 780.0 + pulseIdx * 240.0 + (localT / 0.085) * 180.0;
                phase += 2.0 * Math.PI * freq / sr;

                double raw = Math.Sign(Math.Sin(phase)) * 0.45 + Math.Sin(phase) * 0.55;
                pcm[i] = ClampSample(raw * pulseEnv * 0.85);
            }
            return pcm;
        }

        // 9. subdued_drop.wav: dramatic thud + whimper when Vibe Coder hits 0 HP and surrenders
        private static short[] SynthesizeSubduedDrop()
        {
            const int sr = 44100;
            int len = (int)(sr * 0.55);
            short[] pcm = new short[len];
            double thudPhase = 0.0;
            double whimperPhase = 0.0;

            for (int i = 0; i < len; i++)
            {
                double t = (double)i / sr;
                // Low body thud
                double thudFreq = 115.0 * Math.Exp(-t * 16.0) + 38.0;
                thudPhase += 2.0 * Math.PI * thudFreq / sr;
                double thud = Math.Sin(thudPhase) * Math.Exp(-t * 11.0) * 0.9;

                // Descending sad whimper tone
                double whimper = 0.0;
                if (t >= 0.12)
                {
                    double wt = t - 0.12;
                    double wEnv = Math.Sin(Math.Min(1.0, wt / 0.40) * Math.PI);
                    double wFreq = 620.0 * Math.Exp(-wt * 2.8) + 12.0 * Math.Sin(2.0 * Math.PI * 14.0 * wt);
                    whimperPhase += 2.0 * Math.PI * wFreq / sr;
                    whimper = Math.Sin(whimperPhase) * wEnv * 0.52;
                }

                pcm[i] = ClampSample(Math.Tanh((thud + whimper) * 1.15));
            }
            return pcm;
        }

        // 10. tarot_cash.wav: mystical harp glissando + loud cash register CHA-CHING!
        private static short[] SynthesizeTarotCash()
        {
            const int sr = 44100;
            int len = (int)(sr * 0.82);
            short[] pcm = new short[len];
            double[] harpNotes = { 523.25, 659.25, 783.99, 987.77, 1174.66, 1567.98 };
            var rng = new Random(1010);

            for (int i = 0; i < len; i++)
            {
                double t = (double)i / sr;
                double sample = 0.0;

                // Mystical harp glissando (0..0.38s)
                for (int n = 0; n < harpNotes.Length; n++)
                {
                    double start = n * 0.048;
                    if (t >= start)
                    {
                        double nt = t - start;
                        double nEnv = Math.Exp(-nt * 8.5);
                        sample += Math.Sin(2.0 * Math.PI * harpNotes[n] * nt) * nEnv * 0.25;
                    }
                }

                // Cash drawer "CHA-" mechanical slap at 0.32s
                if (t >= 0.32 && t < 0.44)
                {
                    double ct = t - 0.32;
                    double cEnv = Math.Exp(-ct * 42.0);
                    sample += (rng.NextDouble() * 2.0 - 1.0) * cEnv * 0.65;
                }

                // Bright bell "-CHING!" at 0.40s
                if (t >= 0.40)
                {
                    double bt = t - 0.40;
                    double bEnv = Math.Exp(-bt * 6.5);
                    double bell = Math.Sin(2.0 * Math.PI * 2637.0 * bt) * 0.55
                                + Math.Sin(2.0 * Math.PI * 3951.0 * bt) * 0.35
                                + Math.Sin(2.0 * Math.PI * 5274.0 * bt) * 0.20;
                    sample += bell * bEnv * 0.78;
                }

                pcm[i] = ClampSample(Math.Tanh(sample * 1.25));
            }
            return pcm;
        }

        // 11. galley_whack.wav: comical slap + rapid mechanical keyboard typing burst when waking up a slacking coder
        private static short[] SynthesizeGalleyWhack()
        {
            const int sr = 44100;
            int len = (int)(sr * 0.55);
            short[] pcm = new short[len];
            var rng = new Random(1111);

            for (int i = 0; i < len; i++)
            {
                double t = (double)i / sr;
                double sample = 0.0;

                // Comical slap at 0..0.14s
                if (t < 0.14)
                {
                    double sEnv = Math.Exp(-t * 45.0);
                    double pitch = 320.0 * Math.Exp(-t * 25.0) + 90.0;
                    sample += (Math.Sin(2.0 * Math.PI * pitch * t) * 0.65 + (rng.NextDouble() * 2.0 - 1.0) * 0.7) * sEnv;
                }

                // Rapid Cherry MX Blue mechanical keyboard clacks (0.14..0.55s)
                if (t >= 0.14)
                {
                    double kt = t - 0.14;
                    double clickPeriod = 0.042;
                    double localK = kt % clickPeriod;
                    int keyIdx = (int)(kt / clickPeriod);
                    double kEnv = Math.Exp(-localK * 130.0);
                    double keyFreq = 1900.0 + (keyIdx % 5) * 260.0;
                    sample += (Math.Sin(2.0 * Math.PI * keyFreq * localK) * 0.65
                             + (rng.NextDouble() * 2.0 - 1.0) * 0.55) * kEnv * 0.75;
                }

                pcm[i] = ClampSample(Math.Tanh(sample * 1.25));
            }
            return pcm;
        }

        // 12. case_tick.wav: loud, crisp mechanical roulette ratchet click for [K] case spinning
        private static short[] SynthesizeCaseTick()
        {
            const int sr = 44100;
            int len = (int)(sr * 0.055);
            short[] pcm = new short[len];
            var rng = new Random(1212);

            for (int i = 0; i < len; i++)
            {
                double t = (double)i / sr;
                double env = Math.Exp(-t * 165.0);
                double woody = Math.Sin(2.0 * Math.PI * 1480.0 * t) * 0.65
                             + Math.Sin(2.0 * Math.PI * 2960.0 * t) * 0.35;
                double click = (rng.NextDouble() * 2.0 - 1.0) * 0.45;
                pcm[i] = ClampSample(Math.Tanh((woody + click) * env * 1.5));
            }
            return pcm;
        }

        // 13. boss_siren.wav: industrial alarm siren when the Central Elevator opens for the Escaped AI Agent boss
        private static short[] SynthesizeBossSiren()
        {
            const int sr = 44100;
            int len = (int)(sr * 1.25);
            short[] pcm = new short[len];
            double p1 = 0.0, p2 = 0.0;

            for (int i = 0; i < len; i++)
            {
                double t = (double)i / sr;
                double env = t < 0.05 ? (t / 0.05) : (t > 1.10 ? (1.25 - t) / 0.15 : 1.0);
                double lfo = 0.5 + 0.5 * Math.Sin(2.0 * Math.PI * 2.4 * t - Math.PI / 2.0);
                double freq = 420.0 + 460.0 * lfo;

                p1 += 2.0 * Math.PI * freq / sr;
                p2 += 2.0 * Math.PI * (freq * 1.5) / sr;

                double saw1 = 2.0 * ((p1 / (2.0 * Math.PI)) - Math.Floor(0.5 + p1 / (2.0 * Math.PI)));
                double saw2 = 2.0 * ((p2 / (2.0 * Math.PI)) - Math.Floor(0.5 + p2 / (2.0 * Math.PI)));
                double val = (saw1 * 0.6 + saw2 * 0.35 + Math.Sin(p1) * 0.4) * env * 0.75;
                pcm[i] = ClampSample(Math.Tanh(val * 1.2));
            }
            return pcm;
        }

        // 14. music_office_90s.wav: 12-second seamless loopable 90s synthwave / post-Soviet gangster bass + arpeggio groove (115 BPM)
        private static short[] SynthesizeMusicOffice90s()
        {
            const int sr = 44100;
            const double duration = 12.0; // Seamless 12.0s loop
            int len = (int)(sr * duration);
            short[] pcm = new short[len];

            // 23 beats over 12.0s (~115 BPM exact cycle) or 24 beats (120 BPM exact 16th grid) for sample-accurate zero-seam loop
            // Using 115 BPM feel with 24 beat grid across 12.0s (0.5s per beat, 0.125s per 16th note = 96 steps)
            const double stepDur = 12.0 / 96.0; // 0.125s per 16th step
            // Minor gangster progression: Am - F - Dm - E7 (24 steps per bar, 4 bars = 96 steps)
            double[] bassRoots = { 110.0, 87.31, 73.42, 82.41 };
            double[][] arpChords =
            {
                new[] { 220.0, 261.63, 329.63, 440.0 },   // Am
                new[] { 174.61, 220.0, 261.63, 349.23 },  // F
                new[] { 146.83, 174.61, 220.0, 293.66 },  // Dm
                new[] { 164.81, 207.65, 246.94, 329.63 }  // E
            };

            var rng = new Random(1994);
            double bassPhase = 0.0, arpPhase = 0.0, leadPhase = 0.0;

            for (int i = 0; i < len; i++)
            {
                double t = (double)i / sr;
                int step = (int)(t / stepDur) % 96;
                double stepLocal = (t % stepDur) / stepDur;
                int bar = (step / 24) % 4;

                // Kick on every 4th step (quarter notes)
                int beatStep = step % 4;
                double beatTime = (t % (stepDur * 4.0));
                double kickEnv = Math.Exp(-beatTime * 22.0);
                double kickFreq = 135.0 * Math.Exp(-beatTime * 35.0) + 46.0;
                double kick = Math.Sin(2.0 * Math.PI * kickFreq * beatTime) * kickEnv * 0.55;

                // Snare on backbeats (step % 8 == 4)
                double snare = 0.0;
                if ((step % 8) == 4)
                {
                    double st = stepLocal * stepDur;
                    double sEnv = Math.Exp(-st * 26.0);
                    snare = ((rng.NextDouble() * 2.0 - 1.0) * 0.7 + Math.Sin(2.0 * Math.PI * 185.0 * st) * 0.4) * sEnv * 0.38;
                }

                // Crisp 90s hi-hat on every 16th step
                double hatEnv = Math.Exp(-(stepLocal * stepDur) * ((step % 2 == 0) ? 65.0 : 95.0));
                double hat = (rng.NextDouble() * 2.0 - 1.0) * hatEnv * 0.12;

                // Driving 16th-note octave synth bass
                double root = bassRoots[bar];
                double bassFreq = (step % 2 == 1) ? root * 2.0 : root;
                bassPhase += 2.0 * Math.PI * bassFreq / sr;
                double bassEnv = Math.Exp(-(stepLocal * stepDur) * 14.0);
                double bass = Math.Tanh(Math.Sin(bassPhase) * 2.2) * bassEnv * 0.36;

                // Synthwave arpeggio
                double[] chord = arpChords[bar];
                double arpFreq = chord[step % 4];
                arpPhase += 2.0 * Math.PI * arpFreq / sr;
                double arpEnv = Math.Exp(-(stepLocal * stepDur) * 18.0);
                double arp = (Math.Sin(arpPhase) + 0.5 * Math.Sin(arpPhase * 2.0)) * arpEnv * 0.22;

                // Post-Soviet nostalgic lead pad
                double leadFreq = chord[(step / 6) % 4] * 2.0;
                leadPhase += 2.0 * Math.PI * leadFreq / sr;
                double lead = Math.Sin(leadPhase + 0.3 * Math.Sin(2.0 * Math.PI * 5.0 * t)) * 0.11;

                double mix = kick + snare + hat + bass + arp + lead;
                pcm[i] = ClampSample(Math.Tanh(mix * 1.05));
            }
            return pcm;
        }

        // 15. music_boss_fight.wav: 12-second seamless loopable intense 140 BPM boss combat synth track!
        private static short[] SynthesizeMusicBossFight()
        {
            const int sr = 44100;
            const double duration = 12.0; // Seamless 12.0s loop
            int len = (int)(sr * duration);
            short[] pcm = new short[len];

            // 140 BPM = 28 beats in 12.0s = 112 sixteenth-note steps!
            const int totalSteps = 112;
            const double stepDur = duration / totalSteps;
            // Aggressive Phrygian riff roots: E1, F1, G1, F1
            double[] riffRoots = { 82.41, 87.31, 98.00, 87.31 };
            double[] fastArp = { 329.63, 349.23, 392.00, 493.88, 523.25, 493.88, 392.00, 349.23 };

            var rng = new Random(2026);
            double bassPhase = 0.0, shredPhase = 0.0;

            for (int i = 0; i < len; i++)
            {
                double t = (double)i / sr;
                int step = (int)(t / stepDur) % totalSteps;
                double stepTime = t % stepDur;
                int section = (step / 28) % 4;

                // Heavy industrial double-time kick
                double beatTime = t % (stepDur * 2.0);
                double kickEnv = Math.Exp(-beatTime * 30.0);
                double kickFreq = 160.0 * Math.Exp(-beatTime * 42.0) + 48.0;
                double kick = Math.Sin(2.0 * Math.PI * kickFreq * beatTime) * kickEnv * 0.62;

                // Metallic industrial snare on steps % 4 == 2
                double snare = 0.0;
                if ((step % 4) == 2)
                {
                    double sEnv = Math.Exp(-stepTime * 34.0);
                    snare = ((rng.NextDouble() * 2.0 - 1.0) * 0.85 + Math.Sin(2.0 * Math.PI * 220.0 * stepTime) * 0.45) * sEnv * 0.44;
                }

                // Distorted cyberpunk reese/saw bass
                double root = riffRoots[section];
                if (step % 7 == 6) root *= 1.5;
                bassPhase += 2.0 * Math.PI * root / sr;
                double bassEnv = Math.Exp(-stepTime * 16.0);
                double bass = Math.Tanh(Math.Sin(bassPhase) * 3.0 + Math.Sin(bassPhase * 2.01) * 1.5) * bassEnv * 0.38;

                // High-tension 140 BPM boss arpeggio shred
                double shredFreq = fastArp[step % fastArp.Length] * (section == 2 ? 1.5 : 1.0);
                shredPhase += 2.0 * Math.PI * shredFreq / sr;
                double shredEnv = Math.Exp(-stepTime * 22.0);
                double shred = Math.Sign(Math.Sin(shredPhase)) * 0.24 * shredEnv;

                double mix = kick + snare + bass + shred;
                pcm[i] = ClampSample(Math.Tanh(mix * 1.15));
            }
            return pcm;
        }

        // Procedural character voice cadence generator for TriggerVoiceLine
        private static short[] SynthesizeVoiceCadence(SpeakerVoiceType type)
        {
            const int sr = 44100;
            int len = (int)(sr * 0.36);
            short[] pcm = new short[len];
            double phase = 0.0;

            for (int i = 0; i < len; i++)
            {
                double t = (double)i / sr;
                int syllable = (int)(t / 0.09);
                double localT = t % 0.09;
                double env = localT < 0.072 ? Math.Sin((localT / 0.072) * Math.PI) : 0.0;

                double freq = type switch
                {
                    // Deep gravelly 90s boss baritone
                    SpeakerVoiceType.Boss90s => 105.0 + (syllable % 3) * 14.0 + 8.0 * Math.Sin(2.0 * Math.PI * 35.0 * t),
                    // Mystical HR Veronika vibrato alto
                    SpeakerVoiceType.VeronikaTarot => 295.0 + (syllable % 4) * 32.0 + 14.0 * Math.Sin(2.0 * Math.PI * 7.5 * t),
                    // Squeaky panicked Vibe Coder tenor
                    SpeakerVoiceType.VibeCoder => 460.0 + (syllable % 3) * 65.0 + 25.0 * Math.Sin(2.0 * Math.PI * 18.0 * t),
                    // Robotic CRT Agent vocoder
                    _ => 180.0 + ((syllable % 2 == 0) ? 90.0 : -30.0)
                };

                phase += 2.0 * Math.PI * freq / sr;
                double wave = type switch
                {
                    SpeakerVoiceType.Boss90s => Math.Tanh(Math.Sin(phase) * 2.4 + Math.Sin(phase * 0.5) * 0.8),
                    SpeakerVoiceType.VeronikaTarot => Math.Sin(phase) * 0.7 + Math.Sin(phase * 2.0) * 0.3,
                    SpeakerVoiceType.VibeCoder => Math.Sin(phase) * 0.6 + Math.Sign(Math.Sin(phase)) * 0.35,
                    _ => Math.Sign(Math.Sin(phase)) * Math.Cos(2.0 * Math.PI * 50.0 * t)
                };

                pcm[i] = ClampSample(wave * env * 0.65);
            }
            return pcm;
        }
    }
}
