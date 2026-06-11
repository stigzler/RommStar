using RommStar.Core.CustomAttributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.Extensions
{
    public static class EnumExtensions
    {
        public static string GetCustomName(this System.Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            if (field == null) return value.ToString();

            var attribute = (CustomNameAttribute)System.Attribute.GetCustomAttribute(field, typeof(CustomNameAttribute));
            return attribute != null ? attribute.Name : value.ToString();
        }

        public static string GetDescription(this System.Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            if (field == null) return value.ToString();

            var attribute = (System.ComponentModel.DescriptionAttribute)System.Attribute.GetCustomAttribute(field, typeof(System.ComponentModel.DescriptionAttribute));
            return attribute != null ? attribute.Description : value.ToString();
        }
    }
}