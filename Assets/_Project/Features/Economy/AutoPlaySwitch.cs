using System;
using System.Reflection;

namespace Kturker.UnityBase.Features.Economy
{
    // CompassAutoPlay.Requested: Compass's kit puts CompassAutoPlay in Assembly-CSharp, which no asmdef can
    // reference, so it is found by name (the kit's own pattern). False in a project without the kit.
    public static class AutoPlaySwitch
    {
        private static PropertyInfo requested;
        private static bool searched;

        public static bool Requested
        {
            get
            {
                if (!searched)
                {
                    requested = Find();
                    searched = true;
                }
                return requested != null && requested.GetValue(null) is bool on && on;
            }
        }

        private static PropertyInfo Find()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var property = assembly.GetType("CompassAutoPlay", false)?.GetProperty("Requested", BindingFlags.Public | BindingFlags.Static);
                if (property != null && property.PropertyType == typeof(bool)) return property;
            }
            return null;
        }
    }
}
