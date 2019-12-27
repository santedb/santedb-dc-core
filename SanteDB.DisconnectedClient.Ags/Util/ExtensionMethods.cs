using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Ags.Util
{
    /// <summary>
    /// Utilities for extension methods
    /// </summary>
    public static class ExtensionMethods
    {
        /// <summary>
        /// Encode the specified string to ASCII escape characters
        /// </summary>
        public static String EncodeAscii(this string value)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var c in value)
                if (c > 127)
                    sb.AppendFormat("\\u{0:x4}", (int)c);
                else
                    sb.Append(c);
            return sb.ToString();
        }
    }
}
