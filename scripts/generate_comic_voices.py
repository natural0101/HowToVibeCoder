"""Generate 8 RU + 8 EN studio-mastered surreal intro comic narration clips via VoiceStudio OmniVoice CUDA."""
import io
import json
import time
import wave
from pathlib import Path

import numpy as np
import requests
from scipy.signal import butter, sosfilt

API_URL = "http://127.0.0.1:3900/generate"
REPO_ROOT = Path(r"C:\Users\se-20\.gemini\antigravity\scratch\HowToVibeCoder")
RU_DIR = REPO_ROOT / "assets" / "sounds" / "vo"
EN_DIR = RU_DIR / "en"

ANCHOR_RU = "Две тысячи двадцать шестой год. Башня Энтропик. Старые методы возвращаются."
ANCHOR_EN = "Twenty twenty-six. Entropic Tower. Old-school methods are back."

COMIC_LINES_RU = [
    ("vo_comic_1", "Две тысячи двадцать шестой год. Башня Энтропик. Ты заменил весь айти-отдел на вайбкодеров и ИИ-агента. Первая неделя: блин, реально круто работает!"),
    ("vo_comic_2", "Потом вылез первый баг. Агент поклялся: такого больше никогда не повторится! И через секунду: баг! Ещё баг! И снова баг! Прод горит!"),
    ("vo_comic_3", "Пока офис орал, эйчар Вероника спокойно раскинула Таро: я же говорила — Меркурий в ретрограде. Отбиваем убытки вручную."),
    ("vo_comic_4", "В Северном лифте забаррикадился сам ИИ-Агент. Он всё ещё орёт: всё исправлено — и стирает базу. Переходим к ручному рефакторингу."),
    ("vo_comic_5", "От перегрева в серверной баги буквально материализовались в механических жаб — и разбежались по всему первому этажу."),
    ("vo_comic_6", "Вайбкодеры не верят, что всё сгорело — агент же написал: все тесты пройдены! Но на халявный вай-фай и токены Клодия слетаются мгновенно."),
    ("vo_comic_7", "Агент оптимизировал даже эйчар-отдел — у Вероники вместо головы игровой автомат. Тащи редких кадров, крути пушки и снимай карантин."),
    ("vo_comic_8", "Простите, больше не повторится — пищит из колонок. Счета на нуле. Первый этаж Энтропик. Чиним этот цирк по-старому!"),
]

COMIC_LINES_EN = [
    ("vo_comic_1", "Twenty twenty-six. Entropic Tower. You replaced the whole IT team with vibe coders and an AI agent. Week one: hey, this actually works great!"),
    ("vo_comic_2", "Then the first bug hit. The agent swore: this will never happen again! One second later: bug! Another bug! And another bug! Production is on fire!"),
    ("vo_comic_3", "While the office panicked, HR Veronika calmly dealt her Tarot: told you, Mercury is in retrograde. Time to recover the losses by hand."),
    ("vo_comic_4", "The rogue AI Agent barricaded the North Elevator. It still yells everything is fixed while wiping the database. Time for manual refactoring."),
    ("vo_comic_5", "Overheating racks literally materialized the production bugs into mechanical frogs — swarming all over Floor One."),
    ("vo_comic_6", "The vibe coders refuse to believe prod burned down — the agent printed All Tests Passed! Still, free Wi-Fi and Clodius tokens lure them right in."),
    ("vo_comic_7", "The agent even optimized HR — Veronika's head is now a literal slot machine. Bring her rare catches, spin weapons, and lift the lockdown."),
    ("vo_comic_8", "So sorry, it won't happen again — chirps the intercom. Bank at zero. Floor One of Entropic Corp. Time to fix this circus the old way!"),
]


def read_wav_bytes(data: bytes) -> tuple[np.ndarray, int]:
    with wave.open(io.BytesIO(data), "rb") as wf:
        sr = wf.getframerate()
        nch = wf.getnchannels()
        raw = wf.readframes(wf.getnframes())
    arr = np.frombuffer(raw, dtype=np.int16).astype(np.float32) / 32768.0
    if nch > 1:
        arr = arr.reshape(-1, nch).mean(axis=1)
    return arr, sr


def master_array(x: np.ndarray, sr: int, target_rms: float = 0.168) -> np.ndarray:
    x = sosfilt(butter(2, 68.0, btype="highpass", fs=sr, output="sos"), x).astype(np.float32)
    env = np.convolve(np.abs(x), np.ones(int(sr * 0.02)) / int(sr * 0.02), mode="same")
    idx = np.where(env > max(0.006, float(np.percentile(env, 95)) * 0.06))[0]
    if len(idx) > 0:
        x = x[max(0, idx[0] - int(0.03 * sr)) : min(len(x), idx[-1] + int(0.09 * sr))]
    active = x[np.abs(x) > max(0.008, float(np.percentile(np.abs(x), 75)) * 0.25)]
    rms_cur = float(np.sqrt(np.mean(active ** 2))) if len(active) > 100 else float(np.sqrt(np.mean(x ** 2)))
    if rms_cur > 1e-5:
        x = x * (target_rms / rms_cur)
    x = 0.90 * np.tanh(x / 0.90)
    f = min(int(sr * 0.008), len(x) // 4)
    if f > 0:
        x[:f] *= np.linspace(0, 1, f, dtype=np.float32)
        x[-f:] *= np.linspace(1, 0, f, dtype=np.float32)
    return x


def to_wav_bytes(samples: np.ndarray, sr: int) -> bytes:
    pcm = (np.clip(samples, -1.0, 1.0) * 32767.0).astype(np.int16)
    buf = io.BytesIO()
    with wave.open(buf, "wb") as wf:
        wf.setnchannels(1)
        wf.setsampwidth(2)
        wf.setframerate(sr)
        wf.writeframes(pcm.tobytes())
    return buf.getvalue()


def synth_batch(language: str, anchor_text: str, lines: list[tuple[str, str]], out_dir: Path, base_seed: int):
    manifest_path = out_dir / "vo_manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8")) if manifest_path.exists() else {}

    # Generate a crisp ~4s narrator anchor first so cloning is rock-solid on every line
    r0 = requests.post(
        API_URL,
        data={
            "text": anchor_text,
            "language": language,
            "instruct": "male, middle-aged, low pitch",
            "effect_preset": "raw",
            "num_step": "28",
            "guidance_scale": "2.0",
            "speed": "1.03",
            "seed": str(base_seed),
            "denoise": "true",
            "pronounce": "true",
        },
        timeout=120,
    )
    assert r0.status_code == 200 and r0.content.startswith(b"RIFF")
    a_samp, a_sr = read_wav_bytes(r0.content)
    a_samp = master_array(a_samp, a_sr)
    anchor_bytes = to_wav_bytes(a_samp, a_sr)
    print(f"[{language[:2].upper()} ANCHOR] {len(a_samp)/a_sr:.2f}s", flush=True)

    for idx, (clip_id, text) in enumerate(lines):
        ok = False
        min_dur = max(3.5, len(text) * 0.032)
        for attempt in range(5):
            data = {
                "text": text,
                "language": language,
                "effect_preset": "raw",
                "num_step": "28",
                "guidance_scale": "2.0",
                "speed": "1.04",
                "seed": str(base_seed + 17 + idx * 19 + attempt * 131),
                "denoise": "true",
                "pronounce": "true",
            }
            files = None
            if attempt < 3:
                data["ref_text"] = anchor_text
                files = {"ref_audio": ("narrator_anchor.wav", anchor_bytes, "audio/wav")}
            else:
                data["instruct"] = "male, middle-aged, low pitch"
            r = requests.post(API_URL, data=data, files=files, timeout=120)
            assert r.status_code == 200 and r.content.startswith(b"RIFF"), f"{clip_id} HTTP {r.status_code}"
            samples, sr = read_wav_bytes(r.content)
            samples = master_array(samples, sr)
            dur = len(samples) / float(sr)
            rms = float(np.sqrt(np.mean(samples ** 2)))
            if dur >= min_dur and rms >= 0.08:
                ok = True
                break
            print(f"  retry {clip_id} attempt={attempt} dur={dur:.2f}s < {min_dur:.2f}s", flush=True)
        assert ok, f"Failed quality gate for {language} {clip_id}: dur={dur:.2f}s rms={rms:.3f}"
        out = out_dir / f"{clip_id}.wav"
        out.write_bytes(to_wav_bytes(samples, sr))
        manifest[clip_id] = {
            "character": "narrator",
            "duration": round(dur, 3),
            "rms": round(rms, 4),
            "sr": sr,
            "bytes": out.stat().st_size,
            "text": text,
        }
        print(f"[{language[:2].upper()} {idx + 1}/8] OK {clip_id} | {dur:5.2f}s | rms={rms:.3f}", flush=True)

    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Updated {manifest_path} -> total clips: {len(manifest)}")


def main() -> int:
    for _ in range(45):
        try:
            if requests.get("http://127.0.0.1:3900/health", timeout=2).status_code == 200:
                break
        except Exception:
            pass
        time.sleep(1)

    synth_batch("Russian", ANCHOR_RU, COMIC_LINES_RU, RU_DIR, base_seed=7901)
    synth_batch("English", ANCHOR_EN, COMIC_LINES_EN, EN_DIR, base_seed=8901)
    print("ALL 16 SURREAL COMIC NARRATION CLIPS (8 RU + 8 EN) GENERATED!")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
