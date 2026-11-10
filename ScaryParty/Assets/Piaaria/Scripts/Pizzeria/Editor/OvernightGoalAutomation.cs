#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

namespace ScaryParty.Pizzeria.Editor
{
    [InitializeOnLoad]
    public static class OvernightGoalAutomation
    {
        private const string TriggerFile = "Temp/overnight_trigger.txt";
        private const string LogFile = "Temp/overnight_execution.log";

        static OvernightGoalAutomation()
        {
            EditorApplication.delayCall += ProcessQueue;
        }

        public static void ProcessQueue()
        {
            if (!File.Exists(TriggerFile)) return;
            string command = File.ReadAllText(TriggerFile).Trim();
            File.Delete(TriggerFile);

            string result = $"[{System.DateTime.Now}] Executing: {command}\n";
            try
            {
                result += ExecuteCommand(command);
            }
            catch (System.Exception ex)
            {
                result += $"ERROR: {ex}\n";
            }
            File.WriteAllText(LogFile, result);
            Debug.Log($"[OvernightGoalAutomation] {result}");
        }

        private static string ExecuteCommand(string command)
        {
            switch (command)
            {
                case "PING":
                    return "PONG: Unity Editor is responsive and executing C#.\n";
                default:
                    return $"Unknown command: {command}\n";
            }
        }
    }
}
#endif
