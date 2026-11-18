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
        private static float _masterVolume = .85f;
        public static float MasterVolume
        {
            get => _masterVolume;
            set
            {
                _masterVolume = Math.Clamp(value, 0f, 1f);
                if (Raylib.IsAudioDeviceReady()) ApplyMasterVolume();
            }
        }

        private static bool _silent;
        private static void ApplyMasterVolume()
        {
            if (_silent) Raylib.SetMasterVolume(0.0f);
            else Raylib.SetMasterVolume(_masterVolume);
        }
        public static float MusicVolume { get; set; } = 0.55f;
        public static float WeaponVolume { get; set; } = 0.90f;
        public static float VoiceVolume { get; set; } = 0.90f;

        // One-shot effects and voice cadences use Sound. Music uses looping stereo streams.
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

        private enum Score { Menu, Office, Boss }
        private static readonly string[] MusicFiles = ["music_main_menu.wav", "music_office_90s.wav", "music_boss_fight.wav"];
        private static readonly Music[] Streams = new Music[MusicFiles.Length];
        private static readonly float[] Gains = new float[MusicFiles.Length];
        private static float _dialogueGain = 1f;
        private static float _previewRemaining;
        private static Score _scene, _preview;

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

        public static void Initialize(bool silent = false)
        {
            _silent = silent;
            try
            {
                if (!Raylib.IsAudioDeviceReady()) Raylib.InitAudioDevice();
                if (!Raylib.IsAudioDeviceReady()) return;
                ApplyMasterVolume();
                if (_silent)
                {
                    Console.WriteLine($"[Audio] Silent automation. Native master={Raylib.GetMasterVolume():0.00}, playback disabled.");
                    return;
                }

                EnsureAllWavFilesExist();
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

                for (int i = 0; i < Streams.Length; i++)
                {
                    Streams[i] = Raylib.LoadMusicStream(Path.Combine(SoundsDir, MusicFiles[i]));
                    if (!Raylib.IsMusicValid(Streams[i])) throw new IOException(MusicFiles[i]);
                    Streams[i].Looping = true;
                }

                VoiceBoss90s = LoadAndRegister("voice_boss_90s.wav");
                VoiceVeronika = LoadAndRegister("voice_veronika.wav");
                VoiceVibeCoder = LoadAndRegister("voice_vibecoder.wav");
                VoiceRoboticAgent = LoadAndRegister("voice_agent.wav");
                AudioReady = true;
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
            EnsureMusicFiles(SoundsDir);
            EnsureWavFile("voice_boss_90s.wav", () => SynthesizeVoiceCadence(SpeakerVoiceType.Boss90s));
            EnsureWavFile("voice_veronika.wav", () => SynthesizeVoiceCadence(SpeakerVoiceType.VeronikaTarot));
            EnsureWavFile("voice_vibecoder.wav", () => SynthesizeVoiceCadence(SpeakerVoiceType.VibeCoder));
            EnsureWavFile("voice_agent.wav", () => SynthesizeVoiceCadence(SpeakerVoiceType.RoboticAgent));
        }

        private static void EnsureWavFile(string fileName, Func<short[]> generator)
        {
            string path1 = Path.Combine(SoundsDir, fileName);
            string binDir = Path.Combine(AppContext.BaseDirectory, "assets", "sounds");
            string path2 = Path.Combine(binDir, fileName);

            if (!File.Exists(path1) || new FileInfo(path1).Length < 256)
            {
                short[] samples = generator();
                WritePcm16Wav(path1, samples, 44100);
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

        public static void PreviewMusic()
        {
            if (!AudioReady) return;
            _preview = _scene == Score.Boss ? Score.Menu : Score.Boss;
            _previewRemaining = 5f;
        }

        public static void UpdateMusic(bool isBossActive, float dt = 1f / 60f, bool isMainMenu = false)
        {
            bool speaking = VoiceTimer > 0;
            VoiceTimer = Math.Max(0, VoiceTimer - dt);
            if (speaking && VoiceTimer == 0) ActiveSpeaker = ActiveVoiceText = "";
            if (!AudioReady) return;

            _scene = isMainMenu ? Score.Menu : isBossActive ? Score.Boss : Score.Office;
            _previewRemaining = Math.Max(0, _previewRemaining - dt);
            Score target = _previewRemaining > 0 ? _preview : _scene;
            const float underVoice = .39810717f; // 10^(-8/20)
            _dialogueGain += ((speaking ? underVoice : 1f) - _dialogueGain) *
                (1f - MathF.Exp(-dt / (speaking ? .038f : .36f)));

            for (int i = 0; i < Streams.Length; i++)
            {
                bool selected = i == (int)target;
                float previous = Gains[i];
                Gains[i] = Math.Clamp(previous + (selected ? dt : -dt) / .8f, 0, 1);
                Music stream = Streams[i];
                if (Gains[i] == 0)
                {
                    if (previous > 0) Raylib.StopMusicStream(stream);
                    continue;
                }
                Raylib.SetMusicVolume(stream, Math.Clamp(MusicVolume, 0, 1) * Gains[i] * _dialogueGain);
                if (previous == 0) Raylib.PlayMusicStream(stream);
                Raylib.UpdateMusicStream(stream);
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

        internal static readonly float[] EffectLevels = [.58f,.58f,.58f,.58f,.72f];

        public static void PlayBeltWhip() => PlayWeaponSound(SfxBeltWhip, volumeScale: EffectLevels[0]);
        public static void PlaySolderSizzle() => PlayWeaponSound(SfxSolderSizzle, volumeScale: EffectLevels[0]);
        public static void PlayUziShot() => PlayWeaponSound(SfxUziShot, volumeScale: EffectLevels[1]);
        public static void PlayShotgunBlast() => PlayWeaponSound(SfxShotgunBlast, volumeScale: EffectLevels[1]);
        public static void PlayChatgunBeam() => PlayWeaponSound(SfxChatgunBeam, volumeScale: EffectLevels[1]);
        public static void PlayHitmarker() => PlayWeaponSound(SfxHitmarker, volumeScale: EffectLevels[1]);
        public static void PlayMacbookBait() => PlayWeaponSound(SfxMacbookBait, volumeScale: EffectLevels[2]);
        public static void PlayAnxietyAlarm() => PlayWeaponSound(SfxAnxietyAlarm, volumeScale: EffectLevels[2]);
        public static void PlaySubduedDrop() => PlayWeaponSound(SfxSubduedDrop, volumeScale: EffectLevels[3]);
        public static void PlayTarotCash() => PlayWeaponSound(SfxTarotCash, volumeScale: EffectLevels[3]);
        public static void PlayGalleyWhack() => PlayWeaponSound(SfxGalleyWhack, volumeScale: EffectLevels[3]);
        public static void PlayBossSiren() => PlayWeaponSound(SfxBossSiren, volumeScale: EffectLevels[2]);

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
            PlaySfx("case_tick.wav", 0.96f + (float)_rng.NextDouble() * 0.08f, 0.95f * EffectLevels[4]);
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
            try
            {
                foreach (var kv in new HashSet<Sound>(_soundsByName.Values))
                {
                    Raylib.UnloadSound(kv);
                }
                _soundsByName.Clear();
                for (int i = 0; i < Streams.Length; i++)
                {
                    if (Raylib.IsMusicValid(Streams[i])) Raylib.UnloadMusicStream(Streams[i]);
                    Streams[i] = default;
                    Gains[i] = 0;
                }
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

        private static void WritePcm16Wav(string filePath, short[] samples, int sampleRate, short channels = 1)
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
            bw.Write(channels);
            bw.Write(sampleRate);
            bw.Write(sampleRate * channels * 2); // ByteRate
            bw.Write((short)(channels * 2)); // BlockAlign
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

        private const int MusicRate = 44100;
        private const int MusicFrames = 24 * MusicRate;
        private static double Hz(int midi) => 440 * Math.Pow(2, (midi - 69) / 12.0);
        private static double Sin(double cycles) => Math.Sin(Math.Tau * cycles);
        private static double Attack(double t, double seconds) => Math.Min(1, t / seconds);

        internal static void EnsureMusicFiles(string directory, bool force = false)
        {
            Directory.CreateDirectory(directory);
            foreach (Score score in Enum.GetValues<Score>())
            {
                string path = Path.Combine(directory, MusicFiles[(int)score]);
                // The previous 12-second mono cache has a different length.
                if (!force && File.Exists(path) && new FileInfo(path).Length == 44 + 4 * MusicFrames) continue;
                string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    WritePcm16Wav(temp, SynthesizeDeepStereoMusicWav(score), MusicRate, 2);
                    File.Move(temp, path, true);
                }
                finally { File.Delete(temp); }
            }
        }

        // Write tails across the loop boundary, including the tail of the last musical phrase.
        private static void Note(double start, double duration, Action<int, double> render)
        {
            int frame = (int)Math.Round(start * MusicRate) % MusicFrames;
            int count = (int)Math.Round(duration * MusicRate);
            for (int j = 0; j < count; j++)
            {
                render(frame, (double)j / MusicRate);
                if (++frame == MusicFrames) frame = 0;
            }
        }

        private sealed class ResonantLowPass
        {
            private double low, band;
            public double Sample(double input, double cutoff, double damping)
            {
                // State-variable filter, used only on the low-frequency synth voices.
                double f = 2 * Math.Sin(Math.PI * cutoff / MusicRate);
                low += f * band;
                band += f * (input - low - damping * band);
                return low;
            }
        }

        private sealed class DelayLine(int length)
        {
            private readonly double[] buffer = new double[length];
            private int cursor;
            public double Tick(double input, double feedback, bool allPass = false)
            {
                double delayed = buffer[cursor];
                double stored = input + delayed * feedback;
                buffer[cursor] = stored;
                if (++cursor == length) cursor = 0;
                return allPass ? delayed - feedback * stored : delayed;
            }
        }

        private sealed class SchroederHall(int spread)
        {
            private readonly DelayLine[] combs = [new(1557 + spread), new(1617 + spread),
                new(1491 + spread), new(1422 + spread)];
            private readonly DelayLine diffuser1 = new(225 + spread);
            private readonly DelayLine diffuser2 = new(556 + spread);
            public double Sample(double input)
            {
                double sum = 0;
                foreach (var comb in combs) sum += comb.Tick(input, .79);
                return diffuser2.Tick(diffuser1.Tick(sum * .25, .52, true), .48, true);
            }
        }

        private static double ChorusTap(double[] bus, int frame, double delay)
        {
            int whole = (int)delay;
            double fraction = delay - whole;
            int i = (frame + MusicFrames - whole) % MusicFrames;
            return bus[i] * (1 - fraction) + bus[(i + MusicFrames - 1) % MusicFrames] * fraction;
        }

        private static short[] SynthesizeDeepStereoMusicWav(Score score)
        {
            bool menu = score == Score.Menu, boss = score == Score.Boss;
            double beat = 60.0 / (menu ? 96 : boss ? 138 : 108);
            int bars = (int)(24 / (4 * beat));
            var random = new Random(1994 + (int)score);
            double[] bass = new double[MusicFrames], drum = new double[MusicFrames], air = new double[MusicFrames];
            double[] keysL = new double[MusicFrames], keysR = new double[MusicFrames];
            double[] leadL = new double[MusicFrames], leadR = new double[MusicFrames];
            double[] pump = new double[MusicFrames];
            double Noise() => 2 * random.NextDouble() - 1;

            void Kick(double start, double accent)
            {
                Note(start, .39, (i,t) =>
                {
                    double phase = (boss ? 47 : 43)*t + 2.8*(1-Math.Exp(-43*t));
                    double body = Sin(phase)*Math.Exp(-12*t) + .12*Noise()*Math.Exp(-340*t);
                    drum[i] += accent * Attack(t,.0015) * Math.Tanh(1.5*body);
                    pump[i] = Math.Max(pump[i], accent * Math.Exp(-13*t));
                });
            }

            void Snare(double start, double accent)
            {
                double last = 0;
                Note(start, .29, (i,t) =>
                {
                    double n = Noise();
                    double wire = (n-last)*.48 + n*.24;
                    last = n;
                    double shell = Sin(182*t + .5*(1-Math.Exp(-55*t))) * Math.Exp(-31*t);
                    double metal = boss ? Sin(823*t + 1.8*Sin(537*t))*.42 : .12*Sin(710*t);
                    double hit = Attack(t,.001) * (shell + (wire+metal)*Math.Exp(-(boss ? 18 : 35)*t));
                    drum[i] += accent*hit;
                });
            }

            void Hat(double start, double accent, double decay)
            {
                double last = 0;
                Note(start,.12,(i,t) =>
                {
                    double n = Noise();
                    air[i] += accent*(n-last)*Math.Exp(-decay*t)*Attack(t,.0018);
                    last = n;
                });
            }

            void BassNote(double start, double length, int midi, double accent)
            {
                double hz = Hz(midi);
                var filter = new ResonantLowPass();
                Note(start,length,(i,t) =>
                {
                    double phase = hz*t;
                    double bright = Sin(phase) + .48*Sin(2*phase) + .27*Sin(3*phase) + .16*Sin(5*phase);
                    double cutoff = menu ? 230 + 155*Sin(start/24 + t/6)
                        : boss ? 420 + 950*Math.Exp(-20*t) : 180 + 770*Math.Exp(-29*t);
                    double filtered = filter.Sample(bright, cutoff, boss ? .8 : .52);
                    double drive = boss ? 2.7 : 1.22;
                    bass[i] += accent*Attack(t,.006)*Attack(length-t,.024) *
                        (.65*Math.Tanh(drive*filtered) + .35*Math.Tanh(1.5*Sin(phase)));
                });
            }

            void Chord(double start, double length, int root, int[] intervals, bool stab)
            {
                for (int v = 0; v < intervals.Length; v++)
                {
                    double hz = Hz(root + intervals[v]);
                    double pan = (v * 3 % 5) / 4.0;
                    double detune = Math.Pow(2,(v-2)*2.7/1200);
                    Note(start,length,(i,t) =>
                    {
                        double phase = t*hz*detune;
                        // Band-limited saw ensemble with a slower string bow or a short brass attack.
                        double wave = Sin(phase)+.43*Sin(2*phase)+.26*Sin(3*phase)+.14*Sin(4*phase)+.09*Sin(6*phase);
                        double env = stab ? Attack(t,.028)*Math.Exp(-6*t) : Attack(t,menu ? .6 : 1.2);
                        double value = wave*env*Attack(length-t,stab ? .065 : .68)*(stab ? .084 : .046);
                        keysL[i] += value*(1-.65*pan);
                        keysR[i] += value*(.35+.65*pan);
                    });
                }
            }

            void Melody(double start, int midi, double pan, double accent)
            {
                double hz = Hz(midi), length = boss ? .36 : 1.15;
                Note(start,length,(i,t) =>
                {
                    double phase = hz*t;
                    double tone = boss ? Sin(phase + .18*Sin(phase*2)) + .27*Sin(phase*3)
                        : Sin(phase + (menu ? .95 : 2.5)*Math.Exp(-8*t)*Sin(phase*(menu ? 2.01 : 2.71)));
                    double value = accent*tone*Attack(t,.012)*Math.Exp(-(boss ? 5 : 4)*t)*Attack(length-t,.05);
                    leadL[i] += value*(1-pan);
                    leadR[i] += value*pan;
                });
            }

            int[] roots = menu ? [26,22,19,21] : boss ? [28,29,28,26] : [31,27,24,26];
            int[] minor9 = [0,7,10,15,26], major9 = [0,7,11,16,26], dominant = [0,7,10,16,26];
            int[] arp = [0,7,10,3,14,10,7,3], phrygian = [0,1,7,5,3,1,0,-2];
            int[] riff = [0,0,12,0,7,0,12,1], moog = [0,12,0,7,0,3,10,7];
            for (int bar = 0; bar < bars; bar++)
            {
                double start = bar*4*beat;
                int section = bar*4/bars, root = roots[section];
                if (!boss)
                {
                    double sustain = bar == bars-1 ? 24-start : 4*beat;
                    int[] voicing = section == 1 ? major9 : menu && section == 3 ? dominant : minor9;
                    Chord(start,sustain+.35,root+24,voicing,false);
                }
                if (menu) BassNote(start,4*beat,root,.55);
                if (boss)
                    Note(start,.68,(i,t) => bass[i] += .43*Sin(30*t+7*(1-Math.Exp(-9*t)))*Math.Exp(-6*t)*Attack(t,.009));

                for (int step = 0; step < 16; step++)
                {
                    double at = start + step*beat/4;
                    if (step == 0 || step == 8 || (boss && step%4 == 0) || (bar%2 == 1 && step == 14))
                        Kick(at, step == 14 ? .44 : .82);
                    if (step == 4 || step == 12) Snare(at,menu ? .48 : boss ? .54 : .32);
                    if (!menu || step%2 == 0)
                        Hat(at,step%4 == 2 ? .083 : .036,step%4 == 2 ? 47 : 87);
                    if (boss && bar%2 == 1 && step >= 13) Hat(at+beat/8,.054,110);

                    if (menu && step%4 == 2)
                        Melody(at,root+48+arp[(bar*4+step/4)%8],step%8 == 2 ? .23 : .73,.16);
                    if (!boss && !menu)
                    {
                        BassNote(at,beat*.24,root+moog[step%8],step%4 == 0 ? .47 : .31);
                        if (step == 3 || step == 10 || (bar%2 == 1 && step == 15))
                            Melody(at,root+48+arp[(bar+step)%8],step == 3 ? .8 : .18,.125);
                    }
                    if (boss && step%2 == 0)
                    {
                        BassNote(at,beat*.44,root+riff[step/2],.52);
                        if (step == 0 || step == 6 || step == 12)
                            Chord(at,beat*.78,root+12,[0,7,12,15,19],true);
                        if (step%4 == 2)
                            Melody(at,52+phrygian[(bar*4+step/4)%8],step%8 == 2 ? .25 : .64,.23);
                    }
                }
            }

            // Exact BPMs produce fractional final bars at 24 s. Leave a turnaround
            // with a held harmony and a two-beat pickup timed backwards from the downbeat.
            if (boss) Chord(bars*4*beat,24-bars*4*beat+.25,40,[0,7,12,13,19],false);
            for (int i = 0; i < 4; i++) Hat(24-(4-i)*beat/2,.05+.012*i,65);
            Snare(24-beat/2,.32);
            Melody(24-beat,menu ? 69 : boss ? 53 : 65,.75,.16);

            double[] mixL = new double[MusicFrames], mixR = new double[MusicFrames];
            int echo = (int)(beat*.75*MusicRate);
            var hallL = new SchroederHall(0);
            var hallR = new SchroederHall(37);
            var room = new ResonantLowPass();
            for (int i = 0; i < MusicFrames; i++)
            {
                double cycle = (double)i/MusicFrames, t = i/(double)MusicRate;
                int tap1 = (i+MusicFrames-echo)%MusicFrames, tap2 = (i+MusicFrames-2*echo)%MusicFrames;
                double left = keysL[i]+.48*ChorusTap(keysR,i,720+93*Sin(5*cycle));
                double right = keysR[i]+.48*ChorusTap(keysL,i,867+119*Sin(7*cycle+.24));
                left += leadL[i]+.42*leadR[tap1]+.18*leadL[tap2];
                right += leadR[i]+.42*leadL[tap1]+.18*leadR[tap2];
                if (!boss && !menu)
                {
                    double wind = room.Sample(Noise(),360+110*Sin(cycle),1.2)*.085;
                    double drone = .057*Sin(49*t)+.028*Sin(98*t+.13*Sin(cycle));
                    left += drone+wind+.025*Sin(147*t+.3*Sin(2*cycle));
                    right += drone+.6*wind+.025*Sin(147*t+.7*Sin(3*cycle+.2));
                }
                double duck = 1-(boss ? .43 : .28)*pump[i];
                // Short gated reflections on the drum bus; the hall is reserved for pitched layers.
                double gateL = drum[(i+MusicFrames-1291)%MusicFrames]*.15+drum[(i+MusicFrames-2821)%MusicFrames]*.09;
                double gateR = drum[(i+MusicFrames-1501)%MusicFrames]*.14+drum[(i+MusicFrames-3163)%MusicFrames]*.08;
                double center = bass[i]*duck+drum[i];
                mixL[i] = center+duck*left+air[i]*.78+gateL;
                mixR[i] = center+duck*right+air[i]+gateR;
            }
            // Warm delay networks with one full period before committing the wet return.
            double peak = 0;
            for (int pass = 0; pass < 2; pass++)
            for (int i = 0; i < MusicFrames; i++)
            {
                double wetL = hallL.Sample(keysL[i]+leadL[i]+.18*drum[i]);
                double wetR = hallR.Sample(keysR[i]+leadR[i]+.18*drum[i]);
                if (pass == 0) continue;
                mixL[i] = Math.Tanh((mixL[i]+.27*wetL)*1.14);
                mixR[i] = Math.Tanh((mixR[i]+.27*wetR)*1.14);
                peak = Math.Max(peak,Math.Max(Math.Abs(mixL[i]),Math.Abs(mixR[i])));
            }
            short[] pcm = new short[MusicFrames*2];
            double trim = .57/peak; // -4.88 dBFS sample ceiling leaves headroom for intersample peaks.
            // Match the endpoint over the last 2 ms without fading the bed or its reverb to silence.
            const int stitch = MusicRate / 500;
            double offsetL = mixL[0] - mixL[^1], offsetR = mixR[0] - mixR[^1];
            for (int j = 0; j < stitch; j++)
            {
                double u = (double)j / (stitch-1);
                double blend = u*u*(3-2*u);
                mixL[MusicFrames-stitch+j] += blend*offsetL;
                mixR[MusicFrames-stitch+j] += blend*offsetR;
            }
            for (int i = 0; i < MusicFrames; i++)
            {
                pcm[2*i] = ClampSample(mixL[i]*trim);
                pcm[2*i+1] = ClampSample(mixR[i]*trim);
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
