#if using_addressable
using UnityEngine.AddressableAssets;
#endif
using System;
using System.Collections.Generic;
using System.Linq;
using WaterFlow.Framework.Base.Singleton;
using Cysharp.Threading.Tasks;
using WaterFlow.Enums;
using WaterFlow.Framework.Utils;
using WaterFlow.Framework.Systems;
using WaterFlow.Framework.Systems.EventBus;
using WaterFlow.Framework.Systems.LoadObject;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace WaterFlow.Framework.UIModule
{
    public class PanelManager : SingletonSimple<PanelManager>, IPanelAsyncManager
    {
        //[SerializeField] private Service<LoadObjectService> loadObjectService;
        [SerializeField] private Service<LoadObjectServiceAsync> loadObjectServiceAsync;
        private readonly Dictionary<string, View> _cache = new();
        private readonly List<View> _stackPanels = new();
        private readonly HashSet<string> _openingPanels = new();
        public Action OnPanelsUpdated;
        private EventBinding<PanelUpdatedEvent> onPanelUpdatedEvent;

        private GameState gameState;
        public int panelCount => _stackPanels.Count;

        public Type CurrentPanelType =>
            _stackPanels.Count > 0
                ? _stackPanels.Last()
                    .GetType()
                : null;

        public View GetCurrentPanel => _stackPanels.LastOrDefault();

        private void Start()
        {
            new EventBinding<GameStateChangeEvent>(OnGameStateChanged, true);
        }

#if UNITY_EDITOR || UNITY_STANDALONE_WIN
        //private void Update()
        //{
        //    if (Input.GetKeyDown(KeyCode.Escape))
        //        TryCloseCurrentPanel();
        //}
#endif

        public async UniTaskVoid OpenPanelAsync<T>(Action<T> onPanelCreated, UIData uiData = null, Transform container = null) where T : View
        {
            var panelName = typeof(T).Name;
            var panel = await OpenPanelByNameAsync<T>(panelName, uiData, container);
            onPanelCreated?.Invoke(panel);
        }

        public void OpenForget<T>(UIData uiData = null, Transform container = null) where T : View
        {
            OpenPanelAsync<T>(uiData, container)
                .Forget();
        }

        public async UniTask<T> OpenPanelByNameAsync<T>(string panelName, UIData uiData = null,
            Transform container = null) where T : View
        {
            Debug.Log($"OpenPanelByNameAsync: {panelName}");

            // Dedupe: clicks buffered during a main-thread stall all flush in one frame,
            // so the same panel must not be instantiated once per queued click.
            var openedPanel = GetPanelByName<T>(panelName);
            if (openedPanel != null)
            {
                Debug.Log($"OpenPanelByNameAsync: {panelName} already open, skipping duplicate");
                return openedPanel;
            }

            if (!_openingPanels.Add(panelName))
            {
                // Same panel is mid-open (prefab load still in flight) — wait for that
                // open to finish and reuse its instance instead of spawning another.
                await UniTask.WaitUntil(() => !_openingPanels.Contains(panelName));
                return GetPanelByName<T>(panelName);
            }

            try
            {
                // create panel async
                container ??= transform;
                if (_cache.TryGetValue(panelName, out var panel))
                {
                    panel.transform.SetParent(container);
                    panel.transform.SetAsLastSibling();
                    panel.gameObject.SetActive(true);
                    _cache.Remove(panelName);
                }
                else
                {
                    var panelPref = await loadObjectServiceAsync.Instance.LoadAsync<GameObject>(panelName);
                    panel = Instantiate(panelPref, container)
                        .GetComponent<T>();
                    panel.gameObject.name = panelName;
                    panel.Init();
                }

                SetupOnOpenPanel(panel, uiData, panelName);
                return (T)panel;
            }
            finally
            {
                _openingPanels.Remove(panelName);
            }
        }

        public async UniTask<T> OpenPanelAsync<T>(UIData uiData = null, Transform container = null) where T : View
        {
            var panelName = typeof(T).Name;
            return await OpenPanelByNameAsync<T>(panelName, uiData, container);
        }

        public async UniTask ClosePanelAsync<T>(bool immediately = false, bool waitCloseCompleted = false)
            where T : View
        {
            var panelId = typeof(T).ToString();
            var panel = _stackPanels.Find(panel => panel.id.Equals(panelId));
            if (panel == null) return;
            // play close animation (if not immediately)
            if (immediately)
                panel.CloseImmediately();
            else
                panel.Close();

            // wait until close completed
            if (waitCloseCompleted)
                await UniTask.WaitUntil(() => panel == null);
        }


        // public T OpenPanelByName<T>(string panelName, UIData uiData = null, Transform container = null) where T : View
        // {
        //     container ??= transform;
        //     if (_cache.TryGetValue(panelName, out var panel))
        //     {
        //         panel.transform.SetParent(container);
        //         panel.transform.SetAsLastSibling();
        //         panel.gameObject.SetActive(true);
        //         _cache.Remove(panelName);
        //     }
        //     else
        //     {
        //         var panelPref = loadObjectService.Instance.LoadObject<GameObject>($"{panelName}");
        //         panel = Instantiate(panelPref, container)
        //             .GetComponent<T>();
        //         panel.gameObject.name = panelName;
        //         panel.Init();
        //     }
        //
        //     SetupOnOpenPanel(panel, uiData, panelName);
        //
        //     return (T)panel;
        // }
        //
        // public T OpenPanel<T>(UIData uiData = null, Transform container = null) where T : View
        // {
        //     var panelName = typeof(T).Name;
        //     return OpenPanelByName<T>(panelName, uiData, container);
        // }
        //
        public void ClosePanel<T>(bool immediately = false) where T : View
        {
            var panelId = typeof(T).Name;
            ClosePanel(panelId, immediately);
        }

        public void ReleasePanel(View panelClosed)
        {
            if (!panelClosed)
            {
                return;
            }

            panelClosed.OnFocusLost();
            OnPanelLostFocus?.Invoke(nameof(panelClosed));
            _stackPanels.Remove(panelClosed);

            var newTopScreen = GetPanel(1);
            if (newTopScreen)
            {
                newTopScreen.OnFocus();
                OnPanelFocus?.Invoke(nameof(newTopScreen));
            }

            if (!panelClosed.ignoreTracking)
                UpdatePlacement();

            if (panelClosed.keepCached)
            {
                _cache.TryAdd(panelClosed.id, panelClosed);
                panelClosed.gameObject.SetActive(false);
                return;
            }
            else
            {
                Destroy(panelClosed.gameObject);
            }

            OnPanelsUpdated?.Invoke();
            EventBus<PanelUpdatedEvent>.Raise(new PanelUpdatedEvent() { isOpen = false });
        }

        public event Action<string> OnPanelFocus, OnPanelLostFocus;

        public T GetPanel<T>() where T : View
        {
            return (T)_stackPanels.Find(panel => panel.GetType()
                .ToString()
                .Equals(typeof(T).ToString()));
        }

        /// <summary>
        /// True if any open, active panel is assignable to <typeparamref name="T"/> — i.e. matches
        /// <typeparamref name="T"/> or any subclass. Unlike <see cref="GetPanel{T}"/>, which compares the
        /// exact runtime type name, this recognises derived panels: IsPanelActive&lt;PopupRewardChest&gt;()
        /// also matches PopupRewardChest_WinStreak, and IsPanelActive&lt;PopupReceiveCardBase&gt;() matches
        /// the Immediate/Tutorial variants. Use it for "is a popup of this family showing" gates.
        /// </summary>
        public bool IsPanelActive<T>() where T : View
        {
            return _stackPanels.Exists(panel => panel is T && panel.gameObject.activeInHierarchy);
        }

        public T GetPanelByName<T>(string panelName) where T : View
        {
            return (T)_stackPanels.Find(panel => panel.gameObject.name.Equals(panelName));
        }


        protected override void OnAwake()
        {
            //// check back button in Android by UniRX
            //Observable.EveryUpdate()
            //    .Where(_ => Input.GetKeyDown(KeyCode.Escape))
            //    .Subscribe(_ => TryCloseCurrentPanel())
            //    .AddTo(this);

            foreach (Transform panel in transform) Destroy(panel.gameObject);
        }

        private void OnGameStateChanged(GameStateChangeEvent eventData)
        {
            gameState = eventData.gameState;
        }

        private void SetupOnOpenPanel(View panel, UIData uiData, string panelName)
        {
            panel.Open(uiData);
            var currentDisplay = GetPanel(1);
            if (currentDisplay)
            {
                currentDisplay.OnFocusLost();
                OnPanelLostFocus?.Invoke(nameof(currentDisplay));
            }

            panel.OnFocus();
            OnPanelFocus?.Invoke(panelName);
            _stackPanels.Insert(0, panel);

            if (!panel.ignoreTracking)
                UpdatePlacement();

            OnPanelsUpdated?.Invoke();
            EventBus<PanelUpdatedEvent>.Raise(new PanelUpdatedEvent() { isOpen = true });
        }

        public void ClosePanel(string panelId, bool immediately = false)
        {
            var panel = _stackPanels.Find(panel => panel.id.Equals(panelId));
            if (panel == null) return;

            // play close animation (if not immediately)
            if (immediately)
                panel.CloseImmediately();
            else
                panel.Close();
        }


        private void TryCloseCurrentPanel()
        {
            if (_stackPanels.Count == 0)
            {
                Debug.LogWarning("[PanelManager] Stack is empty");
                return;
            }

            var panelInTop = _stackPanels.First();
            _stackPanels.Remove(panelInTop);
            panelInTop.Close();
        }


        private View GetPanel(int num, bool includeAll = true)
        {
            if (_stackPanels.Count < num) return null;
            foreach (var panel in _stackPanels)
            {
                if (includeAll || !panel.ignoreTracking)
                {
                    num--;
                    if (num == 0) return panel;
                }
            }

            return null;
        }

        public void UpdatePlacement()
        {
            var data = new UpdatePlacementEvent();
            var top = GetPanel(1, false);
            if (top != null)
            {
                data.placement = top.GetPlacement();
            }

            EventBus<UpdatePlacementEvent>.Raise(data);
        }

        public void RemovePanelFromStack(View view)
        {
            _stackPanels.Remove(view);
            if (!view.ignoreTracking)
                UpdatePlacement();

            OnPanelsUpdated?.Invoke();
            EventBus<PanelUpdatedEvent>.Raise(new PanelUpdatedEvent() { isOpen = false });
        }

        public void CloseAllPanel(List<Type> exceptPanels = null)
        {
            var listPanelClose = _stackPanels.Where(x => exceptPanels == null || !exceptPanels.Contains(x.GetType())).ToList();
            foreach (var view in listPanelClose)
            {
                ReleasePanel(view);
            }

            UpdatePlacement();
            OnPanelsUpdated?.Invoke();
            EventBus<PanelUpdatedEvent>.Raise(new PanelUpdatedEvent() { isOpen = false });
        }

        public bool HasAnyPopupPauseGame()
        {
            if (_stackPanels == null || _stackPanels.Count == 0) return false;
            return _stackPanels.Any(panel => panel.pauseGame && panel.gameObject.activeInHierarchy);
        }

        public int PopupCount()
        {
            return _stackPanels.Count;
        }
    }
}