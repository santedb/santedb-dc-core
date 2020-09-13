using SanteDB.Core.Http;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;

namespace SanteDB.DisconnectedClient.Ags.Model
{
    /// <summary>
    /// Represents a code search request
    /// </summary>
    public class CodeSearchRequest
    {

        /// <summary>
        /// Default ctor for serialization
        /// </summary>
        public CodeSearchRequest()
        {

        }

        /// <summary>
        /// Constructor with default values
        /// </summary>
        public CodeSearchRequest(NameValueCollection nvc)
        {
            this.Code = nvc["code"];
            if (Boolean.TryParse(nvc["validate"], out bool r))
                this.Validate = r;
        }

        /// <summary>
        /// The code to be resolved
        /// </summary>
        [FormElement("code")]
        public String Code { get; set; }

        /// <summary>
        /// Validate the code
        /// </summary>
        [FormElement("validate")]
        public bool Validate { get; set; }
    }
}
