using Microsoft.Identity.Client;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADOGenerator.IServices
{
    public interface IAuthService
    {
        public Task<AuthenticationResult> AcquireTokenAsync(IPublicClientApplication app);
        public Task<string> GetProfileInfoAsync(string accessToken);

        public Task<JObject> GetOrganizationsAsync(string accessToken, string memberId);

        public Task<string> SelectOrganization(string accessToken, JObject accountsJson);

        //public Task<string> GetProjectsAsync(string accessToken, string selectedAccountName);
    }
}
