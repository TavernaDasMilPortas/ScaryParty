using System;
using System.Collections.Generic;
using UnityEngine;

namespace ScaryParty.Pizzeria.Authoring
{
    [Serializable]
    public class PizzeriaCandidateAssociation
    {
        public string candidateNodeGuid;
        public string candidateName;
        public float score;
        public string reason;
    }

    /// <summary>
    /// Identidade persistente anexada aos nós filhos do prefab da pizzaria.
    /// Substitui GetInstanceID() e preserva vínculos entre sessões de Prefab Stage.
    /// </summary>
    [DisallowMultipleComponent]
    public class PizzeriaNodeIdentity : MonoBehaviour
    {
        [Tooltip("Identificador estável único deste nó dentro do prefab.")]
        public string nodeGuid;

        [Tooltip("Nome original do nó quando importado do modelo FBX.")]
        public string originalNodeName;

        [Tooltip("Caminho relativo original no FBX.")]
        public string originalRelativePath;

        [Tooltip("Categoria organizacional atribuída.")]
        public PizzeriaHierarchyCategory category = PizzeriaHierarchyCategory.Unclassified;

        [Tooltip("Se verdadeiro, o organizador automático não moverá este nó de seu pai atual.")]
        public bool manualAssociationLocked;

        [Tooltip("GUID do nó pai fixado manualmente, se houver.")]
        public string lockedParentGuid;

        [Tooltip("Se verdadeiro, este nó possui associação ambígua ou pendente de revisão.")]
        public bool pendingReview;

        [Tooltip("Motivo pelo qual o objeto está em Revisar ou requer atenção.")]
        public string reviewReason;

        [Tooltip("Papel visual atribuído na Etapa 3 (decoração, utensílio, caixa, mochila etc.).")]
        public PizzeriaVisualRole visualRole = PizzeriaVisualRole.None;

        [Tooltip("ID de definição de ingrediente/utensílio associado, quando aplicável.")]
        public int boundDefinitionId;

        [Tooltip("Candidatos sugeridos pelo organizador de hierarquia quando há ambiguidade.")]
        public List<PizzeriaCandidateAssociation> candidates = new List<PizzeriaCandidateAssociation>();

        public void EnsureGuid()
        {
            if (string.IsNullOrEmpty(nodeGuid))
            {
                nodeGuid = Guid.NewGuid().ToString("N");
            }
        }
    }
}
