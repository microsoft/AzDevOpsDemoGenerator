using ADOGenerator.IServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static Microsoft.VisualStudio.Services.Graph.GraphResourceIds;

namespace ADOGenerator.Services
{
    public class AuthService : IAuthService
    {
        public static string clientId = "";
        private static string tenantId = "";
        public static string[] scopes = new[] { "" }; // Azure DevOps API Scope
        public static readonly string authority = $"https://login.microsoftonline.com/{tenantId}";

        static AuthService()
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();

            clientId = configuration["AppSettings:clientId"];
            tenantId = configuration["AppSettings:tenantId"];
            scopes = new[] { configuration["AppSettings:scopes"] };
            authority = $"https://login.microsoftonline.com/{tenantId}";
        }

        public async Task<AuthenticationResult> AcquireTokenAsync(IPublicClientApplication app)
        {
            AuthenticationResult result = null;
            try
            {
                result = await app.AcquireTokenWithDeviceCode(scopes, deviceCodeCallback =>
                {
                    Console.WriteLine(deviceCodeCallback.Message);
                    return Task.CompletedTask;
                }).ExecuteAsync();
            }
            catch (MsalServiceException ex)
            {
                Console.WriteLine($"MSALServiceException Error: {ex.Message} \nError Code: {ex.ErrorCode}");

            }
            catch (OperationCanceledException ex)
            {
                Console.WriteLine($"OperationCanceledException Error: {ex.Message}");
            }
            catch (MsalClientException ex)
            {
                Console.WriteLine($"MSALClientException Error: {ex.Message} \nError Code: {ex.ErrorCode}");
            }

            return result; // Ensure a return value in case of an exception
        }

        public async Task<string> GetProfileInfoAsync(string accessToken)
        {
            var profileClient = new HttpClient();
            profileClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var profileResponse = await profileClient.GetAsync("https://app.vssps.visualstudio.com/_apis/profile/profiles/me?api-version=6.0-preview.1");
            profileResponse.EnsureSuccessStatusCode();
            var profileContent = await profileResponse.Content.ReadAsStringAsync();
            var profileJson = JObject.Parse(profileContent);
            return profileJson["id"].ToString();
        }

        public async Task<JObject> GetOrganizationsAsync(string accessToken, string memberId)
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var response = await client.GetAsync($"https://app.vssps.visualstudio.com/_apis/accounts?memberId={memberId}&api-version=6.0-preview.1");
            response.EnsureSuccessStatusCode();
            var responseBody = await response.Content.ReadAsStringAsync();
            return JObject.Parse(responseBody);
        }

        public async Task<string> SelectOrganization(string accessToken, JObject accountsJson)
        {
            return await Task.Run(() =>
            {
                if (accountsJson["count"].Value<int>() > 0)
                {
                    Console.WriteLine("Select an organization:");
                    var accounts = accountsJson["value"];
                    for (int i = 0; i < accounts.Count(); i++)
                    {
                        Console.WriteLine($"{i + 1}. {accounts[i]["accountName"]} (ID: {accounts[i]["accountId"]})");
                    }

                    int selectedIndex;
                    do
                    {
                        Console.Write("Enter the number of the organization: ");
                    } while (!int.TryParse(Console.ReadLine(), out selectedIndex) || selectedIndex < 1 || selectedIndex > accounts.Count());

                    var selectedAccountId = accounts[selectedIndex - 1]["accountId"].ToString();
                    var selectedAccountName = accounts[selectedIndex - 1]["accountName"].ToString();
                    return selectedAccountName;
                }
                else
                {
                    Console.WriteLine("No organizations found.");
                }
                return null;
            });
        }
    }
}
