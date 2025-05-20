using ADOGenerator;
using ADOGenerator.IServices;
using ADOGenerator.Models;
using ADOGenerator.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;
using Microsoft.VisualStudio.Services.DelegatedAuthorization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestAPI;
using System.Text;
using System.Text.RegularExpressions;

var configuration = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
        .Build();

Console.WriteLine("Welcome to Azure DevOps Demo Generator! This tool will help you generate a demo environment for Azure DevOps.");
(string accessToken, string organizationName, string authScheme)? authenticationDetails = null;
string authChoice = string.Empty;
string currentPath = Directory.GetCurrentDirectory()
    .Replace("bin\\Debug\\net8.0", "")
    .Replace("bin\\Release\\net8.0", "")
    .Replace("bin\\Debug", "")
    .Replace("bin\\Release", "")
    .Replace("ADOGenerator\\", "ADOGenerator\\");

do
{
    if (currentPath.EndsWith("ADOGenerator"))
        currentPath = currentPath + "\\";
    string id = Guid.NewGuid().ToString().Split('-')[0];
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("Do you want to create a new template or create a new project using the demo generator project template?");
    Console.WriteLine("1. Create a new project using the demo generator project template");
    Console.WriteLine("2. Generate new artifacts using an existing project.");
    Console.ResetColor();
    id.AddMessage(Environment.NewLine+"Enter the option number from the list of options above:");
    var userChoiceTemplate = Console.ReadLine();

    switch (userChoiceTemplate)
    {
        case "1":
            HandleNewProjectCreation(configuration, id);
            break;

        case "2":
            var (isArtifactsGenerated, template, model) = HandleArtifactGeneration(configuration, id);
            if (isArtifactsGenerated)
            {
                HandleTemplateAndArtifactsUpdate(template, id, model, currentPath);
            }
            else
            {
                if(!template.Equals("skipped", StringComparison.OrdinalIgnoreCase))
                {
                    id.ErrorId().AddMessage(Environment.NewLine + "Artifacts generation failed.");
                }
            }
            break;

        default:
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Invalid choice. Please select either 1 or 2.");
            Console.ResetColor();
            continue;
    }

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine(Environment.NewLine+"Do you want to create another project? (yes/no): press enter to confirm");
    Console.ResetColor();
    var createAnotherProject = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(createAnotherProject) || createAnotherProject.Equals("yes", StringComparison.OrdinalIgnoreCase) || createAnotherProject.Equals("y", StringComparison.OrdinalIgnoreCase))
    {
        if (authChoice == "2")
        {
            authenticationDetails = null;
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Do you want to use the existing authentication details? (yes/no): press enter to confirm");
            Console.ResetColor();
            var useExistingAuth = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(useExistingAuth) || useExistingAuth.Equals("yes", StringComparison.OrdinalIgnoreCase) || useExistingAuth.Equals("y", StringComparison.OrdinalIgnoreCase))
            {
                if (authenticationDetails != null)
                {
                    authenticationDetails = (authenticationDetails.Value.accessToken, null, authenticationDetails.Value.authScheme);
                }
            }
            else
            {
                authenticationDetails = null;
            }
        }
        continue;
    }

    id.AddMessage("Exiting the application.");
    Environment.Exit(0);

} while (true);
return 0;

void HandleTemplateAndArtifactsUpdate(string template, string id, Project model, string currentPath)
{
    var templatePathBin = Path.Combine(Directory.GetCurrentDirectory(), "Templates", "TemplateSetting.json");
    var templatePathOriginal = Path.Combine(currentPath, "Templates", "TemplateSetting.json");
    if (UpdateTemplateSettings(template, id, templatePathOriginal))
    {
        id.AddMessage(Environment.NewLine + "Template settings updated successfully at " + templatePathOriginal);
    }
    else
    {
        id.ErrorId().AddMessage(Environment.NewLine + "Template settings update failed at " + templatePathOriginal);
    }
    if (templatePathBin.Equals(templatePathOriginal))
    {
        id.AddMessage(Environment.NewLine + "Template path is same as original.");
        return;
    }
    id.AddMessage(Environment.NewLine+"Do you want to update the template settings and move the artifacts to the executable directory? (yes/no): press enter to confirm");
    var updateTemplateSettings = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(updateTemplateSettings) || updateTemplateSettings.Equals("yes", StringComparison.OrdinalIgnoreCase) || updateTemplateSettings.Equals("y", StringComparison.OrdinalIgnoreCase))
    {
        id.AddMessage(Environment.NewLine + "Updating template settings...");
        
        CopyFileIfExists(id,templatePathOriginal, templatePathBin);

        id.AddMessage(Environment.NewLine+"Template settings copied to the current directory and updated successfully.");
        id.AddMessage(Environment.NewLine + "Moving artifacts to the current directory...");

        var artifactsPathOriginal = Path.Combine(currentPath, "Templates", $"CT-{model.ProjectName.Replace(" ", "-")}");
        var artifactsPath = Path.Combine(Directory.GetCurrentDirectory(), "Templates", $"CT-{model.ProjectName.Replace(" ", "-")}");
        
        MoveArtifacts(artifactsPathOriginal, artifactsPath, id);
    }
    else
    {
        SkipTemplateAndArtifactsUpdate(id, currentPath, model, template);
    }
}

void CopyFileIfExists(string id,string sourcePath, string destinationPath)
{
    try
    {
        if (File.Exists(sourcePath))
        {
            File.Copy(sourcePath, destinationPath, true);
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            id.AddMessage(Environment.NewLine + $"Source file '{sourcePath}' does not exist. Creating a new file at the destination.");
            string fileContents = File.ReadAllText(sourcePath);
            File.WriteAllText(destinationPath, fileContents);
            id.AddMessage(Environment.NewLine + $"New file created at '{destinationPath}'.");
            Console.ResetColor();
        }
    }
    catch(Exception ex)
    {
        id.ErrorId().AddMessage(Environment.NewLine + "Error copying file: " + ex.Message);
        return;
    }
}

void MoveArtifacts(string sourcePath, string destinationPath, string id)
{
    if (Directory.Exists(sourcePath))
    {
        if (Directory.Exists(destinationPath))
        {
            Directory.Delete(destinationPath, true);
        }
        Directory.CreateDirectory(destinationPath);

        foreach (var directory in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourcePath, directory);
            var destinationDirectory = Path.Combine(destinationPath, relativePath);
            Directory.CreateDirectory(destinationDirectory);
        }

        foreach (var file in Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourcePath, file);
            var destinationFile = Path.Combine(destinationPath, relativePath);
            File.Copy(file, destinationFile, true);
        }

        id.AddMessage(Environment.NewLine+"Artifacts moved to the current directory.");
    }
    else
    {
        id.ErrorId().AddMessage(Environment.NewLine+"Artifacts directory not found at " + sourcePath);
    }
}

void SkipTemplateAndArtifactsUpdate(string id, string currentPath, Project model, string template)
{
    var templatePathBin = Path.Combine(Directory.GetCurrentDirectory(), "Templates", "TemplateSetting.json");
    var artifactsPathOriginal = Path.Combine(currentPath, "Templates", $"CT-{model.ProjectName.Replace(" ", "-")}");
    var artifactsPath = Path.Combine(Directory.GetCurrentDirectory(), "Templates", $"CT-{model.ProjectName.Replace(" ", "-")}");
    id.WarningId().AddMessage(Environment.NewLine+template);
    id.WarningId().AddMessage(Environment.NewLine+"Copy the generated template JSON and update the template settings manually in the following file location: " + templatePathBin);
    id.WarningId().AddMessage(Environment.NewLine+"Template settings update and copying artifacts skipped.");
    id.WarningId().AddMessage(Environment.NewLine+"Copy the generated artifacts directory from " + artifactsPathOriginal + " and update the artifacts manually in the following directory location: " + artifactsPath);
}


void HandleNewProjectCreation(IConfiguration configuration, string id)
{
    Init init = new Init();
    id.AddMessage(Environment.NewLine+"Template Details");

    var templatePath = Path.Combine(Directory.GetCurrentDirectory(), "Templates", "TemplateSetting.json");
    if (!File.Exists(templatePath))
    {
        id.ErrorId().AddMessage("TemplateSettings.json file not found.");
        return;
    }

    var groupwiseTemplates = LoadTemplates(templatePath, id);
    if (groupwiseTemplates == null) return;

    var selectedTemplateName = SelectTemplate(groupwiseTemplates, id);
    if (string.IsNullOrEmpty(selectedTemplateName)) return;

    var templateFolder = string.Empty;
    var confirmedExtension = false;
    if (!TryGetTemplateDetails(groupwiseTemplates, selectedTemplateName, out templateFolder, id))
    {
        return;
    }

    confirmedExtension = ValidateExtensions(templateFolder, id);

    var (accessToken, organizationName, authScheme) = AuthenticateUser(init, id);
    if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(organizationName)) return;

    var projectName = GetValidProjectName(init, id);
    if (string.IsNullOrWhiteSpace(projectName)) return;

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
}

(bool,string,Project) HandleArtifactGeneration(IConfiguration configuration, string id)
{
    Init init = new Init();
    var (accessToken, organizationName, authScheme) = AuthenticateUser(init, id);
    if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(organizationName)) return (false, string.Empty,null);

    IProjectService projService = new ProjectService(configuration);
    var projects = projService.GetProjects(organizationName, accessToken, authScheme);
    var projectDetails = projService.SelectProject(accessToken, projects).Result;

    var projectName = projectDetails[1];
    if (string.IsNullOrWhiteSpace(projectName)) return (false, string.Empty, null);

    var model = new Project
    {
        accountName = organizationName,
        ProjectName = projectName,
        ProjectId = projectDetails[0],
        accessToken = accessToken,
        adoAuthScheme = authScheme,
        id = id
    };

    ITemplateService templateService = new TemplateService(configuration);
    bool isTemplateExists = templateService.CheckTemplateExists(model);
    string template = string.Empty;
    if (isTemplateExists)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(Environment.NewLine + "Do you still need to update the existing template?(yes/no)");
        Console.ResetColor();
        var updateTemplate = Console.ReadLine();
        if (updateTemplate?.Equals("yes", StringComparison.OrdinalIgnoreCase) == true)
        {
            (bool isArtifactsGenerated, template, Project project) = AnalyzeAndGenerateArtifacts(model, templateService);
            if (isArtifactsGenerated)
            {
                return (true, template, project);
            }
        }
        else
        {
            id.AddMessage(Environment.NewLine + "Template update skipped.");
            return (false, "skipped", null);
        }
    }
    else
    {
        (bool isArtifactsGenerated, template, Project project) = AnalyzeAndGenerateArtifacts(model, templateService);
        if (isArtifactsGenerated)
        {
            return (true, template, project);
        }
    }
    return (false, template, null);
}
(bool, string, Project) AnalyzeAndGenerateArtifacts(Project model, ITemplateService templateService)
{
    var analyzed = templateService.AnalyzeProject(model);
    if (analyzed)
    {
        model.id.AddMessage("Artifacts analyzed successfully.");
    }
    else
    {
        model.id.ErrorId().AddMessage("Artifacts analysis failed.");
        return (false, string.Empty, null);
    }
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine(Environment.NewLine + "Do you want to create artifacts yes/no:");
    Console.ResetColor();
    string response = Console.ReadLine();
    if (response == "yes")
    {
        (bool isArtifactsGenerated, string template, string templateLocation) = templateService.GenerateTemplateArtifacts(model);
        if (isArtifactsGenerated)
        {
            model.id.AddMessage(Environment.NewLine + "Artifacts has been generated sccessfully at the location: " + templateLocation);
            return (true, template, model);
        }
        else
        {
            model.id.ErrorId().AddMessage(Environment.NewLine + "Artifacts generation failed.");
        }
    }
    else
    {
        model.id.AddMessage(Environment.NewLine + "Artifacts generation skipped.");
        return (false, "skipped", null);
    }
    return (false, string.Empty, null);
}
JToken LoadTemplates(string templatePath, string id)
{
    var templateSettings = File.ReadAllText(templatePath);
    var json = JObject.Parse(templateSettings);
    var groupwiseTemplates = json["GroupwiseTemplates"];

    if (groupwiseTemplates == null)
    {
        id.ErrorId().AddMessage(Environment.NewLine + "No templates found.");
        return null;
    }

    return groupwiseTemplates;
}

string SelectTemplate(JToken groupwiseTemplates, string id)
{
    int templateIndex = 1;
    var templateDictionary = new Dictionary<int, string>();

    foreach (var group in groupwiseTemplates)
    {
        var groupName = group["Groups"]?.ToString();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(groupName);
        Console.ResetColor();
        var templates = group["Template"];
        if (templates != null)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(new string('-', 100)); // Top border of the table
            Console.WriteLine($"| {"Index",-10} | {"Template Name",-30} | {"Description",-50} |");
            Console.WriteLine(new string('-', 100)); // Separator line  
            foreach (var template in templates)
            {
                var templateName = template["Name"]?.ToString();
                var templateDescription = template["Description"]?.ToString();

                // Wrap content to fit console width  
                var wrappedTemplateName = WrapText(templateName, 30);
                var wrappedTemplateDescription = WrapText(templateDescription, 50);

                var templateNameLines = wrappedTemplateName.Split(Environment.NewLine);
                var templateDescriptionLines = wrappedTemplateDescription.Split(Environment.NewLine);
                var maxLines = Math.Max(templateNameLines.Length, templateDescriptionLines.Length);

                for (int i = 0; i < maxLines; i++)
                {
                    var nameLine = i < templateNameLines.Length ? templateNameLines[i] : string.Empty;
                    var descriptionLine = i < templateDescriptionLines.Length ? templateDescriptionLines[i] : string.Empty;

                    if (i == 0)
                    {
                        Console.WriteLine($"| {templateIndex,-10} | {nameLine,-30} | {descriptionLine,-50} |");
                    }
                    else
                    {
                        Console.WriteLine($"| {"",-10} | {nameLine,-30} | {descriptionLine,-50} |");
                    }
                }

                Console.WriteLine(new string('-', 100)); // Divider after each row
                templateDictionary.Add(templateIndex, templateName);
                templateIndex++;
            }
            Console.ResetColor();
        }
    }
    id.AddMessage(Environment.NewLine+"Enter the template number from the list of templates above:");
    if (!int.TryParse(Console.ReadLine(), out var selectedTemplateNumber) || !templateDictionary.TryGetValue(selectedTemplateNumber, out var selectedTemplateName))
    {
        id.AddMessage(Environment.NewLine+"Invalid template number entered.");
        return null;
    }

    return selectedTemplateName.Trim();
}
string WrapText(string text, int maxWidth)
{
    if (string.IsNullOrEmpty(text)) return string.Empty;

    var wrappedText = new StringBuilder();
    int currentIndex = 0;

    while (currentIndex < text.Length)
    {
        int remainingLength = text.Length - currentIndex;
        int lineLength = Math.Min(maxWidth, remainingLength);

        // Split at the exact maxWidth if no whitespace or special character is found
        wrappedText.AppendLine(text.Substring(currentIndex, lineLength).Trim());
        currentIndex += lineLength;
    }

    return wrappedText.ToString().TrimEnd();
}

(string accessToken, string organizationName, string authScheme) AuthenticateUser(Init init, string id)
{
    string organizationName = string.Empty;
    if (authenticationDetails.HasValue &&
       (!string.IsNullOrWhiteSpace(authenticationDetails.Value.organizationName) ||
        !string.IsNullOrWhiteSpace(authenticationDetails.Value.accessToken) ||
        !string.IsNullOrWhiteSpace(authenticationDetails.Value.authScheme)))
    {
        if (string.IsNullOrWhiteSpace(authenticationDetails.Value.organizationName))
        {
            if (authChoice == "1")
            {
                AuthService authService = new AuthService();
                var memberId = authService.GetProfileInfoAsync(authenticationDetails.Value.accessToken).Result;
                var organizations = authService.GetOrganizationsAsync(authenticationDetails.Value.accessToken, memberId).Result;
                organizationName = authService.SelectOrganization(authenticationDetails.Value.accessToken, organizations).Result;
            }
            else if (authChoice == "2")
            {
                id.AddMessage(Environment.NewLine + "Enter your Azure DevOps organization name:");
                organizationName = Console.ReadLine();
            }
            authenticationDetails = (authenticationDetails.Value.accessToken, organizationName, authenticationDetails.Value.authScheme);
        }
        return authenticationDetails.Value;
    }
    id.AddMessage(Environment.NewLine + "Choose authentication method: 1. Device Login using AD auth 2. Personal Access Token (PAT)");
    authChoice = Console.ReadLine();

    string accessToken = string.Empty;
    string authScheme = string.Empty;

    if (authChoice == "1")
    {
        var app = PublicClientApplicationBuilder.Create(AuthService.clientId)
            .WithAuthority(AuthService.authority)
            .WithDefaultRedirectUri()
            .Build();
        AuthService authService = new AuthService();

        var accounts = app.GetAccountsAsync().Result;
        if (accounts.Any())
        {
            try
            {
                var result = app.AcquireTokenSilent(AuthService.scopes, accounts.FirstOrDefault())
                            .WithForceRefresh(true)
                            .ExecuteAsync().Result;
                accessToken = result.AccessToken;
            }
            catch (MsalUiRequiredException)
            {
                var result = authService.AcquireTokenAsync(app).Result;
                accessToken = result.AccessToken;
            }
        }
        else
        {
            var result = authService.AcquireTokenAsync(app).Result;
            accessToken = result.AccessToken;
        }

        var memberId = authService.GetProfileInfoAsync(accessToken).Result;
        var organizations = authService.GetOrganizationsAsync(accessToken, memberId).Result;
        organizationName = authService.SelectOrganization(accessToken, organizations).Result;
        authScheme = "Bearer";
    }
    else if (authChoice == "2")
    {
        id.AddMessage(Environment.NewLine + "Enter your Azure DevOps organization name:");
        organizationName = Console.ReadLine();

        id.AddMessage(Environment.NewLine + "Enter your Azure DevOps personal access token:");
        accessToken = init.ReadSecret();

        authScheme = "Basic";
    }

    authenticationDetails = (accessToken, organizationName, authScheme);
    return authenticationDetails.Value;
}

string GetValidProjectName(Init init, string id)
{
    string projectName = "";
    do
    {
        id.AddMessage(Environment.NewLine + "Enter the new project name:");
        projectName = Console.ReadLine();
        if (!init.CheckProjectName(projectName))
        {
            id.ErrorId().AddMessage(Environment.NewLine+"Validation error: Project name is not valid.");
            id.AddMessage(Environment.NewLine + "Do you want to try with a valid project name or exit? (type 'retry' to try again or 'exit' to quit):");
            var userChoice = Console.ReadLine();
            if (userChoice?.Equals("exit", StringComparison.OrdinalIgnoreCase) == true)
            {
                id.AddMessage(Environment.NewLine + "Exiting the application.");
                Environment.Exit(1);
            }
            projectName = "";
        }
    } while (string.IsNullOrWhiteSpace(projectName));
    return projectName;
}
bool TryGetTemplateDetails(JToken groupwiseTemplates, string selectedTemplateName, out string templateFolder, string id)
{
    templateFolder = string.Empty;

    foreach (var group in groupwiseTemplates)
    {
        var templates = group["Template"];
        if (templates == null) continue;

        foreach (var template in templates)
        {
            if (template["Name"]?.ToString().Equals(selectedTemplateName, StringComparison.OrdinalIgnoreCase) != true)
                continue;
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
        id.AddMessage(Environment.NewLine + "Do you want to proceed with this extension? (yes/No): press enter to confirm");
        var userConfirmation = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(userConfirmation) || (userConfirmation.Equals("yes", StringComparison.OrdinalIgnoreCase) || userConfirmation.Equals("y", StringComparison.OrdinalIgnoreCase)))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            id.AddMessage(Environment.NewLine + "Agreed for license? (yes/no): press enter to confirm");
            Console.ResetColor();
            var licenseConfirmation = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(licenseConfirmation) || (licenseConfirmation.Equals("yes", StringComparison.OrdinalIgnoreCase) || licenseConfirmation.Equals("y", StringComparison.OrdinalIgnoreCase)))
            {
                id.AddMessage(Environment.NewLine + "Confirmed Extension installation");
                return true;
            }
        }
        else
        {
            id.AddMessage(Environment.NewLine + "Extension installation is not confirmed.");
        }
    }

    return false;
}

void CreateProjectEnvironment(Project model)
{
    Console.WriteLine($"Creating project '{model.ProjectName}' in organization '{model.accountName}' using template from '{model.TemplateName}'...");
    var projectService = new ProjectService(configuration);
    var result = projectService.CreateProjectEnvironment(model);
    if (result)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Project created successfully.");
        Console.ResetColor();
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("Project creation failed.");
        Console.ResetColor();
    }
}
bool UpdateTemplateSettings(string template, string id, string templatePath)
{
    if (!File.Exists(templatePath))
    {
        id.ErrorId().AddMessage(Environment.NewLine+"TemplateSettings.json file not found at " + templatePath);
        return false;
    }

    var templateSettings = File.ReadAllText(templatePath);
    var json = JObject.Parse(templateSettings);

    UpdateGroups(json, id);
    UpdateGroupwiseTemplates(json, template, id);

    File.WriteAllText(templatePath, JsonConvert.SerializeObject(json, Formatting.Indented));
    return true;
}

void UpdateGroups(JObject json, string id)
{
    var groups = json["Groups"] as JArray ?? new JArray(json["Groups"]);
    if (!groups.Any(g => g.ToString().Equals("Custom Templates", StringComparison.OrdinalIgnoreCase)))
    {
        var customTemplates = new JArray { "Custom Templates" };
        foreach (var item in customTemplates)
        {
            groups.Add(item);
        }
        json["Groups"] = groups;
    }
    else
    {
        id.AddMessage("Custom Templates group already exists.");
    }
}

void UpdateGroupwiseTemplates(JObject json, string template, string id)
{
    var groupwiseTemplates = json["GroupwiseTemplates"] as JArray ?? new JArray(json["GroupwiseTemplates"]);
    var newCustomTemplate = JObject.Parse(template);
    var groupwiseCustomTemplates = newCustomTemplate["GroupwiseTemplates"];

    if (groupwiseTemplates != null && groupwiseCustomTemplates != null)
    {
        foreach (var group in groupwiseCustomTemplates)
        {
            var groupName = group["Groups"]?.ToString();
            var existingGroup = groupwiseTemplates.FirstOrDefault(g => g["Groups"]?.ToString().Equals(groupName, StringComparison.OrdinalIgnoreCase) == true);

            if (existingGroup != null)
            {
                MergeTemplates(existingGroup, group, id);
            }
            else
            {
                groupwiseTemplates.Add(group);
            }
        }
    }
}

void MergeTemplates(JToken existingGroup, JToken newGroup, string id)
{
    var existingTemplates = existingGroup["Template"] as JArray ?? new JArray(existingGroup["Template"]);
    var newTemplates = newGroup["Template"] as JArray ?? new JArray(newGroup["Template"]);

    var existingTemplateNames = existingTemplates.Select(t => t["Name"]?.ToString()).ToList();
    var newTemplateNames = newTemplates.Select(t => t["Name"]?.ToString()).ToList();

    var duplicateTemplates = newTemplateNames.Where(t => existingTemplateNames.Contains(t)).ToList();
    if (duplicateTemplates.Any())
    {
        id.AddMessage($"Duplicate templates found: {string.Join(", ", duplicateTemplates)}. Skipping these templates.");
        newTemplates = new JArray(newTemplates.Where(t => !duplicateTemplates.Contains(t["Name"]?.ToString())));
    }
    else
    {
        id.AddMessage($"No duplicate templates found. Adding new templates.");
    }
    foreach (var templateCustom in newTemplates)
    {
        existingTemplates.Add(templateCustom);
    }
}