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

        public const int ExtraSlotsReward = 3;

        public BoardState State { get; private set; }
        public int LevelNumber { get; private set; }
        public Phase CurrentPhase { get; private set; }
        public bool ExtraSlotsUsed { get; private set; }
        public int Moves { get; private set; }

        /// <summary>Son aşama değişikliğinden bu yana geçen süre (kazanma paneli gecikmesi için).</summary>
        public float PhaseTime { get; private set; }

        // İpucu / öğretici
        public bool IsTutorial => LevelNumber == 1 && !Progress.TutorialDone;
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
            SetupCamera();
            SetupLight();
            _view = new GameObject("BoardView").AddComponent<BoardView>();
            _view.transform.SetParent(transform, false);
            _feedback = gameObject.AddComponent<Feedback>();
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
            if (record || preview)
            {
                StartVideoMode(record);
                return;
            }
#endif
            ShowMenu();
        }

        // ---------- Reklam videosu ----------

        /// <summary>Arayüzsüz, kendi kendine oynayan mod. record=true ise kare kare kaydeder.</summary>
        public void StartVideoMode(bool record)
        {
            VideoMode = true;
            Hud.IsVideo = true;
            VideoTime = 0f;
            AudioListener.volume = 1f;
            if (record)
            {
                string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Recordings",
                    "ad_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss")));
                _recorder = gameObject.AddComponent<VideoRecorder>();
                _recorder.Begin(folder, VideoFps);
            }
            StartLevel(VideoLevel);
            StartCoroutine(VideoBot());
        }

        private IEnumerator VideoBot()
        {
            var rng = new System.Random(7);
            yield return new WaitForSeconds(1.8f); // açılış sorusu okunsun

            int safety = 0;
            while (CurrentPhase == Phase.Playing && _hint.Valid && safety++ < 500)
            {
                _hud.ShowTap(HintWorld);
                yield return new WaitForSeconds(0.1f);
                if (_hint.Kind == Tappable.TapKind.Column) HandleColumnTap(_hint.Index);
                else HandleBufferTap(_hint.Index);
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
            LevelNumber = Mathf.Max(1, number);
            _levelData = LevelLoader.Load(LevelNumber);
            RestartLevel();
        }

        public void RestartLevel()
        {
            StopAllCoroutines();
            State = new BoardState(_levelData);
            SetPhase(Phase.Playing);
            ExtraSlotsUsed = false;
            Moves = 0;
            _idle = 0f;
            _feedback.ResetCombo();
            _view.Build(State);
            _view.SetBufferWarning(false);
            UpdateHint();
            FitCamera();
        }

        public void NextLevel() => StartLevel(LevelNumber + 1);

        /// <summary>Kayıp ekranındaki "+3 slot" (ileride ödüllü reklamdan sonra çağrılacak).</summary>
        public void GrantExtraSlots()
        {
            if (CurrentPhase != Phase.Lost || ExtraSlotsUsed) return;
            ExtraSlotsUsed = true;
            State.AddBufferSlots(ExtraSlotsReward);
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
            if (VideoMode) { VideoTime += Time.deltaTime; return; } // video modunda dokunma yok
            if (CurrentPhase == Phase.Playing) _idle += Time.deltaTime;
#if ENABLE_LEGACY_INPUT_MANAGER
            // Android geri tuşu / Escape: oyundan giriş ekranına dön
            if (CurrentPhase != Phase.Menu && Input.GetKeyDown(KeyCode.Escape)) { ShowMenu(); return; }
#endif
            if (CurrentPhase != Phase.Playing) return;
            if (!TryGetTap(out Vector2 screenPos)) return;
            if (Hud.IsPointerOverHud(screenPos)) return;

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
            MoveResult move = State.TapColumn(column);
            if (move == null)
            {
                _view.ShakeColumn(column);
                _feedback.Invalid();
                return;
            }
            AfterMove(move);
        }

        private void HandleBufferTap(int slot)
        {
            MoveResult move = State.TapBuffer(slot);
            if (move == null)
            {
                _view.ShakeBuffer(slot);
                _feedback.Invalid();
                return;
            }
            AfterMove(move);
        }

        private void AfterMove(MoveResult move)
        {
            Moves++;
            _view.Apply(move);
            if (move.ToDock >= 0)
            {
                _feedback.Load();
                int combo = _feedback.Combo;
                // Yalnızca dönüm noktalarında: 3, 5, 8, 10, 15, 20...
                if (combo == 3 || combo == 5 || combo == 8 || (combo >= 10 && combo % 5 == 0))
                    _hud.Popup($"x{combo} Kombo!", _view.DockWorld(move.ToDock), Palette.Warning, 0f);
            }
            else _feedback.ToBuffer();

            if (move.TruckDeparted)
            {
                StartCoroutine(Delayed(0.3f, _feedback.TruckDeparts));
                _hud.Popup("Teslim!", _view.DockWorld(move.ToDock), Color.white, 0.25f);
            }

            _view.SetBufferWarning(State.BufferUsed() >= State.Buffer.Length - 1);

            if (State.IsWon())
            {
                SetPhase(Phase.Won);
                Progress.CurrentLevel = LevelNumber + 1; // ilerleme hemen kaydedilir
                if (LevelNumber == 1) Progress.TutorialDone = true;
                StartCoroutine(Delayed(0.6f, _feedback.Win));
                _view.PlayWinCelebration(ConvoyColors());
                StartCoroutine(Delayed(0.9f, _feedback.TruckDeparts));
                StartCoroutine(Delayed(1.3f, _feedback.TruckDeparts));
            }
            else if (State.IsStuck())
            {
                SetPhase(Phase.Lost);
            }
            UpdateHint();
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
                        Text = "Raftaki koliye dokun: kendi kamyonuna yüklensin!" };
                    return;
                }

            for (int c = 0; c < State.Columns.Count; c++)
            {
                int color = State.FrontCrate(c);
                if (color >= 0 && State.FindDockFor(color) >= 0)
                {
                    _hint = new Hint { Valid = true, Kind = Tappable.TapKind.Column, Index = c,
                        Text = "Öndeki koliye dokun: aynı renkteki kamyona gider!" };
                    return;
                }
            }

            int fallback = -1;
            for (int c = 0; c < State.Columns.Count; c++)
            {
                if (!State.CanTapColumn(c)) continue;
                if (fallback < 0) fallback = c;
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
                    Text = "Uygun kamyon yok: koli rafa gider, kamyonu gelince yüklersin." };
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
