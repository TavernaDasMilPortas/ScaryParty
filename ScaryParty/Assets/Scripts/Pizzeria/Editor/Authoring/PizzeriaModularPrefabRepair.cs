#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Stations;

namespace ScaryParty.Pizzeria.Editor.Authoring
{
    public static class PizzeriaModularPrefabRepair
    {
        public const string ManualPrefabPath = "Assets/Prefabs/Pizzeria/PizzeriaModular_Manual.prefab";

        private struct Replacement
        {
            public StationView oldStation;
            public Type newType;
            public PizzeriaStationBindingRecord record;
        }

        private struct WorkpostLink
        {
            public StationWorkpostTarget workpost;
            public StationView station;
            public int index;
        }

        [MenuItem("Tools/Scary Party/Pizzeria/Reparar prefab modular manual...")]
        private static void RepairManual() => RepairAsset(ManualPrefabPath);

        [MenuItem("Tools/Scary Party/Pizzeria/Reparar prefab selecionado...")]
        private static void RepairSelected() => RepairAsset(AssetDatabase.GetAssetPath(Selection.activeObject));

        [MenuItem("Tools/Scary Party/Pizzeria/Reparar prefab selecionado...", true)]
        private static bool CanRepairSelected() => Selection.activeObject is GameObject &&
            AssetDatabase.GetAssetPath(Selection.activeObject).EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);

        private static void RepairAsset(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) ||
                !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogError("Selecione um asset prefab editável dentro de Assets.");
                return;
            }

            if (!EditorUtility.DisplayDialog("Reparar prefab da pizzaria",
                    $"Pré-validar e reparar '{path}'? O asset só será salvo se houver mudanças não ambíguas.",
                    "Reparar", "Cancelar")) return;

            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(path);
                if (!TryRepair(root, out string report, out bool changed))
                {
                    Debug.LogError($"Reparo cancelado: {report}");
                    return;
                }
                if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"Reparo do prefab '{path}': {report}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Falha ao reparar '{path}': {ex}");
            }
            finally
            {
                if (root != null) PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // Operates only on an editable hierarchy; the caller decides whether to persist it.
        public static bool TryRepair(GameObject root, out string report, out bool changed)
        {
            changed = false;
            report = string.Empty;
            if (root == null) { report = "Raiz ausente."; return false; }
            var bindings = root.GetComponent<PizzeriaAuthoringBindings>();
            var pizzeria = root.GetComponent<PizzeriaRoot>();
            var config = pizzeria != null ? pizzeria.Config : null;
            if (bindings == null || config == null || config.stationInstances == null || bindings.stationRecords == null)
            {
                report = "PizzeriaAuthoringBindings ou PizzeriaRoot.Config/stationInstances ausente.";
                return false;
            }

            var stations = root.GetComponentsInChildren<StationView>(true);
            var replacements = new List<Replacement>();
            var links = new List<WorkpostLink>();
            var byAnchor = new Dictionary<Transform, List<WorkpostLink>>();
            foreach (var station in stations)
            {
                if (station.stationId <= 0) continue;
                int configMatches = 0;
                StationInstanceConfig instance = null;
                foreach (var candidate in config.stationInstances)
                    if (candidate != null && candidate.instanceId == station.stationId)
                    { instance = candidate; configMatches++; }
                int recordMatches = 0;
                PizzeriaStationBindingRecord record = null;
                foreach (var candidate in bindings.stationRecords)
                    if (candidate != null && candidate.stationId == station.stationId)
                    { record = candidate; recordMatches++; }
                // Only a definition shared by the config and binding, with processes,
                // establishes the Prep correction; do not guess other station types.
                bool isPrepCandidate = (station is AssemblyStation || station is PrepStation) &&
                    ((record != null && record.stationKind == PizzeriaStationKind.Prep) ||
                     (instance != null && instance.definition != null && instance.definition.GetEffectiveProcessIds().Count > 0));
                if (isPrepCandidate)
                {
                    if (station.GetComponents<StationView>().Length != 1 ||
                        configMatches != 1 || recordMatches != 1 || instance.definition == null ||
                        record.stationDef != instance.definition || record.stationComponent != station ||
                        instance.definition.GetEffectiveProcessIds().Count == 0 ||
                        Array.FindAll(stations, s => s.stationId == station.stationId).Length != 1)
                    {
                        report = $"Config/binding de preparo inconsistente ou ambíguo para stationId {station.stationId}.";
                        return false;
                    }
                    var descriptor = PizzeriaStationRecipeRegistry.GetDescriptor(PizzeriaStationKind.Prep);
                    if (descriptor.kind != PizzeriaStationKind.Prep || descriptor.runtimeComponentType != typeof(PrepStation))
                    { report = "Receita Prep incompatível."; return false; }
                    if (station is AssemblyStation)
                        replacements.Add(new Replacement { oldStation = station, newType = descriptor.runtimeComponentType, record = record });
                    else if (record.stationKind != PizzeriaStationKind.Prep)
                    { report = $"Binding incompatível no id {station.stationId}."; return false; }
                }

                if (station.slotAnchors == null) continue;
                for (int i = 0; i < station.slotAnchors.Length; i++)
                {
                    var anchor = station.slotAnchors[i];
                    if (anchor == null) continue;
                    if (!byAnchor.TryGetValue(anchor, out var candidates))
                        byAnchor.Add(anchor, candidates = new List<WorkpostLink>());
                    candidates.Add(new WorkpostLink { station = station, index = i });
                }
            }

            int unresolved = 0;
            foreach (var wp in root.GetComponentsInChildren<StationWorkpostTarget>(true))
            {
                if (wp.slotAnchor == null) { unresolved++; continue; }
                if (!byAnchor.TryGetValue(wp.slotAnchor, out var matches) || matches.Count != 1)
                {
                    report = $"Âncora sem destino único no posto '{wp.name}' (slot {wp.slotIndex}).";
                    return false;
                }
                links.Add(new WorkpostLink { workpost = wp, station = matches[0].station, index = matches[0].index });
            }

            foreach (var replacement in replacements)
            {
                var old = replacement.oldStation;
                var station = (StationView)old.gameObject.AddComponent(replacement.newType);
                station.stationId = old.stationId;
                station.stationName = old.stationName;
                station.slotAnchors = old.slotAnchors;
                station.slotsBoundToObjects = old.slotsBoundToObjects;
                station.LastAimExplicitSlot = old.LastAimExplicitSlot;
                station.LastAimWorldPoint = old.LastAimWorldPoint;
                station.enabled = old.enabled;
                station.hideFlags = old.hideFlags;
                foreach (var component in root.GetComponentsInChildren<Component>(true))
                {
                    if (component == null || component == old || component == station) continue;
                    var serialized = new SerializedObject(component);
                    var property = serialized.GetIterator();
                    bool dirty = false;
                    while (property.Next(true))
                    {
                        if (property.propertyType != SerializedPropertyType.ObjectReference || property.objectReferenceValue != old) continue;
                        property.objectReferenceValue = station;
                        dirty = true;
                    }
                    if (dirty) serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                replacement.record.stationComponent = station;
                replacement.record.stationKind = PizzeriaStationKind.Prep;
                for (int i = 0; i < links.Count; i++)
                {
                    var link = links[i];
                    if (link.station != old) continue;
                    link.station = station;
                    links[i] = link;
                }
                UnityEngine.Object.DestroyImmediate(old);
                changed = true;
            }

            int relinked = 0;
            foreach (var link in links)
            {
                if (link.workpost.targetStation == link.station && link.workpost.slotIndex == link.index) continue;
                link.workpost.targetStation = link.station;
                link.workpost.slotIndex = link.index;
                relinked++;
                changed = true;
            }
            report = $"{replacements.Count} componente(s) substituído(s), {relinked} posto(s) religado(s); {unresolved} posto(s) sem âncora preservado(s) para revisão manual.";
            return true;
        }
    }
}
#endif
