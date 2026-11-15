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

        public static void EnsureBuilt(PizzeriaRoot root)
        {
            if (root == null) return;

            Transform existing = root.transform.Find(ContainerName);
            if (existing != null)
            {
                if (Application.isPlaying) Object.Destroy(existing.gameObject);
                else Object.DestroyImmediate(existing.gameObject);
            }

            Vector3 fwd = root.entranceAnchor != null
                ? Vector3.ProjectOnPlane(root.entranceAnchor.forward, Vector3.up).normalized
                : Vector3.ProjectOnPlane(root.transform.forward, Vector3.up).normalized;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;

            Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
            Vector3 entranceBase = root.entranceAnchor != null
                ? root.entranceAnchor.position
                : root.transform.position + fwd * 5f;

            // Escolhe lado direito ou esquerdo livre de colisão externa de prédios
            Vector3 sideDir = ChooseClearSide(entranceBase, right, fwd);
            Vector3 lotCenter = entranceBase + sideDir * 6.8f + fwd * 0.4f;
            float groundY = ResolveGroundY(lotCenter, entranceBase.y);
            lotCenter.y = groundY;

            Quaternion lotRot = Quaternion.LookRotation(fwd, Vector3.up);

            var container = new GameObject(ContainerName);
            container.transform.SetParent(root.transform, false);
            container.transform.SetPositionAndRotation(lotCenter, lotRot);

            // 1. Piso do estacionamento lateral + linha da vaga (estacionamento + ponte até a via)
            BuildAsphaltPad(container.transform);

            // 2. Moto de entregas no centro da vaga voltada para a rua
            Vector3 bikePos = lotCenter + fwd * 0.4f + Vector3.up * 0.05f;
            Bounds lotBounds = new Bounds(lotCenter + fwd * 1.6f, new Vector3(6.8f, 4f, 10.5f));
            BuildMotorcycle(container.transform, bikePos, lotRot, lotBounds);

            // 3. Rack externo de Armas (Pistola 1 mão + Escopeta 2 mãos) junto à parede frontal lateral
            Vector3 rackLocalPos = new Vector3(-2.15f * Mathf.Sign(Vector3.Dot(sideDir, right)), 0f, -1.85f);
            BuildWeaponRack(container.transform, rackLocalPos);

            // 4. Alvo de Prática no canto oposto do pátio para disparos e teste controlado de dano
            Vector3 dummyLocalPos = new Vector3(2.25f * Mathf.Sign(Vector3.Dot(sideDir, right)), 0f, -2.0f);
            BuildPracticeDummy(container.transform, dummyLocalPos);
        }

        private static Vector3 ChooseClearSide(Vector3 basePos, Vector3 right, Vector3 fwd)
        {
            Vector3 rightCand = basePos + right * 7.2f;
            Vector3 leftCand = basePos - right * 7.2f;

            bool rightBlocked = Physics.CheckSphere(rightCand + Vector3.up * 2f, 2.1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            bool leftBlocked = Physics.CheckSphere(leftCand + Vector3.up * 2f, 2.1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            if (rightBlocked && !leftBlocked) return -right;
            return right;
        }

        private static float ResolveGroundY(Vector3 pos, float fallbackY)
        {
            if (Physics.Raycast(pos + Vector3.up * 3f, Vector3.down, out var hit, 8f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return Mathf.Max(0f, fallbackY);
        }

        private static void BuildAsphaltPad(Transform parent)
        {
            var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pad.name = "ParkingLotPad";
            pad.transform.SetParent(parent, false);
            pad.transform.localPosition = new Vector3(0f, 0.02f, 0.5f);
            pad.transform.localScale = new Vector3(5.6f, 0.04f, 7.6f);
            DestroyCollider(pad);
            TintObject(pad, new Color(0.16f, 0.17f, 0.19f));

            for (int side = -1; side <= 1; side += 2)
            {
                var stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                stripe.name = side < 0 ? "StripeLeft" : "StripeRight";
                stripe.transform.SetParent(parent, false);
                stripe.transform.localPosition = new Vector3(side * 1.45f, 0.045f, 0.7f);
                stripe.transform.localScale = new Vector3(0.12f, 0.01f, 3.8f);
                DestroyCollider(stripe);
                TintObject(stripe, new Color(0.95f, 0.78f, 0.15f));
            }
        }

        private static void BuildMotorcycle(Transform parent, Vector3 worldPos, Quaternion rot, Bounds lotBounds)
        {
            var bike = new GameObject("DeliveryMotorcycle");
            bike.transform.SetParent(parent, false);
            bike.transform.SetPositionAndRotation(worldPos, rot);

            var collider = bike.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.6f, 0f);
            collider.size = new Vector3(0.9f, 1.15f, 2.1f);

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

            var seat = new GameObject("RiderSeatAnchor");
            seat.transform.SetParent(bike.transform, false);
            seat.transform.localPosition = new Vector3(0f, 0.78f, -0.1f);

            var ctrl = bike.AddComponent<DeliveryMotorcycleController>();
            ctrl.ConfigureParking(worldPos, rot, lotBounds, seat.transform);
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
            if (rend == null || rend.material == null) return;
            rend.material.color = color;
            if (rend.material.HasProperty("_BaseColor"))
                rend.material.SetColor("_BaseColor", color);
        }
    }
}
