using System;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

namespace ScaryParty.Pizzeria.Composition
{
    public enum PizzeriaLayoutVariant
    {
        L_RecuoOperacional = 0,
        T_PatioLateral = 1
    }

    [Serializable]
    public class ModularPieceCatalogEntry
    {
        public string pieceName;
        public string category;
        public Vector3 boundsSize;
        public Vector3 boundsCenter;
        public bool hasCollider;
        public int materialCount;
    }

    /// <summary>
    /// Perfil autorado do layout da pizzaria (variante L ou T), regiões, âncoras e catálogo de peças reais.
    /// </summary>
    [CreateAssetMenu(fileName = "PizzeriaLayoutProfile", menuName = "Scary Party/Pizzeria/Layout Profile")]
    public class PizzeriaLayoutProfile : ScriptableObject
    {
        [TabGroup("Layout")]
        public PizzeriaLayoutVariant variant = PizzeriaLayoutVariant.L_RecuoOperacional;

        [TabGroup("Layout")]
        public float gridStep = 2.0f;

        [TabGroup("Layout")]
        public int profileVersion = 1;

        [TabGroup("Layout")]
        public Rect diningHallRect = Rect.MinMaxRect(0f, -13.2f, 32.8f, 9.2f);

        [TabGroup("Layout")]
        public Rect kitchenRect = Rect.MinMaxRect(16.4f, -27.6f, 32.8f, -13.2f);

        [TabGroup("Pátio")]
        public Rect patioRectL = Rect.MinMaxRect(0f, -27.6f, 16.4f, -13.2f);

        [TabGroup("Pátio")]
        public Rect patioRectT = Rect.MinMaxRect(-16.4f, -27.6f, 0f, -9.2f);

        [TabGroup("Pátio")]
        public Rect[] accessCorridors = new Rect[] { Rect.MinMaxRect(-3.5f, -27.6f, 0f, 9.2f) };

        [TabGroup("Peças")]
        public List<ModularPieceCatalogEntry> scannedPieces = new List<ModularPieceCatalogEntry>();

        [TabGroup("Spawns")]
        public Vector3[] playerSpawnLocalOffsets = new Vector3[]
        {
            new Vector3(4.5f, 0.05f, -2.5f),
            new Vector3(6.5f, 0.05f, -2.5f),
            new Vector3(4.5f, 0.05f, -4.5f),
            new Vector3(6.5f, 0.05f, -4.5f)
        };

        [TabGroup("Spawns")]
        public Vector3 playerRecoveryLocalOffset = new Vector3(5.5f, 0.05f, -6.5f);

        [TabGroup("Spawns")]
        public Vector3 employeeArrivalLocalOffset = new Vector3(2.0f, 0.05f, -15.5f);

        [TabGroup("Spawns")]
        public Vector3 employeeCashierSpawnLocalOffset = new Vector3(6.0f, 0.05f, -1.5f);

        [TabGroup("Spawns")]
        public Vector3 employeeCookSpawnLocalOffset = new Vector3(20.0f, 0.05f, -18.0f);

        [TabGroup("Spawns")]
        public Vector3 employeeWaitingLocalOffset = new Vector3(4.0f, 0.05f, -16.5f);

        [TabGroup("Spawns")]
        public Vector3 employeeRecoveryLocalOffset = new Vector3(6.0f, 0.05f, -16.5f);

        [TabGroup("Navegação")]
        public float minPedestrianPassageWidth = 1.6f;

        [TabGroup("Navegação")]
        public float minServiceCorridorWidth = 2.0f;

        [TabGroup("Navegação")]
        public float minManeuverWidth = 3.2f;

        public Rect GetActivePatioRect()
        {
            return variant == PizzeriaLayoutVariant.T_PatioLateral ? patioRectT : patioRectL;
        }
    }

    /// <summary>
    /// Âncoras explícitas do pátio externo e spawns da pizzaria, autoradas no prefab ou resolvidas deterministicamente.
    /// </summary>
    [DisallowMultipleComponent]
    public class PizzeriaExteriorAnchors : MonoBehaviour
    {
        [Header("Versão e Perfil")]
        public PizzeriaLayoutVariant layoutVariant = PizzeriaLayoutVariant.L_RecuoOperacional;
        public int authoredVersion = 1;

        [Header("Garagem & Moto")]
        public Transform motorcycleSpawn_01;
        public Transform motorcycleRecovery;
        public Transform dismountLeft;
        public Transform dismountRight;
        public Transform streetExit;

        [Header("Operação Externa")]
        public Transform dispatch;
        public Transform supplierReceiving;
        public Transform exteriorGround;

        [Header("Setor de Treino")]
        public Transform weaponRack;
        public Transform practiceTarget;
        public Transform practiceShooter;

        [Header("Spawns de Jogadores e Contratados")]
        public Transform[] playerSpawns = new Transform[4];
        public Transform playerRecovery;
        public Transform employeeArrival;
        public Transform employeeSpawnCashier;
        public Transform employeeSpawnCook;
        public Transform employeeWaiting;
        public Transform employeeRecovery;

        public bool HasCompleteExteriorAnchors()
        {
            return motorcycleSpawn_01 != null &&
                   motorcycleRecovery != null &&
                   dismountLeft != null &&
                   dismountRight != null &&
                   dispatch != null &&
                   supplierReceiving != null &&
                   weaponRack != null &&
                   practiceTarget != null &&
                   practiceShooter != null &&
                   exteriorGround != null &&
                   streetExit != null;
        }

        public bool HasCompleteSpawnAnchors()
        {
            if (playerRecovery == null || employeeArrival == null ||
                employeeSpawnCashier == null || employeeSpawnCook == null ||
                employeeWaiting == null || employeeRecovery == null)
                return false;

            if (playerSpawns == null || playerSpawns.Length < 4) return false;
            for (int i = 0; i < 4; i++)
            {
                if (playerSpawns[i] == null) return false;
            }
            return true;
        }
    }
}
