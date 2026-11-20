# How to Vibe Coder — Project Handoff Brief

## 1. Workspace & Release Targets
- **Project Repo**: `C:\Users\se-20\.gemini\antigravity\scratch\HowToVibeCoder` (GitHub `origin/main`, clean build `0 errors / 0 warnings`).
- **Desktop Standalone Installer**: `C:\Users\se-20\OneDrive\Рабочий стол\HowToVibeCoder_Setup.exe` (rebuilt via `.\Installer\BuildInstaller.ps1`; `<OutputType>WinExe</OutputType>`, zero console window on launch).
- **Tech Stack**: C# (.NET 10, `Raylib-cs 7.0.1`, OpenGL 3.3 Core) + Blender 5.1 Python 3D pipeline + local VoiceStudio OmniVoice CUDA TTS.

## 2. Proven Studio Pipeline (Keep Using This!)
1. **C# / UI / Shaders / Tests**: Delegate heavy coding sprints to **OpenAI Codex CLI (`gpt-6.2-astra`)**:
   - Dynamically discover latest binary: `(Get-ChildItem "C:\Users\se-20\AppData\Local\OpenAI\Codex\bin" -Filter "codex.exe" -Recurse | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName`
   - Always verify muted: `dotnet build -c Release`, `--autotest`, `--grip-test`, `--coop-smoke`.
2. **3D Models & Viewmodel Grips**: Run a dedicated **Swarm** (`weapons-lead`, `menu-3d-lead`, `cc0-asset-lead`, `office-props-lead`, `critic-lead`) — zero `.cs` overlap with Codex!
   - Mandatory Raylib OpenGL rules for ALL GLBs: `GL_CULL_FACE` outward normals (`recalc_face_normals` + `use_backface_culling = True`), calibrated linear PNG values (`x ** 2.2` to avoid Raylib double-gamma washout), 24-ray `BVHTree` AO baked into `COLOR_0`, exact `verify_viewmodel_contacts.py` (`0` triangle overlap) for first-person weapons/hands.
3. **Art Direction First**: ALWAYS generate a visual concept/material backdrop via `image-generator` BEFORE coding any major UI screen or character.
4. **Studio Voice Acting (RU + EN)**: Local headless **VoiceStudio (`k2-fsa/OmniVoice`, RTX 3070 Ti CUDA)** on `http://127.0.0.1:3900`:
   - Python: `C:\AI\VoiceStudio\DesktopData\runtime\.python\cpython-3.11.16-windows-x86_64-none\python.exe`
   - `PYTHONPATH`: `C:\AI\VoiceStudio\DesktopData\runtime\project\.venv\Lib\site-packages;C:\AI\VoiceStudio\App\resources\backend;C:\AI\VoiceStudio\App\resources\omnivoice;C:\AI\VoiceStudio\App\resources`
   - Env: `OMNIVOICE_DATA_DIR=C:\AI\VoiceStudio\VoiceData`, `OMNIVOICE_CACHE_DIR=C:\AI\VoiceStudio\Models`, `HF_HOME=C:\AI\VoiceStudio\Models`, `HF_HUB_CACHE=C:\AI\VoiceStudio\Models`, Cwd: `C:\AI\VoiceStudio\App\resources\backend`, cmd: `-m uvicorn main:app --host 127.0.0.1 --port 3900`.
   - Always stop the VoiceStudio daemon after generating WAVs to free 5 GB VRAM!
   - Existing batch generators: `scripts/generate_studio_voices.py` (RU), `scripts/generate_english_voices.py` (EN), and `scripts/generate_comic_voices.py` (8 RU + 8 EN comic narration). Currently **46 RU + 46 EN** mastered studio clips verified!

## 3. Current Shipped State (Floor 1 — «Белая Акула IT» / Cloud Entropic Tower)
- **Animated 2-Page Graphic-Novel Intro Comic (`IntroComic.cs`, `assets/ui/comic_p1..8.png`)**:
  - Plays automatically on **New Game** (Continue skips straight into saved run; replayable anytime via Main Menu `Комикс / Intro Comic`).
  - 8 full-bleed hand-inked noir-satire panels across 2 archival cream comic pages with panel-by-panel spring-slam animation, Ken Burns camera drift, spotlight gold rim focus, smooth page-turn wipe + paper/slam SFX, face-safe RU/EN inked caption boxes, and full studio narration (`vo_comic_1..8` in RU & EN).
- **Visual Identity & Typography**: `RussoOne-Regular.ttf` (display/titles/stamps) + `Onest-Bold/SemiBold/Medium.ttf` (UI/HUD/subtitles). Zero AI-slop stripes, bars, `//`, or black SaaS modals.
- **Steam-Safe Parody Brands & Modern 2026 Slang** (NEVER revert to real trademarks or old slang!):
  - `ЯблоБук / AppleBook`, `Клодий / Clodius`, `Табсор / Tabsor`, `Джаба-жабы / Jabba-frogs`, `МегаОблако / MegaCloud`, `Чириктер / Chirper`, `Жира-трекер / Jeera-Tracker`, **`Опенспейс / Open-Space`** (NEVER «Галера» / «гребец» — use «Опенспейс» / «промптер»!).
- **Veronika's Arsenal Shop (`ShopBriefcase.cs`)**: Open dark-walnut & tufted-burgundy velvet 90s briefcase (`assets/ui/shop_briefcase_bg.png`), live transparent 3D hero weapon, brass stat bars, paper hang-tag price, embossed buy button, 2×3 cream catalog cards with real weapon icons & diagonal red `В АРСЕНАЛЕ / OWNED` stamps, 11 visible upgrades, mouse + keyboard support, tactile skin switcher swatches `[Q/R]`.
- **HR Veronika v3b + 3D Slot-Machine Skin Roulette (`WeaponSkins.cs`)**:
  - Live animated modular rig (`body`, `head`, `head_blink`, `arm_l`, `arm_r`, `tarot_table`) with large violet eyes, lush framing hair, connected arms, 3/4 warm portrait light.
  - Delivering ANY non-Common (`Uncommon`+) Vibe Coder transforms her head into `char_hr_veronika_slot_head.glb` + animated `char_hr_veronika_slot_lever.glb` and launches a walnut-brass CS-style Skin Roulette!
  - **12 custom shader weapon skins** across 4 rarities (including shiny **Premium Legendaries**: `Золотой Олигарх 90-х`, `Голографический Клодий`, `Драконий Деплой`, plus `Бензиновый Хром`, `Аметист Вероники`, `Кровавый Прод`, etc.) working on all 5 weapons in 1st-person POV, Briefcase Shop, and Roulette Reveal!
- **Hero Weapons & First-Person Grips**: Dark oiled gunmetal **Micro-Uzi v3** (`weapon_uzi_90s.glb`, `0` overlap), upright **hammer-fist Capture Net** (`fps_hands_net.glb` + 3-phase scoop swing), 90s Belt, Soldering Iron, 12G Sawed-Off, Bare-Hands 4-slot empty hotbar + holster-to-carry (`CarryHarness` upgrade).
- **Beginner Flow ("Линия вливания") & Office Healing**:
  - Act I locks all 8 West/East/North wing portals behind smoked security glass barriers until **3 Vibe Coders (`0/3`)** are delivered to Veronika (3rd Atrium coder guaranteed `Uncommon` -> triggers first Slot Roulette + unlocks wings!).
  - 8-beat voiced onboarding with 3D waypoints + mandatory Coffee Machine healing lesson.
  - 4 tactile healing methods: Espresso Machine (`+45 HP`), Pizza on tables (`+35 HP`), Water Coolers (`+25 HP`, weapon hit spills electro-puddle), Pocket Coffee `[H]` (`+30 HP`, starts with 2 cups).
  - Directional red hit arc + clear damage source labels.
- **Living Office Environment**: All 10 major office props rebuilt to high-craft beveled AO-baked GLBs (`prop_workstation_full`, `prop_server_rack`, `prop_whiteboard`, `prop_water_cooler`, `prop_xerox_printer`, `prop_macbook_trap_desk`, `prop_arsenal_counter`, `prop_vending_machine`, `prop_filing_cabinet`, `prop_trash_bin`), dusk city ground + lit avenues below skyscrapers, human-scale desks (`WorkstationScale = 1.10f`).
- **Full RU / EN Localization**: Live `Русский / English` switcher in Settings (Main Menu + Pause) switching all UI, HUD, 3D signs, comic captions, subtitles, and all **46 RU / 46 EN** studio voice clips without restart (`Loc.cs`).
- **Elevator Mystery & Boss**: Broken North Elevator requires Soldering Iron (`$90`) + Security Keycard (`$1000` clone or looted from SБ Guard / SaaS Guru), reveals 3-phase **AI Agent v0.1** (mocking metallic robot voice!) + Floor 1 Finale Choice.

## 4. Non-Negotiable Self-QA Rules
- Always inspect **real live in-game Raylib screenshots** (never trust Blender renders alone!) before showing the user.
- Reject any generic dark SaaS modal, blurry font, backface-culled mesh, or weak anatomy yourself before presenting.
- Keep responses to user in Russian Caveman Mode with zero technical dumps unless asked, and always provide clickable `file:///` links to Desktop installer & fresh screenshots.
