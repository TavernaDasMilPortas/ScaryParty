using System;
using System.Collections.Generic;
using ScaryParty.Pizzeria.Domain.Definitions;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Domain.Services
{
    public enum EmployeeRole : byte
    {
        Cashier = 0, // Balconista (caro)
        Pizzaiolo = 1
    }

    public enum EmployeeTaskState : byte
    {
        Available = 0,
        Reserved = 1,
        InProgress = 2,
        Blocked = 3,
        Completed = 4,
        Cancelled = 5
    }

    public enum EmployeeBlockReason : byte
    {
        None = 0,
        NoDough = 1,
        MissingIngredient = 2,
        OvenBusy = 3,
        ToolInUse = 4,
        PathBlocked = 5
    }

    public sealed class HiredEmployeeState
    {
        public int EmployeeId;
        public EmployeeRole Role;
        public int Level = 1;
        public bool IsAlive = true;
        public float BaseErrorRate = 0.20f;
        public int AssignedTaskId = -1;
    }

    public sealed class SharedEmployeeTask
    {
        public int TaskId;
        public OrderId TargetOrderId;
        public EmployeeRole RequiredRole;
        public EmployeeTaskState State = EmployeeTaskState.Available;
        public EmployeeBlockReason BlockReason = EmployeeBlockReason.None;
        public ulong ReservedByActorId; // 0 = livre; >0 = ID de funcionário ou jogador humano
        public bool IsReservedByHuman;
        public double LeaseExpiresAt;
        public bool IntentionalErrorTriggered;
        public int CurrentLineIndex;
        public int UnitsCompletedInLine;
        public int TotalUnitsCompleted;
        public int TotalUnitsRequired;
    }

    /// <summary>
    /// Coordenador de tarefas e ciclo de contratação de Balconistas e Pizzaiolos.
    /// Garante reservas exclusivas entre funcionários e humanos (T58), motivos de bloqueio,
    /// erro intencional determinístico por RNG e persistência de contratação entre noites.
    /// </summary>
    public sealed class EmployeeTaskService
    {
        public bool RestoreLostEmployeesNextNight { get; set; } = true;
        public float MinSkillErrorRate { get; set; } = 0.02f;

        private readonly Dictionary<int, HiredEmployeeState> _employees = new Dictionary<int, HiredEmployeeState>();
        private readonly Dictionary<int, SharedEmployeeTask> _tasks = new Dictionary<int, SharedEmployeeTask>();
        private RunBalancingSnapshot _balancingSnapshot;
        private int _nextEmployeeId = 1;
        private int _nextTaskId = 1;

        public EmployeeTaskService(RunBalancingSnapshot balancingSnapshot = null)
        {
            _balancingSnapshot = balancingSnapshot;
        }

        public void SetBalancingSnapshot(RunBalancingSnapshot balancingSnapshot)
        {
            _balancingSnapshot = balancingSnapshot;
        }

        public EmployeeRoleProfileData GetRoleProfile(EmployeeRole role)
        {
            var found = _balancingSnapshot?.FindEmployeeProfile(role);
            if (found != null) return found;

            return role == EmployeeRole.Cashier
                ? new EmployeeRoleProfileData(EmployeeRole.Cashier, "Balconista", 180, 5, 1.0f, 0, 0.05f, 0.15f, 0.02f, 2.2f)
                : new EmployeeRoleProfileData(EmployeeRole.Pizzaiolo, "Pizzaiolo", 120, 5, 1.0f, 0, 0.20f, 0.15f, 0.02f, 1.8f);
        }

        public IReadOnlyDictionary<int, HiredEmployeeState> Employees => _employees;
        public IReadOnlyDictionary<int, SharedEmployeeTask> Tasks => _tasks;

        public int GetHireCost(EmployeeRole role, int level = 1)
        {
            return GetRoleProfile(role).ComputeCostForLevel(level);
        }

        public HiredEmployeeState TryHireEmployee(PizzeriaState state, EmployeeRole role)
        {
            return TryHireEmployeeWithExplicitCost(state, role, GetHireCost(role, 1));
        }

        public HiredEmployeeState TryHireEmployeeWithExplicitCost(PizzeriaState state, EmployeeRole role, int explicitCost)
        {
            if (state == null || explicitCost < 0) return null;
            if (state.RestaurantBudget < explicitCost) return null;

            var profile = GetRoleProfile(role);
            state.RestaurantBudget -= explicitCost;
            state.NightWagesSpent += explicitCost;
            var emp = new HiredEmployeeState
            {
                EmployeeId = _nextEmployeeId++,
                Role = role,
                Level = 1,
                IsAlive = true,
                BaseErrorRate = profile.BaseErrorRate
            };
            _employees[emp.EmployeeId] = emp;
            state.IncrementRevision();
            return emp;
        }

        public bool TryUpgradeEmployee(PizzeriaState state, int employeeId)
        {
            if (state == null || !_employees.TryGetValue(employeeId, out var emp)) return false;
            var profile = GetRoleProfile(emp.Role);
            if (emp.Level >= profile.MaxLevel) return false;
            int cost = GetHireCost(emp.Role, emp.Level + 1);
            if (state.RestaurantBudget < cost) return false;

            state.RestaurantBudget -= cost;
            state.NightWagesSpent += cost;
            emp.Level++;
            state.IncrementRevision();
            return true;
        }

        public SharedEmployeeTask EnqueueOrderTask(OrderId orderId, EmployeeRole role)
        {
            var task = new SharedEmployeeTask
            {
                TaskId = _nextTaskId++,
                TargetOrderId = orderId,
                RequiredRole = role,
                State = EmployeeTaskState.Available
            };
            _tasks[task.TaskId] = task;
            return task;
        }

        public bool TryReserveTask(int taskId, ulong actorId, bool isHuman, double now, double leaseSeconds = 15.0)
        {
            if (!_tasks.TryGetValue(taskId, out var task)) return false;
            if (task.State == EmployeeTaskState.Completed || task.State == EmployeeTaskState.Cancelled) return false;

            if (task.State == EmployeeTaskState.Reserved ||
                task.State == EmployeeTaskState.InProgress ||
                task.State == EmployeeTaskState.Blocked)
            {
                if (task.ReservedByActorId == actorId && task.IsReservedByHuman == isHuman)
                {
                    task.LeaseExpiresAt = now + leaseSeconds;
                    return true;
                }
                if (now < task.LeaseExpiresAt) return false; // Reserva exclusiva ativa
            }

            if (!isHuman && _employees.TryGetValue((int)actorId, out var emp))
            {
                emp.AssignedTaskId = taskId;
            }

            task.ReservedByActorId = actorId;
            task.IsReservedByHuman = isHuman;
            task.LeaseExpiresAt = now + leaseSeconds;
            task.State = EmployeeTaskState.Reserved;
            task.BlockReason = EmployeeBlockReason.None;
            return true;
        }

        public bool TryGetTaskForOrder(OrderId orderId, EmployeeRole role, out SharedEmployeeTask task)
        {
            foreach (var candidate in _tasks.Values)
            {
                if (candidate.TargetOrderId.Equals(orderId) &&
                    candidate.RequiredRole == role &&
                    candidate.State != EmployeeTaskState.Completed &&
                    candidate.State != EmployeeTaskState.Cancelled)
                {
                    task = candidate;
                    return true;
                }
            }
            task = null;
            return false;
        }

        public void ClearTaskBlocked(int taskId)
        {
            if (_tasks.TryGetValue(taskId, out var task) && task.State == EmployeeTaskState.Blocked)
            {
                task.State = EmployeeTaskState.InProgress;
                task.BlockReason = EmployeeBlockReason.None;
            }
        }

        /// <summary>Humano assume tarefa em andamento de um funcionário, liberando o funcionário sem duplicar a tarefa.</summary>
        public bool HumanTakeoverTask(int taskId, ulong humanClientId, double now)
        {
            if (!_tasks.TryGetValue(taskId, out var task)) return false;
            if (task.State == EmployeeTaskState.Completed || task.State == EmployeeTaskState.Cancelled) return false;

            if (!task.IsReservedByHuman && task.ReservedByActorId > 0 && _employees.TryGetValue((int)task.ReservedByActorId, out var prevEmp))
            {
                prevEmp.AssignedTaskId = -1;
            }

            task.ReservedByActorId = humanClientId;
            task.IsReservedByHuman = true;
            task.LeaseExpiresAt = now + 30.0;
            task.State = EmployeeTaskState.InProgress;
            task.BlockReason = EmployeeBlockReason.None;
            return true;
        }

        public void MarkTaskBlocked(int taskId, EmployeeBlockReason reason)
        {
            if (_tasks.TryGetValue(taskId, out var task))
            {
                task.State = EmployeeTaskState.Blocked;
                task.BlockReason = reason;
            }
        }

        public void CancelTask(int taskId)
        {
            if (_tasks.TryGetValue(taskId, out var task))
            {
                task.State = EmployeeTaskState.Cancelled;
                task.BlockReason = EmployeeBlockReason.None;
                if (!task.IsReservedByHuman && _employees.TryGetValue((int)task.ReservedByActorId, out var emp))
                {
                    emp.AssignedTaskId = -1;
                }
            }
        }

        public static int ComputeTotalOrderUnits(OrderState order)
        {
            if (order == null || order.Lines == null) return 0;
            int total = 0;
            for (int i = 0; i < order.Lines.Count; i++)
            {
                total += Math.Max(1, order.Lines[i].Quantity);
            }
            return total;
        }

        /// <summary>
        /// Avança uma unidade produzida na tarefa multipizza/multilinha. Retorna true quando todas as linhas e quantidades foram concluídas.
        /// </summary>
        public bool AdvanceUnitProgress(int taskId, OrderState order)
        {
            if (!_tasks.TryGetValue(taskId, out var task)) return true;
            if (order == null || order.Lines == null || order.Lines.Count == 0)
            {
                CompleteTask(taskId);
                return true;
            }

            task.TotalUnitsRequired = ComputeTotalOrderUnits(order);
            task.TotalUnitsCompleted++;
            task.UnitsCompletedInLine++;

            while (task.CurrentLineIndex < order.Lines.Count)
            {
                int lineQty = Math.Max(1, order.Lines[task.CurrentLineIndex].Quantity);
                if (task.UnitsCompletedInLine < lineQty)
                    break;

                task.CurrentLineIndex++;
                task.UnitsCompletedInLine = 0;
            }

            if (task.CurrentLineIndex >= order.Lines.Count || task.TotalUnitsCompleted >= task.TotalUnitsRequired)
            {
                CompleteTask(taskId);
                return true;
            }

            return false;
        }

        public void CompleteTask(int taskId)
        {
            if (_tasks.TryGetValue(taskId, out var task))
            {
                task.State = EmployeeTaskState.Completed;
                task.BlockReason = EmployeeBlockReason.None;
                if (!task.IsReservedByHuman && _employees.TryGetValue((int)task.ReservedByActorId, out var emp))
                {
                    emp.AssignedTaskId = -1;
                }
            }
        }

        public void ReleaseActorTasks(ulong actorId, bool isHuman)
        {
            foreach (var task in _tasks.Values)
            {
                if (task.ReservedByActorId == actorId && task.IsReservedByHuman == isHuman &&
                    (task.State == EmployeeTaskState.Reserved || task.State == EmployeeTaskState.InProgress || task.State == EmployeeTaskState.Blocked))
                {
                    task.State = EmployeeTaskState.Available;
                    task.ReservedByActorId = 0;
                    task.IsReservedByHuman = false;
                    task.LeaseExpiresAt = 0.0;
                }
            }
        }

        public float ComputeEffectiveErrorProbability(HiredEmployeeState emp, float trainingErrorMultiplier = 1f)
        {
            if (emp == null) return 0f;
            var profile = GetRoleProfile(emp.Role);
            float levelFactor = Math.Max(0.25f, 1f - (emp.Level - 1) * profile.LevelErrorReduction);
            float minRate = Math.Max(MinSkillErrorRate, profile.MinErrorRate);
            return Math.Clamp(emp.BaseErrorRate * levelFactor * trainingErrorMultiplier, minRate, 0.9f);
        }

        public bool RollIntentionalSkillError(int employeeId, Random employeeErrorRng, float trainingErrorMultiplier = 1f)
        {
            if (!_employees.TryGetValue(employeeId, out var emp) || employeeErrorRng == null) return false;
            float prob = ComputeEffectiveErrorProbability(emp, trainingErrorMultiplier);
            return employeeErrorRng.NextDouble() < prob;
        }

        public void OnNightEnded()
        {
            _tasks.Clear();
            foreach (var emp in _employees.Values)
            {
                emp.AssignedTaskId = -1;
                if (!emp.IsAlive && RestoreLostEmployeesNextNight)
                {
                    emp.IsAlive = true;
                }
            }
        }

        public void ResetForNewRun()
        {
            _employees.Clear();
            _tasks.Clear();
            _nextEmployeeId = 1;
            _nextTaskId = 1;
        }

        public void RestoreFromCheckpoint(IReadOnlyList<(EmployeeRole Role, int Level, bool IsAlive, float BaseErrorRate)> employees)
        {
            _employees.Clear();
            _tasks.Clear();
            _nextEmployeeId = 1;
            _nextTaskId = 1;
            if (employees == null) return;

            for (int i = 0; i < employees.Count; i++)
            {
                var item = employees[i];
                var emp = new HiredEmployeeState
                {
                    EmployeeId = _nextEmployeeId++,
                    Role = item.Role,
                    Level = Math.Max(1, item.Level),
                    IsAlive = item.IsAlive,
                    BaseErrorRate = item.BaseErrorRate
                };
                _employees[emp.EmployeeId] = emp;
            }
        }
    }
}
