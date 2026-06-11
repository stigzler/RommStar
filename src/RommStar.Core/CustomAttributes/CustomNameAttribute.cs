using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.CustomAttributes
{
    /// <summary>
    /// Use:
    /// Decorate Property: [CustomName("Genesis")]
    /// Use:
    /// Requires GetCustomName enum extension
    /// string uiFriendlyName = selectedPlatform.GetCustomName();
    /// </summary>
    public class CustomNameAttribute : System.Attribute
    {
        public string Name { get; }

        public CustomNameAttribute(string name) => Name = name;
    }
}