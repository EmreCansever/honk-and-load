using System.Collections;
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
            gameObject.AddComponent<Hud>().Init(this);
        }

        private void Start()
        {
            ApplySettings();
            ShowMenu();
        }

        // ---------- Menü ----------

        /// <summary>Giriş ekranına dön. Oyun alanı temizlenir.</summary>
        public void ShowMenu()
        {
            StopAllCoroutines();
            _view.Clear();
            State = null;
            CurrentPhase = Phase.Menu;
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
            CurrentPhase = Phase.Playing;
            ExtraSlotsUsed = false;
            Moves = 0;
            _feedback.ResetCombo();
            _view.Build(State);
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
            FitCamera();
            CurrentPhase = Phase.Playing;
        }

        // ---------- Girdi ----------

        private void Update()
        {
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
            if (move.ToDock >= 0) _feedback.Load(); else _feedback.ToBuffer();
            if (move.TruckDeparted) StartCoroutine(Delayed(0.3f, _feedback.TruckDeparts));

            if (State.IsWon())
            {
                CurrentPhase = Phase.Won;
                Progress.CurrentLevel = LevelNumber + 1; // ilerleme hemen kaydedilir
                StartCoroutine(Delayed(0.6f, _feedback.Win));
            }
            else if (State.IsStuck())
            {
                CurrentPhase = Phase.Lost;
            }
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
            _camera.transform.rotation = Quaternion.Euler(58f, 0f, 0f);
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

            // Üstte ve altta arayüz için pay bırak
            const float topMargin = 1.6f, bottomMargin = 1.4f, sideMargin = 0.4f;
            maxU += topMargin; minU -= bottomMargin;
            minR -= sideMargin; maxR += sideMargin;

            float halfHeight = (maxU - minU) / 2f;
            float halfWidth = (maxR - minR) / 2f;
            _camera.orthographicSize = Mathf.Max(halfHeight, halfWidth / _camera.aspect);

            Vector3 center = right * ((minR + maxR) / 2f) + up * ((minU + maxU) / 2f);
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
