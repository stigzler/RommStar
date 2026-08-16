using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Unbroken.LaunchBox.Plugins.Data;

namespace RommStar.Core.Extensions
{
    public static class AdditionalApplicationExtensions
    {
        /// <summary>
        /// Uses reflection to extract the internal 'Section' property from an IAdditionalApplication.
        /// </summary>
        public static string Section(this IAdditionalApplication addApp)
        {
            if (addApp == null) return "Unknown";

            try
            {
                Type type = addApp.GetType();

                PropertyInfo sectionProperty = type.GetProperty("Section",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (sectionProperty != null)
                {
                    object rawValue = sectionProperty.GetValue(addApp);
                    if (rawValue != null)
                    {
                        return rawValue.ToString();
                    }
                }
            }
            catch
            {
                // Fail gracefully if LaunchBox changes their internal architecture
                return "Unknown";
            }
            return "Unknown";
        }

        /// <summary>
        /// Uses reflection to set the internal 'Section' property on an IAdditionalApplication.
        /// Valid string values: "AdditionalApp", "Document", "Link", "Unknown", "Version"
        /// </summary>
        public static void SetSection(this IAdditionalApplication addApp, string sectionName)
        {
            if (addApp == null || string.IsNullOrWhiteSpace(sectionName)) return;

            try
            {
                Type type = addApp.GetType();

                PropertyInfo sectionProperty = type.GetProperty("Section",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                // Ensure the property exists and has a setter
                if (sectionProperty != null && sectionProperty.CanWrite)
                {
                    // 1. Get the exact hidden enum type (AdditionalApplicationSection)
                    Type enumType = sectionProperty.PropertyType;

                    // 2. Parse your string into that exact enum type (ignoreCase: true makes it safer)
                    object enumValue = Enum.Parse(enumType, sectionName, true);

                    // 3. Inject the parsed enum value back into the object
                    sectionProperty.SetValue(addApp, enumValue);
                }
            }
            catch
            {
                // Fails gracefully if LaunchBox changes their architecture
                // or if you pass an invalid string that isn't in the enum.
            }
        }
    }
}
