using Microsoft.TeamFoundation.DistributedTask.WebApi;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.VisualStudio.Services.Agent.Util
{
    public static class MaskingUtil
    {
        public static bool IsEndpointAuthorizationParametersSecret(string key)
        {
            return key != EndpointAuthorizationParameters.IdToken
                && key != EndpointAuthorizationParameters.Role
                && key != EndpointAuthorizationParameters.Scope
                && key != EndpointAuthorizationParameters.TenantId;
        }
    }
}
