/*
 * Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors (See NOTICE.md)
 * 
 * Licensed under the Apache License, Version 2.0 (the "License"); you 
 * may not use this file except in compliance with the License. You may 
 * obtain a copy of the License at 
 * 
 * http://www.apache.org/licenses/LICENSE-2.0 
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the 
 * License for the specific language governing permissions and limitations under 
 * the License.
 * 
 * User: fyfej
 * Date: 2021-2-9
 */
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
