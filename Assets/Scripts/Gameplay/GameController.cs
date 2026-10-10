using System.Collections;
using System.IO;
using HonkAndLoad.Core;
using HonkAndLoad.Level;
using HonkAndLoad.UI;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace HonkAndLoad.Gameplay
{
    /// <summary>
    /// Oyunun ana döngüsü: bölüm yükler, dokunmaları okur, kuralları uygular,
    /// kazanma / kaybetme durumunu yönetir.
    /// </summary>
    public class GameController : MonoBehaviour
    {
        public enum Phase { Menu, Playing, Won, Lost }
        public enum GameMode { Adventure, Endless }

        public GameMode Mode { get; private set; }
        public EndlessDirector Endless { get; private set; }
        /// <summary>Sonsuz modda bu oyunda rekor kırıldı mı?</summary>
        public bool NewRecord { get; private set; }
        /// <summary>Oyun başlarken geçerli olan rekor (karşılaştırma için).</summary>
        public int RecordAtStart { get; private set; }

        public const int ExtraSlotsReward = 3;
        /// <summary>Kaybedince altınla devam fiyatı (+3 slot).</summary>
        public const int ReviveCoinPrice = 150;

        // Ekonomi
        /// <summary>Bu oyun/bölüm sonunda kazanılan altın (panelde gösterilir).</summary>
        public int LastReward { get; private set; }
        /// <summary>"Reklam izle, 3 katı" bu oyunda kullanıldı mı?</summary>
        public bool RewardBoosted { get; private set; }
        public int ExtraSlotsBought { get; private set; }
        private int _endlessCoinsGiven;
        private readonly System.Collections.Generic.List<BoardState> _history = new System.Collections.Generic.List<BoardState>();
        private const int MaxHistory = 30;
        private float _inputLockedUntil;
        private AdService _ads;

        public BoardState State { get; private set; }
        public int LevelNumber { get; private set; }
        public Phase CurrentPhase { get; private set; }
        public bool ExtraSlotsUsed { get; private set; }
        public int Moves { get; private set; }

        /// <summary>Son aşama değişikliğinden bu yana geçen süre (kazanma paneli gecikmesi için).</summary>
        public float PhaseTime { get; private set; }

        /// <summary>Ekran taraması sürüyor (günlük ödül penceresi kendiliğinden açılmasın).</summary>
        public bool Sweeping { get; private set; }

        public Vector3 LockBadgeWorld(int column) => _view.LockBadgeWorld(column);

        /// <summary>Yeni mekanik tanıtımı (ilk hamleye kadar gösterilir).</summary>
        public string IntroText { get; private set; }

        // İpucu / öğretici
        public bool IsTutorial => LevelNumber == 1 && (!Progress.TutorialDone || _forceTutorial);
        private bool _forceTutorial;
        public const float IdleHintSeconds = 7f;
        public bool HintVisible => !VideoMode && CurrentPhase == Phase.Playing && _hint.Valid && (IsTutorial || _idle > IdleHintSeconds);
        public Vector3 HintWorld => !_hint.Valid ? Vector3.zero
            : _hint.Kind == Tappable.TapKind.Column ? _view.ColumnFrontWorld(_hint.Index) : _view.BufferWorld(_hint.Index);
        public string HintText => IsTutorial && _hint.Valid ? _hint.Text : null;

        // Reklam videosu modu
        public const int VideoLevel = 10; // zor bölüm: raf kullanılır, gerilim olur
        public const int VideoFps = 30;
        public bool VideoMode { get; private set; }
        public float VideoTime { get; private set; }

        public enum VideoScenario { Adventure = 0, EndlessRecord = 1, EndlessFail = 2 }
        public VideoScenario Scenario { get; private set; }
        /// <summary>Kapanış kartı gösteriliyor mu, ne zamandır.</summary>
        public bool VideoEnding { get; private set; }
        public float VideoEndTime { get; private set; }
        public int VideoFinalScore { get; private set; }
        private VideoRecorder _recorder;

        private struct Hint
        {
            public bool Valid;
            public Tappable.TapKind Kind;
            public int Index;
            public string Text;
        }

        private Hint _hint;
        private float _idle;
        private Hud _hud;
        private BoardView _view;
        private Feedback _feedback;
        private Camera _camera;
        private LevelData _levelData;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            // Oyun açıkken ekran kararmasın; tek parmakla oynanır
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
#if ENABLE_LEGACY_INPUT_MANAGER
            Input.multiTouchEnabled = false;
#endif
            SetupCamera();
            SetupLight();
            _view = new GameObject("BoardView").AddComponent<BoardView>();
            _view.transform.SetParent(transform, false);
            _feedback = gameObject.AddComponent<Feedback>();
            _ads = gameObject.AddComponent<AdService>();
            _hud = gameObject.AddComponent<Hud>();
            _hud.Init(this);
        }

        private void Start()
        {
            ApplySettings();
#if UNITY_EDITOR
            // "Honk & Load → Reklam Videosu Kaydet" menüsünden başlatıldıysa
            bool record = UnityEditor.SessionState.GetBool("hal_record_video", false);
            bool preview = UnityEditor.SessionState.GetBool("hal_record_video_preview", false);
            UnityEditor.SessionState.SetBool("hal_record_video", false);
            UnityEditor.SessionState.SetBool("hal_record_video_preview", false);
            if (UnityEditor.SessionState.GetBool("hal_screen_sweep", false))
            {
                UnityEditor.SessionState.SetBool("hal_screen_sweep", false);
                // Ayrı nesnede çalışır: ShowMenu/StartLevel içindeki StopAllCoroutines onu durdurmasın
                CoroutineHost.Create("ScreenSweep").StartCoroutine(ScreenSweep());
                return;
            }
            if (record || preview)
            {
                var scenario = (VideoScenario)UnityEditor.SessionState.GetInt("hal_video_scenario", 0);
                StartVideoMode(record, scenario);
                return;
            }
#endif
            ShowMenu();
        }

        // ---------- Ekran taraması (yalnızca Editor) ----------

#if UNITY_EDITOR
        /// <summary>
        /// Farklı telefon ekranlarını kontrol etmek için: menü, öğretici, oyun, kayıp ve
        /// kazanma ekranlarının görüntüsünü Recordings/sweep/ altına kaydeder, sonra Play'den çıkar.
        /// Device Simulator'da hangi cihaz seçiliyse o cihazın ekranı ve çentiği kullanılır.
        /// </summary>
        private IEnumerator ScreenSweep()
        {
            string device = SystemInfo.deviceModel;
            foreach (char c in Path.GetInvalidFileNameChars()) device = device.Replace(c, '_');
            device = device.Replace(' ', '_');
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Recordings", "sweep"));
            Directory.CreateDirectory(folder);
            string prefix = Path.Combine(folder, $"{device}_{Screen.width}x{Screen.height}");
            Sweeping = true;

            // Yeni mekanikli bölümler çözülebilir mi?
            foreach (int n in new[] { 15, 20, 25, 26, 30, 40, 50, 60 })
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                LevelData d = LevelLoader.Load(n);
                long genMs = watch.ElapsedMilliseconds;
                LevelSolver.Result res = LevelSolver.Solve(d);
                int hidden = 0;
                foreach (ColumnData col in d.columns) hidden += col.hidden.Count;
                Debug.Log($"[Test] bölüm {n}: çözülebilir={res.Solvable} (düğüm {res.NodesExplored}), gizli {hidden}, " +
                          $"kilit [{string.Join(",", d.locks)}], üretim {genMs} ms");
                yield return null;
            }

            ShowMenu();
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot(prefix + "_1_menu.png");

            _hud.OpenShop();
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot(prefix + "_1b_market.png");
            _hud.CloseModals();

            _hud.OpenSettings();
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot(prefix + "_1c_ayarlar.png");
            _hud.CloseModals();

            _hud.OpenDaily();
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot(prefix + "_1d_gunluk.png");
            _hud.CloseModals();

            // Yeni mekanikler: gizli koliler (15) ve kilitli sütunlar (25)
            _forceIntro = true;
            StartLevel(15);
            yield return new WaitForSecondsRealtime(2.2f);
            yield return Shot(prefix + "_1e_gizli.png");
            StartLevel(25);
            yield return new WaitForSecondsRealtime(2.2f);
            yield return Shot(prefix + "_1f_kilit.png");
            _forceIntro = false;
            int departedBefore = State.DepartedTrucks;
            for (int i = 0; i < 60 && CurrentPhase == Phase.Playing && _hint.Valid && State.HasLocks; i++)
            {
                if (_hint.Kind == Tappable.TapKind.Column) HandleColumnTap(_hint.Index);
                else HandleBufferTap(_hint.Index);
                yield return new WaitForSecondsRealtime(0.12f);
            }
            Debug.Log($"[Test] kilit: giden kamyon {departedBefore} → {State.DepartedTrucks}, kilit kaldı mı: {State.HasLocks}, aşama {CurrentPhase}");
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Shot(prefix + "_1g_kilit_acildi.png");

            _forceTutorial = true;
            StartLevel(1);
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot(prefix + "_2_ogretici.png");
            _forceTutorial = false;

            StartLevel(12);
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot(prefix + "_3_oyun.png");

            _hud.OpenBuyDialog(BoosterType.Magnet);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot(prefix + "_3b_satinal.png");
            _hud.CloseModals();

            // Güçlendirici testi: her birini kullan, koli sayısının korunduğunu kontrol et
            foreach (BoosterType bt in new[] { BoosterType.Undo, BoosterType.Magnet, BoosterType.Shuffle, BoosterType.ExtraSlot })
                if (Economy.Count(bt) == 0) Economy.AddBooster(bt, 1);
            Debug.Log("[Test] başlangıç: " + CrateSummary());
            if (_hint.Valid) { if (_hint.Kind == Tappable.TapKind.Column) HandleColumnTap(_hint.Index); else HandleBufferTap(_hint.Index); }
            yield return new WaitForSecondsRealtime(0.5f);
            Debug.Log("[Test] 1 hamle: " + CrateSummary());
            Debug.Log("[Test] geri al: " + UseBooster(BoosterType.Undo) + " → " + CrateSummary());
            yield return new WaitForSecondsRealtime(0.5f);
            Debug.Log("[Test] mıknatıs: " + UseBooster(BoosterType.Magnet));
            yield return new WaitForSecondsRealtime(1.5f);
            Debug.Log("[Test] mıknatıs sonrası: " + CrateSummary());
            yield return Shot(prefix + "_3c_miknatis.png");
            Debug.Log("[Test] karıştır: " + UseBooster(BoosterType.Shuffle) + " → " + CrateSummary());
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot(prefix + "_3d_karistir.png");
            Debug.Log("[Test] +1 raf: " + UseBooster(BoosterType.ExtraSlot) + " raf=" + State.Buffer.Length);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot(prefix + "_3e_raf.png");

            // Kazanma ödülü: kolay bölümü ipucuyla bitir
            int coinsBefore = Economy.Coins;
            StartLevel(2);
            for (int i = 0; i < 200 && CurrentPhase == Phase.Playing && _hint.Valid; i++)
            {
                if (_hint.Kind == Tappable.TapKind.Column) HandleColumnTap(_hint.Index);
                else HandleBufferTap(_hint.Index);
                yield return new WaitForSecondsRealtime(0.05f);
            }
            Debug.Log($"[Test] bölüm 2: {CurrentPhase}, ödül {LastReward}, altın {coinsBefore} → {Economy.Coins}");
            yield return new WaitForSecondsRealtime(2.2f);
            yield return Shot(prefix + "_5b_odul.png");
            Progress.CurrentLevel = 11;

            SetPhase(Phase.Lost);
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot(prefix + "_4_kayip.png");

            SetPhase(Phase.Won);
            PhaseTime = 2f;
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot(prefix + "_5_kazanma.png");

            StartLevel(46); // en geniş tahta: 8 renk, 9 sütun, 8 slot raf
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot(prefix + "_6_genis.png");

            // Sonsuz mod: birkaç otomatik hamle, sonra oyun sonu paneli
            StartEndless();
            for (int i = 0; i < 12 && CurrentPhase == Phase.Playing && _hint.Valid; i++)
            {
                if (_hint.Kind == Tappable.TapKind.Column) HandleColumnTap(_hint.Index);
                else HandleBufferTap(_hint.Index);
                yield return new WaitForSecondsRealtime(0.15f);
            }
            _hud.Banner(Loc.F("Zorluk {0}!", 2), Loc.T("Koliler daha karışık geliyor"));
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot(prefix + "_7_sonsuz.png");

            SetPhase(Phase.Lost);
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot(prefix + "_8_sonsuz_bitis.png");

            // İngilizce görünüm
            Loc.Language savedLanguage = Loc.Setting;
            Loc.Setting = Loc.Language.English;
            ShowMenu();
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot(prefix + "_9_en_menu.png");
            _hud.OpenShop();
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot(prefix + "_9b_en_market.png");
            _hud.CloseModals();
            _hud.OpenSettings();
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot(prefix + "_9c_en_ayarlar.png");
            _hud.CloseModals();
            _forceTutorial = true;
            StartLevel(1);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot(prefix + "_9d_en_ogretici.png");
            _forceTutorial = false;
            SetPhase(Phase.Lost);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot(prefix + "_9e_en_kayip.png");
            Loc.Setting = savedLanguage;
            ShowMenu();

            Sweeping = false;
            Debug.Log($"[HonkAndLoad] Ekran taraması bitti: {prefix}_*.png");
            UnityEditor.EditorApplication.isPlaying = false;
        }

        /// <summary>Test: depo + raf + kamyonlardaki koli sayıları (renk başına).</summary>
        private string CrateSummary()
        {
            var counts = new System.Collections.Generic.SortedDictionary<int, int>();
            void Add(int c) { if (c >= 0) counts[c] = counts.TryGetValue(c, out int n) ? n + 1 : 1; }
            foreach (var col in State.Columns) foreach (int c in col) Add(c);
            foreach (int c in State.Buffer) Add(c);
            int loaded = 0;
            foreach (Truck t in State.Docks) if (t != null) loaded += t.Load;
            var sb = new System.Text.StringBuilder();
            foreach (var kv in counts) sb.Append($"{kv.Key}:{kv.Value} ");
            return $"{sb}| yüklü {loaded} | giden {State.DepartedTrucks} | raf {State.BufferUsed()}/{State.Buffer.Length}";
        }

        private static IEnumerator Shot(string path)
        {
            yield return new WaitForEndOfFrame();
            Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Destroy(tex);
        }
#endif

        // ---------- Reklam videosu ----------

        /// <summary>Arayüzsüz, kendi kendine oynayan mod. record=true ise kare kare kaydeder.</summary>
        public void StartVideoMode(bool record, VideoScenario scenario = VideoScenario.Adventure)
        {
            VideoMode = true;
            Hud.IsVideo = true;
            Scenario = scenario;
            VideoTime = 0f;
            VideoEnding = false;
            AudioListener.volume = 1f;
            if (record)
            {
                string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Recordings",
                    $"ad_{scenario}_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss")));
                _recorder = gameObject.AddComponent<VideoRecorder>();
                _recorder.Begin(folder, VideoFps);
            }

            switch (scenario)
            {
                case VideoScenario.EndlessRecord:
                    StartEndless(4242);
                    StartCoroutine(VideoBotEndlessRecord());
                    break;
                case VideoScenario.EndlessFail:
                    StartEndless(1717);
                    StartCoroutine(VideoBotEndlessFail());
                    break;
                default:
                    StartLevel(VideoLevel);
                    StartCoroutine(VideoBot());
                    break;
            }
        }

        // --- Video botu yardımcıları ---

        private int DirectColumn()
        {
            for (int c = 0; c < State.Columns.Count; c++)
            {
                int f = State.FrontCrate(c);
                if (f >= 0 && !State.IsLocked(c) && State.FindDockFor(f) >= 0) return c;
            }
            return -1;
        }

        private int CountDirect(int except)
        {
            int n = 0;
            for (int c = 0; c < State.Columns.Count; c++)
            {
                if (c == except) continue;
                int f = State.FrontCrate(c);
                if (f >= 0 && !State.IsLocked(c) && State.FindDockFor(f) >= 0) n++;
            }
            return n;
        }

        private IEnumerator BotTap(int column)
        {
            if (column < 0 || State == null) yield break;
            _hud.ShowTap(_view.ColumnFrontWorld(column));
            yield return new WaitForSeconds(0.1f);
            if (CurrentPhase == Phase.Playing) HandleColumnTap(column);
        }

        private int _bufferPeakShown;

        /// <summary>Raf doluluğuna göre altyazılar: "Raf doluyor!", "Son 1 yer!", "Kurtuldu!".</summary>
        private void VideoCaptions()
        {
            if (State == null) return;
            int used = State.BufferUsed(), size = State.Buffer.Length;
            if (used >= size - 1 && _bufferPeakShown < 2) { _hud.Caption(Loc.T("Son 1 yer!"), new Color(1f, 0.35f, 0.3f)); _bufferPeakShown = 2; }
            else if (used >= size - 2 && _bufferPeakShown < 1) { _hud.Caption(Loc.T("Raf doluyor!"), Palette.Warning); _bufferPeakShown = 1; }
            else if (used <= 1 && _bufferPeakShown >= 1)
            {
                _hud.Caption(Loc.T("Kurtuldu!"), new Color(0.45f, 1f, 0.55f));
                _bufferPeakShown = 0;
            }
        }

        private IEnumerator EndCard(float seconds)
        {
            VideoFinalScore = Endless != null ? Endless.Score : 0;
            VideoEnding = true;
            VideoEndTime = 0f;
            yield return new WaitForSeconds(seconds);
            FinishVideo();
        }

        /// <summary>Sonsuz – "Rekoru geç": hızlı oyun, gerilim, kurtuluş, skorla kapanış (~22 sn).</summary>
        private IEnumerator VideoBotEndlessRecord()
        {
            var rng = new System.Random(11);
            _bufferPeakShown = 0;
            yield return new WaitForSeconds(1.6f);

            // 1) Akıcı, kombolu oyun
            while (CurrentPhase == Phase.Playing && VideoTime < 9.0f)
            {
                yield return BotTap(LevelSolver.CarefulChoice(State, rng));
                VideoCaptions();
                yield return new WaitForSeconds(0.22f + (float)rng.NextDouble() * 0.1f);
            }

            // 2) Gerilim: sıradaki kamyonların kolilerini bilerek rafa koy
            while (CurrentPhase == Phase.Playing && VideoTime < 14.5f
                   && State.BufferUsed() < State.Buffer.Length - 1)
            {
                var upcoming = Endless.PeekUpcoming(2);
                int pick = -1;
                for (int c = 0; c < State.Columns.Count && pick < 0; c++)
                {
                    int f = State.FrontCrate(c);
                    if (f < 0 || State.FindDockFor(f) >= 0) continue;
                    if (!upcoming.Contains(f)) continue;
                    bool last = State.BufferUsed() == State.Buffer.Length - 2;
                    if (last && CountDirect(c) == 0) continue; // son yeri doldurup kilitlenme
                    pick = c;
                }
                if (pick < 0) pick = DirectColumn();
                if (pick < 0) break;
                yield return BotTap(pick);
                VideoCaptions();
                yield return new WaitForSeconds(0.5f);
            }

            // 3) Kurtuluş: doğrudan yüklemelerle kamyonları gönder, raf kendiliğinden boşalsın
            while (CurrentPhase == Phase.Playing && VideoTime < 20.5f)
            {
                int c = DirectColumn();
                if (c < 0)
                {
                    if (State.BufferUsed() >= State.Buffer.Length - 1) break; // kaybetmemek için dur
                    c = LevelSolver.CarefulChoice(State, rng);
                }
                yield return BotTap(c);
                VideoCaptions();
                yield return new WaitForSeconds(0.25f + (float)rng.NextDouble() * 0.1f);
            }

            yield return new WaitForSeconds(0.6f);
            yield return EndCard(3.8f);
        }

        /// <summary>Sonsuz – "Kaybetme": iyi başlar, dikkatsiz dokunuşlarla raf dolar (~15 sn).</summary>
        private IEnumerator VideoBotEndlessFail()
        {
            var rng = new System.Random(5);
            _bufferPeakShown = 0;
            yield return new WaitForSeconds(1.6f);

            while (CurrentPhase == Phase.Playing && VideoTime < 6.5f)
            {
                yield return BotTap(LevelSolver.CarefulChoice(State, rng));
                yield return new WaitForSeconds(0.25f);
            }

            // Dikkatsiz dokunuşlar: uymayan kolileri rafa at
            while (CurrentPhase == Phase.Playing && VideoTime < 25f)
            {
                int pick = -1;
                for (int c = 0; c < State.Columns.Count; c++)
                {
                    int f = State.FrontCrate(c);
                    if (f >= 0 && State.FindDockFor(f) < 0) { pick = c; break; }
                }
                if (pick < 0) pick = DirectColumn();
                bool lastSlot = State.BufferUsed() == State.Buffer.Length - 1;
                if (lastSlot) yield return new WaitForSeconds(0.9f); // son dokunuştan önce gerilim
                yield return BotTap(pick);
                VideoCaptions();
                if (CurrentPhase != Phase.Playing) break;
                yield return new WaitForSeconds(0.55f);
            }

            _hud.Caption(Loc.T("RAF DOLDU!"), new Color(1f, 0.3f, 0.3f));
            yield return new WaitForSeconds(1.4f);
            yield return EndCard(3.6f);
        }

        /// <summary>Macera: zor bölüm, altyazılar, konvoyla kapanış.</summary>
        private IEnumerator VideoBot()
        {
            var rng = new System.Random(7);
            _bufferPeakShown = 0;
            yield return new WaitForSeconds(1.8f); // açılış sorusu okunsun

            int safety = 0;
            while (CurrentPhase == Phase.Playing && _hint.Valid && safety++ < 500)
            {
                _hud.ShowTap(HintWorld);
                yield return new WaitForSeconds(0.1f);
                if (_hint.Kind == Tappable.TapKind.Column) HandleColumnTap(_hint.Index);
                else HandleBufferTap(_hint.Index);
                VideoCaptions();
                yield return new WaitForSeconds(0.26f + (float)rng.NextDouble() * 0.14f);
            }

            // Konvoy + kapanış ekranı
            yield return new WaitForSeconds(CurrentPhase == Phase.Won ? 5.2f : 1f);
            FinishVideo();
        }

        private void FinishVideo()
        {
            if (_recorder != null) _recorder.End();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        // ---------- Menü ----------

        /// <summary>Giriş ekranına dön. Oyun alanı temizlenir.</summary>
        public void ShowMenu()
        {
            _hud.CloseModals();
            SaveEndlessBest();
            StopAllCoroutines();
            _view.Clear();
            State = null;
            SetPhase(Phase.Menu);
        }

        /// <summary>Giriş ekranındaki "Oyna" butonu.</summary>
        public void Play() => StartLevel(Progress.CurrentLevel);

        public void ApplySettings()
        {
            AudioListener.volume = Progress.SoundOn ? 1f : 0f;
            _feedback.HapticsEnabled = Progress.HapticsOn;
        }

        // ---------- Bölüm akışı ----------

        public void StartLevel(int number)
        {
            Mode = GameMode.Adventure;
            LevelNumber = Mathf.Max(1, number);
            _levelData = LevelLoader.Load(LevelNumber);
            // Önce aç: güçlendirici çubuğu görünürse kamera ona göre sığdırılsın
            var unlocked = VideoMode ? null : Economy.CheckUnlocks(LevelNumber);
            RestartLevel();
            IntroText = VideoMode ? null : MechanicIntro();
            if (unlocked != null)
                foreach (Economy.BoosterInfo b in unlocked)
                    _hud.Banner(Loc.F("Yeni: {0}!", b.Name), Loc.F("{0} tane hediye. {1}", Economy.UnlockGift, b.Description));
        }

        public void RestartLevel()
        {
            if (Mode == GameMode.Endless)
            {
                StartEndless();
                return;
            }
            StopAllCoroutines();
            State = new BoardState(_levelData);
            SetPhase(Phase.Playing);
            ResetGameEconomy();
            ExtraSlotsUsed = false;
            Moves = 0;
            _idle = 0f;
            _feedback.ResetCombo();
            _view.Build(State);
            _view.SetBufferWarning(false);
            UpdateHint();
            FitCamera();
        }

        public void NextLevel()
        {
            _ads.MaybeShowInterstitial(LevelNumber);
            StartLevel(LevelNumber + 1);
        }

        /// <summary>Bölümde ilk kez görülen mekanik varsa kısa açıklama (bir kez gösterilir).</summary>
        private string MechanicIntro()
        {
            bool hasHidden = false;
            foreach (var h in State.Hidden) if (h.Contains(true)) { hasHidden = true; break; }
            if (State.HasLocks && (Progress.GetInt("intro_lock") == 0 || _forceIntro))
            {
                if (!_forceIntro) Progress.SetInt("intro_lock", 1);
                _hud.Banner(Loc.T("Yeni: Kilitli sütun!"), Loc.T("Sayı kadar kamyon gidince açılır"));
                return Loc.T("Kilitli sütuna dokunamazsın. Üstündeki sayı kadar kamyon yola çıkınca kilit açılır!");
            }
            if (hasHidden && (Progress.GetInt("intro_hidden") == 0 || _forceIntro))
            {
                if (!_forceIntro) Progress.SetInt("intro_hidden", 1);
                _hud.Banner(Loc.T("Yeni: Gizli koliler!"), Loc.T("Rengi, öne gelince görünür"));
                return Loc.T("Gri koliler gizli: hangi renk olduğu, sütunun önüne gelince ortaya çıkar. Rafı dikkatli kullan!");
            }
            return null;
        }

        private bool _forceIntro;

        private void ResetGameEconomy()
        {
            IntroText = null;
            _history.Clear();
            LastReward = 0;
            RewardBoosted = false;
            ExtraSlotsBought = 0;
            _endlessCoinsGiven = 0;
            _inputLockedUntil = 0f;
        }

        // ---------- Altın ödülleri ----------

        private void GiveWinReward()
        {
            if (VideoMode) return;
            LastReward = Economy.CoinsForLevel(LevelNumber);
            Economy.AddCoins(LastReward);
        }

        private void GiveEndlessReward()
        {
            if (VideoMode || Endless == null) return;
            int total = Economy.CoinsForEndless(Endless.Score);
            LastReward = total;
            Economy.AddCoins(total - _endlessCoinsGiven);
            _endlessCoinsGiven = total;
        }

        /// <summary>Panel butonu: ödüllü reklam izle, kazanılan altını 3 katına çıkar.</summary>
        public void BoostRewardWithAd()
        {
            if (RewardBoosted || LastReward <= 0) return;
            _ads.ShowRewarded(ok =>
            {
                if (!ok || RewardBoosted) return;
                RewardBoosted = true;
                int extra = LastReward * (Economy.RewardedMultiplier - 1);
                Economy.AddCoins(extra);
                LastReward += extra;
                _feedback.Win();
            });
        }

        // ---------- Güçlendiriciler ----------

        /// <summary>Güçlendirici şu an kullanılabilir mi; değilse nedeni.</summary>
        public bool CanUseBooster(BoosterType type, out string reason)
        {
            reason = null;
            Economy.BoosterInfo info = Economy.Info(type);
            if (State == null || VideoMode) { reason = ""; return false; }
            if (!Economy.IsUnlocked(type)) { reason = Loc.F("Bölüm {0}'de açılır", info.UnlockLevel); return false; }
            if (info.AdventureOnly && Mode == GameMode.Endless) { reason = Loc.T("Sonsuz modda kullanılamaz"); return false; }
            bool lostOk = type == BoosterType.Undo && CurrentPhase == Phase.Lost;
            if (CurrentPhase != Phase.Playing && !lostOk) { reason = ""; return false; }
            if (Time.time < _inputLockedUntil) { reason = ""; return false; }
            switch (type)
            {
                case BoosterType.Undo:
                    if (_history.Count == 0) { reason = Loc.T("Geri alınacak hamle yok"); return false; }
                    break;
                case BoosterType.Magnet:
                    if (State.ChooseMagnetDock() < 0) { reason = Loc.T("Çekilecek koli yok"); return false; }
                    break;
                case BoosterType.Shuffle:
                    int crates = 0;
                    foreach (var col in State.Columns) crates += col.Count;
                    if (crates < 2) { reason = Loc.T("Karıştırılacak koli yok"); return false; }
                    break;
                case BoosterType.ExtraSlot:
                    if (ExtraSlotsBought >= Economy.MaxExtraSlotsPerGame)
                    { reason = Loc.F("Bir oyunda en fazla {0} kez", Economy.MaxExtraSlotsPerGame); return false; }
                    break;
            }
            return true;
        }

        /// <summary>Envanterden bir adet kullanır. Başarılıysa true.</summary>
        public bool UseBooster(BoosterType type)
        {
            if (!CanUseBooster(type, out _)) return false;
            if (!Economy.TryConsume(type)) return false;
            _idle = 0f;
            switch (type)
            {
                case BoosterType.Undo: DoUndo(); break;
                case BoosterType.Magnet: StartCoroutine(DoMagnet()); break;
                case BoosterType.Shuffle: DoShuffle(); break;
                case BoosterType.ExtraSlot: DoExtraSlot(); break;
            }
            return true;
        }

        private void DoUndo()
        {
            BoardState previous = _history[_history.Count - 1];
            _history.RemoveAt(_history.Count - 1);
            State = previous;
            Moves = Mathf.Max(0, Moves - 1);
            _feedback.ResetCombo();
            _view.Build(State);
            _view.SetBufferWarning(State.BufferUsed() >= State.Buffer.Length - 1);
            SetPhase(Phase.Playing);
            FitCamera();
            UpdateHint();
            _hud.Popup(Loc.T("Geri alındı"), _view.BufferWorld(0), Color.white, 0f);
        }

        private IEnumerator DoMagnet()
        {
            int dock = State.ChooseMagnetDock();
            int color = State.Docks[dock].Color;
            _history.Clear();
            var pulls = State.MagnetPull(dock);
            _inputLockedUntil = Time.time + 0.2f * pulls.Count + 0.4f;
            _view.MagnetBurst(dock, color);
            _hud.Popup(Loc.T("Mıknatıs!"), _view.DockWorld(dock), Palette.Warning, 0f);
            for (int i = 0; i < pulls.Count; i++)
            {
                _view.Apply(pulls[i]);
                AfterMove(pulls[i], false, i == pulls.Count - 1);
                yield return new WaitForSeconds(0.2f);
            }
        }

        private void DoShuffle()
        {
            _history.Clear();
            var rng = new System.Random(System.Environment.TickCount);
            BoardState best = null;
            int bestScore = int.MinValue;
            for (int i = 0; i < 24; i++)
            {
                BoardState candidate = State.Clone();
                candidate.ShuffleColumns(rng);
                int score = candidate.DirectFrontCount() * 10 + rng.Next(5);
                if (Mode == GameMode.Adventure && LevelSolver.IsSolvableFrom(candidate)) score += 1000;
                if (score > bestScore) { bestScore = score; best = candidate; }
            }
            for (int c = 0; c < State.Columns.Count; c++)
            {
                State.Columns[c].Clear();
                State.Columns[c].AddRange(best.Columns[c]);
                State.Hidden[c].Clear();
                State.Hidden[c].AddRange(best.Hidden[c]);
            }
            _feedback.ResetCombo();
            _view.RebuildColumns();
            _inputLockedUntil = Time.time + 0.4f;
            UpdateHint();
        }

        private void DoExtraSlot()
        {
            _history.Clear();
            ExtraSlotsBought++;
            State.AddBufferSlots(1);
            _view.RebuildBuffer();
            _view.SetBufferWarning(State.BufferUsed() >= State.Buffer.Length - 1);
            FitCamera();
            UpdateHint();
        }

        /// <summary>Kaybedince altınla +3 slot.</summary>
        public bool ReviveWithCoins()
        {
            if (CurrentPhase != Phase.Lost || ExtraSlotsUsed) return false;
            if (!Economy.TrySpend(ReviveCoinPrice)) return false;
            GrantExtraSlots();
            return true;
        }

        /// <summary>Kaybedince ödüllü reklamla +3 slot.</summary>
        public void ReviveWithAd()
        {
            if (CurrentPhase != Phase.Lost || ExtraSlotsUsed) return;
            _ads.ShowRewarded(ok => { if (ok) GrantExtraSlots(); });
        }

        // ---------- Sonsuz mod ----------

        /// <summary>Giriş ekranındaki "Sonsuz" kartı ve oyun sonu "Tekrar Oyna".</summary>
        public void StartEndless() => StartEndless(System.Environment.TickCount);

        public void StartEndless(int seed)
        {
            SaveEndlessBest();
            StopAllCoroutines();
            Mode = GameMode.Endless;
            LevelNumber = 0; // öğretici açılmasın
            Endless = new EndlessDirector(seed);
            State = Endless.CreateBoard();
            RecordAtStart = Progress.EndlessBest;
            NewRecord = false;
            SetPhase(Phase.Playing);
            ResetGameEconomy();
            ExtraSlotsUsed = false;
            Moves = 0;
            _idle = 0f;
            _feedback.ResetCombo();
            _view.Build(State);
            _view.SetBufferWarning(false);
            UpdateHint();
            FitCamera();
        }

        private void SaveEndlessBest()
        {
            if (VideoMode) return; // reklam kaydı oyuncunun rekorunu değiştirmesin
            if (Mode != GameMode.Endless || Endless == null) return;
            if (Endless.Score > Progress.EndlessBest)
            {
                Progress.EndlessBest = Endless.Score;
                NewRecord = true;
            }
            // Oyun bitince ya da yarıda bırakılınca puan kadar altın (yalnızca fark verilir)
            GiveEndlessReward();
        }

        private void AfterEndlessMove(MoveResult move)
        {
            EndlessDirector.ScoreEvent e = Endless.OnMove(move);
            if (e.TruckDone)
            {
                Vector3 at = _view.DockWorld(e.Dock);
                if (e.Perfect) _hud.Popup(Loc.F("MÜKEMMEL! +{0}", e.Points), at, Palette.Warning, 0.2f);
                else _hud.Popup($"+{e.Points}", at, Color.white, 0.2f);
            }
            if (e.ComboMilestone > 0)
                _hud.Popup(Loc.F("x{0} Kombo!", e.ComboMilestone), _view.DockWorld(e.Dock), Palette.Warning, 0f);
            if (e.TierUp)
            {
                string sub = e.NewTier == EndlessDirector.HiddenFromTier ? "Gizli koliler geliyor!"
                    : e.NewTier % 2 == 0 ? "Yeni bir renk geldi!" : "Koliler daha karışık geliyor";
                _hud.Banner(Loc.F("Zorluk {0}!", e.NewTier + 1), Loc.T(sub));
                StartCoroutine(Delayed(0.2f, _feedback.TruckDeparts));
            }
            if (!VideoMode && !NewRecord && RecordAtStart > 0 && Endless.Score > RecordAtStart)
            {
                NewRecord = true;
                _hud.Banner(Loc.T("Yeni Rekor!"), Loc.F("{0} puan", Endless.Score));
            }
        }

        /// <summary>Kayıp ekranındaki "+3 slot": reklam ya da altın karşılığında.</summary>
        private void GrantExtraSlots()
        {
            if (CurrentPhase != Phase.Lost || ExtraSlotsUsed) return;
            ExtraSlotsUsed = true;
            _history.Clear();
            State.AddBufferSlots(ExtraSlotsReward);
            State.Revive();
            _view.RebuildBuffer();
            _view.SetBufferWarning(false);
            FitCamera();
            SetPhase(Phase.Playing);
            UpdateHint();
        }

        // ---------- Girdi ----------

        private void SetPhase(Phase phase)
        {
            CurrentPhase = phase;
            PhaseTime = 0f;
        }

        private void Update()
        {
            PhaseTime += Time.deltaTime;
            if (VideoMode)
            {
                VideoTime += Time.deltaTime;
                if (VideoEnding) VideoEndTime += Time.deltaTime;
                return; // video modunda dokunma yok
            }
            if (CurrentPhase == Phase.Playing) _idle += Time.deltaTime;
#if ENABLE_LEGACY_INPUT_MANAGER
            // Android geri tuşu / Escape: oyundan giriş ekranına dön
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_hud.HandleBack()) return;
                if (CurrentPhase != Phase.Menu) { ShowMenu(); return; }
                // Ana menüde: iki kez basınca çık
                if (Time.unscaledTime - _lastBackPress < 2f) Application.Quit();
                else { _lastBackPress = Time.unscaledTime; _hud.Toast(Loc.T("Çıkmak için tekrar bas")); }
                return;
            }
#endif
            if (CurrentPhase != Phase.Playing) return;
            if (!TryGetTap(out Vector2 screenPos)) return;
            if (Hud.IsPointerOverHud(screenPos) || Hud.ModalOpen) return;
            if (_ads.IsShowing || Time.time < _inputLockedUntil) return;

            Ray ray = _camera.ScreenPointToRay(screenPos);
            if (!Physics.Raycast(ray, out RaycastHit hit, 200f)) return;
            var tappable = hit.collider.GetComponent<Tappable>();
            if (tappable == null) return;

            _idle = 0f;
            if (tappable.Kind == Tappable.TapKind.Column) HandleColumnTap(tappable.Index);
            else HandleBufferTap(tappable.Index);
        }

        private static bool TryGetTap(out Vector2 position)
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
            {
                position = Pointer.current.position.ReadValue();
                return true;
            }
#else
            if (Input.GetMouseButtonDown(0))
            {
                position = Input.mousePosition;
                return true;
            }
#endif
            position = default;
            return false;
        }

        private void HandleColumnTap(int column)
        {
            BoardState before = Mode == GameMode.Adventure ? State.Clone() : null;
            MoveResult move = State.TapColumn(column);
            if (move == null)
            {
                _view.ShakeColumn(column);
                _feedback.Invalid();
                if (State.IsLocked(column)) _hud.Toast(Loc.F("Kilitli: {0} kamyon", State.LockRemaining(column)));
                return;
            }
            if (before != null) { _history.Add(before); if (_history.Count > MaxHistory) _history.RemoveAt(0); }
            AfterMove(move);
        }

        private void HandleBufferTap(int slot)
        {
            BoardState before = Mode == GameMode.Adventure ? State.Clone() : null;
            MoveResult move = State.TapBuffer(slot);
            if (move == null)
            {
                _view.ShakeBuffer(slot);
                _feedback.Invalid();
                return;
            }
            if (before != null) { _history.Add(before); if (_history.Count > MaxHistory) _history.RemoveAt(0); }
            AfterMove(move);
        }

        private void AfterMove(MoveResult move, bool applyView = true, bool checkEnd = true)
        {
            Moves++;
            IntroText = null;
            if (applyView) _view.Apply(move);
            foreach (int opened in _view.RefreshLocks())
            {
                _hud.Popup(Loc.T("Açıldı!"), _view.ColumnFrontWorld(opened) + Vector3.up * 0.8f, Palette.Warning, 0.35f);
                StartCoroutine(Delayed(0.35f, _feedback.Load));
            }
            if (Mode == GameMode.Endless) AfterEndlessMove(move);
            if (move.ToDock >= 0)
            {
                _feedback.Load();
                int combo = Mode == GameMode.Endless ? 0 : _feedback.Combo;
                // Yalnızca dönüm noktalarında: 3, 5, 8, 10, 15, 20...
                if (combo == 3 || combo == 5 || combo == 8 || (combo >= 10 && combo % 5 == 0))
                    _hud.Popup(Loc.F("x{0} Kombo!", combo), _view.DockWorld(move.ToDock), Palette.Warning, 0f);
            }
            else _feedback.ToBuffer();

            if (move.TruckDeparted)
            {
                StartCoroutine(Delayed(0.3f, _feedback.TruckDeparts));
                if (Mode == GameMode.Adventure)
                    _hud.Popup(Loc.T("Teslim!"), _view.DockWorld(move.ToDock), Color.white, 0.25f);
            }

            // Raftan kendiliğinden binen koliler: ses, puan, kamyon kalkışı
            ForEachAutoLoad(move, auto =>
            {
                if (Mode == GameMode.Endless) AfterEndlessMove(auto);
                _feedback.Load();
                if (auto.TruckDeparted)
                {
                    StartCoroutine(Delayed(0.3f, _feedback.TruckDeparts));
                    if (Mode == GameMode.Adventure)
                        _hud.Popup(Loc.T("Teslim!"), _view.DockWorld(auto.ToDock), Color.white, 0.25f);
                }
            });

            _view.SetBufferWarning(State.BufferUsed() >= State.Buffer.Length - 1);
            if (!checkEnd) return;

            if (State.IsWon())
            {
                SetPhase(Phase.Won);
                Progress.CurrentLevel = LevelNumber + 1; // ilerleme hemen kaydedilir
                if (LevelNumber == 1) Progress.TutorialDone = true;
                GiveWinReward();
                StartCoroutine(Delayed(0.6f, _feedback.Win));
                _view.PlayWinCelebration(ConvoyColors());
                StartCoroutine(Delayed(0.9f, _feedback.TruckDeparts));
                StartCoroutine(Delayed(1.3f, _feedback.TruckDeparts));
            }
            else if (State.IsStuck())
            {
                SetPhase(Phase.Lost);
                SaveEndlessBest();
            }
            UpdateHint();
        }

        private static void ForEachAutoLoad(MoveResult move, System.Action<MoveResult> action)
        {
            if (move.AutoLoads == null) return;
            foreach (MoveResult auto in move.AutoLoads)
            {
                action(auto);
                ForEachAutoLoad(auto, action);
            }
        }

        /// <summary>Konvoyda geçecek kamyon renkleri (bölümdeki ilk 3 farklı renk).</summary>
        private int[] ConvoyColors()
        {
            var colors = new System.Collections.Generic.List<int>();
            foreach (int c in _levelData.trucks)
            {
                if (!colors.Contains(c)) colors.Add(c);
                if (colors.Count == 3) break;
            }
            return colors.ToArray();
        }

        // ---------- İpucu ----------

        /// <summary>
        /// Önerilen hamle: önce raftan yüklenebilen koli, sonra doğrudan kamyona gidebilen
        /// koli, yoksa rafa konduğunda bölümü çözülebilir bırakan sütun.
        /// </summary>
        private void UpdateHint()
        {
            _hint = default;
            if (State == null || CurrentPhase != Phase.Playing) return;

            for (int s = 0; s < State.Buffer.Length; s++)
                if (State.CanTapBuffer(s))
                {
                    _hint = new Hint { Valid = true, Kind = Tappable.TapKind.Buffer, Index = s,
                        Text = Loc.T("Raftaki koliye dokun: kendi kamyonuna yüklensin!") };
                    return;
                }

            for (int c = 0; c < State.Columns.Count; c++)
            {
                int color = State.FrontCrate(c);
                if (color >= 0 && !State.IsLocked(c) && State.FindDockFor(color) >= 0)
                {
                    _hint = new Hint { Valid = true, Kind = Tappable.TapKind.Column, Index = c,
                        Text = Loc.T("Öndeki koliye dokun: aynı renkteki kamyona gider!") };
                    return;
                }
            }

            int fallback = -1;
            for (int c = 0; c < State.Columns.Count; c++)
            {
                if (!State.CanTapColumn(c)) continue;
                if (fallback < 0) fallback = c;
                if (State.IsEndless) { fallback = LevelSolver.CarefulChoice(State, null); break; }
                BoardState next = State.Clone();
                next.TapColumn(c);
                if (LevelSolver.IsSolvableFrom(next))
                {
                    fallback = c;
                    break;
                }
            }
            if (fallback >= 0)
                _hint = new Hint { Valid = true, Kind = Tappable.TapKind.Column, Index = fallback,
                    Text = Loc.T("Uygun kamyon yok: koli rafa gider, kamyonu gelince kendiliğinden biner. Raf dolarsa kaybedersin!") };
        }

        private static IEnumerator Delayed(float seconds, System.Action action)
        {
            yield return new WaitForSeconds(seconds);
            action();
        }

        // ---------- Kamera ve ışık ----------

        private void SetupCamera()
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                _camera = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            _camera.orthographic = true;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Palette.Background;
            _camera.transform.rotation = Quaternion.Euler(48f, 0f, 0f);
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 100f;
        }

        private static void SetupLight()
        {
            if (Object.FindAnyObjectByType<Light>() != null) return;
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
        }

        /// <summary>Tüm oyun alanı ekrana sığacak şekilde ortografik kamerayı ayarlar.</summary>
        private void FitCamera()
        {
            Bounds b = _view.ContentBounds;
            Transform cam = _camera.transform;
            Vector3 right = cam.right, up = cam.up;

            float minR = float.MaxValue, maxR = float.MinValue, minU = float.MaxValue, maxU = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = b.center + Vector3.Scale(b.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                float r = Vector3.Dot(corner, right), u = Vector3.Dot(corner, up);
                minR = Mathf.Min(minR, r); maxR = Mathf.Max(maxR, r);
                minU = Mathf.Min(minU, u); maxU = Mathf.Max(maxU, u);
            }

            const float sideMargin = 0.3f, edgeMargin = 0.4f;
            minR -= sideMargin; maxR += sideMargin;
            minU -= edgeMargin; maxU += edgeMargin;

            // Ekranın üstü arayüze (ve çentiğe), altı güvenli alana ayrılır;
            // oyun alanı kalan banda sığdırılır.
            float topFrac = Mathf.Clamp(Hud.TopReservedPixels() / Screen.height, 0f, 0.4f);
            float bottomFrac = Mathf.Clamp(Screen.safeArea.y / Screen.height + 0.02f, 0f, 0.2f);
            // Reklam videosu: Instagram/Shorts alt %20'sini açıklama ve butonlar kaplar
            if (VideoMode) bottomFrac = Mathf.Max(bottomFrac, 0.15f);
            else bottomFrac = Mathf.Clamp(bottomFrac + Hud.BottomReservedPixels(this) / Screen.height, 0f, 0.3f);
            float band = 1f - topFrac - bottomFrac;

            float halfHeight = (maxU - minU) / 2f;
            float halfWidth = (maxR - minR) / 2f;
            float size = Mathf.Max(halfHeight / band, halfWidth / _camera.aspect);
            _camera.orthographicSize = size;

            // İçeriğin ortası bandın ortasına gelsin
            float centerU = (minU + maxU) / 2f + (topFrac - bottomFrac) * size;
            Vector3 center = right * ((minR + maxR) / 2f) + up * centerU;
            cam.position = center - cam.forward * 30f;
        }

        private float _lastAspect;
        private float _lastBackPress = -10f;

        /// <summary>Uygulama arka plana alınınca (telefon kilitlendi, başka uygulamaya geçildi).</summary>
        private void OnApplicationPause(bool paused)
        {
            if (!paused || VideoMode) return;
            // Sonsuz modda rekor ve altın hemen kaydedilsin; uygulama kapatılsa da kaybolmasın
            SaveEndlessBest();
            PlayerPrefs.Save();
        }

        private void LateUpdate()
        {
            // Ekran döndürülürse veya pencere boyutu değişirse kamerayı yeniden sığdır
            if (State != null && !Mathf.Approximately(_lastAspect, _camera.aspect))
            {
                _lastAspect = _camera.aspect;
                FitCamera();
            }
        }
    }
}
