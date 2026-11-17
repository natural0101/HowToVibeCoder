import os
import shutil
import sqlite3
import subprocess
import time
import json
import datetime

# 1. Update global npm codex binary/shim to the latest Codex Desktop 0.169.0-alpha.11.1 engine
desktop_codex = r"C:\Users\se-20\AppData\Local\OpenAI\Codex\bin\c38161ad8494f88c\codex.exe"
npm_codex_dir = r"C:\Users\se-20\AppData\Roaming\npm\node_modules\@openai\codex\vendor\x86_64-pc-windows-msvc\codex"
if os.path.exists(desktop_codex):
    os.makedirs(npm_codex_dir, exist_ok=True)
    target_exe = os.path.join(npm_codex_dir, "codex.exe")
    try:
        shutil.copy2(desktop_codex, target_exe)
        print("Updated global npm codex.exe to 0.169.0-alpha.11.1!")
    except Exception as e:
        print("Note updating npm codex.exe:", e)

proj_dir = r"C:\Users\se-20\.gemini\antigravity\scratch\HowToVibeCoder"
prompts_dir = os.path.join(proj_dir, "codex_7_chats")
os.makedirs(prompts_dir, exist_ok=True)

chats = [
    (1, "Цех 1: Этаж Белая Акула (Лифт в центре, развилки и 75+ предметов)", "gpt-6.2-astra", """Ты — Цех 1 (Левел-дизайн этажа в духе спецоперации «Белая Акула» из Warface) в проекте C:\\Users\\se-20\\.gemini\\antigravity\\scratch\\HowToVibeCoder.
Проверь `LevelAndEntities.cs` (масштаб этажа 76x76 м, бронированная шахта Центрального Лифта 9x9 м прямо в центре этажа, развилки влево в Западный опенспейс и вправо в Восточную серверную, 75+ детализированных 3D офисных предметов: кубиклы, столы с мониторами, ксероксы, шкафы, доски, кулеры, диваны, фикусы). Проверь целостность кода и дай краткий отчёт по готовым объектам."""),

    (2, "Цех 2: Оружие 90-х (Анимация ремня с пряжкой, паяльник с дымом, УЗИ)", "gpt-6.2-astra", """Ты — Цех 2 (Оружие 90-х, уникальные 3D-анимации и стрельба) в проекте C:\\Users\\se-20\\.gemini\\antigravity\\scratch\\HowToVibeCoder.
Проверь `WeaponAndCombatView.cs`: руки коммерса в малиновом пиджаке с золотой печаткой, анимацию замаха и удара Отцовского ремня с золотой пряжкой и 3D-дугой рассечения, анимацию выпада раскалённым Паяльником из 90-х с постоянным дымком канифоли и снопом искр припоя, стрельбу Micro-Uzi с гильзами и трассерами, Обрез и Секретный Чат-Ган. Проверь и дай отчёт."""),

    (3, "Цех 3: Звуки ремня и паяльника, синтвейв 90-х и озвучка фраз", "gpt-6.2-sol", """Ты — Цех 3 (Аудио-движок, процедурные WAV-звуки оружия, музыка 90-х и голоса) в проекте C:\\Users\\se-20\\.gemini\\antigravity\\scratch\\HowToVibeCoder.
Проверь `AudioMusicSystem.cs`: генерацию 15 WAV-звуков (`belt_whip.wav` со свистом и шлепком кожи + звоном пряжки, `solder_sizzle.wav` с шипением канифоли и треском разряда, `uzi_shot.wav`, `shotgun_blast.wav`, `hitmarker.wav`, `music_office_90s.wav`, `music_boss_fight.wav` и 4 голосовых тембра). Проверь и дай отчёт."""),

    (4, "Цех 4: Внутриигровой лор, слитые чаты [N], микшер [F8] и кейсы [K]", "gpt-6.2-sol", """Ты — Цех 4 (Внутриигровые окна: Слив чатов опенспейса [N], Аудио-микшер [F8] и Рулетка скинов [K]) в проекте C:\\Users\\se-20\\.gemini\\antigravity\\scratch\\HowToVibeCoder.
Проверь `LoreAndMenusUI.cs`: 4 вкладки лора и переписок Slack/TG на [N], микшер громкости и настроек на [F8] и рулетку 12 скинов на [K] со звуком трещотки. Проверь и дай отчёт."""),

    (5, "Цех 5: Классическое Главное Меню, сохранения в 3 слота и Моды", "gpt-6.2-astra", """Ты — Цех 5 (Классическое Главное Меню ПК-игры, система сохранений и менеджер модов) в проекте C:\\Users\\se-20\\.gemini\\antigravity\\scratch\\HowToVibeCoder.
Проверь `MainMenuAndPauseUI.cs`: классические кнопки Главного Меню (`НОВАЯ ИГРА`, `ЗАГРУЗИТЬ ИГРУ` на 3 слота в `saves/*.json`, `НАСТРОЙКИ`, `МОДЫ`, `ВЫХОД`) и Меню Паузы с `СОХРАНИТЬ ИГРУ`. Проверь и дай отчёт."""),

    (6, "Цех 6: Главный интегратор Program.cs, сборка и 8 скриншотов --autotest", "gpt-6.2-sol", """Ты — Цех 6 (Главный Интегратор `Program.cs`, компиляция и живой `--autotest`) в проекте C:\\Users\\se-20\\.gemini\\antigravity\\scratch\\HowToVibeCoder.
1. Напиши полный рабочий `Program.cs`, который связывает `LevelAndEntities.cs`, `WeaponAndCombatView.cs`, `AudioMusicSystem.cs`, `LoreAndMenusUI.cs` и `MainMenuAndPauseUI.cs`:
   - Загружает шрифт `C:\\Windows\\Fonts\\arialbd.ttf` с поддержкой ASCII + Кириллицы (0x0020..0x007F, 0x0400..0x04FF, 0x2010..0x2030, 0x2116, 0x2190..0x21FF) и иконку окна `assets/app_icon.png`.
   - Реализует полный геймплей 1-го этажа (WASD + мышь, засада C, действие E, оружие 1..4, кейсы K, чаты и лор N, микшер F8, быстрые $2000 F9, пауза ESC).
   - Реализует флаг `--autotest`, который открывает 3D-окно 1280x720, выставляет сочные ракурсы (включая вид на Центральную Шахту Лифта в центре этажа, развилки влево и вправо и десятки офисных предметов!) и сохраняет все 8 реальных PNG-скриншотов в `screenshots/`:
     * `screenshots/00_main_menu_classic.png`
     * `screenshots/01_office_hub_hr_tarot.png`
     * `screenshots/02_macbook_trap_ambush.png`
     * `screenshots/03_uzi_combat_saas_miniboss.png`
     * `screenshots/04_hr_tarot_payout_and_galley.png`
     * `screenshots/05_elevator_boss_escaped_agent.png`
     * `screenshots/06_lore_chats_window_n.png`
     * `screenshots/07_settings_mixer_and_mods.png`
2. Запусти `dotnet build -c Release`, исправь любые ошибки компиляции, запусти `bin\\Release\\net10.0\\HowToVibeCoder.exe --autotest` и убедись, что все 8 скриншотов созданы!"""),

    (7, "Цех 7: Графический установщик HowToVibeCoder_Setup.exe с AAA-иконкой и GitHub", "gpt-6.2-sol", """Ты — Цех 7 (Графический установщик Windows `HowToVibeCoder_Setup.exe` и публикация в GitHub) в проекте C:\\Users\\se-20\\.gemini\\antigravity\\scratch\\HowToVibeCoder.
1. Обнови `Installer\\SetupProgram.cs` так, чтобы установщик использовал новую 217 КБ AAA-иконку `Installer\\HowToVibeCoder.ico` и рисовал новый баннер `assets\\menu_banner.png` в верхней части окна установщика, а при создании ярлыка на Рабочем столе привязывал `HowToVibeCoder.ico` и вызывал `ie4uinit.exe -show`.
2. Как только `bin\\Release\\net10.0\\HowToVibeCoder.exe` будет собран, запусти `powershell -ExecutionPolicy Bypass -File Installer\\BuildInstaller.ps1`, чтобы обновить `C:\\Users\\se-20\\OneDrive\\Рабочий стол\\HowToVibeCoder_Setup.exe` и `HowToVibeCoder.bat`.""")
]

procs = []
for num, title, model, prompt in chats:
    pf = os.path.join(prompts_dir, f"chat_{num}.txt")
    out_f = os.path.join(prompts_dir, f"chat_{num}_out.log")
    with open(pf, "w", encoding="utf-8") as f:
        f.write(prompt)
    fin = open(pf, "r", encoding="utf-8")
    fout = open(out_f, "w", encoding="utf-8")
    cmd = [
        desktop_codex,
        "exec",
        "--thread-source", "user",
        "--trust",
        "-m", model,
        "-c", 'model_reasoning_effort="medium"',
        "--dangerously-bypass-approvals-and-sandbox",
        "-C", proj_dir,
        "-"
    ]
    p = subprocess.Popen(cmd, stdin=fin, stdout=fout, stderr=subprocess.STDOUT)
    procs.append((num, title, model, p, fin, fout, out_f))
    print(f"Started Codex Desktop Chat #{num}: {title} (PID {p.pid}, model={model})")

time.sleep(6)

# Register project and make all 7 new threads visible in Codex Desktop App sidebar (source='vscode', originator='Codex Desktop')
db_path = r"C:\Users\se-20\.codex\state_5.sqlite"
idx_path = r"C:\Users\se-20\.codex\session_index.jsonl"

conn = sqlite3.connect(db_path, timeout=20)
conn.row_factory = sqlite3.Row
cur = conn.cursor()

now_ms = int(time.time() * 1000)
proj_id = "01a1ebc0-0000-7000-8000-000000000777"
cur.execute(
    "INSERT OR REPLACE INTO projects (id, name, metadata, position, created_at_ms, updated_at_ms) VALUES (?, ?, ?, 0, ?, ?)",
    (proj_id, "HowToVibeCoder (Этаж 1: Белая Акула)", "{}", now_ms, now_ms)
)
cur.execute(
    "INSERT OR REPLACE INTO project_roots (project_id, position, path) VALUES (?, 0, ?)",
    (proj_id, proj_dir)
)

rows = cur.execute(
    "SELECT id, first_user_message, created_at FROM threads WHERE cwd LIKE '%HowToVibeCoder%' ORDER BY created_at_ms DESC LIMIT 10"
).fetchall()

updated_ids = []
for r in rows:
    tid = r["id"]
    msg = r["first_user_message"] or ""
    matched_title = None
    for num, title, _, _ in chats:
        if f"Цех {num}" in msg:
            matched_title = title
            break
    if matched_title:
        cur.execute(
            "UPDATE threads SET source='vscode', originator='Codex Desktop', thread_source='user', history_mode='paginated', name=?, title=?, project_id=?, archived=0 WHERE id=?",
            (matched_title, matched_title, proj_id, tid)
        )
        updated_ids.append((tid, matched_title))

conn.commit()
conn.close()

if updated_ids:
    with open(idx_path, "a", encoding="utf-8") as idxf:
        iso_now = datetime.datetime.now(datetime.timezone.utc).isoformat().replace("+00:00", "Z")
        for tid, mtitle in updated_ids:
            idxf.write(json.dumps({"id": tid, "thread_name": mtitle, "updated_at": iso_now}, ensure_ascii=False) + "\n")
    print(f"Registered {len(updated_ids)} visible chats in Codex Desktop App sidebar!")
