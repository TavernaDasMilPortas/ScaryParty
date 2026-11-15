using System;
using UnityEngine;
using UnityEditor;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Stations;

namespace ScaryParty.Pizzeria.Editor.Authoring
{
    public enum ViewportDisplayMode
    {
        Isolated = 0,         // Modelo isolado do asset selecionado
        OccupiedStation = 1,  // Bancada com slots, itens apoiados e utensílios
        PizzeriaContext = 2   // Bancada no contexto da sala/arquitetura
    }

    [Serializable]
    public class PinnedAssemblyState
    {
        public bool isPinned = false;
        public StationView pinnedStation;
        public StationDef pinnedStationDef;
        public int pinnedSlotIndex = 0;
        public IngredientDef pinnedIngredient;
        public ToolDef pinnedTool;
        public ProcessDef pinnedProcess;
        public float processProgress = 0f; // 0.0f a 1.0f
    }

    /// <summary>
    /// Sessão compartilhada do Estúdio de Conteúdo da Pizzaria.
    /// Permite que a bancada permaneça fixada com tomate e faca em uma janela
    /// enquanto o desenvolvedor edita os parâmetros do ingrediente ou da faca na ficha Odin.
    /// </summary>
    public static class PizzeriaContentStudioSession
    {
        private static PinnedAssemblyState _pinnedState = new PinnedAssemblyState();
        private static bool _followSelection = true;
        private static ViewportDisplayMode _displayMode = ViewportDisplayMode.OccupiedStation;
        private static UnityEngine.Object _activeInspectedObject;

        public static event Action OnSessionChanged;

        public static PinnedAssemblyState PinnedState => _pinnedState;
        public static bool IsPinned => _pinnedState.isPinned;

        public static bool FollowSelection
        {
            get => _followSelection;
            set
            {
                if (_followSelection != value)
                {
                    _followSelection = value;
                    NotifySessionChanged();
                }
            }
        }

        public static ViewportDisplayMode DisplayMode
        {
            get => _displayMode;
            set
            {
                if (_displayMode != value)
                {
                    _displayMode = value;
                    NotifySessionChanged();
                }
            }
        }

        public static UnityEngine.Object ActiveInspectedObject
        {
            get => _activeInspectedObject;
            set
            {
                if (_activeInspectedObject != value)
                {
                    _activeInspectedObject = value;
                    if (_followSelection && !_pinnedState.isPinned)
                    {
                        AutoConfigurePreviewForObject(value);
                    }
                    NotifySessionChanged();
                }
            }
        }

        [InitializeOnLoadMethod]
        private static void Init()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            Undo.undoRedoPerformed += OnUndoRedo;
            Selection.selectionChanged -= OnUnitySelectionChanged;
            Selection.selectionChanged += OnUnitySelectionChanged;
        }

        private static void OnUndoRedo()
        {
            NotifySessionChanged();
        }

        private static void OnUnitySelectionChanged()
        {
            if (_followSelection && !_pinnedState.isPinned)
            {
                if (Selection.activeObject != null)
                {
                    ActiveInspectedObject = Selection.activeObject;
                }
            }
        }

        public static void SetPinned(bool pinned)
        {
            _pinnedState.isPinned = pinned;
            NotifySessionChanged();
        }

        public static void PinAssembly(
            StationView station,
            StationDef stationDef,
            int slotIndex,
            IngredientDef ingredient,
            ToolDef tool,
            ProcessDef process = null)
        {
            _pinnedState.isPinned = true;
            _pinnedState.pinnedStation = station;
            _pinnedState.pinnedStationDef = stationDef;
            _pinnedState.pinnedSlotIndex = slotIndex;
            _pinnedState.pinnedIngredient = ingredient;
            _pinnedState.pinnedTool = tool;
            _pinnedState.pinnedProcess = process;
            _pinnedState.processProgress = 0f;
            _displayMode = ViewportDisplayMode.OccupiedStation;
            NotifySessionChanged();
        }

        public static void SetProcessProgress(float progress)
        {
            _pinnedState.processProgress = Mathf.Clamp01(progress);
            NotifySessionChanged();
        }

        public static void NotifySessionChanged()
        {
            OnSessionChanged?.Invoke();
        }

        private static void AutoConfigurePreviewForObject(UnityEngine.Object obj)
        {
            if (obj == null) return;

            if (obj is StationDef stDef)
            {
                _pinnedState.pinnedStationDef = stDef;
            }
            else if (obj is IngredientDef ingDef)
            {
                _pinnedState.pinnedIngredient = ingDef;
            }
            else if (obj is ToolDef toolDef)
            {
                _pinnedState.pinnedTool = toolDef;
            }
            else if (obj is ProcessDef procDef)
            {
                _pinnedState.pinnedProcess = procDef;
                if (procDef.inputIngredient != null) _pinnedState.pinnedIngredient = procDef.inputIngredient;
            }
            else if (obj is GameObject go)
            {
                var st = go.GetComponent<StationView>();
                if (st != null) _pinnedState.pinnedStation = st;
            }
        }
    }
}
