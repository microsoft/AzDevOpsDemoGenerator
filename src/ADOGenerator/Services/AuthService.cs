using ADOGenerator.IServices;
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
        public static readonly string clientId = "c5d3c380-cd7c-4822-a031-cde279164a19";
        private static readonly string tenantId = "0c88fa98-b222-4fd8-9414-559fa424ce64";
        public static readonly string[] scopes = new[] { "499b84ac-1321-427f-aa17-267ca6975798/.default" }; // Azure DevOps API Scope
        public static readonly string authority = $"https://login.microsoftonline.com/{tenantId}";

        public async Task<AuthenticationResult> AcquireTokenAsync(IPublicClientApplication app)
        {
            return await app.AcquireTokenWithDeviceCode(scopes, deviceCodeCallback =>
            {
                Console.WriteLine(deviceCodeCallback.Message);
                return Task.CompletedTask;
            }).ExecuteAsync();
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

        //public async Task<string> GetProjectsAsync(string accessToken, string selectedAccountName)
        //{
        //    var projectsClient = new HttpClient();
        //    projectsClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        //    var projectsResponse = await projectsClient.GetAsync($"https://dev.azure.com/{selectedAccountName}/_apis/projects?api-version=6.0");
        //    projectsResponse.EnsureSuccessStatusCode();
        //    var projectsResponseBody = await projectsResponse.Content.ReadAsStringAsync();

        //    var projectsJson = JObject.Parse(projectsResponseBody);
        //    if (projectsJson["count"].Value<int>() > 0)
        //    {
        //        Console.WriteLine("Select a project:");
        //        var projects = projectsJson["value"];
        //        for (int i = 0; i < projects.Count(); i++)
        //        {
        //            Console.WriteLine($"{i + 1}. {projects[i]["name"]} (ID: {projects[i]["id"]})");
        //        }
        //        int selectedProjectIndex;
        //        do
        //        {
        //            Console.Write("Enter the number of the project: ");
        //        } while (!int.TryParse(Console.ReadLine(), out selectedProjectIndex) || selectedProjectIndex < 1 || selectedProjectIndex > projects.Count());
        //        var selectedProjectId = projects[selectedProjectIndex - 1]["id"].ToString();
        //        var selectedProjectName = projects[selectedProjectIndex - 1]["name"].ToString();
        //        Console.WriteLine($"Selected project: {selectedProjectName} (ID: {selectedProjectId})");
        //        return selectedProjectName;
        //    }
        //    else
        //    {
        //        Console.WriteLine("No projects found.");
        //    }
        //    return null;
        //}
    }
}
