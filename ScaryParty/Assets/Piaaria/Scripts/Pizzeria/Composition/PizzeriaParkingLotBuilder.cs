using ScaryParty.City;
using ScaryParty.Combat;
using ScaryParty.Pizzeria.Stations;
using UnityEngine;

namespace ScaryParty.Pizzeria.Composition
{
    /// <summary>
    /// Construtor runtime idempotente do pátio externo da pizzaria:
    /// vaga de estacionamento lateral, moto de entrega, rack de armas (Pistola + Escopeta)
    /// e alvo de prática para combate/teste de dano em mochila.
    /// </summary>
    public static class PizzeriaParkingLotBuilder
    {
        private const string ContainerName = "__PizzeriaExteriorFacilities";
        private static readonly MaterialPropertyBlock _sharedPropBlock = new MaterialPropertyBlock();

        public static void EnsureBuilt(PizzeriaRoot root)
        {
            if (root == null) return;

            var footprint = root.GetComponent<PizzeriaFootprint>();
            var anchors = root.GetComponent<PizzeriaExteriorAnchors>();
            if (anchors == null) anchors = root.gameObject.AddComponent<PizzeriaExteriorAnchors>();

            Transform existing = root.transform.Find(ContainerName);
            if (existing != null)
            {
                // Se já existe e está válido com controlador de moto e rack, preserva idempotentemente
                var existingBike = existing.GetComponentInChildren<DeliveryMotorcycleController>(true);
                var existingRack = existing.GetComponentInChildren<WeaponRackStation>(true);
                if (existingBike != null && existingRack != null && anchors.HasCompleteExteriorAnchors())
                {
                    return;
                }

                if (Application.isPlaying) Object.Destroy(existing.gameObject);
                else Object.DestroyImmediate(existing.gameObject);
            }

            // Determinístico a partir do recuo do L em PizzeriaFootprint (X: 0..16.4, Z: -27.6..-13.2)
            Rect patioRect = footprint != null
                ? footprint.GetExteriorRegions()[0]
                : Rect.MinMaxRect(0f, -27.6f, 16.4f, -13.2f);
            float floorLocalY = footprint != null ? footprint.floorSurfaceLocalY : -1.6f;

            Vector3 localPatioCenter = new Vector3(patioRect.center.x, floorLocalY, patioRect.center.y);
            Vector3 worldPatioCenter = root.transform.TransformPoint(localPatioCenter);

            Vector3 streetOutwardLocal = footprint != null
                ? footprint.GetEntranceDirectionLocal3D()
                : Vector3.left;
            Vector3 streetOutwardWorld = root.transform.TransformDirection(streetOutwardLocal).normalized;
            if (streetOutwardWorld.sqrMagnitude < 0.01f) streetOutwardWorld = root.transform.forward;
            Quaternion lotRot = Quaternion.LookRotation(streetOutwardWorld, Vector3.up);

            var container = new GameObject(ContainerName);
            container.transform.SetParent(root.transform, false);
            container.transform.SetPositionAndRotation(worldPatioCenter, lotRot);

            // 1. Piso sólido do pátio com colisor real (apoiado no chão da quadra) + faixas de vaga
            BuildAsphaltPad(container.transform, patioRect.width, patioRect.height);

            // Âncoras explícitas dos setores do pátio
            anchors.exteriorGround = ResolveOrCreateAnchor(container.transform, "ExteriorGround", Vector3.zero, Quaternion.identity);
            anchors.streetExit = ResolveOrCreateAnchor(container.transform, "StreetExit", new Vector3(0f, 0.05f, patioRect.width * 0.45f), Quaternion.identity);

            // 2. Garagem + Moto (lado frontal do recuo voltado para a saída da rua)
            Vector3 bikeLocalOffset = new Vector3(-2.6f, 0.05f, 1.8f);
            Vector3 bikeWorldPos = container.transform.TransformPoint(bikeLocalOffset);
            anchors.motorcycleSpawn_01 = ResolveOrCreateAnchor(container.transform, "MotorcycleSpawn_01", bikeLocalOffset, Quaternion.identity);
            anchors.motorcycleRecovery = ResolveOrCreateAnchor(container.transform, "MotorcycleRecovery", bikeLocalOffset + new Vector3(0f, 0.05f, -0.5f), Quaternion.identity);
            anchors.dismountLeft = ResolveOrCreateAnchor(container.transform, "DismountLeft", bikeLocalOffset + new Vector3(-1.35f, 0.05f, 0f), Quaternion.identity);
            anchors.dismountRight = ResolveOrCreateAnchor(container.transform, "DismountRight", bikeLocalOffset + new Vector3(1.35f, 0.05f, 0f), Quaternion.identity);

            Bounds lotBounds = new Bounds(worldPatioCenter, new Vector3(patioRect.width, 4f, patioRect.height));
            BuildMotorcycle(container.transform, bikeWorldPos, lotRot, lotBounds);

            // 3. Expedição e Recebimento (separados da manobra da moto e do treino)
            anchors.dispatch = ResolveOrCreateAnchor(container.transform, "Dispatch", new Vector3(-3.8f, 0.05f, -3.5f), Quaternion.identity);
            anchors.supplierReceiving = ResolveOrCreateAnchor(container.transform, "SupplierReceiving", new Vector3(3.8f, 0.05f, -4.5f), Quaternion.identity);

            // 4. Setor de Treino isolado por anteparo (Rack de Armas + Posição de Tiro + Boneco)
            BuildTrainingBackstop(container.transform, new Vector3(3.6f, 0f, 1.5f));
            Vector3 rackLocalPos = new Vector3(1.8f, 0f, 3.6f);
            Vector3 shooterLocalPos = new Vector3(2.2f, 0.05f, 1.8f);
            Vector3 dummyLocalPos = new Vector3(5.2f, 0f, 1.8f);
            anchors.weaponRack = ResolveOrCreateAnchor(container.transform, "WeaponRack_Anchor", rackLocalPos, Quaternion.identity);
            anchors.practiceShooter = ResolveOrCreateAnchor(container.transform, "PracticeShooter", shooterLocalPos, Quaternion.Euler(0f, 90f, 0f));
            anchors.practiceTarget = ResolveOrCreateAnchor(container.transform, "PracticeTarget_Anchor", dummyLocalPos, Quaternion.Euler(0f, -90f, 0f));

            BuildWeaponRack(container.transform, rackLocalPos);
            BuildPracticeDummy(container.transform, dummyLocalPos);
        }

        private static Transform ResolveOrCreateAnchor(Transform parent, string name, Vector3 localPos, Quaternion localRot)
        {
            var existing = parent.Find(name);
            if (existing != null) return existing;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            return go.transform;
        }

        private static void BuildAsphaltPad(Transform parent, float width, float depth)
        {
            float padW = Mathf.Clamp(width - 0.6f, 6f, 15.8f);
            float padD = Mathf.Clamp(depth - 0.6f, 6f, 13.8f);

            var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pad.name = "ParkingLotPad";
            pad.transform.SetParent(parent, false);
            pad.transform.localPosition = new Vector3(0f, -0.02f, 0f);
            pad.transform.localScale = new Vector3(padW, 0.06f, padD);
            // Mantém o BoxCollider no piso para apoio real de jogadores, NPCs e moto!
            TintObject(pad, new Color(0.16f, 0.17f, 0.19f));

            for (int side = -1; side <= 1; side += 2)
            {
                var stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                stripe.name = side < 0 ? "StripeLeft" : "StripeRight";
                stripe.transform.SetParent(parent, false);
                stripe.transform.localPosition = new Vector3(-2.6f + side * 1.15f, 0.02f, 1.8f);
                stripe.transform.localScale = new Vector3(0.12f, 0.01f, 3.6f);
                DestroyCollider(stripe);
                TintObject(stripe, new Color(0.95f, 0.78f, 0.15f));
            }
        }

        private static void BuildTrainingBackstop(Transform parent, Vector3 localCenter)
        {
            var backstop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backstop.name = "TrainingSafetyBackstop";
            backstop.transform.SetParent(parent, false);
            backstop.transform.localPosition = localCenter + new Vector3(2.4f, 1.25f, 0f);
            backstop.transform.localScale = new Vector3(0.28f, 2.5f, 4.2f);
            TintObject(backstop, new Color(0.28f, 0.24f, 0.22f));
        }

        private static void BuildMotorcycle(Transform parent, Vector3 worldPos, Quaternion rot, Bounds lotBounds)
        {
            var bike = new GameObject("DeliveryMotorcycle");
            bike.transform.SetParent(parent, false);
            bike.transform.SetPositionAndRotation(worldPos, rot);

            var collider = bike.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.6f, 0f);
            collider.size = new Vector3(0.9f, 1.15f, 2.1f);

            var rb = bike.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            var bikeRig = bike.AddComponent<ScaryParty.Pizzeria.Presentation.EntityVisualAnchorRig>();
            bikeRig.rigRole = ScaryParty.Pizzeria.Presentation.EntityVisualAnchorRig.RigRole.Motorcycle;
            bikeRig.placeholderId = "delivery_motorcycle";

            var manifest = ScaryParty.Pizzeria.Authoring.PlaytestReferenceManifest.LoadDefault();
            Transform seat = null;
            Transform hbLeft = null;
            Transform hbRight = null;
            Transform leftExit = null;
            Transform rightExit = null;
            Transform cargoRack = null;

            if (manifest != null && manifest.motorcycleVisualPrefab != null)
            {
                var inst = Object.Instantiate(manifest.motorcycleVisualPrefab, bike.transform, false);
                inst.name = "MotorcycleVisual";
                inst.transform.localPosition = Vector3.zero;
                inst.transform.localRotation = Quaternion.identity;
                foreach (var c in inst.GetComponentsInChildren<Collider>(true)) DestroyCollider(c.gameObject);

                var prefabRig = inst.GetComponentInChildren<ScaryParty.Pizzeria.Presentation.EntityVisualAnchorRig>(true);
                if (prefabRig != null)
                {
                    seat = prefabRig.seatAnchor;
                    hbLeft = prefabRig.handlebarLeftAnchor;
                    hbRight = prefabRig.handlebarRightAnchor;
                    leftExit = prefabRig.leftDismountAnchor;
                    rightExit = prefabRig.rightDismountAnchor;
                    cargoRack = prefabRig.cargoAnchor;
                }
            }
            else
            {
                var chassis = GameObject.CreatePrimitive(PrimitiveType.Cube);
                chassis.name = "Chassis";
                chassis.transform.SetParent(bike.transform, false);
                chassis.transform.localPosition = new Vector3(0f, 0.58f, 0f);
                chassis.transform.localScale = new Vector3(0.45f, 0.42f, 1.65f);
                DestroyCollider(chassis);
                TintObject(chassis, new Color(0.82f, 0.14f, 0.14f));

                var wheelFront = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                wheelFront.name = "WheelFront";
                wheelFront.transform.SetParent(bike.transform, false);
                wheelFront.transform.localPosition = new Vector3(0f, 0.32f, 0.72f);
                wheelFront.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                wheelFront.transform.localScale = new Vector3(0.62f, 0.09f, 0.62f);
                DestroyCollider(wheelFront);
                TintObject(wheelFront, new Color(0.1f, 0.1f, 0.12f));

                var wheelBack = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                wheelBack.name = "WheelBack";
                wheelBack.transform.SetParent(bike.transform, false);
                wheelBack.transform.localPosition = new Vector3(0f, 0.32f, -0.72f);
                wheelBack.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                wheelBack.transform.localScale = new Vector3(0.62f, 0.11f, 0.62f);
                DestroyCollider(wheelBack);
                TintObject(wheelBack, new Color(0.1f, 0.1f, 0.12f));

                var handlebar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                handlebar.name = "Handlebar";
                handlebar.transform.SetParent(bike.transform, false);
                handlebar.transform.localPosition = new Vector3(0f, 1.02f, 0.56f);
                handlebar.transform.localScale = new Vector3(0.75f, 0.08f, 0.1f);
                DestroyCollider(handlebar);
                TintObject(handlebar, new Color(0.75f, 0.78f, 0.82f));
            }

            if (seat == null) seat = bikeRig.ResolveAnchor(ref bikeRig.seatAnchor, "RiderSeatAnchor", new Vector3(0f, 0.78f, -0.1f), Vector3.zero);
            else bikeRig.seatAnchor = seat;
            if (hbLeft == null) hbLeft = bikeRig.ResolveAnchor(ref bikeRig.handlebarLeftAnchor, "HandlebarLeftAnchor", new Vector3(-0.32f, 1.02f, 0.52f), Vector3.zero);
            else bikeRig.handlebarLeftAnchor = hbLeft;
            if (hbRight == null) hbRight = bikeRig.ResolveAnchor(ref bikeRig.handlebarRightAnchor, "HandlebarRightAnchor", new Vector3(0.32f, 1.02f, 0.52f), Vector3.zero);
            else bikeRig.handlebarRightAnchor = hbRight;
            if (leftExit == null) leftExit = bikeRig.ResolveAnchor(ref bikeRig.leftDismountAnchor, "LeftExitAnchor", new Vector3(-1.35f, 0.05f, 0f), Vector3.zero);
            else bikeRig.leftDismountAnchor = leftExit;
            if (rightExit == null) rightExit = bikeRig.ResolveAnchor(ref bikeRig.rightDismountAnchor, "RightExitAnchor", new Vector3(1.35f, 0.05f, 0f), Vector3.zero);
            else bikeRig.rightDismountAnchor = rightExit;
            if (cargoRack == null) cargoRack = bikeRig.ResolveAnchor(ref bikeRig.cargoAnchor, "CargoRackAnchor", new Vector3(0f, 0.86f, -0.78f), Vector3.zero);
            else bikeRig.cargoAnchor = cargoRack;

            var ctrl = bike.AddComponent<DeliveryMotorcycleController>();
            ctrl.ConfigureParking(
                worldPos,
                rot,
                lotBounds,
                seat,
                hbLeft,
                hbRight,
                leftExit,
                rightExit,
                cargoRack);
        }

        private static void BuildWeaponRack(Transform parent, Vector3 localPos)
        {
            var rackGo = new GameObject("WeaponRackStation");
            rackGo.transform.SetParent(parent, false);
            rackGo.transform.localPosition = localPos;
            rackGo.transform.localRotation = Quaternion.identity;

            var rackCol = rackGo.AddComponent<BoxCollider>();
            rackCol.center = new Vector3(0f, 0.9f, 0f);
            rackCol.size = new Vector3(1.5f, 1.8f, 0.5f);

            var frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frame.name = "RackBoard";
            frame.transform.SetParent(rackGo.transform, false);
            frame.transform.localPosition = new Vector3(0f, 0.95f, 0f);
            frame.transform.localScale = new Vector3(1.45f, 1.75f, 0.14f);
            DestroyCollider(frame);
            TintObject(frame, new Color(0.24f, 0.17f, 0.12f));

            // Slot Pistola
            var pistolSlot = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pistolSlot.name = "RackSlot_Pistol";
            pistolSlot.transform.SetParent(rackGo.transform, false);
            pistolSlot.transform.localPosition = new Vector3(-0.36f, 1.15f, 0.12f);
            pistolSlot.transform.localScale = new Vector3(0.35f, 0.22f, 0.14f);
            TintObject(pistolSlot, new Color(0.18f, 0.22f, 0.28f));

            // Slot Escopeta
            var shotgunSlot = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shotgunSlot.name = "RackSlot_Shotgun";
            shotgunSlot.transform.SetParent(rackGo.transform, false);
            shotgunSlot.transform.localPosition = new Vector3(0.32f, 1.15f, 0.12f);
            shotgunSlot.transform.localScale = new Vector3(0.55f, 0.24f, 0.14f);
            TintObject(shotgunSlot, new Color(0.32f, 0.24f, 0.18f));

            var rackRig = rackGo.AddComponent<ScaryParty.Pizzeria.Presentation.EntityVisualAnchorRig>();
            rackRig.rigRole = ScaryParty.Pizzeria.Presentation.EntityVisualAnchorRig.RigRole.Station;
            rackRig.placeholderId = "weapon_rack";
            rackRig.stationSlots = new[] { pistolSlot.transform, shotgunSlot.transform };

            var rack = rackGo.AddComponent<WeaponRackStation>();
            rack.ConfigureVisuals(pistolSlot, shotgunSlot);

            pistolSlot.AddComponent<WeaponRackSlotInteractable>().Configure(rack, WeaponType.Pistol);
            shotgunSlot.AddComponent<WeaponRackSlotInteractable>().Configure(rack, WeaponType.Shotgun);
        }

        private static void BuildPracticeDummy(Transform parent, Vector3 localPos)
        {
            var dummyGo = new GameObject("PracticeTargetDummy");
            dummyGo.transform.SetParent(parent, false);
            dummyGo.transform.localPosition = localPos;
            dummyGo.transform.localRotation = Quaternion.identity;

            var col = dummyGo.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.95f, 0f);
            col.size = new Vector3(0.8f, 1.85f, 0.5f);

            var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            post.name = "Post";
            post.transform.SetParent(dummyGo.transform, false);
            post.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            post.transform.localScale = new Vector3(0.12f, 0.55f, 0.12f);
            DestroyCollider(post);
            TintObject(post, new Color(0.3f, 0.22f, 0.15f));

            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "TargetBoard";
            board.transform.SetParent(dummyGo.transform, false);
            board.transform.localPosition = new Vector3(0f, 1.28f, 0f);
            board.transform.localScale = new Vector3(0.75f, 0.9f, 0.12f);
            DestroyCollider(board);
            TintObject(board, new Color(0.85f, 0.25f, 0.2f));

            dummyGo.AddComponent<PracticeTargetDummy>();
        }

        private static void DestroyCollider(GameObject go)
        {
            var c = go.GetComponent<Collider>();
            if (c == null) return;
            if (Application.isPlaying) Object.Destroy(c);
            else Object.DestroyImmediate(c);
        }

        private static void TintObject(GameObject go, Color color)
        {
            var rend = go.GetComponent<Renderer>();
            if (rend == null) return;

            var baseMat = ScaryParty.VisualStyles.VisualStyleManager.ResolveMaterialOrFallback(
                rend.sharedMaterial,
                ScaryParty.VisualStyles.VisualSemanticRole.BuildingFacade,
                ScaryParty.VisualStyles.VisualCategoryIds.City,
                () => rend.sharedMaterial);

            if (baseMat != null && rend.sharedMaterial != baseMat)
            {
                rend.sharedMaterial = baseMat;
            }

            ScaryParty.VisualStyles.VisualStyleShaderUtility.ApplyColorBlockToSlot(rend, 0, color, _sharedPropBlock);
        }
    }
}
