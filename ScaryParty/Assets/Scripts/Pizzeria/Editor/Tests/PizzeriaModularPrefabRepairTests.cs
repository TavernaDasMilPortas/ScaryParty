#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Editor.Authoring;
using ScaryParty.Pizzeria.Stations;

namespace ScaryParty.Pizzeria.Editor.Tests
{
    public class PizzeriaModularPrefabRepairTests
    {
        private GameObject root;
        private PizzeriaConfig config;
        private StationDef definition;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Pizzeria");
            config = ScriptableObject.CreateInstance<PizzeriaConfig>();
            definition = ScriptableObject.CreateInstance<StationDef>();
            definition.acceptedProcessIds.Add(1);
            var pizzeria = root.AddComponent<PizzeriaRoot>();
            var serialized = new SerializedObject(pizzeria);
            serialized.FindProperty("_config").objectReferenceValue = config;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            root.AddComponent<PizzeriaAuthoringBindings>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(config);
            Object.DestroyImmediate(definition);
        }

        private AssemblyStation AddStation(out PizzeriaStationBindingRecord record, int id = 11)
        {
            var station = new GameObject("Bench").AddComponent<AssemblyStation>();
            station.transform.SetParent(root.transform);
            station.stationId = id;
            station.stationName = "Custom";
            station.slotsBoundToObjects = true;
            var anchor = new GameObject("Anchor").transform;
            anchor.SetParent(station.transform);
            station.slotAnchors = new[] { anchor };
            record = new PizzeriaStationBindingRecord
            {
                stationId = id, stationKind = PizzeriaStationKind.Assembly,
                stationDef = definition, stationComponent = station, bindingGuid = "original"
            };
            root.GetComponent<PizzeriaAuthoringBindings>().stationRecords.Add(record);
            config.stationInstances.Add(new StationInstanceConfig { instanceId = id, definition = definition });
            return station;
        }

        [Test]
        public void RepairsByExactAnchorAndPreservesExistingObjectsAndBindings()
        {
            var old = AddStation(out var record);
            var anchor = old.slotAnchors[0];
            var renderer = old.gameObject.AddComponent<MeshRenderer>();
            var material = new Material(Shader.Find("Sprites/Default"));
            renderer.sharedMaterial = material;
            var post = new GameObject("Board").AddComponent<StationWorkpostTarget>();
            post.transform.SetParent(root.transform);
            post.slotAnchor = anchor;
            post.targetStation = root.AddComponent<TicketBoardStation>();
            post.slotIndex = 7;
            var unresolved = new GameObject("Unresolved").AddComponent<StationWorkpostTarget>();
            unresolved.transform.SetParent(root.transform);
            unresolved.targetStation = post.targetStation;
            unresolved.slotIndex = 4;
            try
            {
                Assert.IsTrue(PizzeriaModularPrefabRepair.TryRepair(root, out var report, out var changed), report);
                Assert.IsTrue(changed);
                var prep = anchor.GetComponentInParent<PrepStation>();
                Assert.IsNotNull(prep);
                Assert.AreEqual(11, prep.stationId);
                Assert.AreEqual("Custom", prep.stationName);
                Assert.IsTrue(prep.slotsBoundToObjects);
                Assert.AreSame(anchor, prep.slotAnchors[0]);
                Assert.AreSame(prep, record.stationComponent);
                Assert.AreEqual("original", record.bindingGuid);
                Assert.AreEqual(PizzeriaStationKind.Prep, record.stationKind);
                Assert.AreSame(prep, post.targetStation);
                Assert.AreEqual(0, post.slotIndex);
                Assert.AreEqual(4, unresolved.slotIndex);
                Assert.AreSame(renderer.sharedMaterial, material);
                Assert.IsTrue(report.Contains("sem âncora"));
                Assert.IsTrue(PizzeriaModularPrefabRepair.TryRepair(root, out _, out changed));
                Assert.IsFalse(changed);
            }
            finally { Object.DestroyImmediate(material); }
        }

        [Test]
        public void DuplicateAnchorAbortsWithoutPartialChanges()
        {
            var first = AddStation(out _);
            var second = AddStation(out _, 13);
            second.slotAnchors[0] = first.slotAnchors[0];
            var post = new GameObject("Post").AddComponent<StationWorkpostTarget>();
            post.transform.SetParent(root.transform);
            post.slotAnchor = first.slotAnchors[0];
            Assert.IsFalse(PizzeriaModularPrefabRepair.TryRepair(root, out _, out var changed));
            Assert.IsFalse(changed);
            Assert.IsNotNull(first);
            Assert.IsNull(post.targetStation);
        }

        [Test]
        public void DuplicateConfigAbortsBeforeReplacement()
        {
            var station = AddStation(out var record);
            config.stationInstances.Add(new StationInstanceConfig { instanceId = station.stationId, definition = definition });
            Assert.IsFalse(PizzeriaModularPrefabRepair.TryRepair(root, out _, out var changed));
            Assert.IsFalse(changed);
            Assert.AreSame(station, record.stationComponent);
        }
    }
}
#endif
