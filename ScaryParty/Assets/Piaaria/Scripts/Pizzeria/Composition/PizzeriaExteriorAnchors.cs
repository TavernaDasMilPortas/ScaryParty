using UnityEngine;

namespace ScaryParty.Pizzeria.Composition
{
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
