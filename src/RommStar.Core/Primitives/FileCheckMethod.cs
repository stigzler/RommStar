using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.Primitives
{
    public enum FileCheckMethod
    {
        [Description("File Only")]
        FileOnly,

        [Description("File and SHA1")]
        FileAndSHA1
    }
}
