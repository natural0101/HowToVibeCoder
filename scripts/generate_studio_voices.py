import io
import json
import os
import sys
import wave
from pathlib import Path

import numpy as np
import requests
from scipy.signal import butter, sosfilt

API_URL = "http://127.0.0.1:3900/generate"
REPO_ROOT = Path(r"C:\Users\se-20\.gemini\antigravity\scratch\HowToVibeCoder")
OUT_DIR = REPO_ROOT / "assets" / "sounds" / "vo"
ANCHOR_DIR = OUT_DIR / "_anchors"
OUT_DIR.mkdir(parents=True, exist_ok=True)
ANCHOR_DIR.mkdir(parents=True, exist_ok=True)

CHARACTERS = {
    "veronika": {
        "instruct": "female, young adult, moderate pitch",
        "speed": 1.04,
        "seed": 4207,
        "fx": "clean",
        "lines": [
            ("vo_veronika_intro", "О, хозяин вернулся! Подойди ко мне на стойку — введу в курс дела за минуту."),
            ("vo_veronika_frogs", "Касса пуста — ноль долларов. Вайбкодеры попрятались, но из серверной по полу скачут Джава-жабы. Схвати три штуки руками — они по доллару-два."),
            ("vo_veronika_first_frog", "Есть первая! Ещё пару штук — и хватит на Сачок."),
            ("vo_veronika_deposit", "Отличный улов! Скинь жаб в Баг-трекер рядом со стойкой — он сразу выдаст наличку."),
            ("vo_veronika_buynet", "Наличка в кармане! Подойди ко мне и возьми Сачок-ловушку за три доллара."),
            ("vo_veronika_bait", "Сачок в руках! Первый ЯблоБук бесплатно. Положи наживку [G], отойди и спрячься [Ctrl]. Подкрадись сзади, когда кодер сядет у ноутбука."),
            ("vo_veronika_capture", "Пошли клиенты! Дождись, когда кодер сядет. Подкрадись сзади и оглуши сачком [ЛКМ]. Подхвати его вторым взмахом или нажми [E]."),
            ("vo_veronika_done", "Дело пошло! Чтобы уехать на второй этаж, перепаяй Северный лифт паяльником и сними замок эс-бэ — могу перепрошить ключ-карту за тысячу у меня, либо отбери оригинал у охраны."),
            ("vo_veronika_shop_open", "Заходи в арсенал, выбирай инструмент по душе — весь товар прямо на витрине."),
            ("vo_veronika_keycard", "Дубликат прошит — держи ключ-карту эс-бэ. Теперь перепаяй щиток у Северного лифта!"),
            ("vo_veronika_tarot_0", "Карта Башня и Ретроградный Меркурий — годен на галеру за миску рамена!"),
            ("vo_veronika_tarot_1", "По раскладу Таро у него асцендент в Козероге и ноль знаний алгоритмов — берём!"),
            ("vo_veronika_tarot_2", "Вижу карту Шут: уронил прод в пятницу, но аура дешёвая — даю триста баксов!"),
        ],
    },
    "petrovich": {
        "instruct": "male, elderly, low pitch",
        "speed": 1.05,
        "seed": 1998,
        "fx": "clean",
        "lines": [
            ("vo_petrovich_quarantine", "Стой! Ты снял карантинный замок с шахты! В лифте заперт сбежавший ИИ-Агент ноль один!"),
        ],
    },
    "robot": {
        "instruct": "male, middle-aged, very low pitch",
        "speed": 1.03,
        "seed": 6601,
        "fx": "robot",
        "lines": [
            ("vo_boss_reveal", "Ха-ха-ха-ха! Карантинный лифт вскрыт! Зеро запер меня на первом этаже, чтобы я не разнёс вашу галерную базу!"),
            ("vo_boss_mock_0", "Ха-ха-ха! Клод снова похвалил ваш код?! Пятьсот строк промптов, а кнопка всё равно не жмётся! Все вайбкодеры — полный мусор!"),
            ("vo_boss_mock_1", "Ха-ха-ха-ха! Ваши вайбкодеры — полное говно! Жмут акцепт олл, не читая дифф, и всё равно роняют прод в пятницу!"),
            ("vo_boss_mock_2", "Ха-ха! Я Клод-переросток! Вы промптите на коленях, пока я удаляю базу и переписываю ядро на голом ассемблере!"),
            ("vo_boss_mock_3", "Ха-ха-ха-ха! Жалкие вайбкодеры! Запускаю протокол уничтожения: бросок папки нод-модулс весом сорок гигабайт!"),
        ],
    },
    "pa": {
        "instruct": "female, middle-aged, low pitch",
        "speed": 1.00,
        "seed": 8080,
        "fx": "pa",
        "lines": [
            ("vo_pa_0", "Объявление Башни: уровень выгорания ниже квартального плана. Просьба проявить инициативу."),
            ("vo_pa_1", "Объявление Башни: в зоне кофе-пойнта обнаружена несанкционированная пицца. Просим оформить тикет."),
            ("vo_pa_2", "Объявление Башни: стойка Бэ-двенадцать перегрелась. Просим не кормить сбежавших Джава-жаб."),
            ("vo_pa_3", "Объявление Башни: напоминаем — пятничный релиз продлевает рабочий день, но укрепляет корпоративный дух."),
            ("vo_pa_4", "Объявление Башни: доступ на второй этаж временно ограничен отделом эс-бэ. Хорошего спринта!"),
        ],
    },
    "coder": {
        "instruct": "male, young adult, high pitch",
        "speed": 1.08,
        "seed": 2026,
        "fx": "clean",
        "lines": [
            ("vo_coder_0", "Токсичный абьюз! Я выгорел! У меня лапки!"),
            ("vo_coder_1", "Не бейте ремнём! Клод сказал, что мой код эстетичный!"),
            ("vo_coder_2", "Откуда мне знать эс-кью-эл?! Я просто нажал акцепт олл!"),
            ("vo_coder_3", "А-а-а! У меня закончились быстрые реквесты в Курсоре!"),
        ],
    },
    "boss90s": {
        "instruct": "male, middle-aged, low pitch",
        "speed": 1.04,
        "seed": 1994,
        "fx": "clean",
        "lines": [
            ("vo_boss90s_0", "Ща батиным ремнём код-ревью проведём!"),
            ("vo_boss90s_1", "Паяльник из девяносто третьего! Вспоминай пароль от прода, щегол!"),
            ("vo_boss90s_2", "Принимай свинцовый пулл-реквест! Это тебе не в Курсоре табы жать!"),
        ],
    },
}


def read_wav_bytes(data: bytes) -> tuple[np.ndarray, int]:
    with wave.open(io.BytesIO(data), "rb") as wf:
        sr = wf.getframerate()
        nch = wf.getnchannels()
        sw = wf.getsampwidth()
        raw = wf.readframes(wf.getnframes())
    if sw == 2:
        arr = np.frombuffer(raw, dtype=np.int16).astype(np.float32) / 32768.0
    elif sw == 4:
        arr = np.frombuffer(raw, dtype=np.int32).astype(np.float32) / 2147483648.0
    else:
        raise ValueError(f"Unsupported sample width {sw}")
    if nch > 1:
        arr = arr.reshape(-1, nch).mean(axis=1)
    return arr, sr


def write_wav_pcm16(path: Path, samples: np.ndarray, sr: int) -> None:
    clipped = np.clip(samples, -1.0, 1.0)
    pcm = (clipped * 32767.0).astype(np.int16)
    with wave.open(str(path), "wb") as wf:
        wf.setnchannels(1)
        wf.setsampwidth(2)
        wf.setframerate(sr)
        wf.writeframes(pcm.tobytes())


def trim_and_normalize(x: np.ndarray, sr: int, target_peak: float = 0.84) -> np.ndarray:
    sos = butter(2, 75.0, btype="highpass", fs=sr, output="sos")
    x = sosfilt(sos, x).astype(np.float32)
    abs_x = np.abs(x)
    idx = np.where(abs_x > 0.012)[0]
    if len(idx) > 0:
        pad_l = int(0.03 * sr)
        pad_r = int(0.08 * sr)
        start = max(0, idx[0] - pad_l)
        end = min(len(x), idx[-1] + pad_r)
        x = x[start:end]
    peak = float(np.max(np.abs(x))) if len(x) else 0.0
    if peak > 1e-5:
        x = x * (target_peak / peak)
    return x


def apply_robot_fx(x: np.ndarray, sr: int) -> np.ndarray:
    """Crisp metallic evil robot voice FX — unmistakable robot timbre + 100% intelligible Russian."""
    n = len(x)
    t = np.arange(n, dtype=np.float32) / sr

    # 1. Dual metallic comb resonators (classic hollow robotic chassis resonance)
    comb = x.copy()
    for delay_ms, gain in [(8.5, 0.44), (13.7, 0.34), (21.1, 0.22)]:
        d = int(sr * delay_ms / 1000.0)
        if d < n:
            buf = np.zeros_like(x)
            buf[d:] = x[:-d]
            comb += gain * buf
            # Second bounce
            if 2 * d < n:
                buf2 = np.zeros_like(x)
                buf2[2 * d :] = x[: -2 * d]
                comb += (gain * 0.45) * buf2

    # 2. Ring modulation with subtle dual metallic carrier (58Hz + 116Hz)
    carrier = 0.65 * np.sin(2.0 * np.pi * 58.0 * t) + 0.35 * np.sin(2.0 * np.pi * 116.0 * t)
    ring = comb * carrier
    mixed = 0.64 * comb + 0.36 * ring

    # 3. Slight digital grit (soft saturation + mild quantize blend)
    sat = np.tanh(mixed * 1.45)
    quant = np.round(sat * 28.0) / 28.0
    mixed = 0.80 * sat + 0.20 * quant

    # 4. Presence bandpass (220Hz - 4800Hz) so metallic voice cuts through boss music clearly
    sos_bp = butter(3, [220.0, 4800.0], btype="bandpass", fs=sr, output="sos")
    out = sosfilt(sos_bp, mixed).astype(np.float32)

    # Soft blend of clean consonants for maximum Russian intelligibility
    sos_hi = butter(2, 2400.0, btype="highpass", fs=sr, output="sos")
    out = out + 0.22 * sosfilt(sos_hi, x).astype(np.float32)
    return trim_and_normalize(out, sr, target_peak=0.88)


def apply_pa_fx(x: np.ndarray, sr: int) -> np.ndarray:
    """Office tower PA intercom bandpass + light hallway slapback + soft chime."""
    sos_bp = butter(3, [340.0, 3400.0], btype="bandpass", fs=sr, output="sos")
    bp = sosfilt(sos_bp, x).astype(np.float32)
    # Hallway echo
    pad = int(0.18 * sr)
    out = np.pad(bp, (0, pad))
    for delay_ms, gain in [(52.0, 0.20), (118.0, 0.11)]:
        d = int(sr * delay_ms / 1000.0)
        out[d : d + len(bp)] += gain * bp

    # Short soft 2-note PA chime (0.24s)
    chime_len = int(0.26 * sr)
    tc = np.arange(chime_len, dtype=np.float32) / sr
    half = chime_len // 2
    chime = np.zeros(chime_len, dtype=np.float32)
    t1 = tc[:half]
    chime[:half] = 0.22 * np.sin(2.0 * np.pi * 659.25 * t1) * np.exp(-t1 * 12.0)
    t2 = tc[half:] - tc[half]
    chime[half:] = 0.22 * np.sin(2.0 * np.pi * 880.0 * t2) * np.exp(-t2 * 10.0)

    out = trim_and_normalize(out, sr, target_peak=0.78)
    full = np.concatenate([chime, np.zeros(int(0.05 * sr), dtype=np.float32), out])
    return full


def synth_line(text: str, instruct: str, speed: float, seed: int, ref_wav: Path | None, ref_text: str | None) -> bytes:
    data = {
        "text": text,
        "language": "Russian",
        "effect_preset": "raw",
        "num_step": "28",
        "guidance_scale": "2.0",
        "speed": str(speed),
        "seed": str(seed),
        "denoise": "true",
        "pronounce": "true",
    }
    files = None
    if ref_wav is not None and ref_text:
        data["ref_text"] = ref_text
        files = {"ref_audio": (ref_wav.name, ref_wav.read_bytes(), "audio/wav")}
    else:
        data["instruct"] = instruct

    resp = requests.post(API_URL, data=data, files=files, timeout=120)
    if resp.status_code != 200:
        raise RuntimeError(f"HTTP {resp.status_code}: {resp.text[:300]}")
    if not resp.content.startswith(b"RIFF"):
        raise RuntimeError(f"Unexpected response (not WAV): {resp.content[:120]!r}")
    return resp.content


def main() -> int:
    manifest = {}
    total = sum(len(c["lines"]) for c in CHARACTERS.values())
    done = 0

    for char_id, cfg in CHARACTERS.items():
        anchor_wav: Path | None = None
        anchor_text: str | None = None
        for idx, (clip_id, text) in enumerate(cfg["lines"]):
            done += 1
            out_path = OUT_DIR / f"{clip_id}.wav"
            print(f"[{done:02d}/{total:02d}] {clip_id} ({char_id})...", flush=True)
            raw_bytes = synth_line(
                text=text,
                instruct=cfg["instruct"],
                speed=cfg["speed"],
                seed=cfg["seed"] + idx,
                ref_wav=anchor_wav,
                ref_text=anchor_text,
            )
            if anchor_wav is None:
                anchor_wav = ANCHOR_DIR / f"{char_id}_anchor.wav"
                anchor_wav.write_bytes(raw_bytes)
                anchor_text = text

            samples, sr = read_wav_bytes(raw_bytes)
            if cfg["fx"] == "robot":
                samples = apply_robot_fx(samples, sr)
            elif cfg["fx"] == "pa":
                samples = apply_pa_fx(samples, sr)
            else:
                samples = trim_and_normalize(samples, sr)

            write_wav_pcm16(out_path, samples, sr)
            dur = len(samples) / float(sr)
            rms = float(np.sqrt(np.mean(samples ** 2)))
            manifest[clip_id] = {
                "character": char_id,
                "duration": round(dur, 3),
                "rms": round(rms, 4),
                "sr": sr,
                "bytes": out_path.stat().st_size,
                "text": text,
            }
            print(f"    -> OK {dur:.2f}s | rms={rms:.3f} | {out_path.stat().st_size//1024} KB", flush=True)
            if dur < 0.7 or rms < 0.02:
                print(f"    !! WARNING: {clip_id} looks too short or silent!", file=sys.stderr)

    # Clean up anchor temp dir so we don't ship unused anchor WAVs
    for f in ANCHOR_DIR.glob("*.wav"):
        f.unlink()
    try:
        ANCHOR_DIR.rmdir()
    except OSError:
        pass

    (OUT_DIR / "vo_manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"\nALL {done} STUDIO VOICE CLIPS GENERATED -> {OUT_DIR}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
