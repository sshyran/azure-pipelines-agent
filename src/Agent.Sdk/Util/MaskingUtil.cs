using Microsoft.VisualStudio.Services.ServiceEndpoints.WebApi;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.VisualStudio.Services.Agent.Util
{
    public static class MaskingUtil
    {
        /// <summary>
        /// Returns true if endpoint authorization parameter with provided key is a secret
        /// Masks all keys except the specific fields - for which we know that they don't contain secrets
        /// </summary>
        /// <param name="key">Key to check</param>
        /// <returns>Returns true if key is a secret</returns>
        public static bool IsEndpointAuthorizationParametersSecret(string key)
        {
            return key != EndpointAuthorizationParameters.IdToken
                && key != EndpointAuthorizationParameters.Role
                && key != EndpointAuthorizationParameters.Scope
                && key != EndpointAuthorizationParameters.TenantId
                && key != EndpointAuthorizationParameters.IdToken
                && key != EndpointAuthorizationParameters.IssuedAt
                && key != EndpointAuthorizationParameters.ExpiresAt
                && key != EndpointAuthorizationParameters.ExpiresIn
                && key != EndpointAuthorizationParameters.Audience
                && key != EndpointAuthorizationParameters.AuthenticationType
                && key != EndpointAuthorizationParameters.AuthorizationType
                && key != EndpointAuthorizationParameters.AccessTokenType
                && key != EndpointAuthorizationParameters.AccessTokenFetchingMethod
                && key != EndpointAuthorizationParameters.UseWindowsSecurity
                && key != EndpointAuthorizationParameters.Unsecured
                && key != EndpointAuthorizationParameters.OAuthAccessTokenIsSupplied
                && key != EndpointAuthorizationParameters.Audience
                && key != EndpointAuthorizationParameters.CompleteCallbackPayload
                && key != EndpointAuthorizationParameters.AcceptUntrustedCertificates;
        }
    }
}
