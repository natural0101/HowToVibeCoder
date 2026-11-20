"""Generate 8 RU + 8 EN studio-mastered intro comic narration clips via VoiceStudio OmniVoice CUDA."""
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

COMIC_LINES_RU = [
    ("vo_comic_1", "Две тысячи двадцать шестой год. Башня Клауд Энтропик. Старые методы возвращаются."),
    ("vo_comic_2", "Офис захватили вайбкодеры. Код не читают — жмут акцепт олл, и прод горит синим пламенем."),
    ("vo_comic_3", "Касса на нуле. Но эйчар Вероника знает схему: лови их, сажай в опенспейс и выбивай кэш."),
    ("vo_comic_4", "А за запертым Северным лифтом затаился кое-кто пострашнее... Пора наводить порядок на первом этаже!"),
    ("vo_comic_5", "Ещё в девяносто восьмом сисадмин Петрович собрал Джаба-жаб для отладки. Теперь они скачут по всему залу."),
    ("vo_comic_6", "Включи халявный вай-фай на ЯблоБуке в центре — вайбкодеры сами сбегутся на приманку."),
    ("vo_comic_7", "Сдавай редких кадров Веронике — крути скины на её автомате и снимай карантин с опенспейса."),
    ("vo_comic_8", "Первый этаж. Ноль долларов в кармане. Добро пожаловать в Энтропик."),
]

COMIC_LINES_EN = [
    ("vo_comic_1", "Twenty twenty-six. Cloud Entropic Tower. Old-school methods are back."),
    ("vo_comic_2", "Vibe coders overran the floor. Never read the code — just spam Accept All while production burns."),
    ("vo_comic_3", "The register's at zero. But HR Veronika has a plan: catch them, seat them in the open-space, and print cash."),
    ("vo_comic_4", "And behind the sealed North Elevator lurks something far worse... Time to clean up Floor One!"),
    ("vo_comic_5", "Back in ninety-eight, sysadmin Petrovich built Jabba-frogs to debug servers. Now they're hopping everywhere."),
    ("vo_comic_6", "Flip on free Wi-Fi at the AppleBook in the center — vibe coders will swarm straight to the bait."),
    ("vo_comic_7", "Bring rare catches to Veronika — spin weapon skins on her slot head and lift the open-space lockdown."),
    ("vo_comic_8", "Floor One. Zero bucks in your pocket. Welcome to Entropic Corp."),
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


def synth_batch(language: str, lines: list[tuple[str, str]], out_dir: Path, base_seed: int):
    manifest_path = out_dir / "vo_manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8")) if manifest_path.exists() else {}
    anchor_bytes = None
    anchor_text = None

    for idx, (clip_id, text) in enumerate(lines):
        ok = False
        for attempt in range(4):
            data = {
                "text": text,
                "language": language,
                "effect_preset": "raw",
                "num_step": "28",
                "guidance_scale": "2.0",
                "speed": "1.02",
                "seed": str(base_seed + idx * 11 + attempt * 100),
                "denoise": "true",
                "pronounce": "true",
            }
            files = None
            if anchor_bytes is not None and anchor_text:
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
            if dur >= 1.8 and rms >= 0.08:
                ok = True
                break
        assert ok, f"Failed quality gate for {language} {clip_id}: dur={dur:.2f}s rms={rms:.3f}"
        out = out_dir / f"{clip_id}.wav"
        pcm = (np.clip(samples, -1.0, 1.0) * 32767.0).astype(np.int16)
        with wave.open(str(out), "wb") as wf:
            wf.setnchannels(1)
            wf.setsampwidth(2)
            wf.setframerate(sr)
            wf.writeframes(pcm.tobytes())
        if anchor_bytes is None:
            anchor_bytes = out.read_bytes()
            anchor_text = text
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
    for _ in range(30):
        try:
            if requests.get("http://127.0.0.1:3900/health", timeout=2).status_code == 200:
                break
        except Exception:
            pass
        time.sleep(1)

    synth_batch("Russian", COMIC_LINES_RU, RU_DIR, base_seed=7701)
    synth_batch("English", COMIC_LINES_EN, EN_DIR, base_seed=8801)
    print("ALL 16 COMIC NARRATION CLIPS (8 RU + 8 EN) GENERATED!")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
