using ADOGenerator;
using ADOGenerator.IServices;
using ADOGenerator.Models;
using ADOGenerator.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

var configuration = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
        .Build();

Console.WriteLine("Welcome to Azure DevOps Demo Generator! This tool will help you generate a demo environment for Azure DevOps.");

try
{
    do
    {
        string id = Guid.NewGuid().ToString().Split('-')[0];
        Init init = new Init();
        id.AddMessage("Template Details");
        var templatePath = Path.Combine(Directory.GetCurrentDirectory(), "Templates", "TemplateSetting.json");

        if (!File.Exists(templatePath))
        {
            id.ErrorId().AddMessage("TemplateSettings.json file not found.");
            break;
        }

        var templateSettings = File.ReadAllText(templatePath);
        var json = JObject.Parse(templateSettings);
        var groupwiseTemplates = json["GroupwiseTemplates"];

        if (groupwiseTemplates == null)
        {
            id.ErrorId().AddMessage("No templates found.");
            break;
        }

        int templateIndex = 1;
        var templateDictionary = new Dictionary<int, string>();

        foreach (var group in groupwiseTemplates)
        {
            var groupName = group["Groups"]?.ToString();
            Console.WriteLine(groupName);

            var templates = group["Template"];
            if (templates != null)
            {
                foreach (var template in templates)
                {
                    var templateName = template["Name"]?.ToString();
                    Console.WriteLine($"  {templateIndex}. {templateName}");
                    templateDictionary.Add(templateIndex, templateName);
                    templateIndex++;
                }
            }
        }

        id.AddMessage("Enter the template number from the list of templates above:");
        if (!int.TryParse(Console.ReadLine(), out var selectedTemplateNumber) || !templateDictionary.TryGetValue(selectedTemplateNumber, out var selectedTemplateName))
        {
            id.AddMessage("Invalid template number entered.");
            continue;
        }
        selectedTemplateName = selectedTemplateName.Trim();
        if (!TryGetTemplateDetails(groupwiseTemplates, selectedTemplateName, out var templateFolder, out var confirmedExtension, id))
        {
            id.AddMessage($"Template '{selectedTemplateName}' not found in the list.");
            id.AddMessage("Would you like to try again or exit? (type 'retry' to try again or 'exit' to quit):");
            var userChoice = Console.ReadLine();
            if (userChoice?.Equals("exit", StringComparison.OrdinalIgnoreCase) == true)
            {
                id.AddMessage("Exiting the application.");
                return 0;
            }
            continue;
        }
        else
        {
            id.AddMessage($"Selected template: {selectedTemplateName}");
            ValidateExtensions(templateFolder, id);
        }

        id.AddMessage("Choose authentication method: 1. Device Login using AD auth 2. Personal Access Token (PAT)");
        var authChoice = Console.ReadLine();

        string accessToken = string.Empty;
        string organizationName = string.Empty;
        string authScheme = string.Empty;

        if (authChoice == "1")
        {
            try
            {
                var app = PublicClientApplicationBuilder.Create(AuthService.clientId)
                    .WithAuthority(AuthService.authority)
                    .WithDefaultRedirectUri()
                    .Build();
                AuthService authService = new AuthService();

                var accounts = await app.GetAccountsAsync();
                if (accounts.Any())
                {
                    try
                    {
                        var result = await app.AcquireTokenSilent(AuthService.scopes, accounts.FirstOrDefault())
                                             .WithForceRefresh(true)
                                             .ExecuteAsync();
                        accessToken = result.AccessToken;
                    }
                    catch (Exception ex)
                    {
                        var result = await authService.AcquireTokenAsync(app);
                        accessToken = result.AccessToken;
                        id.ErrorId().AddMessage($"Error: {ex.Message}");
                    }
                }
                else
                {
                    var result = await authService.AcquireTokenAsync(app);
                    accessToken = result.AccessToken;
                }

                var memberId = await authService.GetProfileInfoAsync(accessToken);
                var organizations = await authService.GetOrganizationsAsync(accessToken, memberId);
                organizationName = await authService.SelectOrganization(accessToken, organizations);
                authScheme = "Bearer";
            }
            catch (Exception ex)
            {
                id.ErrorId().AddMessage($"Error: {ex.Message}");
                id.AddMessage("Exiting the application.");
                Environment.Exit(1);
            }
        }
        else if (authChoice == "2")
        {
            id.AddMessage("Enter your Azure DevOps organization name:");
            organizationName = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(organizationName))
            {
                id.ErrorId().AddMessage("Organization name cannot be empty.");
                continue;
            }

            if (Uri.IsWellFormedUriString(organizationName, UriKind.Absolute))
            {
                id.ErrorId().AddMessage("Please enter only the last part of the Azure DevOps URL (e.g., {ORGANIZATION_NAME} from https://dev.azure.com/{ORGANIZATION_NAME}).");
                continue;
            }

            id.AddMessage("Enter your Azure DevOps personal access token:");
            accessToken = init.ReadSecret();
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                id.ErrorId().AddMessage("Personal access token cannot be empty.");
                continue;
            }

            authScheme = "Basic";
        }

        string projectName = "";
        do
        {
            id.AddMessage("Enter the new project name:");
            projectName = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(projectName))
            {
                id.ErrorId().AddMessage("Project name cannot be empty.");
                continue;
            }
            if (!init.CheckProjectName(projectName))
            {
                id.ErrorId().AddMessage("Validation error: Project name is not valid.");
                id.AddMessage("Do you want to try with a valid project name or exit? (type 'retry' to try again or 'exit' to quit):");
                var userChoice = Console.ReadLine();
                if (userChoice?.Equals("exit", StringComparison.OrdinalIgnoreCase) == true)
                {
                    id.AddMessage("Exiting the application.");
                    Environment.Exit(1);
                }
                projectName = "";
                continue;
            }
        } while (string.IsNullOrWhiteSpace(projectName));

        if (string.IsNullOrWhiteSpace(organizationName) || string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(projectName))
        {
            id.ErrorId().AddMessage("Validation error: All inputs must be provided. Exiting..");
            Environment.Exit(1);
        }

        var project = new Project
        {
            id = id,
            accessToken = accessToken,
            accountName = organizationName,
            ProjectName = projectName,
            TemplateName = selectedTemplateName,
            selectedTemplateFolder = templateFolder,
            SelectedTemplate = templateFolder,
            isExtensionNeeded = confirmedExtension,
            isAgreeTerms = confirmedExtension,
            adoAuthScheme = authScheme
        };

        CreateProjectEnvironment(project);

    } while (true);
}
catch (Exception ex)
{
    Console.WriteLine($"An error occurred: {ex.Message}");
    Console.WriteLine("Exiting the application.");
    Environment.Exit(1);
}

bool TryGetTemplateDetails(JToken groupwiseTemplates, string selectedTemplateName, out string templateFolder, out bool confirmedExtension, string id)
{
    templateFolder = string.Empty;
    confirmedExtension = false;

    foreach (var group in groupwiseTemplates)
    {
        var templates = group["Template"];
        if (templates == null) continue;

        foreach (var template in templates)
        {
            if (template["Name"]?.ToString().Equals(selectedTemplateName, StringComparison.OrdinalIgnoreCase) != true)
                continue;
            else
            {
                templateFolder = template["TemplateFolder"]?.ToString();
                var templateFolderPath = Path.Combine(Directory.GetCurrentDirectory(), "Templates", templateFolder);

                if (!Directory.Exists(templateFolderPath))
                {
                    id.ErrorId().AddMessage($"Template '{selectedTemplateName}' is not found.");
                    return false;
                }
                id.AddMessage($"Template '{selectedTemplateName}' is present.");
                return true;
            }

        }
    }
    return false;
}

bool ValidateExtensions(string templateFolderPath, string id)
{
    Init init = new Init();

    var extensionsFilePath = Path.Combine(Directory.GetCurrentDirectory(), "Templates", templateFolderPath, "Extensions.json");
    if (!File.Exists(extensionsFilePath))
    {
        return false;
    }

    var extensionsFile = File.ReadAllText(extensionsFilePath);
    if(string.IsNullOrEmpty(extensionsFile))
    {
        return false;
    }
    var extensionjson = JObject.Parse(extensionsFile);
    var extensions = extensionjson["Extensions"];
    if (extensions == null)
    {
        return false;
    }

    foreach (var extension in extensions)
    {
        var extensionName = extension["extensionName"]?.ToString();
        var link = extension["link"]?.ToString();
        var licenseLink = extension["License"]?.ToString();

        var href = init.ExtractHref(link);
        var licenseHref = init.ExtractHref(licenseLink);

        Console.WriteLine($"Extension Name: {extensionName}");
        Console.WriteLine($"Link: {href}");
        Console.WriteLine($"License: {licenseHref}");
        Console.WriteLine();
    }

    if (extensions.HasValues)
    {
        id.AddMessage("Do you want to proceed with this extension? (yes/No): press enter to confirm");
        var userConfirmation = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(userConfirmation) || (userConfirmation.Equals("yes", StringComparison.OrdinalIgnoreCase) || userConfirmation.Equals("y", StringComparison.OrdinalIgnoreCase)))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            id.AddMessage("Agreed for license? (yes/no): press enter to confirm");
            Console.ResetColor();
            var licenseConfirmation = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(licenseConfirmation) || (licenseConfirmation.Equals("yes", StringComparison.OrdinalIgnoreCase) || licenseConfirmation.Equals("y", StringComparison.OrdinalIgnoreCase)))
            {
                id.AddMessage("Confirmed Extension installation");
                return true;
            }
        }
        else
        {
            id.AddMessage("Extension installation is not confirmed.");
        }
    }

    return false;
}

void CreateProjectEnvironment(Project model)
{
    try
    {
        Console.WriteLine($"Creating project '{model.ProjectName}' in organization '{model.accountName}' using template from '{model.TemplateName}'...");
        var projectService = new ProjectService(configuration);
        var result = projectService.CreateProjectEnvironment(model);
        if (result)
        {
            Console.WriteLine("Project created successfully.");
        }
        else
        {
            Console.WriteLine("Project creation failed.");
        }
        Console.WriteLine("Do you want to create another project? (yes/no): press enter to confirm");
        var createAnotherProject = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(createAnotherProject) || createAnotherProject.Equals("yes", StringComparison.OrdinalIgnoreCase) || createAnotherProject.Equals("y", StringComparison.OrdinalIgnoreCase))
        {
            createAnotherProject = "yes";
        }
        else
        {
            createAnotherProject = "no";
        }
        Console.WriteLine();
        if (createAnotherProject.Equals("no", StringComparison.OrdinalIgnoreCase))
        {
            model.id.AddMessage("Exiting the application.");
            Environment.Exit(0);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"An error occurred while creating the project: {ex.Message}");
        model.id.AddMessage("Exiting the application.");
        Environment.Exit(1);
    }
}
return 0;

