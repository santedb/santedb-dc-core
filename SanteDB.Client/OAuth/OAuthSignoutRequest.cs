using SanteDB.Core.Http;
using SanteDB.Rest.OAuth;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Client.OAuth
{
    /// <summary>
    /// A request to the signout endpoint of the oauth provider.
    /// </summary>
    internal class OAuthSignoutRequest
    {
        /// <summary>
        /// The ID token of the session to sign out of. Required if no other contextual information is available to the provider.
        /// </summary>
        [FormElement(OAuthConstants.FormField_IdTokenHint)]
        public string IdTokenHint { get; set; }

        /// <summary>
        /// The user that the provider should sign out. Required if multiple users are established with the provider for the context.
        /// </summary>
        [FormElement(OAuthConstants.FormField_LogoutHint)]
        public string LogoutHint { get; set; }

        /// <summary>
        /// The URI that the provider should redirect the client to. This is typically used when a chained provider needs to direct 
        /// a user interface component to a authentication endpoint when a downstream provider is not aware of the endpoint.
        /// </summary>
        [FormElement(OAuthConstants.FormField_PostLogoutRedirectUri)]
        public string PostLogoutRedirectUri { get; set; }



    }
}
