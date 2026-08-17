using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Unbroken.LaunchBox.Plugins.Data;

namespace RommStar.Core.Extensions
{
    public static class IDataManagerExtensions
    {
        /// <summary>
        /// Uses reflection to extract the internal list of Emulators for a specific platform.
        /// </summary>
        public static List<IEmulator> GetAllEmulatorsForPlatform(this IDataManager dataManager, string platformName, bool defaultOnly)
        {
            // Returning an empty list rather than null prevents NullReferenceExceptions when chaining
            if (dataManager == null || string.IsNullOrWhiteSpace(platformName))
                return new List<IEmulator>();

            try
            {
                Type type = dataManager.GetType();

                // Find the specific method matching the (string, bool) signature
                MethodInfo method = type.GetMethod("GetAllEmulatorsForPlatform",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static,
                    null,
                    new Type[] { typeof(string), typeof(bool) },
                    null);

                if (method != null)
                {
                    object result = method.Invoke(dataManager, new object[] { platformName, defaultOnly });

                    // Safely cast the internal Emulator objects to the public IEmulator interface
                    if (result is IEnumerable enumerableResult)
                    {
                        return enumerableResult.Cast<IEmulator>().ToList();
                    }
                }
            }
            catch
            {
                // Fail gracefully if LaunchBox changes their internal architecture in a future update
            }

            return new List<IEmulator>();
        }


    }
}
