using System;
using System.Collections.Generic;
using System.Text;

namespace ScaryParty.Pizzeria.Presentation
{
    public interface ISceneReadinessRequirement
    {
        string Name { get; }
        bool IsRequired { get; }
        bool IsSatisfied(out string diagnostic);
    }

    public sealed class DelegateReadinessRequirement : ISceneReadinessRequirement
    {
        private readonly Func<bool> _predicate;
        private readonly string _failureDiagnostic;

        public string Name { get; }
        public bool IsRequired { get; }

        public DelegateReadinessRequirement(string name, bool isRequired, Func<bool> predicate, string failureDiagnostic = null)
        {
            Name = name ?? "UnnamedRequirement";
            IsRequired = isRequired;
            _predicate = predicate ?? (() => false);
            _failureDiagnostic = failureDiagnostic;
        }

        public bool IsSatisfied(out string diagnostic)
        {
            if (_predicate())
            {
                diagnostic = null;
                return true;
            }

            diagnostic = string.IsNullOrWhiteSpace(_failureDiagnostic) ? $"{Name} ausente" : _failureDiagnostic;
            return false;
        }
    }

    /// <summary>
    /// Contrato de prontidão por destino que distingue dependências obrigatórias de opcionais
    /// e gera diagnóstico objetivo em caso de timeout.
    /// </summary>
    public sealed class SceneReadinessContract
    {
        private readonly List<ISceneReadinessRequirement> _requirements = new List<ISceneReadinessRequirement>();

        public string DestinationName { get; }

        public SceneReadinessContract(string destinationName)
        {
            DestinationName = string.IsNullOrWhiteSpace(destinationName) ? "Cena" : destinationName;
        }

        public SceneReadinessContract Add(ISceneReadinessRequirement requirement)
        {
            if (requirement != null)
            {
                _requirements.Add(requirement);
            }
            return this;
        }

        public bool Evaluate(List<string> missingRequired, List<string> missingOptional)
        {
            missingRequired?.Clear();
            missingOptional?.Clear();
            bool allRequiredSatisfied = true;

            for (int i = 0; i < _requirements.Count; i++)
            {
                var req = _requirements[i];
                if (req.IsSatisfied(out string diag)) continue;

                string entry = string.IsNullOrWhiteSpace(diag) ? req.Name : $"{req.Name}: {diag}";
                if (req.IsRequired)
                {
                    allRequiredSatisfied = false;
                    missingRequired?.Add(entry);
                }
                else
                {
                    missingOptional?.Add(entry);
                }
            }

            return allRequiredSatisfied;
        }

        public string BuildDiagnosticSummary()
        {
            var reqMissing = new List<string>();
            var optMissing = new List<string>();
            if (Evaluate(reqMissing, optMissing))
            {
                return optMissing.Count == 0
                    ? $"'{DestinationName}' pronto."
                    : $"'{DestinationName}' pronto (opcionais pendentes: {string.Join(", ", optMissing)}).";
            }

            var sb = new StringBuilder();
            sb.Append($"Prontidão incompleta em '{DestinationName}'. Obrigatórios ausentes: ");
            sb.Append(string.Join("; ", reqMissing));
            if (optMissing.Count > 0)
            {
                sb.Append($" | Opcionais: {string.Join("; ", optMissing)}");
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// Amostrador leve de duração por estágio de carregamento (sem alocações por frame).
    /// </summary>
    public sealed class StageTimingRecorder
    {
        private readonly Dictionary<string, float> _stageStarts = new Dictionary<string, float>();
        private readonly List<(string stage, float durationMs)> _completedStages = new List<(string, float)>();
        private string _activeStage;

        public IReadOnlyList<(string stage, float durationMs)> CompletedStages => _completedStages;

        public void Reset()
        {
            _stageStarts.Clear();
            _completedStages.Clear();
            _activeStage = null;
        }

        public void BeginStage(string stageName, float nowRealtime)
        {
            if (string.Equals(_activeStage, stageName, StringComparison.Ordinal)) return;
            EndActiveStage(nowRealtime);
            _activeStage = stageName;
            _stageStarts[stageName] = nowRealtime;
        }

        public void EndActiveStage(float nowRealtime)
        {
            if (string.IsNullOrEmpty(_activeStage)) return;
            if (_stageStarts.TryGetValue(_activeStage, out float start))
            {
                float ms = Math.Max(0f, (nowRealtime - start) * 1000f);
                _completedStages.Add((_activeStage, ms));
            }
            _activeStage = null;
        }

        public string BuildSummary()
        {
            if (_completedStages.Count == 0) return "sem estágios registrados";
            var sb = new StringBuilder();
            for (int i = 0; i < _completedStages.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append($"{_completedStages[i].stage}={_completedStages[i].durationMs:F0}ms");
            }
            return sb.ToString();
        }
    }
}
