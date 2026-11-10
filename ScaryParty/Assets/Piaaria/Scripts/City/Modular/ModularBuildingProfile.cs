using System;
using UnityEngine;

namespace ScaryParty.City.Modular
{
    /// <summary>
    /// Perfil de configuração para geração de prédios modulares por zona (Residencial, Comercial, etc.).
    /// Define limites de módulos, número de andares e pesos de distribuição das peças.
    /// </summary>
    [CreateAssetMenu(fileName = "ModularBuildingProfile", menuName = "ScaryParty/City/Modular/Building Profile")]
    public class ModularBuildingProfile : ScriptableObject
    {
        [Header("Zona e Tipologia")]
        public ZoneType zone = ZoneType.Commercial;

        [Header("Limites de Dimensão (em Módulos de 2m)")]
        [Tooltip("Largura mínima em módulos de 2m (Ex: 2 = 4 metros).")]
        [Range(2, 30)] public int minModulesWidth = 2;

        [Tooltip("Largura máxima em módulos de 2m (Ex: 6 = 12 metros).")]
        [Range(2, 50)] public int maxModulesWidth = 6;

        [Tooltip("Profundidade mínima em módulos de 2m (Ex: 2 = 4 metros).")]
        [Range(2, 30)] public int minModulesDepth = 2;

        [Tooltip("Profundidade máxima em módulos de 2m (Ex: 6 = 12 metros).")]
        [Range(2, 50)] public int maxModulesDepth = 6;

        [Tooltip("Número mínimo de pavimentos.")]
        [Range(1, 50)] public int minFloors = 1;

        [Tooltip("Número máximo de pavimentos.")]
        [Range(1, 50)] public int maxFloors = 3;

        [Header("Pesos — Fachada Térrea")]
        [Tooltip("Chance relativa de porta dupla (J) vs porta simples (C).")]
        [Range(0f, 1f)] public float doubleDoorRatio = 0.3f;

        [Tooltip("Chance de vitrine (D) em slots restantes do térreo.")]
        [Range(0f, 1f)] public float shopfrontWeight = 0.5f;

        [Tooltip("Chance de garagem (I) se a fachada tiver largura >= 4 módulos.")]
        [Range(0f, 1f)] public float garageWeight = 0.2f;

        [Tooltip("Chance de janelas B/G/H no térreo.")]
        [Range(0f, 1f)] public float groundWindowWeight = 0.3f;

        [Header("Pesos — Andares Superiores")]
        [Tooltip("Chance de janela veneziana (G) vs janela normal (B).")]
        [Range(0f, 1f)] public float shuttersWeight = 0.35f;

        [Tooltip("Chance de janela com floreira (H) vs janela normal (B).")]
        [Range(0f, 1f)] public float planterWeight = 0.25f;

        [Tooltip("Chance de parede lisa (A) nos andares superiores da frente.")]
        [Range(0f, 1f)] public float upperSolidWallWeight = 0.15f;

        [Header("Detalhes")]
        [Tooltip("Chance de toldo (K) sobre vitrines D ou portas no térreo.")]
        [Range(0f, 1f)] public float awningChance = 0.4f;

        public static ModularBuildingProfile CreateDefault(ZoneType zone)
        {
            var profile = CreateInstance<ModularBuildingProfile>();
            profile.zone = zone;

            switch (zone)
            {
                case ZoneType.Residential:
                    profile.minModulesWidth = 3; // 6m
                    profile.maxModulesWidth = 5; // 10m
                    profile.minModulesDepth = 3; // 6m
                    profile.maxModulesDepth = 4; // 8m
                    profile.minFloors = 1;
                    profile.maxFloors = 2;
                    profile.doubleDoorRatio = 0.2f;
                    profile.shopfrontWeight = 0.0f;
                    profile.garageWeight = 0.35f;
                    profile.groundWindowWeight = 0.6f;
                    profile.shuttersWeight = 0.4f;
                    profile.planterWeight = 0.35f;
                    profile.awningChance = 0.1f;
                    break;

                case ZoneType.Commercial:
                    profile.minModulesWidth = 3; // 6m
                    profile.maxModulesWidth = 6; // 12m
                    profile.minModulesDepth = 3; // 6m
                    profile.maxModulesDepth = 5; // 10m
                    profile.minFloors = 1;
                    profile.maxFloors = 4;
                    profile.doubleDoorRatio = 0.5f;
                    profile.shopfrontWeight = 0.7f;
                    profile.garageWeight = 0.1f;
                    profile.groundWindowWeight = 0.1f;
                    profile.shuttersWeight = 0.2f;
                    profile.planterWeight = 0.1f;
                    profile.awningChance = 0.6f;
                    break;

                case ZoneType.Industrial:
                    profile.minModulesWidth = 4; // 8m
                    profile.maxModulesWidth = 6; // 12m
                    profile.minModulesDepth = 4; // 8m
                    profile.maxModulesDepth = 6; // 12m
                    profile.minFloors = 1;
                    profile.maxFloors = 2;
                    profile.doubleDoorRatio = 0.7f;
                    profile.shopfrontWeight = 0.0f;
                    profile.garageWeight = 0.6f;
                    profile.groundWindowWeight = 0.2f;
                    profile.shuttersWeight = 0.1f;
                    profile.planterWeight = 0.0f;
                    profile.awningChance = 0.1f;
                    break;

                case ZoneType.MonsterZone:
                    profile.minModulesWidth = 3;
                    profile.maxModulesWidth = 5;
                    profile.minModulesDepth = 3;
                    profile.maxModulesDepth = 5;
                    profile.minFloors = 1;
                    profile.maxFloors = 3;
                    profile.doubleDoorRatio = 0.3f;
                    profile.shopfrontWeight = 0.2f;
                    profile.garageWeight = 0.2f;
                    profile.groundWindowWeight = 0.3f;
                    profile.shuttersWeight = 0.3f;
                    profile.planterWeight = 0.05f;
                    profile.awningChance = 0.2f;
                    break;
            }

            return profile;
        }
    }
}
