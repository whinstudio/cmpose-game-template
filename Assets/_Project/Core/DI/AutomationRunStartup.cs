using Kturker.UnityBase.Core.Domain;
using UnityEngine;

namespace Kturker.UnityBase.Core.DI
{
    // Sets AutomationRun.Active once, before any scope is built (Domain has no UnityEngine to read persistentDataPath)
    internal static class AutomationRunStartup
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Detect() => AutomationRun.Detect(Application.persistentDataPath);
    }
}
