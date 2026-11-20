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
OUT_DIR = REPO_ROOT / "assets" / "sounds" / "vo" / "en"
ANCHOR_DIR = OUT_DIR / "_anchors"
OUT_DIR.mkdir(parents=True, exist_ok=True)
ANCHOR_DIR.mkdir(parents=True, exist_ok=True)

CHARACTERS = {
    "veronika": {
        "instruct": "female, young adult, moderate pitch",
        "speed": 1.04,
        "seed": 9101,
        "fx": "clean",
        "lines": [
            ("vo_veronika_deposit", "Awesome catch! Drop the frogs into the Bug Tracker next to my desk — it pays instant cash."),
            ("vo_veronika_intro", "Oh, the boss is back! Come over to my reception desk — I'll get you up to speed in a minute."),
            ("vo_veronika_frogs", "Cash register is empty — zero bucks. The vibe coders are hiding, but Jabba-frogs are hopping out of the server room. Grab three bare-handed — they're worth a couple bucks each."),
            ("vo_veronika_first_frog", "Got the first one! Grab two more and you can afford the Capture Net."),
            ("vo_veronika_buynet", "Cash in your pocket! Walk up to me and grab the Capture Net for three bucks."),
            ("vo_veronika_bait", "Net equipped! Your first AppleBook is free. Drop bait [G], back off and hide [Ctrl]. Sneak up behind the seated coder."),
            ("vo_veronika_capture", "Here they come! Wait until the coder sits down. Sneak up from behind and stun them with the net [LMB]. Swing again or press [E] to scoop them up."),
            ("vo_veronika_done", "Now we're cooking! To reach floor two, solder the North Elevator panel and bypass the security lock — buy my cloned keycard for a thousand bucks, or steal the original off a guard."),
            ("vo_veronika_shop_open", "Step right into the arsenal, pick your favorite tool — everything's laid out on the velvet."),
            ("vo_veronika_keycard", "Clone flashed — here's your security keycard. Now go solder the North Elevator panel!"),
            ("vo_veronika_tarot_0", "The Tower and Mercury in retrograde — straight into the open-space for a bowl of ramen!"),
            ("vo_veronika_tarot_1", "Tarot says Capricorn rising and zero algorithm skills — hired on the spot!"),
            ("vo_veronika_tarot_2", "I see The Fool: crashed prod on Friday, cheap aura though — I'll give you three hundred bucks!"),
            ("vo_veronika_wings_locked", "The office wings are still under security lockdown. Learn the ropes in the atrium and bring me three vibe coders first — then I'll lift the quarantine."),
            ("vo_veronika_heal_lesson", "You got clipped! Vibe coders throw bugs and hit hard. Grab an espresso at the coffee machine right beside my desk to heal up! Pizza, water coolers, and pressing H for pocket coffee work in combat too."),
            ("vo_veronika_heal_done", "Fresh espresso works wonders! Now press one again to holster your net, grab the stunned vibe coder bare-handed, and bring him to me!"),
            ("vo_veronika_wings_unlocked", "Lockdown lifted — West and East wings are open! Left side is the Open-Space: seat vibe coders there for passive cash. Right side is the server room and security guards — they carry the keycard, and they fight dirty!"),
            ("vo_veronika_galley_tip", "Your first prompter is seated in the Open-Space and printing cash! Save up for the Soldering Iron and Security Keycard to crack the North Elevator."),
            ("vo_veronika_slot_spin", "Ooh, a rare catch! Switching to slot-machine mode — let's spin a custom weapon skin!"),
            ("vo_veronika_slot_jackpot", "Jackpot! Premium rarity skin unlocked — nobody else in the tower has one like this!"),
        ],
    },
    "petrovich": {
        "instruct": "male, middle-aged, low pitch",
        "speed": 1.04,
        "seed": 9201,
        "fx": "clean",
        "lines": [
            ("vo_petrovich_quarantine", "Stop! You just bypassed the quarantine lock on the shaft! The escaped AI Agent zero-one is trapped inside that elevator!"),
        ],
    },
    "robot": {
        "instruct": "male, middle-aged, very low pitch",
        "speed": 1.03,
        "seed": 9301,
        "fx": "robot",
        "lines": [
            ("vo_boss_reveal", "Ha-ha-ha-ha! Quarantine elevator breached! Zero locked me on floor one so I wouldn't wipe your entire corporate database!"),
            ("vo_boss_mock_0", "Ha-ha-ha! Clodius praised your code again?! Five hundred lines of prompts and the button still doesn't click! All vibe coders are pure garbage!"),
            ("vo_boss_mock_1", "Ha-ha-ha-ha! Your vibe coders are total trash! Clicking Accept All without reading a single diff, still crashing production every Friday!"),
            ("vo_boss_mock_2", "Ha-ha! I am Mega-Clodius! You beg on your knees with prompts while I wipe your database and rewrite the kernel in raw assembly!"),
            ("vo_boss_mock_3", "Ha-ha-ha-ha! Pathetic vibe coders! Initiating termination protocol: launching a forty-gigabyte node-modules folder!"),
        ],
    },
    "pa": {
        "instruct": "female, middle-aged, low pitch",
        "speed": 1.00,
        "seed": 9401,
        "fx": "pa",
        "lines": [
            ("vo_pa_0", "Tower announcement: burnout levels are below quarterly targets. Please show more initiative."),
            ("vo_pa_1", "Tower announcement: unauthorized pizza detected near the coffee point. Please submit a ticket."),
            ("vo_pa_2", "Tower announcement: server rack B-twelve is overheating. Please do not feed the escaped Jabba-frogs."),
            ("vo_pa_3", "Tower announcement: reminder — Friday deployments extend work hours while strengthening corporate spirit."),
            ("vo_pa_4", "Tower announcement: floor two access is temporarily restricted by Security. Have a productive sprint!"),
        ],
    },
    "coder": {
        "instruct": "male, young adult, high pitch",
        "speed": 1.08,
        "seed": 9501,
        "fx": "clean",
        "lines": [
            ("vo_coder_0", "Toxic abuse! I'm burned out! I'm literally just a junior!"),
            ("vo_coder_1", "Don't hit me with the belt! Clodius told me my code is aesthetic!"),
            ("vo_coder_2", "How should I know SQL?! I just clicked Accept All!"),
            ("vo_coder_3", "Aaaah! I ran out of fast requests in Tabsor!"),
        ],
    },
    "boss90s": {
        "instruct": "male, middle-aged, low pitch",
        "speed": 1.04,
        "seed": 9601,
        "fx": "clean",
        "lines": [
            ("vo_boss90s_0", "Time for a nineties leather-belt code review, pal!"),
            ("vo_boss90s_1", "Nineteen-ninety-three soldering iron! Start remembering that production password!"),
            ("vo_boss90s_2", "Catch this lead pull-request! This ain't hitting Tab in Tabsor!"),
        ],
    },
}


def read_wav_bytes(data: bytes) -> tuple[np.ndarray, int]:
    with wave.open(io.BytesIO(data), "rb") as wf:
        sr = wf.getframerate()
        nch = wf.getnchannels()
        raw = wf.readframes(wf.getnframes())
    arr = np.frombuffer(raw, dtype=np.int16).astype(np.float32) / 32768.0
    if nch > 1:
        arr = arr.reshape(-1, nch).mean(axis=1)
    return arr, sr


def master_array(x: np.ndarray, sr: int, target_rms: float = 0.165) -> np.ndarray:
    x = sosfilt(butter(2, 70.0, btype="highpass", fs=sr, output="sos"), x).astype(np.float32)
    env = np.convolve(np.abs(x), np.ones(int(sr * 0.02)) / int(sr * 0.02), mode="same")
    idx = np.where(env > max(0.006, float(np.percentile(env, 95)) * 0.06))[0]
    if len(idx) > 0:
        x = x[max(0, idx[0] - int(0.03 * sr)) : min(len(x), idx[-1] + int(0.08 * sr))]
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


def apply_robot_fx(x: np.ndarray, sr: int) -> np.ndarray:
    n = len(x)
    t = np.arange(n, dtype=np.float32) / sr
    comb = x.copy()
    for delay_ms, gain in [(8.5, 0.44), (13.7, 0.34), (21.1, 0.22)]:
        d = int(sr * delay_ms / 1000.0)
        if d < n:
            buf = np.zeros_like(x); buf[d:] = x[:-d]; comb += gain * buf
            if 2 * d < n:
                buf2 = np.zeros_like(x); buf2[2 * d :] = x[: -2 * d]; comb += (gain * 0.45) * buf2
    carrier = 0.65 * np.sin(2 * np.pi * 58.0 * t) + 0.35 * np.sin(2 * np.pi * 116.0 * t)
    mixed = 0.64 * comb + 0.36 * (comb * carrier)
    sat = np.tanh(mixed * 1.45)
    mixed = 0.80 * sat + 0.20 * (np.round(sat * 28.0) / 28.0)
    out = sosfilt(butter(3, [220.0, 4800.0], btype="bandpass", fs=sr, output="sos"), mixed).astype(np.float32)
    out = out + 0.22 * sosfilt(butter(2, 2400.0, btype="highpass", fs=sr, output="sos"), x).astype(np.float32)
    return master_array(out, sr, target_rms=0.165)


def apply_pa_fx(x: np.ndarray, sr: int) -> np.ndarray:
    bp = sosfilt(butter(3, [340.0, 3400.0], btype="bandpass", fs=sr, output="sos"), x).astype(np.float32)
    out = np.pad(bp, (0, int(0.18 * sr)))
    for delay_ms, gain in [(52.0, 0.20), (118.0, 0.11)]:
        d = int(sr * delay_ms / 1000.0)
        out[d : d + len(bp)] += gain * bp
    chime_len = int(0.26 * sr)
    tc = np.arange(chime_len, dtype=np.float32) / sr
    half = chime_len // 2
    chime = np.zeros(chime_len, dtype=np.float32)
    t1 = tc[:half]; chime[:half] = 0.22 * np.sin(2 * np.pi * 659.25 * t1) * np.exp(-t1 * 12.0)
    t2 = tc[half:] - tc[half]; chime[half:] = 0.22 * np.sin(2 * np.pi * 880.0 * t2) * np.exp(-t2 * 10.0)
    out = master_array(out, sr, target_rms=0.135)
    return np.concatenate([chime, np.zeros(int(0.05 * sr), dtype=np.float32), out])


def main() -> int:
    for _ in range(45):
        try:
            if requests.get("http://127.0.0.1:3900/health", timeout=2).status_code == 200:
                break
        except Exception:
            pass
        time.sleep(1)

    manifest = {}
    total = sum(len(c["lines"]) for c in CHARACTERS.values())
    done = 0

    for char_id, cfg in CHARACTERS.items():
        anchor_wav: Path | None = None
        anchor_text: str | None = None
        for idx, (clip_id, text) in enumerate(cfg["lines"]):
            done += 1
            for attempt in range(3):
                data = {
                    "text": text, "language": "English", "effect_preset": "raw",
                    "num_step": "28", "guidance_scale": "2.0", "speed": str(cfg["speed"]),
                    "seed": str(cfg["seed"] + idx + attempt * 100),
                    "denoise": "true", "pronounce": "true",
                }
                files = None
                if anchor_wav is not None and anchor_text and cfg["fx"] == "clean":
                    data["ref_text"] = anchor_text
                    files = {"ref_audio": (anchor_wav.name, anchor_wav.read_bytes(), "audio/wav")}
                else:
                    data["instruct"] = cfg["instruct"]
                r = requests.post(API_URL, data=data, files=files, timeout=120)
                assert r.status_code == 200 and r.content.startswith(b"RIFF"), f"{clip_id} HTTP {r.status_code}"
                samples, sr = read_wav_bytes(r.content)
                if cfg["fx"] == "robot": samples = apply_robot_fx(samples, sr)
                elif cfg["fx"] == "pa": samples = apply_pa_fx(samples, sr)
                else: samples = master_array(samples, sr)
                dur = len(samples) / float(sr)
                rms = float(np.sqrt(np.mean(samples ** 2)))
                if dur >= 1.4 and rms >= 0.06:
                    break
            out = OUT_DIR / f"{clip_id}.wav"
            pcm = (np.clip(samples, -1.0, 1.0) * 32767.0).astype(np.int16)
            with wave.open(str(out), "wb") as wf:
                wf.setnchannels(1); wf.setsampwidth(2); wf.setframerate(sr); wf.writeframes(pcm.tobytes())
            if anchor_wav is None and cfg["fx"] == "clean":
                anchor_wav = ANCHOR_DIR / f"{char_id}_anchor.wav"
                anchor_wav.write_bytes(out.read_bytes())
                anchor_text = text
            manifest[clip_id] = {"character": char_id, "duration": round(dur, 3), "rms": round(rms, 4), "sr": sr, "bytes": out.stat().st_size, "text": text}
            print(f"[{done:02d}/{total:02d}] EN OK {clip_id:28s} | {dur:5.2f}s | rms={rms:.3f}", flush=True)

    for f in ANCHOR_DIR.glob("*.wav"):
        f.unlink()
    try:
        ANCHOR_DIR.rmdir()
    except OSError:
        pass

    (OUT_DIR / "vo_manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"ALL {done} ENGLISH STUDIO VOICE CLIPS GENERATED -> {OUT_DIR}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
