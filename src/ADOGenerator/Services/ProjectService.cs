using ADOGenerator.IServices;
using ADOGenerator.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.ExtensionManagement.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestAPI;
using RestAPI.Builds;
using RestAPI.DeliveryPlans;
using RestAPI.DeploymentGRoup;
using RestAPI.Extractor;
using RestAPI.Git;
using RestAPI.ProjectsAndTeams;
using RestAPI.QueriesAndWidgets;
using RestAPI.ReleasesDef;
using RestAPI.Service;
using RestAPI.Services;
using RestAPI.TestManagement;
using RestAPI.Viewmodel.BranchPolicy;
using RestAPI.Viewmodel.Extractor;
using RestAPI.Viewmodel.GitHub;
using RestAPI.Viewmodel.Importer;
using RestAPI.Viewmodel.ProjectAndTeams;
using RestAPI.Viewmodel.QueriesAndWidgets;
using RestAPI.Viewmodel.Repository;
using RestAPI.Viewmodel.Sprint;
using RestAPI.Viewmodel.Wiki;
using RestAPI.Viewmodel.WorkItem;
using RestAPI.Wiki;
using RestAPI.WorkItemAndTracking;
using System.Diagnostics;

namespace ADOGenerator.Services
{
    public class ProjectService : IProjectService
    {
        private static readonly object objLock = new();
        private static Dictionary<string, string> statusMessages;

        public bool isDefaultRepoTodetele = true;
        public string websiteUrl = string.Empty;
        public string templateUsed = string.Empty;
        private string adoAuthScheme = string.Empty;
        private static string projectName = string.Empty;
        private static AccessDetails AccessDetails = new AccessDetails();

        private string templateVersion = string.Empty;
        private readonly IConfiguration _configuration;

        public ProjectService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public HttpResponseMessage GetprojectList(string accname, string pat)
        {
            string defaultHost = _configuration["AppSettings:DefaultHost"];
            if (string.IsNullOrEmpty(defaultHost))
            {
                throw new InvalidOperationException("DefaultHost configuration is missing.");
            }

            string ProjectCreationVersion = _configuration["AppSettings:ProjectCreationVersion"];
            if (ProjectCreationVersion == null)
            {
                throw new InvalidOperationException("ProjectCreationVersion configuration is missing.");
            }

            ADOConfiguration config = new ADOConfiguration() { AccountName = accname, PersonalAccessToken = pat, UriString = defaultHost + accname, VersionNumber = ProjectCreationVersion };
            Projects projects = new Projects(config);
            HttpResponseMessage response = projects.GetListOfProjects();
            return response;
        }
        /// <summary>
        /// Get the path where we can file template related json files for selected template
        /// </summary>
        /// <param name="TemplateFolder"></param>
        /// <param name="TemplateName"></param>
        /// <param name="FileName"></param>
        public string GetJsonFilePath(bool IsPrivate, string TemplateFolder, string TemplateName, string FileName = "")
        {
            string filePath = string.Empty;
            filePath = string.Format(Path.Combine(Directory.GetCurrentDirectory(), "Templates", TemplateName, FileName));
            return filePath;
        }

        #region Project Setup Operations

        /// <summary>
        /// start provisioning project - calls required
        /// </summary>
        /// <param name="model"></param>
        /// <param name="pat"></param>
        /// <param name="accountName"></param>
        /// <returns></returns>
        //public string[] CreateProjectEnvironment(string accountName, string newProjectName, string token, string templateFolder, string templateUsed)
        public bool CreateProjectEnvironment(Project model)
        {
            string pat = model.accessToken;
            templateUsed = model.selectedTemplateFolder;
            string accountName = model.accountName;
            //define versions to be use
            var appSettings = _configuration.GetSection("AppSettings");
            string projectCreationVersion = appSettings["ProjectCreationVersion"] ?? string.Empty;
            string repoVersion = appSettings["RepoVersion"] ?? string.Empty;
            string buildVersion = appSettings["BuildVersion"] ?? string.Empty;
            string releaseVersion = appSettings["ReleaseVersion"] ?? string.Empty;
            string wikiVersion = appSettings["WikiVersion"] ?? string.Empty;
            string boardVersion = appSettings["BoardVersion"] ?? string.Empty;
            string workItemsVersion = appSettings["WorkItemsVersion"] ?? string.Empty;
            string queriesVersion = appSettings["QueriesVersion"] ?? string.Empty;
            string endPointVersion = appSettings["EndPointVersion"] ?? string.Empty;
            string extensionVersion = appSettings["ExtensionVersion"] ?? string.Empty;
            string dashboardVersion = appSettings["DashboardVersion"] ?? string.Empty;
            string agentQueueVersion = appSettings["AgentQueueVersion"] ?? string.Empty;
            string getSourceCodeVersion = appSettings["GetSourceCodeVersion"] ?? string.Empty;
            string testPlanVersion = appSettings["TestPlanVersion"] ?? string.Empty;
            string releaseHost = appSettings["ReleaseHost"] ?? string.Empty;
            string defaultHost = appSettings["DefaultHost"] ?? string.Empty;
            string deploymentGroup = appSettings["DeloymentGroup"] ?? string.Empty;
            string graphApiVersion = appSettings["GraphApiVersion"] ?? string.Empty;
            string graphAPIHost = appSettings["GraphAPIHost"] ?? string.Empty;
            string gitHubBaseAddress = appSettings["GitHubBaseAddress"] ?? string.Empty;
            string variableGroupsApiVersion = appSettings["VariableGroupsApiVersion"] ?? string.Empty;

            string processTemplateId = Default.SCRUM;
            model.Environment = new EnvironmentValues
            {
                serviceEndpoints = new(),
                repositoryIdList = new(),
                pullRequests = new(),
                gitHubRepos = new(),
                variableGroups = new(),
                reposImported = new()
            };
            ProjectTemplate template = null;
            ProjectSettings settings = null;
            List<RestAPI.WorkItemAndTracking.WIMapData> wiMapping = new List<RestAPI.WorkItemAndTracking.WIMapData>();
            //AccountMembers.Account accountMembers = new AccountMembers.Account();
            model.accountUsersForWi = new List<string>();
            websiteUrl = model.websiteUrl;
            projectName = model.ProjectName;
            adoAuthScheme = model.adoAuthScheme;


            if (appSettings["AppSettings:LogWIT"] == "true")
            {
                string patBase64 = appSettings["PATBase64"];
                string url = appSettings["URL"];
                string projectId = appSettings["PROJECTID"];
                string reportName = "AzureDevOps_Analytics-DemoGenerator";
                var objIssue = new IssueWI();
                objIssue.CreateReportWI(patBase64, "4.1", url, websiteUrl, reportName, "", templateUsed, projectId, model.Region);
            }

            //ADOConfiguration _gitHubConfig = new ADOConfiguration() { _gitbaseAddress = gitHubBaseAddress, _gitcredential = model.GitHubToken, _mediaType = "application/json", _scheme = "Bearer" };

            //if (model.GitHubFork && model.GitHubToken != null)
            //{
            //    GitHubImportRepo gitHubImport = new GitHubImportRepo(_gitHubConfig);
            //    HttpResponseMessage userResponse = gitHubImport.GetUserDetail();
            //    GitHubUserDetail userDetail = new GitHubUserDetail();
            //    if (userResponse.IsSuccessStatusCode)
            //    {
            //        userDetail = JsonConvert.DeserializeObject<GitHubUserDetail>(userResponse.Content.ReadAsStringAsync().Result);
            //        _gitHubConfig.userName = userDetail.login;
            //        model.GitHubUserName = userDetail.login;
            //    }
            //}
            //configuration setup
            string _credentials = model.accessToken;
            string baseUri = defaultHost + accountName + "/";
            string releaseUri = releaseHost + accountName + "/";
            string graphUri = graphAPIHost + accountName + "/";

            ADOConfiguration _projectCreationVersion = new ADOConfiguration() { UriString = baseUri, VersionNumber = projectCreationVersion, PersonalAccessToken = pat, Project = model.ProjectName, AccountName = accountName, _adoAuthScheme = adoAuthScheme };
            ADOConfiguration _releaseVersion = new ADOConfiguration() { UriString = releaseUri, VersionNumber = releaseVersion, PersonalAccessToken = pat, Project = model.ProjectName, AccountName = accountName, _adoAuthScheme = adoAuthScheme };
            ADOConfiguration _buildVersion = new ADOConfiguration() { UriString = baseUri, VersionNumber = buildVersion, PersonalAccessToken = pat, Project = model.ProjectName, AccountName = accountName, _gitbaseAddress = gitHubBaseAddress, _gitcredential = model.GitHubToken, _adoAuthScheme = adoAuthScheme };
            ADOConfiguration _workItemsVersion = new ADOConfiguration() { UriString = baseUri, VersionNumber = workItemsVersion, PersonalAccessToken = pat, Project = model.ProjectName, AccountName = accountName, _adoAuthScheme = adoAuthScheme };
            ADOConfiguration _queriesVersion = new ADOConfiguration() { UriString = baseUri, VersionNumber = queriesVersion, PersonalAccessToken = pat, Project = model.ProjectName, AccountName = accountName, _adoAuthScheme = adoAuthScheme };
            ADOConfiguration _boardVersion = new ADOConfiguration() { UriString = baseUri, VersionNumber = boardVersion, PersonalAccessToken = pat, Project = model.ProjectName, AccountName = accountName, _adoAuthScheme = adoAuthScheme };
            ADOConfiguration _wikiVersion = new ADOConfiguration() { UriString = baseUri, VersionNumber = wikiVersion, PersonalAccessToken = pat, Project = model.ProjectName, AccountName = accountName, _adoAuthScheme = adoAuthScheme };
            ADOConfiguration _endPointVersion = new ADOConfiguration() { UriString = baseUri, VersionNumber = endPointVersion, PersonalAccessToken = pat, Project = model.ProjectName, AccountName = accountName, _gitbaseAddress = gitHubBaseAddress, _gitcredential = model.GitHubToken, _adoAuthScheme = adoAuthScheme };
            ADOConfiguration _extensionVersion = new ADOConfiguration() { UriString = baseUri, VersionNumber = extensionVersion, PersonalAccessToken = pat, Project = model.ProjectName, AccountName = accountName, _adoAuthScheme = adoAuthScheme };
            ADOConfiguration _dashboardVersion = new ADOConfiguration() { UriString = baseUri, VersionNumber = dashboardVersion, PersonalAccessToken = pat, Project = model.ProjectName, AccountName = accountName, _adoAuthScheme = adoAuthScheme };
            ADOConfiguration _repoVersion = new ADOConfiguration() { UriString = baseUri, VersionNumber = repoVersion, PersonalAccessToken = pat, Project = model.ProjectName, AccountName = accountName, _gitbaseAddress = gitHubBaseAddress, _gitcredential = model.GitHubToken, _adoAuthScheme = adoAuthScheme };
            ADOConfiguration _getSourceCodeVersion = new ADOConfiguration() { UriString = baseUri, VersionNumber = getSourceCodeVersion, PersonalAccessToken = pat, Project = model.ProjectName, AccountName = accountName, _gitbaseAddress = gitHubBaseAddress, _gitcredential = model.GitHubToken, _adoAuthScheme = adoAuthScheme };
            ADOConfiguration _agentQueueVersion = new ADOConfiguration() { UriString = baseUri, VersionNumber = agentQueueVersion, PersonalAccessToken = pat, Project = model.ProjectName, AccountName = accountName, _adoAuthScheme = adoAuthScheme };
            ADOConfiguration _testPlanVersion = new ADOConfiguration() { UriString = baseUri, VersionNumber = testPlanVersion, PersonalAccessToken = pat, Project = model.ProjectName, AccountName = accountName, _adoAuthScheme = adoAuthScheme };
            ADOConfiguration _deploymentGroup = new ADOConfiguration() { UriString = baseUri, VersionNumber = deploymentGroup, PersonalAccessToken = pat, Project = model.ProjectName, AccountName = accountName, _adoAuthScheme = adoAuthScheme };
            ADOConfiguration _graphApiVersion = new ADOConfiguration() { UriString = graphUri, VersionNumber = graphApiVersion, PersonalAccessToken = pat, Project = model.ProjectName, AccountName = accountName, _adoAuthScheme = adoAuthScheme };
            ADOConfiguration _variableGroupApiVersion = new ADOConfiguration() { UriString = baseUri, VersionNumber = variableGroupsApiVersion, PersonalAccessToken = pat, Project = model.ProjectName, AccountName = accountName, _adoAuthScheme = adoAuthScheme };

            string projTemplateFile = GetJsonFilePath(model.IsPrivatePath, model.PrivateTemplatePath, templateUsed, "ProjectTemplate.json");
            ProjectSetting setting = null;
            if (File.Exists(projTemplateFile))
            {
                string _checkIsPrivate = File.ReadAllText(projTemplateFile);
                if (_checkIsPrivate != "")
                {
                    setting = JsonConvert.DeserializeObject<ProjectSetting>(_checkIsPrivate);
                }
            }
            //initialize project template and settings
            string projectSettingsFile = string.Empty;
            try
            {
                if (File.Exists(projTemplateFile))
                {
                    string templateItems = model.ReadJsonFile(projTemplateFile);
                    template = JsonConvert.DeserializeObject<ProjectTemplate>(templateItems);
                    projectSettingsFile = GetJsonFilePath(model.IsPrivatePath, model.PrivateTemplatePath, templateUsed, template.ProjectSettings);

                    if (File.Exists(projectSettingsFile))
                    {
                        settings = JsonConvert.DeserializeObject<ProjectSettings>(model.ReadJsonFile(projectSettingsFile));

                        if (!string.IsNullOrWhiteSpace(settings.type))
                        {
                            if (settings.type.ToLower() == TemplateType.Scrum.ToString().ToLower())
                            {
                                processTemplateId = Default.SCRUM;
                            }
                            else if (settings.type.ToLower() == TemplateType.Agile.ToString().ToLower())
                            {
                                processTemplateId = Default.Agile;
                            }
                            else if (settings.type.ToLower() == TemplateType.CMMI.ToString().ToLower())
                            {
                                processTemplateId = Default.CMMI;
                            }
                            else if (settings.type.ToLower() == TemplateType.Basic.ToString().ToLower())
                            {
                                processTemplateId = Default.BASIC;
                            }
                            else if (!string.IsNullOrEmpty(settings.id))
                            {
                                processTemplateId = settings.id;
                            }
                            else
                            {
                                model.id.ErrorId().AddMessage("Could not recognize process template. Make sure that the exported project template is belog to standard process template or project setting file has valid process template id.");
                                return false;
                            }
                        }
                        else
                        {
                            settings.type = "scrum";
                            processTemplateId = Default.SCRUM;
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Project Templates not found");
                    model.id.ErrorId().AddMessage("Project Templates not found");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in reading project template file:" + ex.Message);
            }
            //create team project
            string jsonProject = model.ReadJsonFile(Path.Combine(Directory.GetCurrentDirectory(), "Templates", "CreateProject.json"));
            jsonProject = jsonProject.Replace("$projectName$", model.ProjectName).Replace("$processTemplateId$", processTemplateId);

            Projects proj = new Projects(_projectCreationVersion);
            string projectID = proj.CreateTeamProject(jsonProject);

            if (projectID == "-1")
            {
                if (!string.IsNullOrEmpty(proj.LastFailureMessage))
                {
                    if (proj.LastFailureMessage.Contains("TF400813"))
                    {
                        model.id.ErrorId().AddMessage("OAUTHACCESSDENIED");
                    }
                    else if (proj.LastFailureMessage.Contains("TF50309"))
                    {
                        model.id.ErrorId().AddMessage(proj.LastFailureMessage);
                    }
                    else
                    {
                        model.id.ErrorId().AddMessage(proj.LastFailureMessage);
                    }
                }
                Thread.Sleep(2000); // Adding Delay to Get Error message
                return false;
            }
            else
            {
                model.id.AddMessage(string.Format("Project {0} created", model.ProjectName));
            }
            // waiting to add first message
            Thread.Sleep(2000);

            //Check for project state 
            Stopwatch watch = new Stopwatch();
            watch.Start();
            string projectStatus = string.Empty;
            Projects objProject = new Projects(_projectCreationVersion);
            while (projectStatus.ToLower() != "wellformed")
            {
                projectStatus = objProject.GetProjectStateByName(model.ProjectName);
                if (watch.Elapsed.Minutes >= 5)
                {
                    return false;
                }
            }
            watch.Stop();

            //get project id after successfull in VSTS
            model.Environment.ProjectId = objProject.GetProjectIdByName(model.ProjectName);
            model.Environment.ProjectName = model.ProjectName;

            // Fork Repo
            //if (model.GitHubFork && model.GitHubToken != null)
            //{
            //    ForkGitHubRepository(model, _gitHubConfig);
            //}

            //Install required extensions
            if (model.isExtensionNeeded && model.isAgreeTerms)
            {
                bool isInstalled = InstallExtensions(model, model.accountName, model.accessToken);
                Thread.Sleep(1000);
                if (isInstalled)
                {
                    model.id.AddMessage("Required extensions are installed");
                }

            }
            //current user Details
            string teamName = model.ProjectName + " team";
            TeamMemberResponse.TeamMembers teamMembers = GetTeamMembers(model.ProjectName, teamName, _projectCreationVersion, model.id);

            var teamMember = teamMembers.value != null ? teamMembers.value.FirstOrDefault() : new TeamMemberResponse.Value();

            if (teamMember != null)
            {
                model.Environment.UserUniquename = model.Environment.UserUniquename ?? teamMember.identity.uniqueName;
                model.Environment.UserUniqueId = model.Environment.UserUniqueId ?? teamMember.identity.id;
            }

            //model.Environment.UserUniqueId = model.Email;
            //model.Environment.UserUniquename = model.Email;
            //update board columns and rows
            // Checking for template version
            string projectTemplate = File.ReadAllText(GetJsonFilePath(model.IsPrivatePath, model.PrivateTemplatePath, templateUsed, "ProjectTemplate.json"));

            if (!string.IsNullOrEmpty(projectTemplate))
            {
                JObject jObject = JsonConvert.DeserializeObject<JObject>(projectTemplate);
                templateVersion = jObject["TemplateVersion"] == null ? string.Empty : jObject["TemplateVersion"].ToString();
            }

            #region setup teams and iterations
            UpdateIterations(model, _boardVersion, "Iterations.json");
            // for newer version of templates
            string teamsJsonPath = GetJsonFilePath(model.IsPrivatePath, model.PrivateTemplatePath, templateUsed, "Teams\\Teams.json");
            if (File.Exists(teamsJsonPath))
            {
                template.Teams = "Teams\\Teams.json";
                template.TeamArea = "TeamArea.json";
                CreateTeams(model, template.Teams, _projectCreationVersion, model.id, template.TeamArea);
                string jsonTeams = model.ReadJsonFile(teamsJsonPath);
                JArray jTeams = JsonConvert.DeserializeObject<JArray>(jsonTeams);
                JContainer teamsParsed = JsonConvert.DeserializeObject<JContainer>(jsonTeams);
                _buildVersion.ProjectId = model.Environment.ProjectId;
                foreach (var jteam in jTeams)
                {
                    string _teamName = jteam["isDefault"]?.ToString() == "true" ? model.ProjectName + " Team" : jteam["name"].ToString();
                    string teamFolderPath = GetJsonFilePath(model.IsPrivatePath, model.PrivateTemplatePath, templateUsed, $"Teams\\{jteam["name"]}");
                    if (Directory.Exists(teamFolderPath))
                    {
                        BoardColumn objBoard = new BoardColumn(_boardVersion);

                        // updating swimlanes for each teams each board(epic, feature, PBI, Stories) 
                        string updateSwimLanesJSON = Path.Combine(teamFolderPath, "BoardRows.json");
                        if (File.Exists(updateSwimLanesJSON))
                        {
                            updateSwimLanesJSON = File.ReadAllText(updateSwimLanesJSON);
                            List<ImportBoardRows.Rows> importRows = JsonConvert.DeserializeObject<List<ImportBoardRows.Rows>>(updateSwimLanesJSON);
                            foreach (var board in importRows)
                            {
                                SwimLanes objSwimLanes = new SwimLanes(_boardVersion);
                                objSwimLanes.UpdateSwimLanes(JsonConvert.SerializeObject(board.value), model.ProjectName, board.BoardName, _teamName);
                            }
                        }

                        // updating team setting for each team
                        string teamSettingJson = Path.Combine(teamFolderPath, "TeamSetting.json");
                        if (File.Exists(teamSettingJson))
                        {
                            teamSettingJson = File.ReadAllText(teamSettingJson);
                            EnableEpic(model, teamSettingJson, _boardVersion, model.id, _teamName);
                        }

                        // updating board columns for each teams each board
                        string teamBoardColumns = Path.Combine(teamFolderPath, "BoardColumns.json");
                        if (File.Exists(teamBoardColumns))
                        {
                            teamBoardColumns = File.ReadAllText(teamBoardColumns);
                            List<ImportBoardColumns.ImportBoardCols> importBoardCols = JsonConvert.DeserializeObject<List<ImportBoardColumns.ImportBoardCols>>(teamBoardColumns);
                            foreach (var board in importBoardCols)
                            {
                                UpdateBoardColumn(model, JsonConvert.SerializeObject(board.value, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }), _boardVersion, model.id, board.BoardName, _teamName);
                            }
                        }

                        // updating card fields for each team and each board
                        try
                        {
                            string teamCardFields = Path.Combine(teamFolderPath, "CardFields.json");
                            if (File.Exists(teamCardFields))
                            {
                                teamCardFields = File.ReadAllText(teamCardFields);
                                List<ImportCardFields.CardFields> cardFields = JsonConvert.DeserializeObject<List<ImportCardFields.CardFields>>(teamCardFields);
                                foreach (var card in cardFields)
                                {
                                    UpdateCardFields(model, JsonConvert.SerializeObject(card, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }), _boardVersion, model.id, card.BoardName, _teamName);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            model.id.ErrorId().AddMessage(ex.Message);
                        }

                        // updating card styles for each team and each board
                        string teamCardStyle = Path.Combine(teamFolderPath, "CardStyles.json");
                        if (File.Exists(teamCardStyle))
                        {
                            teamCardStyle = File.ReadAllText(teamCardStyle);
                            List<CardStyle.Style> cardStyles = JsonConvert.DeserializeObject<List<CardStyle.Style>>(teamCardStyle);
                            foreach (var cardStyle in cardStyles)
                            {
                                if (cardStyle.rules.fill != null)
                                {
                                    UpdateCardStyles(model, JsonConvert.SerializeObject(cardStyle), _boardVersion, model.id, cardStyle.BoardName, _teamName);
                                }
                            }
                        }

                        string includeSubArea = Path.Combine(teamFolderPath, "IncludeSubAreas.json");
                        if (File.Exists(includeSubArea))
                        {
                            Teams objTeam = new Teams(_boardVersion);
                            TeamResponse teamRes = objTeam.GetTeamByName(model.ProjectName, _teamName);
                            _boardVersion.ProjectId = model.Environment.ProjectId;

                            includeSubArea = File.ReadAllText(includeSubArea);
                            IncludeSubAreas.Root subAreas = JsonConvert.DeserializeObject<IncludeSubAreas.Root>(includeSubArea);

                            subAreas.defaultValue = model.Environment.ProjectName;
                            subAreas.values.FirstOrDefault().includeChildren = true;
                            subAreas.values.FirstOrDefault().value = model.Environment.ProjectName;

                            BoardColumn board = new BoardColumn(_boardVersion);
                            board.IncludeSubAreas(JsonConvert.SerializeObject(subAreas), _boardVersion, teamRes);
                        }
                    }
                }
                model.id.AddMessage("Board-Column, Swimlanes, Styles updated");
                UpdateSprintItems(model, _boardVersion, settings);

                RenameIterations(model, _boardVersion, settings.renameIterations);
            }
            #endregion


            #region create service endpoint
            List<string> listEndPointsJsonPath = new List<string>();
            string serviceEndPointsPath = GetJsonFilePath(model.IsPrivatePath, model.PrivateTemplatePath, templateUsed, "ServiceEndpoints");
            if (Directory.Exists(serviceEndPointsPath))
            {
                Directory.GetFiles(serviceEndPointsPath).ToList().ForEach(i => listEndPointsJsonPath.Add(i));
            }
            CreateServiceEndPoint(model, listEndPointsJsonPath, _endPointVersion);
            //create agent queues on demand
            RestAPI.Queues.Queue queue = new RestAPI.Queues.Queue(_agentQueueVersion);
            model.Environment.AgentQueues = queue.GetQueues();
            if (settings.queues != null && settings.queues.Count > 0)
            {
                foreach (string aq in settings.queues)
                {
                    if (model.Environment.AgentQueues.ContainsKey(aq))
                    {
                        continue;
                    }

                    var id = queue.CreateQueue(aq);
                    if (id > 0)
                    {
                        model.Environment.AgentQueues[aq] = id;
                    }
                }
            }

            #endregion


            #region import source code
            List<string> listImportSourceCodeJsonPaths = new List<string>();
            string importSourceCodePath = GetJsonFilePath(model.IsPrivatePath, model.PrivateTemplatePath, templateUsed, "ImportSourceCode");
            //templatesFolder + templateUsed + @"\ImportSourceCode";
            if (Directory.Exists(importSourceCodePath))
            {
                Directory.GetFiles(importSourceCodePath).ToList().ForEach(i => listImportSourceCodeJsonPaths.Add(i));
                if (listImportSourceCodeJsonPaths.Contains(importSourceCodePath + "\\GitRepository.json"))
                {
                    listImportSourceCodeJsonPaths.Remove(importSourceCodePath + "\\GitRepository.json");
                }
            }
            foreach (string importSourceCode in listImportSourceCodeJsonPaths)
            {
                model.id.AddMessage("Importing source code");
                ImportSourceCode(model, importSourceCode, _repoVersion, model.id, _getSourceCodeVersion);
            }
            if (isDefaultRepoTodetele)
            {
                Repository objRepository = new Repository(_repoVersion);
                string repositoryToDelete = objRepository.GetRepositoryToDelete(model.ProjectName);
                bool isDeleted = objRepository.DeleteRepository(repositoryToDelete);
            }

            #endregion

            //Create Pull request
            Thread.Sleep(10000); //Adding delay to wait for the repository to create and import from the source

            #region pull request

            List<string> listPullRequestJsonPaths = new List<string>();
            string pullRequestFolder = GetJsonFilePath(model.IsPrivatePath, model.PrivateTemplatePath, templateUsed, "PullRequests");
            //templatesFolder + templateUsed + @"\PullRequests";
            if (Directory.Exists(pullRequestFolder))
            {
                Directory.GetFiles(pullRequestFolder).ToList().ForEach(i => listPullRequestJsonPaths.Add(i));
            }
            foreach (string pullReq in listPullRequestJsonPaths)
            {
                CreatePullRequest(model, pullReq, _workItemsVersion);
            }

            #endregion


            #region work items
            Dictionary<string, string> workItems = new();
            string _WitPath = GetJsonFilePath(model.IsPrivatePath, model.PrivateTemplatePath, templateUsed, "WorkItems");
            //Path.Combine(templatesFolder + templateUsed + "\\WorkItems");
            if (Directory.Exists(_WitPath))
            {
                string[] workItemFilePaths = Directory.GetFiles(_WitPath);
                if (workItemFilePaths.Length > 0)
                {
                    foreach (var workItem in workItemFilePaths)
                    {
                        string[] workItemPatSplit = workItem.Split('\\');
                        if (workItemPatSplit.Length > 0)
                        {
                            string workItemName = workItemPatSplit[workItemPatSplit.Length - 1];
                            if (!string.IsNullOrEmpty(workItemName))
                            {
                                string[] nameExtension = workItemName.Split('.');
                                string name = nameExtension[0];
                                if (!workItems.ContainsKey(name))
                                {
                                    workItems.Add(name, model.ReadJsonFile(workItem));
                                }
                            }
                        }
                    }
                }
            }

            ImportWorkItems import = new ImportWorkItems(_workItemsVersion, model.Environment.BoardRowFieldName);
            if (File.Exists(projectSettingsFile))
            {
                string attchmentFilesFolder = GetJsonFilePath(model.IsPrivatePath, model.PrivateTemplatePath, templateUsed, "WorkItemAttachments");
                //string.Format(templatesFolder + @"{0}\WorkItemAttachments", templateUsed);
                if (listPullRequestJsonPaths.Count > 0)
                {
                    if (templateUsed == "MyHealthClinic")
                    {
                        wiMapping = import.ImportWorkitems(workItems, model.ProjectName, model.Environment.UserUniquename, model.ReadJsonFile(projectSettingsFile), attchmentFilesFolder, model.Environment.repositoryIdList.ContainsKey("MyHealthClinic") ? model.Environment.repositoryIdList["MyHealthClinic"] : string.Empty, model.Environment.ProjectId, model.Environment.pullRequests, model.UserMethod, model.accountUsersForWi, templateUsed);
                    }
                    else if (templateUsed == "SmartHotel360")
                    {
                        wiMapping = import.ImportWorkitems(workItems, model.ProjectName, model.Environment.UserUniquename, model.ReadJsonFile(projectSettingsFile), attchmentFilesFolder, model.Environment.repositoryIdList.ContainsKey("PublicWeb") ? model.Environment.repositoryIdList["PublicWeb"] : string.Empty, model.Environment.ProjectId, model.Environment.pullRequests, model.UserMethod, model.accountUsersForWi, templateUsed);
                    }
                    else
                    {
                        wiMapping = import.ImportWorkitems(workItems, model.ProjectName, model.Environment.UserUniquename, model.ReadJsonFile(projectSettingsFile), attchmentFilesFolder, model.Environment.repositoryIdList.ContainsKey(templateUsed) ? model.Environment.repositoryIdList[templateUsed] : string.Empty, model.Environment.ProjectId, model.Environment.pullRequests, model.UserMethod, model.accountUsersForWi, templateUsed);
                    }
                }
                else
                {
                    wiMapping = import.ImportWorkitems(workItems, model.ProjectName, model.Environment.UserUniquename, model.ReadJsonFile(projectSettingsFile), attchmentFilesFolder, string.Empty, model.Environment.ProjectId, model.Environment.pullRequests, model.UserMethod, model.accountUsersForWi, templateUsed);
                }
                model.id.AddMessage("Work Items created");
            }

            #endregion

            //Creat TestPlans and TestSuites
            List<string> listTestPlansJsonPaths = new List<string>();
            string testPlansFolder = GetJsonFilePath(model.IsPrivatePath, model.PrivateTemplatePath, templateUsed, "TestPlans");
            //templatesFolder + templateUsed + @"\TestPlans";
            if (Directory.Exists(testPlansFolder))
            {
                Directory.GetFiles(testPlansFolder).ToList().ForEach(i => listTestPlansJsonPaths.Add(i));
            }
            foreach (string testPlan in listTestPlansJsonPaths)
            {
                CreateTestManagement(wiMapping, model, testPlan, _testPlanVersion);
            }
            if (listTestPlansJsonPaths.Count > 0)
            {
                //model.id.AddMessage( "TestPlans, TestSuites and TestCases created");
            }
            // create varibale groups

            CreateVaribaleGroups(model, _variableGroupApiVersion);
            // create delivery plans
            CreateDeliveryPlans(model, _workItemsVersion);
            //create build Definition
            string buildDefinitionsPath = string.Empty;
            model.BuildDefinitions = new List<BuildDef>();
            // if the template is private && agreed to GitHubFork && GitHub Token is not null
            if (setting.IsPrivate == "true" && model.GitHubFork && !string.IsNullOrEmpty(model.GitHubToken))
            {
                buildDefinitionsPath = GetJsonFilePath(model.IsPrivatePath, model.PrivateTemplatePath, templateUsed, "BuildDefinitions");
                if (Directory.Exists(buildDefinitionsPath))
                {
                    Directory.GetFiles(buildDefinitionsPath, "*.json", SearchOption.AllDirectories).ToList().ForEach(i => model.BuildDefinitions.Add(new BuildDef() { FilePath = i }));
                }
                buildDefinitionsPath = GetJsonFilePath(model.IsPrivatePath, model.PrivateTemplatePath, templateUsed, "BuildDefinitionGitHub");
                if (Directory.Exists(buildDefinitionsPath))
                {
                    Directory.GetFiles(buildDefinitionsPath, "*.json", SearchOption.AllDirectories).ToList().ForEach(i => model.BuildDefinitions.Add(new BuildDef() { FilePath = i }));
                }
            }
            // if the template is private && not agreed to GitHubFork && GitHub Token is null
            else if (setting.IsPrivate == "true" && !model.GitHubFork && string.IsNullOrEmpty(model.GitHubToken))
            {
                buildDefinitionsPath = GetJsonFilePath(model.IsPrivatePath, model.PrivateTemplatePath, templateUsed, "BuildDefinitions");
                if (Directory.Exists(buildDefinitionsPath))
                {
                    Directory.GetFiles(buildDefinitionsPath, "*.json", SearchOption.AllDirectories).ToList().ForEach(i => model.BuildDefinitions.Add(new BuildDef() { FilePath = i }));
                }
            }
            // if the template is not private && agreed to GitHubFork && GitHub Token is not null
            else if (string.IsNullOrEmpty(setting.IsPrivate) && model.GitHubFork && !string.IsNullOrEmpty(model.GitHubToken))
            {
                buildDefinitionsPath = GetJsonFilePath(model.IsPrivatePath, model.PrivateTemplatePath, templateUsed, "BuildDefinitionGitHub");
                if (Directory.Exists(buildDefinitionsPath))
                {
                    Directory.GetFiles(buildDefinitionsPath, "*.json", SearchOption.AllDirectories).ToList().ForEach(i => model.BuildDefinitions.Add(new BuildDef() { FilePath = i }));
                }
            }
            // if the template is not private && not agreed to GitHubFork && GitHub Token is null
            else if (string.IsNullOrEmpty(setting.IsPrivate) && !model.GitHubFork && string.IsNullOrEmpty(model.GitHubToken))
            {
                buildDefinitionsPath = GetJsonFilePath(model.IsPrivatePath, model.PrivateTemplatePath, templateUsed, "BuildDefinitions");
                if (Directory.Exists(buildDefinitionsPath))
                {
                    Directory.GetFiles(buildDefinitionsPath, "*.json", SearchOption.AllDirectories).ToList().ForEach(i => model.BuildDefinitions.Add(new BuildDef() { FilePath = i }));
                }
            }
            bool isBuild = CreateBuildDefinition(model, _buildVersion, model.id);
            if (isBuild)
            {
                model.id.AddMessage("Build definition created");
            }

            //Queue a Build
            string buildJson = GetJsonFilePath(model.IsPrivatePath, model.PrivateTemplatePath, templateUsed, "QueueBuild.json");
            //string.Format(templatesFolder + @"{0}\QueueBuild.json", templateUsed);
            if (File.Exists(buildJson))
            {
                QueueABuild(model, buildJson, _buildVersion);
            }

            //create release Definition
            string releaseDefinitionsPath = GetJsonFilePath(model.IsPrivatePath, model.PrivateTemplatePath, templateUsed, "ReleaseDefinitions");
            //templatesFolder + templateUsed + @"\ReleaseDefinitions";
            model.ReleaseDefinitions = new List<ReleaseDef>();
            if (Directory.Exists(releaseDefinitionsPath))
            {
                Directory.GetFiles(releaseDefinitionsPath, "*.json", SearchOption.AllDirectories).ToList().ForEach(i => model.ReleaseDefinitions.Add(new Models.ReleaseDef() { FilePath = i }));
            }
            bool isReleased = CreateReleaseDefinition(model, _releaseVersion, model.id, teamMembers);
            if (isReleased)
            {
                model.id.AddMessage("Release definition created");
            }

            //Create Branch Policy
            bool isBuildPolicyCreated = CreateBranchPolicy(model, _buildVersion);
            if (isBuildPolicyCreated)
            {
                model.id.AddMessage("Branch Policy created");
            }

            //Create query and widgets
            List<string> listDashboardQueriesPath = new List<string>();
            string dashboardPath = GetJsonFilePath(model.IsPrivatePath, model.PrivateTemplatePath, templateUsed, "Dashboard");
            List<string> dashboardDirectories = new List<string>();
            if (Directory.Exists(dashboardPath))
            {
                dashboardDirectories = Directory.GetDirectories(dashboardPath).ToList();
            }
            teamName = string.Empty;
            if (dashboardDirectories.Count > 0)
            {
                foreach (string dashboardDirectory in dashboardDirectories)
                {
                    if (Path.GetFileName(dashboardDirectory) == "Queries")
                    {
                        teamName = null;
                    }
                    else
                    {
                        teamName = Path.GetFileName(dashboardDirectory);
                    }
                    string dashboardQueryPath = string.Empty;
                    dashboardQueryPath = GetJsonFilePath(model.IsPrivatePath, model.PrivateTemplatePath, templateUsed, "Dashboard\\Queries");
                    if (teamName != null)
                    {
                        dashboardQueryPath = GetJsonFilePath(model.IsPrivatePath, model.PrivateTemplatePath, templateUsed, $"\\Dashboard\\{teamName}\\Queries");
                    }
                    if (Directory.Exists(dashboardQueryPath))
                    {
                        listDashboardQueriesPath = Directory.GetFiles(dashboardQueryPath).ToList();
                        if (listDashboardQueriesPath.Count > 0)
                        {
                            CreateQueryAndWidgets(model, listDashboardQueriesPath, _queriesVersion, _dashboardVersion, _releaseVersion, _projectCreationVersion, _boardVersion, teamName);
                        }
                    }
                }
                model.id.AddMessage("Queries, Widgets and Charts created");
            }
            return true;
        }


        bool CreateBranchPolicy(Project model, ADOConfiguration buildConfig)
        {
            bool isBranchPolicyCreated = false;
            try
            {
                string branchPolicyPath = GetJsonFilePath(model.IsPrivatePath, model.PrivateTemplatePath, templateUsed, "BranchPolicy");
                List<string> branchPolicyPaths = new List<string>();
                if (Directory.Exists(branchPolicyPath))
                {
                    Directory.GetFiles(branchPolicyPath, "*.json", SearchOption.AllDirectories).ToList().ForEach(i => branchPolicyPaths.Add(i));
                }
                BuildandReleaseDefs objBuild = new BuildandReleaseDefs(buildConfig);
                List<JObject> buildDefsList = objBuild.ExportBuildDefinitions();
                if (buildDefsList != null && buildDefsList.Count > 0)
                {
                    int buildDefId = 0;
                    foreach (JObject buildDef in buildDefsList)
                    {
                        var yamalfilename = buildDef["process"]["yamlFilename"];
                        if (yamalfilename != null && !string.IsNullOrEmpty(yamalfilename.ToString()))
                        {
                            buildDefId = Convert.ToInt32(buildDef["id"]);
                        }
                    }
                    BranchPolicyTypes.PolicyTypes policyTypes = objBuild.GetPolicyTypes();
                    if (policyTypes != null)
                    {
                        if (branchPolicyPaths.Count > 0)
                        {
                            foreach (string branchPolicyJsonPath in branchPolicyPaths)
                            {
                                string policyJson = File.ReadAllText(branchPolicyJsonPath);
                                if (!string.IsNullOrEmpty(policyJson))
                                {
                                    BranchPolicy.Policy branchPolicy = JsonConvert.DeserializeObject<BranchPolicy.Policy>(policyJson);
                                    if (branchPolicy != null)
                                    {
                                        string policyTypeId = policyTypes.value.Where(x => x.displayName == branchPolicy.type.displayName).Select(x => x.id).FirstOrDefault();
                                        string policyUrl = policyTypes.value.Where(x => x.displayName == branchPolicy.type.displayName).Select(x => x.url).FirstOrDefault();
                                        policyJson = policyJson.Replace("$policyTypeId$", policyTypeId).Replace("$policyTypeUrl$", policyUrl);
                                        foreach (string repository in model.Environment.repositoryIdList.Keys)
                                        {
                                            string placeHolder = string.Format("${0}$", repository.ToLower());
                                            policyJson = policyJson.Replace(placeHolder, model.Environment.repositoryIdList[repository]);
                                        }
                                        policyJson = policyJson.Replace("$buildDefId$", buildDefId.ToString());
                                        bool isBuildPolicyCreated = objBuild.CreateBranchPolicy(policyJson, model.ProjectName);
                                    }
                                }
                            }
                            isBranchPolicyCreated = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // logger.Info(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + "\t" + ex.Message + "\t" + "\n" + ex.StackTrace + "\n");
                model.id.ErrorId().AddMessage("Error while creating branch policy : " + ex.Message);
            }
            return isBranchPolicyCreated;
        }

        void ForkGitHubRepository(Project model, ADOConfiguration _gitHubConfig)
        {
            try
            {
                List<string> listRepoFiles = new List<string>();
                string repoFilePath = GetJsonFilePath(model.IsPrivatePath, model.PrivateTemplatePath, model.SelectedTemplate, "ImportSourceCode\\GitRepository.json");
                if (File.Exists(repoFilePath))
                {
                    string readRepoFile = model.ReadJsonFile(repoFilePath);
                    if (!string.IsNullOrEmpty(readRepoFile))
                    {
                        ForkRepos.Fork forkRepos = new ForkRepos.Fork();
                        forkRepos = JsonConvert.DeserializeObject<ForkRepos.Fork>(readRepoFile);
                        if (forkRepos.repositories != null && forkRepos.repositories.Count > 0)
                        {
                            foreach (var repo in forkRepos.repositories)
                            {
                                GitHubImportRepo user = new GitHubImportRepo(_gitHubConfig);
                                GitHubUserDetail userDetail = new GitHubUserDetail();
                                GitHubRepoResponse.RepoCreated GitHubRepo = new GitHubRepoResponse.RepoCreated();
                                //HttpResponseMessage listForks = user.ListForks(repo.fullName);
                                HttpResponseMessage forkResponse = user.ForkRepo(repo.fullName);
                                if (forkResponse.IsSuccessStatusCode)
                                {
                                    string forkedRepo = forkResponse.Content.ReadAsStringAsync().Result;
                                    dynamic fr = JsonConvert.DeserializeObject<dynamic>(forkedRepo);
                                    model.GitRepoName = fr.name;
                                    model.GitRepoURL = fr.html_url;
                                    if (!model.Environment.gitHubRepos.ContainsKey(model.GitRepoName))
                                    {
                                        model.Environment.gitHubRepos.Add(model.GitRepoName, model.GitRepoURL);
                                    }
                                    model.id.AddMessage(string.Format("Forked {0} repository to {1} user", model.GitRepoName, _gitHubConfig.userName));
                                }
                                else
                                {
                                    model.id.ErrorId().AddMessage("Error while forking the repository: " + forkResponse.Content.ReadAsStringAsync().Result);
                                    // logger.Info(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + "\t" + "Error while forking the repository: " + forkResponse.Content.ReadAsStringAsync().Result + "\n");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // logger.Info(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + "\t" + ex.Message + "\t" + "\n" + ex.StackTrace + "\n");
                model.id.ErrorId().AddMessage("Error while forking repository :" + ex.Message);
            }
        }

        /// <summary>
        /// Create Teams
        /// </summary>
        /// <param name="model"></param>
        /// <param name="teamsJSON"></param>
        /// <param name="_defaultADOConfiguration"></param>
        /// <param name="id"></param>
        /// <param name="teamAreaJSON"></param>
        void CreateTeams1(Project model, string teamsJSON, ADOConfiguration _projectConfig, string id, string teamAreaJSON)
        {
            try
            {
                string jsonTeams = GetJsonFilePath(model.IsPrivatePath, model.PrivateTemplatePath, model.SelectedTemplate, teamsJSON);
                if (File.Exists(jsonTeams))
                {
                    Teams objTeam = new Teams(_projectConfig);
                    jsonTeams = model.ReadJsonFile(jsonTeams);
                    JArray jTeams = JsonConvert.DeserializeObject<JArray>(jsonTeams);
                    JContainer teamsParsed = JsonConvert.DeserializeObject<JContainer>(jsonTeams);

                    //get Backlog Iteration Id
                    string backlogIteration = objTeam.GetTeamSetting(model.ProjectName);
                    //get all Iterations
                    TeamIterationsResponse.Iterations iterations = objTeam.GetAllIterations(model.ProjectName);

                    foreach (var jTeam in jTeams)
                    {
                        string teamIterationMap = GetJsonFilePath(model.IsPrivatePath, model.PrivateTemplatePath, model.SelectedTemplate, "TeamIterationMap.json");
                        if (File.Exists(teamIterationMap))
                        {
                            //BEGIN - Mapping only given iterations for team in Team Iteration Mapping file
                            if (!string.IsNullOrEmpty(teamIterationMap))
                            {
                                string data = model.ReadJsonFile(teamIterationMap);
                                TeamIterations.Map iterationMap = new TeamIterations.Map();
                                iterationMap = JsonConvert.DeserializeObject<TeamIterations.Map>(data);
                                if (iterationMap.TeamIterationMap.Count > 0)
                                {
                                    foreach (var teamMap in iterationMap.TeamIterationMap)
                                    {
                                        if (teamMap.TeamName.ToLower() == jTeam["name"].ToString().ToLower())
                                        {
                                            // AS IS

                                            GetTeamResponse.Team teamResponse = objTeam.CreateNewTeam(jTeam.ToString(), model.ProjectName);
                                            if (!(string.IsNullOrEmpty(teamResponse.id)))
                                            {
                                                string areaName = objTeam.CreateArea(model.ProjectName, teamResponse.name);
                                                string updateAreaJSON = GetJsonFilePath(model.IsPrivatePath, model.PrivateTemplatePath, model.SelectedTemplate, teamAreaJSON);

                                                //updateAreaJSON = string.Format(templatesFolder + @"{0}\{1}", model.SelectedTemplate, teamAreaJSON);

                                                if (File.Exists(updateAreaJSON))
                                                {
                                                    updateAreaJSON = model.ReadJsonFile(updateAreaJSON);
                                                    updateAreaJSON = updateAreaJSON.Replace("$ProjectName$", model.ProjectName).Replace("$AreaName$", areaName);
                                                    bool isUpdated = objTeam.SetAreaForTeams(model.ProjectName, teamResponse.name, updateAreaJSON);
                                                }
                                                bool isBackLogIterationUpdated = objTeam.SetBackLogIterationForTeam(backlogIteration, model.ProjectName, teamResponse.name);
                                                if (iterations.count > 0)
                                                {
                                                    foreach (var iteration in iterations.value)
                                                    {
                                                        if (iteration.structureType == "iteration")
                                                        {
                                                            foreach (var child in iteration.children)
                                                            {
                                                                if (teamMap.Iterations.Contains(child.name))
                                                                {
                                                                    bool isIterationUpdated = objTeam.SetIterationsForTeam(child.identifier, teamResponse.name, model.ProjectName);
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }

                                            // TILL HERE
                                        }
                                    }
                                }
                            }
                            // END
                        }
                        else
                        {
                            string isDefault = jTeam["isDefault"] != null ? jTeam["isDefault"].ToString() : string.Empty;
                            if (isDefault == "false" || isDefault == "")
                            {
                                GetTeamResponse.Team teamResponse = objTeam.CreateNewTeam(jTeam.ToString(), model.ProjectName);
                                if (!(string.IsNullOrEmpty(teamResponse.id)))
                                {
                                    string areaName = objTeam.CreateArea(model.ProjectName, teamResponse.name);
                                    string updateAreaJSON = GetJsonFilePath(model.IsPrivatePath, model.PrivateTemplatePath, model.SelectedTemplate, teamAreaJSON);

                                    //updateAreaJSON = string.Format(templatesFolder + @"{0}\{1}", model.SelectedTemplate, teamAreaJSON);

                                    if (File.Exists(updateAreaJSON))
                                    {
                                        updateAreaJSON = model.ReadJsonFile(updateAreaJSON);
                                        updateAreaJSON = updateAreaJSON.Replace("$ProjectName$", model.ProjectName).Replace("$AreaName$", areaName);
                                        bool isUpdated = objTeam.SetAreaForTeams(model.ProjectName, teamResponse.name, updateAreaJSON);
                                    }
                                    bool isBackLogIterationUpdated = objTeam.SetBackLogIterationForTeam(backlogIteration, model.ProjectName, teamResponse.name);
                                    if (iterations.count > 0)
                                    {
                                        foreach (var iteration in iterations.value)
                                        {
                                            if (iteration.structureType == "iteration")
                                            {
                                                foreach (var child in iteration.children)
                                                {
                                                    bool isIterationUpdated = objTeam.SetIterationsForTeam(child.identifier, teamResponse.name, model.ProjectName);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            if (!(string.IsNullOrEmpty(objTeam.LastFailureMessage)))
                            {
                                id.ErrorId().AddMessage("Error while creating teams: " + objTeam.LastFailureMessage + Environment.NewLine);
                            }
                            else
                            {
                                id.AddMessage(string.Format("{0} team(s) created", teamsParsed.Count));
                            }
                            if (model.SelectedTemplate.ToLower() == "smarthotel360")
                            {
                                string updateAreaJSON = GetJsonFilePath(model.IsPrivatePath, model.PrivateTemplatePath, model.SelectedTemplate, "UpdateTeamArea.json");

                                //updateAreaJSON = string.Format(templatesFolder + @"{0}\{1}", model.SelectedTemplate, "UpdateTeamArea.json");
                                if (File.Exists(updateAreaJSON))
                                {
                                    updateAreaJSON = model.ReadJsonFile(updateAreaJSON);
                                    updateAreaJSON = updateAreaJSON.Replace("$ProjectName$", model.ProjectName);
                                    bool isUpdated = objTeam.UpdateTeamsAreas(model.ProjectName, updateAreaJSON);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // logger.Info(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + "\t" + ex.Message + "\t" + "\n" + ex.StackTrace + "\n");
                id.ErrorId().AddMessage("Error while creating teams: " + ex.Message);

            }
        }
        void CreateTeams(Project model, string teamsJSON, ADOConfiguration _projectConfig, string id, string teamAreaJSON)
        {
            try
            {
                string jsonTeams = GetJsonFilePath(model.IsPrivatePath, model.PrivateTemplatePath, model.SelectedTemplate, teamsJSON);
                if (!File.Exists(jsonTeams)) return;

                Teams objTeam = new Teams(_projectConfig);
                jsonTeams = model.ReadJsonFile(jsonTeams);
                JArray jTeams = JsonConvert.DeserializeObject<JArray>(jsonTeams);
                JContainer teamsParsed = JsonConvert.DeserializeObject<JContainer>(jsonTeams);

                string backlogIteration = objTeam.GetTeamSetting(model.ProjectName);
                TeamIterationsResponse.Iterations iterations = objTeam.GetAllIterations(model.ProjectName);

                foreach (var jTeam in jTeams)
                {
                    string teamIterationMap = GetJsonFilePath(model.IsPrivatePath, model.PrivateTemplatePath, model.SelectedTemplate, "TeamIterationMap.json");
                    if (File.Exists(teamIterationMap))
                    {
                        MapTeamIterations(model, objTeam, jTeam, teamIterationMap, backlogIteration, iterations, teamAreaJSON);
                    }
                    else
                    {
                        CreateDefaultTeam(model, objTeam, jTeam, backlogIteration, iterations, teamAreaJSON, teamsParsed, id);
                    }
                }
            }
            catch (Exception ex)
            {
                id.ErrorId().AddMessage("Error while creating teams: " + ex.Message);
            }
        }

        private void MapTeamIterations(Project model, Teams objTeam, JToken jTeam, string teamIterationMap, string backlogIteration, TeamIterationsResponse.Iterations iterations, string teamAreaJSON)
        {
            string data = model.ReadJsonFile(teamIterationMap);
            TeamIterations.Map iterationMap = JsonConvert.DeserializeObject<TeamIterations.Map>(data);

            foreach (var teamMap in iterationMap.TeamIterationMap)
            {
                if (teamMap.TeamName.ToLower() != jTeam["name"].ToString().ToLower()) continue;

                GetTeamResponse.Team teamResponse = objTeam.CreateNewTeam(jTeam.ToString(), model.ProjectName);
                if (string.IsNullOrEmpty(teamResponse.id)) continue;

                string areaName = objTeam.CreateArea(model.ProjectName, teamResponse.name);
                string updateAreaJSON = GetJsonFilePath(model.IsPrivatePath, model.PrivateTemplatePath, model.SelectedTemplate, teamAreaJSON);

                if (File.Exists(updateAreaJSON))
                {
                    updateAreaJSON = model.ReadJsonFile(updateAreaJSON);
                    updateAreaJSON = updateAreaJSON.Replace("$ProjectName$", model.ProjectName).Replace("$AreaName$", areaName);
                    objTeam.SetAreaForTeams(model.ProjectName, teamResponse.name, updateAreaJSON);
                }

                objTeam.SetBackLogIterationForTeam(backlogIteration, model.ProjectName, teamResponse.name);

                foreach (var iteration in iterations.value)
                {
                    if (iteration.structureType != "iteration") continue;

                    foreach (var child in iteration.children)
                    {
                        if (teamMap.Iterations.Contains(child.name))
                        {
                            objTeam.SetIterationsForTeam(child.identifier, teamResponse.name, model.ProjectName);
                        }
                    }
                }
            }
        }

        private void CreateDefaultTeam(Project model, Teams objTeam, JToken jTeam, string backlogIteration, TeamIterationsResponse.Iterations iterations, string teamAreaJSON, JContainer teamsParsed, string id)
        {

            string isDefault = jTeam["isDefault"]?.ToString() ?? string.Empty;
            if (isDefault == "false" || isDefault == "")
            {
                GetTeamResponse.Team teamResponse = objTeam.CreateNewTeam(jTeam.ToString(), model.ProjectName);
                if (!string.IsNullOrEmpty(teamResponse.id))
                {
                    model.id.AddMessage(string.Format("{0} team created", teamResponse.name));
                }
                string areaName = objTeam.CreateArea(model.ProjectName, teamResponse.name);
                if (!string.IsNullOrEmpty(areaName))
                {
                    model.id.AddMessage(string.Format("{0} team area created", areaName));
                }
                string updateAreaJSON = GetJsonFilePath(model.IsPrivatePath, model.PrivateTemplatePath, model.SelectedTemplate, teamAreaJSON);

                if (File.Exists(updateAreaJSON))
                {
                    updateAreaJSON = model.ReadJsonFile(updateAreaJSON);
                    updateAreaJSON = updateAreaJSON.Replace("$ProjectName$", model.ProjectName).Replace("$AreaName$", areaName);
                    objTeam.SetAreaForTeams(model.ProjectName, teamResponse.name, updateAreaJSON);
                }

                objTeam.SetBackLogIterationForTeam(backlogIteration, model.ProjectName, teamResponse.name);

                foreach (var iteration in iterations.value)
                {
                    if (iteration.structureType != "iteration") continue;

                    foreach (var child in iteration.children)
                    {
                        objTeam.SetIterationsForTeam(child.identifier, teamResponse.name, model.ProjectName);
                    }
                }
            }

            if (!string.IsNullOrEmpty(objTeam.LastFailureMessage))
            {
                id.ErrorId().AddMessage("Error while creating teams: " + objTeam.LastFailureMessage + Environment.NewLine);
            }
            if (model.SelectedTemplate.ToLower() == "smarthotel360")
            {
                string updateAreaJSON = GetJsonFilePath(model.IsPrivatePath, model.PrivateTemplatePath, model.SelectedTemplate, "UpdateTeamArea.json");

                if (File.Exists(updateAreaJSON))
                {
                    updateAreaJSON = model.ReadJsonFile(updateAreaJSON);
                    updateAreaJSON = updateAreaJSON.Replace("$ProjectName$", model.ProjectName);
                    objTeam.UpdateTeamsAreas(model.ProjectName, updateAreaJSON);
                }
            }
        }


        /// <summary>
        /// Get Team members
        /// </summary>
        /// <param name="projectName"></param>
        /// <param name="teamName"></param>
        /// <param name="_ADOConfiguration"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        TeamMemberResponse.TeamMembers GetTeamMembers(string projectName, string teamName, ADOConfiguration _configuration, string id)
        {
            try
            {
                TeamMemberResponse.TeamMembers viewModel = new TeamMemberResponse.TeamMembers();
                RestAPI.ProjectsAndTeams.Teams objTeam = new RestAPI.ProjectsAndTeams.Teams(_configuration);
                viewModel = objTeam.GetTeamMembers(projectName, teamName);

                if (!(string.IsNullOrEmpty(objTeam.LastFailureMessage)))
                {
                    id.ErrorId().AddMessage("Error while getting team members: " + objTeam.LastFailureMessage + Environment.NewLine);
                }
                return viewModel;
            }
            catch (Exception ex)
            {
                // logger.Info(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + "\t" + ex.Message + "\t" + "\n" + ex.StackTrace + "\n");
                id.ErrorId().AddMessage("Error while getting team members: " + ex.Message);
            }

            return new TeamMemberResponse.TeamMembers();
        }

        /// <summary>
        /// Create Work Items
        /// </summary>
        /// <param name="model"></param>
        /// <param name="workItemJSON"></param>
        /// <param name="_defaultConfiguration"></param>
        /// <param name="id"></param>
        void CreateWorkItems(Project model, string workItemJSON, ADOConfiguration _defaultConfiguration, string id)
        {
            try
            {
                string jsonWorkItems = GetJsonFilePath(model.IsPrivatePath, model.PrivateTemplatePath, model.SelectedTemplate, workItemJSON);
                //string.Format(templatesFolder + @"{0}\{1}", model.SelectedTemplate, workItemJSON);
                if (File.Exists(jsonWorkItems))
                {
                    WorkItem objWorkItem = new WorkItem(_defaultConfiguration);
                    jsonWorkItems = model.ReadJsonFile(jsonWorkItems);
                    JContainer workItemsParsed = JsonConvert.DeserializeObject<JContainer>(jsonWorkItems);

                    id.AddMessage("Creating " + workItemsParsed.Count + " work items...");

                    jsonWorkItems = jsonWorkItems.Replace("$version$", _defaultConfiguration.VersionNumber);
                    bool workItemResult = objWorkItem.CreateWorkItemUsingByPassRules(model.ProjectName, jsonWorkItems);

                    if (!(string.IsNullOrEmpty(objWorkItem.LastFailureMessage)))
                    {
                        id.ErrorId().AddMessage("Error while creating workitems: " + objWorkItem.LastFailureMessage + Environment.NewLine);
                    }
                }

            }
            catch (Exception ex)
            {
                // logger.Info(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + "\t" + ex.Message + "\t" + "\n" + ex.StackTrace + "\n");
                id.ErrorId().AddMessage("Error while creating workitems: " + ex.Message);

            }
        }

        /// <summary>
        /// Update Board Columns styles
        /// </summary>
        /// <param name="model"></param>
        /// <param name="BoardColumnsJSON"></param>
        /// <param name="_defaultConfiguration"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        bool UpdateBoardColumn(Project model, string BoardColumnsJSON, ADOConfiguration _BoardConfig, string id, string BoardType, string team)
        {
            bool result = false;
            try
            {
                BoardColumn objBoard = new BoardColumn(_BoardConfig);
                bool boardColumnResult = objBoard.UpdateBoard(model.ProjectName, BoardColumnsJSON, BoardType, team);
                if (boardColumnResult)
                {
                    model.Environment.BoardRowFieldName = objBoard.rowFieldName;
                    result = true;
                }
                else if (!(string.IsNullOrEmpty(objBoard.LastFailureMessage)))
                {
                    id.ErrorId().AddMessage("Error while updating board column " + objBoard.LastFailureMessage + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                // logger.Info(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + "\t" + ex.Message + "\t" + "\n" + ex.StackTrace + "\n");
                id.ErrorId().AddMessage("Error while updating board column " + ex.Message);
            }
            return result;
        }

        /// <summary>
        /// Updates Card Fields
        /// </summary>
        /// <param name="model"></param>
        /// <param name="json"></param>
        /// <param name="_configuration"></param>
        /// <param name="id"></param>
        void UpdateCardFields(Project model, string json, ADOConfiguration _configuration, string id, string boardType, string team)
        {
            try
            {
                json = json.Replace("null", "\"\"");
                Cards objCards = new Cards(_configuration);
                objCards.UpdateCardField(model.ProjectName, json, boardType, team);

                if (!string.IsNullOrEmpty(objCards.LastFailureMessage))
                {
                    id.ErrorId().AddMessage("Error while updating card fields: " + objCards.LastFailureMessage + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                // logger.Info(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + "\t" + ex.Message + "\t" + "\n" + ex.StackTrace + "\n");
                id.ErrorId().AddMessage("Error while updating card fields: " + ex.Message);

            }

        }

        /// <summary>
        /// Udpate Card Styles
        /// </summary>
        /// <param name="model"></param>
        /// <param name="json"></param>
        /// <param name="_configuration"></param>
        /// <param name="id"></param>
        void UpdateCardStyles(Project model, string json, ADOConfiguration _configuration, string id, string boardType, string team)
        {
            try
            {
                Cards objCards = new Cards(_configuration);
                objCards.ApplyRules(model.ProjectName, json, boardType, team);

                if (!string.IsNullOrEmpty(objCards.LastFailureMessage))
                {
                    id.ErrorId().AddMessage("Error while updating card styles: " + objCards.LastFailureMessage + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                // logger.Info(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + "\t" + ex.Message + "\t" + "\n" + ex.StackTrace + "\n");
                id.ErrorId().AddMessage("Error while updating card styles: " + ex.Message);
            }

        }

        /// <summary>
        /// Enable Epic
        /// </summary>
        /// <param name="model"></param>
        /// <param name="json"></param>
        /// <param name="_config3_0"></param>
        /// <param name="id"></param>
        void EnableEpic(Project model, string json, ADOConfiguration _boardVersion, string id, string team)
        {
            try
            {
                Cards objCards = new Cards(_boardVersion);
                Projects project = new Projects(_boardVersion);
                objCards.EnablingEpic(model.ProjectName, json, model.ProjectName, team);

                if (!string.IsNullOrEmpty(objCards.LastFailureMessage))
                {
                    id.ErrorId().AddMessage("Error while Setting Epic Settings: " + objCards.LastFailureMessage + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                // logger.Info(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + "\t" + ex.Message + "\t" + "\n" + ex.StackTrace + "\n");
                id.ErrorId().AddMessage("Error while Setting Epic Settings: " + ex.Message);
            }

        }

        /// <summary>
        /// Updates work items with parent child links
        /// </summary>
        /// <param name="model"></param>
        /// <param name="workItemUpdateJSON"></param>
        /// <param name="_defaultConfiguration"></param>
        /// <param name="id"></param>
        /// <param name="currentUser"></param>
        /// <param name="projectSettingsJSON"></param>
        void UpdateWorkItems(Project model, string workItemUpdateJSON, ADOConfiguration _defaultConfiguration, string id, string currentUser, string projectSettingsJSON)
        {
            try
            {
                string jsonWorkItemsUpdate = GetJsonFilePath(model.IsPrivatePath, model.PrivateTemplatePath, model.SelectedTemplate, workItemUpdateJSON);
                //string.Format(templatesFolder + @"{0}\{1}", model.SelectedTemplate, workItemUpdateJSON);
                string jsonProjectSettings = GetJsonFilePath(model.IsPrivatePath, model.PrivateTemplatePath, model.SelectedTemplate, projectSettingsJSON);
                //string.Format(templatesFolder + @"{0}\{1}", model.SelectedTemplate, projectSettingsJSON);
                if (File.Exists(jsonWorkItemsUpdate))
                {
                    WorkItem objWorkItem = new WorkItem(_defaultConfiguration);
                    jsonWorkItemsUpdate = model.ReadJsonFile(jsonWorkItemsUpdate);
                    jsonProjectSettings = model.ReadJsonFile(jsonProjectSettings);

                    bool workItemUpdateResult = objWorkItem.UpdateWorkItemUsingByPassRules(jsonWorkItemsUpdate, model.ProjectName, currentUser, jsonProjectSettings);
                    if (!(string.IsNullOrEmpty(objWorkItem.LastFailureMessage)))
                    {
                        id.ErrorId().AddMessage("Error while updating work items: " + objWorkItem.LastFailureMessage + Environment.NewLine);
                    }
                }
            }
            catch (Exception ex)
            {
                // logger.Info(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + "\t" + ex.Message + "\t" + "\n" + ex.StackTrace + "\n");

                id.ErrorId().AddMessage("Error while updating work items: " + ex.Message);

            }
        }
        string path = string.Empty;

        /// <summary>
        /// Update Iterations
        /// </summary>
        /// <param name="model"></param>
        /// <param name="_defaultConfiguration"></param>
        /// <param name="iterationsJSON"></param>
        void UpdateIterations(Project model, ADOConfiguration _boardConfig, string iterationsJSON)
        {
            try
            {
                string jsonIterations = GetJsonFilePath(model.IsPrivatePath, model.PrivateTemplatePath, model.SelectedTemplate, iterationsJSON);
                //string.Format(templatesFolder + @"{0}\{1}", model.SelectedTemplate, iterationsJSON);
                if (File.Exists(jsonIterations))
                {
                    iterationsJSON = model.ReadJsonFile(jsonIterations);
                    RestAPI.WorkItemAndTracking.ClassificationNodes objClassification = new RestAPI.WorkItemAndTracking.ClassificationNodes(_boardConfig);

                    GetNodesResponse.Nodes nodes = objClassification.GetIterations(model.ProjectName);

                    GetNodesResponse.Nodes projectNode = JsonConvert.DeserializeObject<GetNodesResponse.Nodes>(iterationsJSON);

                    if (projectNode.hasChildren)
                    {
                        foreach (var child in projectNode.children)
                        {
                            CreateIterationNode(model, objClassification, child, nodes);
                        }
                    }

                    if (projectNode.hasChildren)
                    {
                        foreach (var child in projectNode.children)
                        {
                            path = string.Empty;
                            MoveIterationNode(model, objClassification, child);
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                // logger.Info(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + "\t" + ex.Message + "\t" + "\n" + ex.StackTrace + "\n");

                model.id.ErrorId().AddMessage("Error while updating iteration: " + ex.Message);
            }
        }

        /// <summary>
        /// Create Iterations
        /// </summary>
        /// <param name="model"></param>
        /// <param name="objClassification"></param>
        /// <param name="child"></param>
        /// <param name="currentIterations"></param>
        void CreateIterationNode(Project model, RestAPI.WorkItemAndTracking.ClassificationNodes objClassification, GetNodesResponse.Child child, GetNodesResponse.Nodes currentIterations)
        {
            string[] defaultSprints = new string[] { "Sprint 1", "Sprint 2", "Sprint 3", "Sprint 4", "Sprint 5", "Sprint 6", };
            if (defaultSprints.Contains(child.name))
            {
                var nd = (currentIterations.hasChildren) ? currentIterations.children.FirstOrDefault(i => i.name == child.name) : null;
                if (nd != null)
                {
                    child.id = nd.id;
                }
                else
                {
                    var node = objClassification.CreateIteration(model.ProjectName, child.name);
                    child.id = node.id;
                }
            }
            else
            {
                var node = objClassification.CreateIteration(model.ProjectName, child.name);
                child.id = node.id;
            }

            if (child.hasChildren && child.children != null)
            {
                foreach (var c in child.children)
                {
                    CreateIterationNode(model, objClassification, c, currentIterations);
                }
            }
        }

        /// <summary>
        /// Move Iterations to nodes
        /// </summary>
        /// <param name="model"></param>
        /// <param name="objClassification"></param>
        /// <param name="child"></param>
        void MoveIterationNode(Project model, RestAPI.WorkItemAndTracking.ClassificationNodes objClassification, GetNodesResponse.Child child)
        {
            if (child.hasChildren && child.children != null)
            {
                foreach (var c in child.children)
                {
                    path += child.name + "\\";
                    var nd = objClassification.MoveIteration(model.ProjectName, path, c.id);

                    if (c.hasChildren)
                    {
                        MoveIterationNode(model, objClassification, c);
                    }
                }
            }
        }

        /// <summary>
        /// Udpate Sprints dates
        /// </summary>
        /// <param name="model"></param>
        /// <param name="_defaultConfiguration"></param>
        /// <param name="settings"></param>
        void UpdateSprintItems(Project model, ADOConfiguration _boardConfig, ProjectSettings settings)
        {
            try
            {
                if (settings.type.ToLower() == "scrum" || settings.type.ToLower() == "agile" || settings.type.ToLower() == "basic")
                {
                    string teamIterationMap = GetJsonFilePath(model.IsPrivatePath, model.PrivateTemplatePath, model.SelectedTemplate, "TeamIterationMap.json");

                    RestAPI.WorkItemAndTracking.ClassificationNodes objClassification = new RestAPI.WorkItemAndTracking.ClassificationNodes(_boardConfig);
                    bool classificationNodesResult = objClassification.UpdateIterationDates(model.ProjectName, settings.type, model.SelectedTemplate, teamIterationMap);

                    if (!(string.IsNullOrEmpty(objClassification.LastFailureMessage)))
                    {
                        model.id.ErrorId().AddMessage("Error while updating sprint items: " + objClassification.LastFailureMessage + Environment.NewLine);
                    }
                }
            }
            catch (Exception ex)
            {
                // logger.Info(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + "\t" + ex.Message + "\t" + "\n" + ex.StackTrace + "\n");
                model.id.ErrorId().AddMessage("Error while updating sprint items: " + ex.Message);

            }
        }

        /// <summary>
        /// Rename Iterations
        /// </summary>
        /// <param name="model"></param>
        /// <param name="_defaultConfiguration"></param>
        /// <param name="renameIterations"></param>
        void RenameIterations(Project model, ADOConfiguration _defaultConfiguration, Dictionary<string, string> renameIterations)
        {
            try
            {
                if (renameIterations != null && renameIterations.Count > 0)
                {
                    RestAPI.WorkItemAndTracking.ClassificationNodes objClassification = new RestAPI.WorkItemAndTracking.ClassificationNodes(_defaultConfiguration);
                    bool IsRenamed = objClassification.RenameIteration(model.ProjectName, renameIterations);
                }
            }
            catch (Exception ex)
            {
                // logger.Info(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + "\t" + ex.Message + "\t" + "\n" + ex.StackTrace + "\n");
                model.id.ErrorId().AddMessage("Error while renaming iterations: " + ex.Message);
            }
        }

        /// <summary>
        /// Import source code from sourec repo or GitHub
        /// </summary>
        /// <param name="model"></param>
        /// <param name="sourceCodeJSON"></param>
        /// <param name="_defaultConfiguration"></param>
        /// <param name="importSourceConfiguration"></param>
        /// <param name="id"></param>
        void ImportSourceCode(Project model, string sourceCodeJSON, ADOConfiguration _repo, string id, ADOConfiguration _retSourceCodeVersion)
        {

            try
            {
                string[] repositoryDetail = new string[2];
                if (model.GitHubFork)
                {

                }
                if (File.Exists(sourceCodeJSON))
                {
                    Repository objRepository = new Repository(_repo);
                    string repositoryName = Path.GetFileName(sourceCodeJSON).Replace(".json", "");
                    if (model.ProjectName.ToLower() == repositoryName.ToLower())
                    {
                        repositoryDetail = objRepository.GetDefaultRepository(model.ProjectName);
                    }
                    else
                    {
                        repositoryDetail = objRepository.CreateRepository(repositoryName, model.Environment.ProjectId);
                    }
                    if (repositoryDetail.Length > 0)
                    {
                        model.Environment.repositoryIdList[repositoryDetail[1]] = repositoryDetail[0];
                    }

                    string jsonSourceCode = model.ReadJsonFile(sourceCodeJSON);

                    //update endpoint ids
                    foreach (string endpoint in model.Environment.serviceEndpoints.Keys)
                    {
                        string placeHolder = string.Format("${0}$", endpoint);
                        jsonSourceCode = jsonSourceCode.Replace(placeHolder, model.Environment.serviceEndpoints[endpoint]);
                    }

                    Repository objRepositorySourceCode = new Repository(_retSourceCodeVersion);
                    bool copySourceCode = objRepositorySourceCode.GetSourceCodeFromGitHub(jsonSourceCode, model.ProjectName, repositoryDetail[0]);
                    if (copySourceCode)
                    {
                        model.id.AddMessage($"Source code imported to {repositoryName} repository");
                    }
                    if (!model.Environment.reposImported.ContainsKey(repositoryDetail[0]))
                    {
                        model.Environment.reposImported.Add(repositoryDetail[0], copySourceCode);
                    }

                    if (!(string.IsNullOrEmpty(objRepository.LastFailureMessage)))
                    {
                        id.ErrorId().AddMessage("Error while importing source code: " + objRepository.LastFailureMessage + Environment.NewLine);
                    }
                }

            }
            catch (Exception ex)
            {
                // logger.Info(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + "\t" + ex.Message + "\t" + "\n" + ex.StackTrace + "\n");
                id.ErrorId().AddMessage("Error while importing source code: " + ex.Message);
            }
        }

        /// <summary>
        /// Creates pull request
        /// </summary>
        /// <param name="model"></param>
        /// <param name="pullRequestJsonPath"></param>
        /// <param name="_configuration3_0"></param>
        void CreatePullRequest(Project model, string pullRequestJsonPath, ADOConfiguration _workItemConfig)
        {
            try
            {
                if (File.Exists(pullRequestJsonPath))
                {
                    string commentFile = Path.GetFileName(pullRequestJsonPath);
                    string repositoryId = string.Empty;
                    if (model.SelectedTemplate == "MyHealthClinic") { repositoryId = model.Environment.repositoryIdList["MyHealthClinic"]; }
                    if (model.SelectedTemplate == "SmartHotel360") { repositoryId = model.Environment.repositoryIdList["PublicWeb"]; }
                    else { repositoryId = model.Environment.repositoryIdList.ContainsKey(model.SelectedTemplate) ? model.Environment.repositoryIdList[model.SelectedTemplate] : ""; }

                    pullRequestJsonPath = model.ReadJsonFile(pullRequestJsonPath);
                    pullRequestJsonPath = pullRequestJsonPath.Replace("$reviewer$", model.Environment.UserUniqueId);
                    Repository objRepository = new Repository(_workItemConfig);
                    string[] pullReqResponse = new string[2];

                    pullReqResponse = objRepository.CreatePullRequest(pullRequestJsonPath, repositoryId);
                    if (pullReqResponse.Length > 0)
                    {
                        if (!string.IsNullOrEmpty(pullReqResponse[0]) && !string.IsNullOrEmpty(pullReqResponse[1]))
                        {
                            model.Environment.pullRequests.Add(pullReqResponse[1], pullReqResponse[0]);
                            commentFile = GetJsonFilePath(model.IsPrivatePath, model.PrivateTemplatePath, model.SelectedTemplate, "PullRequests\\Comments\\" + commentFile);
                            //string.Format(templatesFolder + @"{0}\PullRequests\Comments\{1}", model.SelectedTemplate, commentFile);
                            if (File.Exists(commentFile))
                            {
                                commentFile = model.ReadJsonFile(commentFile);
                                PullRequestComments.Comments commentsList = JsonConvert.DeserializeObject<PullRequestComments.Comments>(commentFile);
                                if (commentsList.count > 0)
                                {
                                    foreach (PullRequestComments.Value thread in commentsList.value)
                                    {
                                        string threadID = objRepository.CreateCommentThread(repositoryId, pullReqResponse[0], JsonConvert.SerializeObject(thread));
                                        if (!string.IsNullOrEmpty(threadID))
                                        {
                                            if (thread.Replies != null && thread.Replies.Count > 0)
                                            {
                                                foreach (var reply in thread.Replies)
                                                {
                                                    objRepository.AddCommentToThread(repositoryId, pullReqResponse[0], threadID, JsonConvert.SerializeObject(reply));
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // logger.Info(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + "\t" + ex.Message + "\t" + "\n" + ex.StackTrace + "\n");
                model.id.ErrorId().AddMessage("Error while creating pull Requests: " + ex.Message);
            }
        }

        /// <summary>
        /// Creates service end points
        /// </summary>
        /// <param name="model"></param>
        /// <param name="jsonPaths"></param>
        /// <param name="_defaultConfiguration"></param>
        void CreateServiceEndPoint(Project model, List<string> jsonPaths, ADOConfiguration _endpointConfig)
        {
            try
            {
                string serviceEndPointId = string.Empty;
                foreach (string jsonPath in jsonPaths)
                {
                    string fileName = Path.GetFileName(jsonPath);
                    string jsonCreateService = jsonPath;
                    if (File.Exists(jsonCreateService))
                    {
                        string username = _configuration["AppSettings:UserID"];
                        string password = _configuration["AppSettings:Password"];
                        //string extractPath = HostingEnvironment.MapPath("~/Templates/" + model.SelectedTemplate);
                        string projectFileData = File.ReadAllText(GetJsonFilePath(model.IsPrivatePath, model.PrivateTemplatePath, model.SelectedTemplate, "ProjectTemplate.json"));
                        ProjectSetting settings = JsonConvert.DeserializeObject<ProjectSetting>(projectFileData);
                        ServiceEndPoint objService = new ServiceEndPoint(_endpointConfig);

                        string gitUserName = _configuration["AppSettings:GitUserName"];
                        string gitUserPassword = _configuration["AppSettings:GitUserPassword"];

                        jsonCreateService = model.ReadJsonFile(jsonCreateService);

                        if (!string.IsNullOrEmpty(settings.IsPrivate))
                        {
                            jsonCreateService = jsonCreateService.Replace("$ProjectName$", model.ProjectName);
                            jsonCreateService = jsonCreateService.Replace("$username$", model.Email).Replace("$password$", model.accessToken);
                        }
                        // File contains "GitHub_" means - it contains GitHub URL, user wanted to fork repo to his github
                        if (fileName.Contains("GitHub_") && model.GitHubFork && model.GitHubToken != null)
                        {
                            JObject jsonToCreate = JObject.Parse(jsonCreateService);
                            string type = jsonToCreate["type"].ToString();
                            string url = jsonToCreate["url"].ToString();
                            string repoNameInUrl = Path.GetFileName(url);
                            // Endpoint type is Git(External Git), so we should point Build def to his repo by creating endpoint of Type GitHub(Public)
                            foreach (var repo in model.Environment.gitHubRepos.Keys)
                            {
                                if (repoNameInUrl.Contains(repo))
                                {
                                    if (type.ToLower() == "git")
                                    {
                                        jsonToCreate["type"] = "GitHub"; //Changing endpoint type
                                        jsonToCreate["url"] = model.Environment.gitHubRepos[repo].ToString(); // updating endpoint URL with User forked repo URL
                                    }
                                    // Endpoint type is GitHub(Public), so we should point the build def to his repo by updating the URL
                                    else if (type.ToLower() == "github")
                                    {
                                        jsonToCreate["url"] = model.Environment.gitHubRepos[repo].ToString(); // Updating repo URL to user repo
                                    }
                                    else
                                    {

                                    }
                                }
                            }
                            jsonCreateService = jsonToCreate.ToString();
                            jsonCreateService = jsonCreateService.Replace("$GitUserName$", model.GitHubUserName).Replace("$GitUserPassword$", model.GitHubToken);
                        }
                        // user doesn't want to fork repo
                        else
                        {
                            jsonCreateService = jsonCreateService.Replace("$ProjectName$", model.ProjectName); // Replaces the Place holder with project name if exists
                            jsonCreateService = jsonCreateService.Replace("$username$", username).Replace("$password$", password) // Replaces user name and password with app setting username and password if require[to import soure code to Azure Repos]
                                .Replace("$GitUserName$", gitUserName).Replace("$GitUserPassword$", gitUserPassword); // Replaces GitUser name and passwords with Demo gen username and password [Just to point build def to respective repo]
                        }
                        if (model.SelectedTemplate.ToLower() == "bikesharing360")
                        {
                            string bikeSharing360username = _configuration["AppSettings:UserID"];
                            string bikeSharing360password = _configuration["AppSettings:BikeSharing360Password"];
                            jsonCreateService = jsonCreateService.Replace("$BikeSharing360username$", bikeSharing360username).Replace("$BikeSharing360password$", bikeSharing360password);
                        }
                        else if (model.SelectedTemplate.ToLower() == "contososhuttle" || model.SelectedTemplate.ToLower() == "contososhuttle2")
                        {
                            string contosousername = _configuration["AppSettings:ContosoUserID"];
                            string contosopassword = _configuration["AppSettings:ContosoPassword"];
                            jsonCreateService = jsonCreateService.Replace("$ContosoUserID$", contosousername).Replace("$ContosoPassword$", contosopassword);
                        }
                        else if (model.SelectedTemplate.ToLower() == "sonarqube")
                        {
                            if (!string.IsNullOrEmpty(model.SonarQubeDNS))
                            {
                                jsonCreateService = jsonCreateService.Replace("$URL$", model.SonarQubeDNS);
                            }
                        }
                        else if (model.SelectedTemplate.ToLower() == "octopus")
                        {
                            var url = model.Parameters["OctopusURL"];
                            var apiKey = model.Parameters["APIkey"];
                            if (!string.IsNullOrEmpty(url.ToString()) && !string.IsNullOrEmpty(apiKey.ToString()))
                            {
                                jsonCreateService = jsonCreateService.Replace("$URL$", url).Replace("$Apikey$", apiKey);

                            }
                        }
                        var endpoint = objService.CreateServiceEndPoint(jsonCreateService, model.ProjectName);

                        if (!(string.IsNullOrEmpty(objService.LastFailureMessage)))
                        {
                            model.id.ErrorId().AddMessage("Error while creating service endpoint: " + objService.LastFailureMessage + Environment.NewLine);
                        }
                        else
                        {
                            model.Environment.serviceEndpoints[endpoint.name] = endpoint.id;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // logger.Info(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + "\t" + ex.Message + "\t" + "\n" + ex.StackTrace + "\n");
                model.id.ErrorId().AddMessage("Error while creating service endpoint: " + ex.Message);
            }
        }

        /// <summary>
        /// Create Test Cases
        /// </summary>
        /// <param name="wiMapping"></param>
        /// <param name="model"></param>
        /// <param name="testPlanJson"></param>
        /// <param name="_defaultConfiguration"></param>
        void CreateTestManagement(List<RestAPI.WorkItemAndTracking.WIMapData> wiMapping, Project model, string testPlanJson, ADOConfiguration _testPlanVersion)
        {
            try
            {
                if (File.Exists(testPlanJson))
                {
                    List<RestAPI.WorkItemAndTracking.WIMapData> testCaseMap = new List<RestAPI.WorkItemAndTracking.WIMapData>();
                    testCaseMap = wiMapping.Where(x => x.WIType == "Test Case").ToList();

                    string fileName = Path.GetFileName(testPlanJson);
                    testPlanJson = model.ReadJsonFile(testPlanJson);

                    testPlanJson = testPlanJson.Replace("$project$", model.ProjectName);
                    TestManagement objTest = new TestManagement(_testPlanVersion);
                    string[] testPlanResponse = new string[2];
                    testPlanResponse = objTest.CreateTestPlan(testPlanJson, model.ProjectName);

                    if (testPlanResponse.Length > 0)
                    {
                        string testSuiteJson = GetJsonFilePath(model.IsPrivatePath, model.PrivateTemplatePath, model.SelectedTemplate, "\\TestPlans\\TestSuites\\" + fileName);
                        //string.Format(templateFolder + @"{0}\TestPlans\TestSuites\{1}", model.SelectedTemplate, fileName);
                        if (File.Exists(testSuiteJson))
                        {
                            testSuiteJson = model.ReadJsonFile(testSuiteJson);
                            testSuiteJson = testSuiteJson.Replace("$planID$", testPlanResponse[0]).Replace("$planName$", testPlanResponse[1]);
                            foreach (var wi in wiMapping)
                            {
                                string placeHolder = string.Format("${0}$", wi.OldID);
                                testSuiteJson = testSuiteJson.Replace(placeHolder, wi.NewID);
                            }
                            TestSuite.TestSuites listTestSuites = JsonConvert.DeserializeObject<TestSuite.TestSuites>(testSuiteJson);
                            if (listTestSuites.count > 0)
                            {
                                foreach (var TS in listTestSuites.value)
                                {
                                    string[] testSuiteResponse = new string[2];
                                    string testSuiteJSON = JsonConvert.SerializeObject(TS);
                                    testSuiteResponse = objTest.CreatTestSuite(testSuiteJSON, testPlanResponse[0], model.ProjectName);
                                    if (testSuiteResponse[0] != null && testSuiteResponse[1] != null)
                                    {
                                        string testCasesToAdd = string.Empty;
                                        foreach (string id in TS.TestCases)
                                        {
                                            foreach (var wiMap in testCaseMap)
                                            {
                                                if (wiMap.OldID == id)
                                                {
                                                    testCasesToAdd = testCasesToAdd + wiMap.NewID + ",";
                                                }
                                            }
                                        }
                                        testCasesToAdd = testCasesToAdd.TrimEnd(',');
                                        bool isTestCasesAddded = objTest.AddTestCasesToSuite(testCasesToAdd, testPlanResponse[0], testSuiteResponse[0], model.ProjectName);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // logger.Info(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + "\t" + ex.Message + "\t" + "\n" + ex.StackTrace + "\n");
                model.id.ErrorId().AddMessage("Error while creating test plan and test suites: " + ex.Message);
            }
        }

        /// <summary>
        /// Creates Build Definitions
        /// </summary>
        /// <param name="model"></param>
        /// <param name="_defaultConfiguration"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        bool CreateBuildDefinition(Project model, ADOConfiguration _buildConfig, string id)
        {
            bool flag = false;
            try
            {
                foreach (BuildDef buildDef in model.BuildDefinitions)
                {
                    if (File.Exists(buildDef.FilePath))
                    {
                        BuildDefinition objBuild = new BuildDefinition(_buildConfig);
                        string jsonBuildDefinition = model.ReadJsonFile(buildDef.FilePath);
                        jsonBuildDefinition = jsonBuildDefinition.Replace("$ProjectName$", model.Environment.ProjectName)
                                             .Replace("$ProjectId$", model.Environment.ProjectId)
                                             .Replace("$username$", model.GitHubUserName)
                                             .Replace("$Organization$", model.accountName);

                        if (model.Environment.variableGroups.Count > 0)
                        {
                            foreach (var vGroupsId in model.Environment.variableGroups)
                            {
                                string placeHolder = string.Format("${0}$", vGroupsId.Value);
                                jsonBuildDefinition = jsonBuildDefinition.Replace(placeHolder, vGroupsId.Key.ToString());
                            }
                        }

                        //update repositoryId 
                        foreach (string repository in model.Environment.repositoryIdList.Keys)
                        {
                            string placeHolder = string.Format("${0}$", repository);
                            jsonBuildDefinition = jsonBuildDefinition.Replace(placeHolder, model.Environment.repositoryIdList[repository]);
                        }
                        //update endpoint ids
                        foreach (string endpoint in model.Environment.serviceEndpoints.Keys)
                        {
                            string placeHolder = string.Format("${0}$", endpoint);
                            jsonBuildDefinition = jsonBuildDefinition.Replace(placeHolder, model.Environment.serviceEndpoints[endpoint]);
                        }
                        if (model.Environment.AgentQueues.Count > 0)
                        {
                            foreach (var agentPool in model.Environment.AgentQueues.Keys)
                            {
                                string placeHolder = string.Format("${0}$", agentPool);
                                jsonBuildDefinition = jsonBuildDefinition.Replace(placeHolder, Convert.ToString(model.Environment.AgentQueues[agentPool]));
                            }
                        }

                        string[] buildResult = objBuild.CreateBuildDefinition(jsonBuildDefinition, model.ProjectName, model.SelectedTemplate);

                        if (!(string.IsNullOrEmpty(objBuild.LastFailureMessage)))
                        {
                            id.ErrorId().AddMessage("Error while creating build definition: " + objBuild.LastFailureMessage + Environment.NewLine);
                        }
                        if (buildResult.Length > 0)
                        {
                            buildDef.Id = buildResult[0];
                            buildDef.Name = buildResult[1];
                        }
                    }
                    flag = true;
                }
                return flag;
            }

            catch (Exception ex)
            {
                // logger.Info(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + "\t" + ex.Message + "\t" + "\n" + ex.StackTrace + "\n");
                id.ErrorId().AddMessage("Error while creating build definition: " + ex.Message);
            }
            return flag;
        }

        /// <summary>
        /// Queue build after provisioning project
        /// </summary>
        /// <param name="model"></param>
        /// <param name="json"></param>
        /// <param name="_configuration"></param>
        void QueueABuild(Project model, string json, ADOConfiguration _buildConfig)
        {
            try
            {
                string jsonQueueABuild = json;
                if (File.Exists(jsonQueueABuild))
                {
                    string buildId = model.BuildDefinitions.FirstOrDefault().Id;

                    jsonQueueABuild = model.ReadJsonFile(jsonQueueABuild);
                    jsonQueueABuild = jsonQueueABuild.Replace("$buildId$", buildId.ToString());
                    BuildDefinition objBuild = new BuildDefinition(_buildConfig);
                    int queueId = objBuild.QueueBuild(jsonQueueABuild, model.ProjectName);

                    if (!string.IsNullOrEmpty(objBuild.LastFailureMessage))
                    {
                        model.id.ErrorId().AddMessage("Error while Queueing build: " + objBuild.LastFailureMessage + Environment.NewLine);
                    }
                }
            }
            catch (Exception ex)
            {
                // logger.Info(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + "\t" + ex.Message + "\t" + "\n" + ex.StackTrace + "\n");
                model.id.ErrorId().AddMessage("Error while Queueing Build: " + ex.Message);
            }
        }

        /// <summary>
        /// Create Release Definitions
        /// </summary>
        /// <param name="model"></param>
        /// <param name="_releaseConfiguration"></param>
        /// <param name="_config3_0"></param>
        /// <param name="id"></param>
        /// <param name="teamMembers"></param>
        /// <returns></returns>
        bool CreateReleaseDefinition(Project model, ADOConfiguration _releaseConfiguration, string id, TeamMemberResponse.TeamMembers teamMembers)
        {
            bool flag = false;
            try
            {
                var teamMember = teamMembers.value.FirstOrDefault();
                foreach (ReleaseDef relDef in model.ReleaseDefinitions)
                {
                    if (File.Exists(relDef.FilePath))
                    {
                        ReleaseDefinition objRelease = new ReleaseDefinition(_releaseConfiguration);
                        string jsonReleaseDefinition = model.ReadJsonFile(relDef.FilePath);
                        jsonReleaseDefinition = jsonReleaseDefinition.Replace("$ProjectName$", model.Environment.ProjectName)
                                             .Replace("$ProjectId$", model.Environment.ProjectId)
                                             .Replace("$OwnerUniqueName$", teamMember.identity.uniqueName)
                                             .Replace("$OwnerId$", teamMember.identity.id)
                                  .Replace("$OwnerDisplayName$", teamMember.identity.displayName);

                        if (model.Environment.variableGroups.Count > 0)
                        {
                            foreach (var vGroupsId in model.Environment.variableGroups)
                            {
                                string placeHolder = string.Format("${0}$", vGroupsId.Value);
                                jsonReleaseDefinition = jsonReleaseDefinition.Replace(placeHolder, vGroupsId.Key.ToString());
                            }
                        }
                        //Adding randon UUID to website name
                        string uuid = Guid.NewGuid().ToString();
                        uuid = uuid.Substring(0, 8);
                        jsonReleaseDefinition = jsonReleaseDefinition.Replace("$UUID$", uuid).Replace("$RandomNumber$", uuid).Replace("$AccountName$", model.accountName); ;

                        //update agent queue ids
                        foreach (string queue in model.Environment.AgentQueues.Keys)
                        {
                            string placeHolder = string.Format("${0}$", queue);
                            jsonReleaseDefinition = jsonReleaseDefinition.Replace(placeHolder, model.Environment.AgentQueues[queue].ToString());
                        }

                        //update endpoint ids
                        foreach (string endpoint in model.Environment.serviceEndpoints.Keys)
                        {
                            string placeHolder = string.Format("${0}$", endpoint);
                            jsonReleaseDefinition = jsonReleaseDefinition.Replace(placeHolder, model.Environment.serviceEndpoints[endpoint]);
                        }

                        foreach (BuildDef objBuildDef in model.BuildDefinitions)
                        {
                            //update build ids
                            string placeHolder = string.Format("${0}-id$", objBuildDef.Name);
                            jsonReleaseDefinition = jsonReleaseDefinition.Replace(placeHolder, objBuildDef.Id);
                        }
                        string[] releaseDef = objRelease.CreateReleaseDefinition(jsonReleaseDefinition, model.ProjectName);
                        if (!(string.IsNullOrEmpty(objRelease.LastFailureMessage)))
                        {
                            if (objRelease.LastFailureMessage.TrimEnd() == "Tasks with versions 'ARM Outputs:3.*' are not valid for deploy job 'Function' in stage Azure-Dev.")
                            {
                                jsonReleaseDefinition = jsonReleaseDefinition.Replace("3.*", "4.*");
                                releaseDef = objRelease.CreateReleaseDefinition(jsonReleaseDefinition, model.ProjectName);
                                if (releaseDef.Length > 0)
                                {
                                    relDef.Id = releaseDef[0];
                                    relDef.Name = releaseDef[1];
                                }
                                if (!string.IsNullOrEmpty(relDef.Name))
                                {
                                    objRelease.LastFailureMessage = string.Empty;
                                }
                            }
                        }
                        relDef.Id = releaseDef[0];
                        relDef.Name = releaseDef[1];

                        if (!(string.IsNullOrEmpty(objRelease.LastFailureMessage)))
                        {
                            id.ErrorId().AddMessage("Error while creating release definition: " + objRelease.LastFailureMessage + Environment.NewLine);
                        }
                    }
                    flag = true;
                }
                return flag;
            }
            catch (Exception ex)
            {
                // logger.Info(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + "\t" + ex.Message + "\t" + "\n" + ex.StackTrace + "\n");
                id.ErrorId().AddMessage("Error while creating release definition: " + ex.Message);
            }
            flag = false;
            return flag;
        }

        /// <summary>
        /// Dashboard set up operations
        /// </summary>
        /// <param name="model"></param>
        /// <param name="listQueries"></param>
        /// <param name="_defaultConfiguration"></param>
        /// <param name="_configuration2"></param>
        /// <param name="_configuration3"></param>
        /// <param name="releaseConfig"></param>
        void CreateQueryAndWidgets(Project model, List<string> listQueries, ADOConfiguration _queriesVersion, ADOConfiguration _dashboardVersion, ADOConfiguration _releaseConfig, ADOConfiguration _projectConfig, ADOConfiguration _boardConfig, string teamName = null)
        {
            try
            {
                Queries objWidget = new Queries(_dashboardVersion);
                Queries objQuery = new Queries(_queriesVersion);
                List<QueryResponse> queryResults = new List<QueryResponse>();

                //GetDashBoardDetails
                string dashBoardId = objWidget.GetDashBoardId(model.ProjectName, teamName);
                Thread.Sleep(2000); // Adding delay to get the existing dashboard ID 

                if (!string.IsNullOrEmpty(objQuery.LastFailureMessage))
                {
                    model.id.ErrorId().AddMessage("Error while getting dashboardId: " + objWidget.LastFailureMessage + Environment.NewLine);
                }
                Queries _newobjQuery = new Queries(_queriesVersion);
                bool isFolderCreated = false;
                if (!string.IsNullOrEmpty(teamName))
                {
                    string createQueryFolderJson = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "PreSetting", "CreateQueryFolder.json"));
                    createQueryFolderJson = createQueryFolderJson.Replace("$TeamName$", teamName);
                    QueryResponse createFolderResponse = _newobjQuery.CreateQuery(model.ProjectName, createQueryFolderJson);
                    isFolderCreated = createFolderResponse.id != null ? true : false;
                }
                foreach (string query in listQueries)
                {
                    //create query
                    string json = model.ReadJsonFile(query);
                    json = json.Replace("$projectId$", model.Environment.ProjectName);
                    QueryResponse response = new QueryResponse();
                    if (isFolderCreated)
                    {
                        response = _newobjQuery.CreateQuery(model.ProjectName, json, teamName);
                    }
                    else
                    {
                        response = _newobjQuery.CreateQuery(model.ProjectName, json);
                    }
                    queryResults.Add(response);

                    if (!string.IsNullOrEmpty(_newobjQuery.LastFailureMessage))
                    {
                        model.id.ErrorId().AddMessage("Error while creating query: " + _newobjQuery.LastFailureMessage + Environment.NewLine);
                    }

                }
                //Create DashBoards
                string dashBoardTemplate = string.Empty;
                if (!string.IsNullOrEmpty(teamName))
                {
                    dashBoardTemplate = GetJsonFilePath(model.IsPrivatePath, model.PrivateTemplatePath, model.SelectedTemplate, $"\\Dashboard\\{teamName}\\Dashboard.json");
                }
                else
                {
                    dashBoardTemplate = GetJsonFilePath(model.IsPrivatePath, model.PrivateTemplatePath, model.SelectedTemplate, "Dashboard\\Dashboard.json");
                }
                if (File.Exists(dashBoardTemplate))
                {
                    dynamic dashBoard = new System.Dynamic.ExpandoObject();
                    dashBoard.name = "Working";
                    dashBoard.position = 4;

                    string jsonDashBoard = Newtonsoft.Json.JsonConvert.SerializeObject(dashBoard);
                    string dashBoardIdToDelete = objWidget.CreateNewDashBoard(model.ProjectName, jsonDashBoard, teamName);

                    bool isDashboardDeleted = objWidget.DeleteDefaultDashboard(model.ProjectName, dashBoardId, teamName);

                    if (model.SelectedTemplate.ToLower() == "gen-partsunlimited")
                    {
                        if (isDashboardDeleted)
                        {
                            dashBoardTemplate = model.ReadJsonFile(dashBoardTemplate);

                            QueryResponse feedBack = objQuery.GetQueryByPathAndName(model.ProjectName, "Feedback_WI", "Shared%20Queries");
                            QueryResponse unfinishedWork = objQuery.GetQueryByPathAndName(model.ProjectName, "Unfinished Work_WI", "Shared%20Queries");


                            dashBoardTemplate = dashBoardTemplate.Replace("$Feedback$", feedBack.id).
                                         Replace("$AllItems$", queryResults.Where(x => x.name == "All Items_WI").FirstOrDefault() != null ? queryResults.Where(x => x.name == "All Items_WI").FirstOrDefault().id : string.Empty).
                                         Replace("$UserStories$", queryResults.Where(x => x.name == "User Stories").FirstOrDefault() != null ? queryResults.Where(x => x.name == "User Stories").FirstOrDefault().id : string.Empty).
                                         Replace("$TestCase$", queryResults.Where(x => x.name == "Test Case-Readiness").FirstOrDefault() != null ? queryResults.Where(x => x.name == "Test Case-Readiness").FirstOrDefault().id : string.Empty).
                                         Replace("$teamID$", "").
                                         Replace("$teamName$", model.ProjectName + " Team").
                                         Replace("$projectID$", model.Environment.ProjectId).
                                         Replace("$Unfinished Work$", unfinishedWork.id).
                                         Replace("$projectId$", model.Environment.ProjectId).
                                         Replace("$projectName$", model.ProjectName);


                            if (model.SelectedTemplate == "Gen-MyHealthClinic")
                            {
                                dashBoardTemplate = dashBoardTemplate.Replace("$ReleaseDefId$", model.ReleaseDefinitions.Where(x => x.Name == "MyHealthClinicE2E").FirstOrDefault() != null ? model.ReleaseDefinitions.Where(x => x.Name == "MyHealthClinicE2E").FirstOrDefault().Id : string.Empty).
                                             Replace("$ActiveBugs$", queryResults.Where(x => x.name == "Active Bugs_WI").FirstOrDefault() != null ? queryResults.Where(x => x.name == "Active Bugs_WI").FirstOrDefault().id : string.Empty).
                                             Replace("$MyHealthClinicE2E$", model.BuildDefinitions.Where(x => x.Name == "MyHealthClinicE2E").FirstOrDefault() != null ? model.BuildDefinitions.Where(x => x.Name == "MyHealthClinicE2E").FirstOrDefault().Id : string.Empty).
                                                 Replace("$RepositoryId$", model.Environment.repositoryIdList.Any(i => i.Key.ToLower().Contains("myhealthclinic")) ? model.Environment.repositoryIdList.Where(x => x.Key.ToLower() == "myhealthclinic").FirstOrDefault().Value : string.Empty);
                            }
                            if (model.SelectedTemplate == "Gen-MyHealthClinic" || model.SelectedTemplate == "Gen-PartsUnlimited")
                            {
                                QueryResponse workInProgress = objQuery.GetQueryByPathAndName(model.ProjectName, "Work in Progress_WI", "Shared%20Queries");

                                dashBoardTemplate = dashBoardTemplate.Replace("$ReleaseDefId$", model.ReleaseDefinitions.Where(x => x.Name == "PartsUnlimitedE2E").FirstOrDefault() != null ? model.ReleaseDefinitions.Where(x => x.Name == "PartsUnlimitedE2E").FirstOrDefault().Id : string.Empty).
                                          Replace("$ActiveBugs$", queryResults.Where(x => x.name == "Critical Bugs").FirstOrDefault() != null ? queryResults.Where(x => x.name == "Critical Bugs").FirstOrDefault().id : string.Empty).
                                          Replace("$PartsUnlimitedE2E$", model.BuildDefinitions.Where(x => x.Name == "PartsUnlimitedE2E").FirstOrDefault() != null ? model.BuildDefinitions.Where(x => x.Name == "PartsUnlimitedE2E").FirstOrDefault().Id : string.Empty)
                                          .Replace("$WorkinProgress$", workInProgress.id)
                                .Replace("$RepositoryId$", model.Environment.repositoryIdList.Any(i => i.Key.ToLower().Contains("partsunlimited")) ? model.Environment.repositoryIdList.Where(x => x.Key.ToLower() == "partsunlimited").FirstOrDefault().Value : string.Empty);

                            }
                            string isDashBoardCreated = objWidget.CreateNewDashBoard(model.ProjectName, dashBoardTemplate);
                            objWidget.DeleteDefaultDashboard(model.ProjectName, dashBoardIdToDelete);

                        }
                    }
                    if (model.SelectedTemplate.ToLower() == "dl-docker")
                    {
                        if (isDashboardDeleted)
                        {
                            dashBoardTemplate = model.ReadJsonFile(dashBoardTemplate);
                            var buildDefId = model.BuildDefinitions.FirstOrDefault();
                            dashBoardTemplate = dashBoardTemplate.Replace("$BuildDefId$", buildDefId.Id)
                                  .Replace("$projectId$", model.Environment.ProjectId)
                                  .Replace("$PBI$", queryResults.Where(x => x.name == "Product Backlog Items").FirstOrDefault().id != null ? queryResults.Where(x => x.name == "Product Backlog Items").FirstOrDefault().id : string.Empty)
                                  .Replace("$Bugs$", queryResults.Where(x => x.name == "Bugs").FirstOrDefault() != null ? queryResults.Where(x => x.name == "Bugs").FirstOrDefault().id : string.Empty)
                                  .Replace("$AllWorkItems$", queryResults.Where(x => x.name == "All Work Items").FirstOrDefault() != null ? queryResults.Where(x => x.name == "All Work Items").FirstOrDefault().id : string.Empty)
                                  .Replace("$Feature$", queryResults.Where(x => x.name == "Feature").FirstOrDefault() != null ? queryResults.Where(x => x.name == "Feature").FirstOrDefault().id : string.Empty)
                                  .Replace("$Task$", queryResults.Where(x => x.name == "Tasks").FirstOrDefault() != null ? queryResults.Where(x => x.name == "Tasks").FirstOrDefault().id : string.Empty)
                                  .Replace("$Epic$", queryResults.Where(x => x.name == "Epics").FirstOrDefault() != null ? queryResults.Where(x => x.name == "Tasks").FirstOrDefault().id : string.Empty)
                                  .Replace("$RepoMyShuttleDocker$", model.Environment.repositoryIdList.Where(x => x.Key == "Docker").FirstOrDefault().ToString() != "" ? model.Environment.repositoryIdList.Where(x => x.Key == "MyShuttleDocker").FirstOrDefault().Value : string.Empty)
                                  .Replace("$Task$", queryResults.Where(x => x.name == "Tasks").FirstOrDefault() != null ? queryResults.Where(x => x.name == "Tasks").FirstOrDefault().id : string.Empty)
                                  .Replace("$AllWorkItems$", queryResults.Where(x => x.name == "All Work Items").FirstOrDefault() != null ? queryResults.Where(x => x.name == "All Work Items").FirstOrDefault().id : string.Empty)
                                  .Replace("$Feature$", queryResults.Where(x => x.name == "Feature").FirstOrDefault() != null ? queryResults.Where(x => x.name == "Feature").FirstOrDefault().id : string.Empty)
                                  .Replace("$Projectid$", model.Environment.ProjectId)
                                  .Replace("$BuildDocker$", model.BuildDefinitions.Where(x => x.Name == "MHCDocker.build").FirstOrDefault() != null ? model.BuildDefinitions.Where(x => x.Name == "MHCDocker.build").FirstOrDefault().Id : string.Empty)
                                  .Replace("$ReleaseDocker$", model.ReleaseDefinitions.Where(x => x.Name == "MHCDocker.release").FirstOrDefault() != null ? model.ReleaseDefinitions.Where(x => x.Name == "MHCDocker.release").FirstOrDefault().Id : string.Empty);

                            string isDashBoardCreated = objWidget.CreateNewDashBoard(model.ProjectName, dashBoardTemplate);
                            objWidget.DeleteDefaultDashboard(model.ProjectName, dashBoardIdToDelete);
                        }
                    }
                    if (model.SelectedTemplate.ToLower() == "gen-myshuttle")
                    {
                        if (isDashboardDeleted)
                        {
                            dashBoardTemplate = model.ReadJsonFile(dashBoardTemplate);
                            dashBoardTemplate = dashBoardTemplate
                            .Replace("$PBI$", queryResults.Where(x => x.name == "Product Backlog Items").FirstOrDefault() != null ? queryResults.Where(x => x.name == "Product Backlog Items").FirstOrDefault().id : string.Empty)
                            .Replace("$Bugs$", queryResults.Where(x => x.name == "Bugs").FirstOrDefault() != null ? queryResults.Where(x => x.name == "Bugs").FirstOrDefault().id : string.Empty)
                            .Replace("$AllWorkItems$", queryResults.Where(x => x.name == "All Work Items").FirstOrDefault() != null ? queryResults.Where(x => x.name == "All Work Items").FirstOrDefault().id : string.Empty)
                            .Replace("$TestPlan$", queryResults.Where(x => x.name == "Test Plans").FirstOrDefault().id != null ? queryResults.Where(x => x.name == "Test Plans").FirstOrDefault().id : string.Empty)
                            .Replace("$Test Cases$", queryResults.Where(x => x.name == "Test Cases").FirstOrDefault() != null ? queryResults.Where(x => x.name == "Test Cases").FirstOrDefault().id : string.Empty)
                            .Replace("$Features$", queryResults.Where(x => x.name == "Feature").FirstOrDefault() != null ? queryResults.Where(x => x.name == "Feature").FirstOrDefault().id : string.Empty)
                            .Replace("$Tasks$", queryResults.Where(x => x.name == "Tasks").FirstOrDefault() != null ? queryResults.Where(x => x.name == "Tasks").FirstOrDefault().id : string.Empty)
                            .Replace("$TestSuite$", queryResults.Where(x => x.name == "Test Suites").FirstOrDefault() != null ? queryResults.Where(x => x.name == "Test Suites").FirstOrDefault().id : string.Empty);


                            string isDashBoardCreated = objWidget.CreateNewDashBoard(model.ProjectName, dashBoardTemplate);
                            objWidget.DeleteDefaultDashboard(model.ProjectName, dashBoardIdToDelete);
                        }
                    }
                    if (model.SelectedTemplate.ToLower() == "dl-docker" || model.SelectedTemplate.ToLower() == "dl-php" || model.SelectedTemplate.ToLower() == "dl-sonarqube" || model.SelectedTemplate.ToLower() == "dl-github" || model.SelectedTemplate.ToLower() == "dl-whitesource bolt" || model.SelectedTemplate.ToLower() == "dl-deploymentgroups" || model.SelectedTemplate.ToLower() == "dl-octopus")
                    {
                        if (isDashboardDeleted)
                        {
                            dashBoardTemplate = dashBoardTemplate.Replace("$Task$", queryResults.Where(x => x.name == "Tasks").FirstOrDefault() != null ? queryResults.Where(x => x.name == "Tasks").FirstOrDefault().id : string.Empty)
                                         .Replace("$AllWorkItems$", queryResults.Where(x => x.name == "All Work Items").FirstOrDefault() != null ? queryResults.Where(x => x.name == "All Work Items").FirstOrDefault().id : string.Empty)
                                         .Replace("$Feature$", queryResults.Where(x => x.name == "Feature").FirstOrDefault() != null ? queryResults.Where(x => x.name == "Feature").FirstOrDefault().id : string.Empty)
                                         .Replace("$Projectid$", model.Environment.ProjectId)
                                         .Replace("$Epic$", queryResults.Where(x => x.name == "Epics").FirstOrDefault() != null ? queryResults.Where(x => x.name == "Epics").FirstOrDefault().id : string.Empty);

                            if (model.SelectedTemplate.ToLower() == "dl-php")
                            {
                                dashBoardTemplate = dashBoardTemplate.Replace("$buildPHP$", model.BuildDefinitions.Where(x => x.Name == "PHP").FirstOrDefault() != null ? model.BuildDefinitions.Where(x => x.Name == "PHP").FirstOrDefault().Id : string.Empty)
                        .Replace("$releasePHP$", model.ReleaseDefinitions.Where(x => x.Name == "PHP").FirstOrDefault() != null ? model.ReleaseDefinitions.Where(x => x.Name == "PHP").FirstOrDefault().Id : string.Empty)
                                 .Replace("$PBI$", queryResults.Where(x => x.name == "Product Backlog Items").FirstOrDefault() != null ? queryResults.Where(x => x.name == "Product Backlog Items").FirstOrDefault().id : string.Empty);
                            }
                            else if (model.SelectedTemplate.ToLower() == "dl-sonarqube")
                            {
                                dashBoardTemplate = dashBoardTemplate.Replace("$BuildSonarQube$", model.BuildDefinitions.Where(x => x.Name == "SonarQube").FirstOrDefault() != null ? model.BuildDefinitions.Where(x => x.Name == "SonarQube").FirstOrDefault().Id : string.Empty)
                                .Replace("$PBI$", queryResults.Where(x => x.name == "Product Backlog Items").FirstOrDefault() != null ? queryResults.Where(x => x.name == "Product Backlog Items").FirstOrDefault().id : string.Empty);

                            }
                            else if (model.SelectedTemplate.ToLower() == "dl-github")
                            {
                                dashBoardTemplate = dashBoardTemplate.Replace("$PBI$", queryResults.Where(x => x.name == "Product Backlog Items").FirstOrDefault() != null ? queryResults.Where(x => x.name == "Product Backlog Items").FirstOrDefault().id : string.Empty)
                                             .Replace("$buildGitHub$", model.BuildDefinitions.Where(x => x.Name == "GitHub").FirstOrDefault() != null ? model.BuildDefinitions.Where(x => x.Name == "GitHub").FirstOrDefault().Id : string.Empty)
                                             .Replace("$Hosted$", model.Environment.AgentQueues["Hosted"].ToString())
                                             .Replace("$releaseGitHub$", model.ReleaseDefinitions.Where(x => x.Name == "GitHub").FirstOrDefault() != null ? model.ReleaseDefinitions.Where(x => x.Name == "GitHub").FirstOrDefault().Id : string.Empty);

                            }
                            else if (model.SelectedTemplate.ToLower() == "dl-whitesource bolt")
                            {
                                dashBoardTemplate = dashBoardTemplate.Replace("$PBI$", queryResults.Where(x => x.name == "Product Backlog Items").FirstOrDefault() != null ? queryResults.Where(x => x.name == "Product Backlog Items").FirstOrDefault().id : string.Empty)
                                          .Replace("$buildWhiteSource$", model.BuildDefinitions.Where(x => x.Name == "WhiteSourceBolt").FirstOrDefault() != null ? model.BuildDefinitions.Where(x => x.Name == "WhiteSourceBolt").FirstOrDefault().Id : string.Empty);
                            }

                            else if (model.SelectedTemplate.ToLower() == "dl-deploymentGroups")
                            {
                                QueryResponse WorkInProgress = objQuery.GetQueryByPathAndName(model.ProjectName, "Work in Progress_WI", "Shared%20Queries");
                                dashBoardTemplate = dashBoardTemplate.Replace("$WorkinProgress$", WorkInProgress.id);
                            }

                            else if (model.SelectedTemplate.ToLower() == "dl-octopus")
                            {
                                var BuildDefId = model.BuildDefinitions.FirstOrDefault();
                                if (BuildDefId != null)
                                {
                                    dashBoardTemplate = dashBoardTemplate.Replace("$BuildDefId$", BuildDefId.Id)
                                            .Replace("$PBI$", queryResults.Where(x => x.name == "Product Backlog Items").FirstOrDefault() != null ? queryResults.Where(x => x.name == "Product Backlog Items").FirstOrDefault().id : string.Empty);
                                }
                            }


                            string isDashBoardCreated = objWidget.CreateNewDashBoard(model.ProjectName, dashBoardTemplate);
                            objWidget.DeleteDefaultDashboard(model.ProjectName, dashBoardIdToDelete);
                        }
                    }
                    if (model.SelectedTemplate.ToLower() == "gen-smarthotel360")
                    {
                        if (isDashboardDeleted)
                        {
                            string startdate = DateTime.Now.ToString("yyyy-MM-dd");
                            RestAPI.ProjectsAndTeams.Teams objTeam = new RestAPI.ProjectsAndTeams.Teams(_projectConfig);
                            TeamResponse defaultTeam = objTeam.GetTeamByName(model.ProjectName, model.ProjectName + " team");
                            RestAPI.WorkItemAndTracking.ClassificationNodes objnodes = new RestAPI.WorkItemAndTracking.ClassificationNodes(_boardConfig);
                            SprintResponse.Sprints sprints = objnodes.GetSprints(model.ProjectName);
                            QueryResponse allItems = objQuery.GetQueryByPathAndName(model.ProjectName, "All Items_WI", "Shared%20Queries");
                            QueryResponse backlogBoardWI = objQuery.GetQueryByPathAndName(model.ProjectName, "BacklogBoard WI", "Shared%20Queries");
                            QueryResponse boardWI = objQuery.GetQueryByPathAndName(model.ProjectName, "Board WI", "Shared%20Queries");
                            QueryResponse bugsWithoutReproSteps = objQuery.GetQueryByPathAndName(model.ProjectName, "Bugs without Repro Steps", "Shared%20Queries");
                            QueryResponse feedback = objQuery.GetQueryByPathAndName(model.ProjectName, "Feedback_WI", "Shared%20Queries");
                            QueryResponse mobileTeamWork = objQuery.GetQueryByPathAndName(model.ProjectName, "MobileTeam_Work", "Shared%20Queries");
                            QueryResponse webTeamWork = objQuery.GetQueryByPathAndName(model.ProjectName, "WebTeam_Work", "Shared%20Queries");
                            QueryResponse stateofTestCase = objQuery.GetQueryByPathAndName(model.ProjectName, "State of TestCases", "Shared%20Queries");
                            QueryResponse bugs = objQuery.GetQueryByPathAndName(model.ProjectName, "Open Bugs_WI", "Shared%20Queries");

                            QueryResponse unfinishedWork = objQuery.GetQueryByPathAndName(model.ProjectName, "Unfinished Work_WI", "Shared%20Queries");
                            QueryResponse workInProgress = objQuery.GetQueryByPathAndName(model.ProjectName, "Work in Progress_WI", "Shared%20Queries");
                            dashBoardTemplate = model.ReadJsonFile(dashBoardTemplate);
                            dashBoardTemplate = dashBoardTemplate.Replace("$WorkinProgress$", workInProgress.id)
                                .Replace("$projectId$", model.Environment.ProjectId != null ? model.Environment.ProjectId : string.Empty)
                                .Replace("$PublicWebBuild$", model.BuildDefinitions.Where(x => x.Name == "SmartHotel_Petchecker-Web").FirstOrDefault() != null ? model.BuildDefinitions.Where(x => x.Name == "SmartHotel_Petchecker-Web").FirstOrDefault().Id : string.Empty)
                                .Replace("$DefaultTeamId$", defaultTeam.id != null ? defaultTeam.id : string.Empty).Replace("$AllItems$", allItems.id != null ? allItems.id : string.Empty)
                                .Replace("$BacklogBoardWI$", backlogBoardWI.id != null ? backlogBoardWI.id : string.Empty)
                                .Replace("$StateofTestCases$", stateofTestCase.id != null ? stateofTestCase.id : string.Empty)
                                .Replace("$Feedback$", feedback.id != null ? feedback.id : string.Empty)
                                .Replace("$RepoPublicWeb$", model.Environment.repositoryIdList.ContainsKey("PublicWeb") ? model.Environment.repositoryIdList["PublicWeb"] : string.Empty)
                                .Replace("$MobileTeamWork$", mobileTeamWork.id != null ? mobileTeamWork.id : string.Empty).Replace("$WebTeamWork$", webTeamWork.id != null ? webTeamWork.id : string.Empty)
                                .Replace("$Bugs$", bugs.id != null ? bugs.id : string.Empty)
                                .Replace("$sprint2$", sprints.value.Where(x => x.name == "Sprint 2").FirstOrDefault() != null ? sprints.value.Where(x => x.name == "Sprint 2").FirstOrDefault().id : string.Empty)
                                .Replace("$sprint3$", sprints.value.Where(x => x.name == "Sprint 3").FirstOrDefault() != null ? sprints.value.Where(x => x.name == "Sprint 3").FirstOrDefault().id : string.Empty)
                                .Replace("$startDate$", startdate)
                                .Replace("$BugswithoutRepro$", bugsWithoutReproSteps.id != null ? bugsWithoutReproSteps.id : string.Empty).Replace("$UnfinishedWork$", unfinishedWork.id != null ? unfinishedWork.id : string.Empty)
                                .Replace("$RepoSmartHotel360$", model.Environment.repositoryIdList.ContainsKey("SmartHotel360") ? model.Environment.repositoryIdList["SmartHotel360"] : string.Empty)
                                .Replace("$SmartHotel360_Website-Deploy$", model.ReleaseDefinitions.Where(x => x.Name == "SmartHotel360_Website-Deploy").FirstOrDefault() != null ? model.ReleaseDefinitions.Where(x => x.Name == "SmartHotel360_Website-Deploy").FirstOrDefault().Id : string.Empty);

                            string isDashBoardCreated = objWidget.CreateNewDashBoard(model.ProjectName, dashBoardTemplate);
                            objWidget.DeleteDefaultDashboard(model.ProjectName, dashBoardIdToDelete);

                        }
                    }
                    if (model.SelectedTemplate.ToLower() == "gen-eshoponweb")
                    {
                        if (isDashboardDeleted)
                        {
                            string startDate = DateTime.Now.AddDays(-3).ToString("yyyy-MM-dd");
                            string endDate = DateTime.Now.AddDays(3).ToString("yyyy-MM-dd");
                            dashBoardTemplate = model.ReadJsonFile(dashBoardTemplate);
                            Teams objTeam = new Teams(_projectConfig);
                            TeamResponse teamDetails = objTeam.GetTeamByName(model.ProjectName, teamName != null ? teamName : model.ProjectName + " team");
                            foreach (string queries in listQueries)
                            {
                                string queryName = Path.GetFileName(queries).Replace(".json", string.Empty);
                                string placeHolder = "$" + queryName + "$";
                                QueryResponse query = objQuery.GetQueryByPathAndName(model.ProjectName, queryName, "Shared%20Queries/" + teamName);
                                dashBoardTemplate = dashBoardTemplate.Replace(placeHolder, query.id != null ? query.id : string.Empty);
                            }
                            dashBoardTemplate = dashBoardTemplate.Replace("$projectId$", model.Environment.ProjectId != null ? model.Environment.ProjectId : string.Empty).
                                Replace("$DefaultTeamId$", teamDetails.id != null ? teamDetails.id : string.Empty).Replace("$startDate$", startDate).Replace("$endDate$", endDate);
                            string dashboardId = objWidget.CreateNewDashBoard(model.ProjectName, dashBoardTemplate, teamName);
                            objWidget.DeleteDefaultDashboard(model.ProjectName, dashBoardIdToDelete, teamName);
                        }
                    }
                }
            }
            catch (OperationCanceledException oce)
            {
                // logger.Info(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + "\t" + oce.Message + "\t" + oce.InnerException.Message + "\n" + oce.StackTrace + "\n");
                model.id.ErrorId().AddMessage("Error while creating Queries and Widgets: Operation cancelled exception " + oce.Message + "\r\n");
            }
            catch (Exception ex)
            {
                // logger.Info(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + "\t" + ex.Message + "\t" + "\n" + ex.StackTrace + "\n");
                model.id.ErrorId().AddMessage("Error while creating Queries and Widgets: " + ex.Message);
            }
        }

        public bool InstallExtensions(Project model, string accountName, string PAT)
        {
            try
            {
                //string templatesFolder = HostingEnvironment.MapPath("~") + @"\Templates\";
                string projTemplateFile = GetJsonFilePath(model.IsPrivatePath, model.PrivateTemplatePath, model.SelectedTemplate, "Extensions.json");
                //string.Format(templatesFolder + @"{0}\Extensions.json", model.SelectedTemplate);
                if (!(File.Exists(projTemplateFile)))
                {
                    return false;
                }
                string templateItems = File.ReadAllText(projTemplateFile);
                var template = JsonConvert.DeserializeObject<RequiredExtensions.Extension>(templateItems);
                string requiresExtensionNames = string.Empty;

                //Check for existing extensions
                if (template.Extensions.Count > 0)
                {
                    Dictionary<string, bool> dict = new Dictionary<string, bool>();
                    foreach (RequiredExtensions.ExtensionWithLink ext in template.Extensions)
                    {
                        if (!dict.ContainsKey(ext.extensionName))
                        {
                            dict.Add(ext.extensionName, false);
                        }
                    }
                    //var connection = new VssConnection(new Uri(string.Format("https://{0}.visualstudio.com", accountName)), new Microsoft.VisualStudio.Services.OAuth.VssOAuthAccessTokenCredential(PAT));// VssOAuthCredential(PAT));
                    var connection = new VssConnection(new Uri(string.Format("https://{0}.visualstudio.com", accountName)), new VssBasicCredential(string.Empty, PAT));// VssOAuthCredential(PAT));

                    var client = connection.GetClient<ExtensionManagementHttpClient>();
                    var installed = client.GetInstalledExtensionsAsync().Result;
                    var extensions = installed.Where(x => x.Flags == 0).ToList();

                    var trustedFlagExtensions = installed.Where(x => x.Flags == ExtensionFlags.Trusted).ToList();
                    var builtInExtensions = installed.Where(x => x.Flags.ToString() == "BuiltIn, Trusted").ToList();
                    extensions.AddRange(trustedFlagExtensions);
                    extensions.AddRange(builtInExtensions);

                    foreach (var ext in extensions)
                    {
                        foreach (var extension in template.Extensions)
                        {
                            if (extension.extensionName.ToLower() == ext.ExtensionDisplayName.ToLower() && extension.extensionId.ToLower() == ext.ExtensionName.ToLower())
                            {
                                dict[extension.extensionName] = true;
                            }
                        }
                    }
                    var required = dict.Where(x => x.Value == false).ToList();

                    if (required.Count > 0)
                    {
                        Parallel.ForEach(required, async req =>
                        {
                            string publisherName = template.Extensions.Where(x => x.extensionName == req.Key).FirstOrDefault().publisherId;
                            string extensionName = template.Extensions.Where(x => x.extensionName == req.Key).FirstOrDefault().extensionId;
                            try
                            {
                                InstalledExtension extension = null;
                                extension = await client.InstallExtensionByNameAsync(publisherName, extensionName);
                            }
                            catch (OperationCanceledException cancelException)
                            {
                                model.id.ErrorId().AddMessage("Error while Installing extensions - operation cancelled: " + cancelException.Message + Environment.NewLine);
                            }
                            catch (Exception exc)
                            {
                                model.id.ErrorId().AddMessage("Error while Installing extensions: " + exc.Message);
                            }
                        });
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                // logger.Info(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + "\t" + ex.Message + "\t" + "\n" + ex.StackTrace + "\n");
                model.id.ErrorId().AddMessage("Error while Installing extensions: " + ex.Message);
                return false;
            }
        }
        /// <summary>
        /// WIKI set up operations 
        /// Project as Wiki and Code as Wiki
        /// </summary>
        /// <param name="model"></param>
        /// <param name="_wikiConfiguration"></param>
        public void CreateProjetWiki(string templatesFolder, Project model, ADOConfiguration _wikiConfiguration)
        {
            try
            {
                ManageWiki manageWiki = new ManageWiki(_wikiConfiguration);
                string projectWikiFolderPath = GetJsonFilePath(model.IsPrivatePath, model.PrivateTemplatePath, model.SelectedTemplate, "Wiki\\ProjectWiki");
                //templatesFolder + model.SelectedTemplate + "\\Wiki\\ProjectWiki";
                if (Directory.Exists(projectWikiFolderPath))
                {
                    string createWiki = string.Format(templatesFolder + "\\CreateWiki.json"); // check is path
                    if (File.Exists(createWiki))
                    {
                        string jsonString = File.ReadAllText(createWiki);
                        jsonString = jsonString.Replace("$ProjectID$", model.Environment.ProjectId)
                            .Replace("$Name$", model.Environment.ProjectName);
                        ProjectwikiResponse.Projectwiki projectWikiResponse = manageWiki.CreateProjectWiki(jsonString, model.Environment.ProjectId);
                        string[] subDirectories = Directory.GetDirectories(projectWikiFolderPath);
                        foreach (var dir in subDirectories)
                        {
                            //dirName==parentName//
                            string[] dirSplit = dir.Split('\\');
                            string dirName = dirSplit[dirSplit.Length - 1];
                            string sampleContent = File.ReadAllText(templatesFolder + "\\SampleContent.json");
                            sampleContent = sampleContent.Replace("$Content$", "Sample wiki content");
                            bool isPage = manageWiki.CreateUpdatePages(sampleContent, model.Environment.ProjectName, projectWikiResponse.id, dirName);//check is created

                            if (isPage)
                            {
                                string[] getFiles = Directory.GetFiles(dir);
                                if (getFiles.Length > 0)
                                {
                                    List<string> childFileNames = new List<string>();
                                    foreach (var file in getFiles)
                                    {
                                        string[] fileNameExtension = file.Split('\\');
                                        string fileName = (fileNameExtension[fileNameExtension.Length - 1].Split('.'))[0];
                                        string fileContent = model.ReadJsonFile(file);
                                        bool isCreated = false;
                                        Dictionary<string, string> dic = new Dictionary<string, string>();
                                        dic.Add("content", fileContent);
                                        string newContent = JsonConvert.SerializeObject(dic);
                                        if (fileName == dirName)
                                        {
                                            manageWiki.DeletePage(model.Environment.ProjectName, projectWikiResponse.id, fileName);
                                            isCreated = manageWiki.CreateUpdatePages(newContent, model.Environment.ProjectName, projectWikiResponse.id, fileName);
                                        }
                                        else
                                        {
                                            isCreated = manageWiki.CreateUpdatePages(newContent, model.Environment.ProjectName, projectWikiResponse.id, fileName);
                                        }
                                        if (isCreated)
                                        {
                                            childFileNames.Add(fileName);
                                        }
                                    }
                                    if (childFileNames.Count > 0)
                                    {
                                        foreach (var child in childFileNames)
                                        {
                                            if (child != dirName)
                                            {
                                                string movePages = File.ReadAllText(templatesFolder + "MovePages.json");
                                                if (!string.IsNullOrEmpty(movePages))
                                                {
                                                    movePages = movePages.Replace("$ParentFile$", dirName).Replace("$ChildFile$", child);
                                                    manageWiki.MovePages(movePages, model.Environment.ProjectId, projectWikiResponse.id);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                model.id.ErrorId().AddMessage(ex.Message);
            }
        }
        public void CreateCodeWiki(Project model, ADOConfiguration _wikiConfiguration)
        {
            try
            {
                string wikiFolder = GetJsonFilePath(model.IsPrivatePath, model.PrivateTemplatePath, model.SelectedTemplate, "Wiki");
                //templatesFolder + model.SelectedTemplate + "\\Wiki";
                if (Directory.Exists(wikiFolder))
                {
                    string[] wikiFilePaths = Directory.GetFiles(wikiFolder);
                    if (wikiFilePaths.Length > 0)
                    {
                        ManageWiki manageWiki = new ManageWiki(_wikiConfiguration);

                        foreach (string wiki in wikiFilePaths)
                        {
                            string[] nameExtension = wiki.Split('\\');
                            string name = (nameExtension[nameExtension.Length - 1]).Split('.')[0];
                            string json = model.ReadJsonFile(wiki);
                            bool isImported = false;
                            foreach (string repository in model.Environment.repositoryIdList.Keys)
                            {
                                if (model.Environment.repositoryIdList.ContainsKey(repository) && !string.IsNullOrEmpty(model.Environment.repositoryIdList[repository]))
                                {
                                    if (model.Environment.reposImported.ContainsKey(model.Environment.repositoryIdList[repository]))
                                    {
                                        isImported = model.Environment.reposImported[model.Environment.repositoryIdList[repository]];
                                    }

                                    string placeHolder = string.Format("${0}$", repository);
                                    if (json.Contains(placeHolder))
                                    {
                                        json = json.Replace(placeHolder, model.Environment.repositoryIdList[repository])
                                            .Replace("$Name$", name).Replace("$ProjectID$", model.Environment.ProjectId);
                                        break;
                                    }
                                }
                            }
                            if (isImported)
                            {
                                bool isWiki = manageWiki.CreateCodeWiki(json);
                                if (isWiki)
                                {
                                    model.id.AddMessage("Created Wiki");
                                }
                                else if (!string.IsNullOrEmpty(manageWiki.LastFailureMessage))
                                {
                                    model.id.ErrorId().AddMessage("Error while creating wiki: " + manageWiki.LastFailureMessage);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // logger.Info(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + "\t" + ex.Message + "\t" + "\n" + ex.StackTrace + "\n");
                model.id.ErrorId().AddMessage("Error while creating wiki: " + ex.Message);
            }
        }
        void CreateDeploymentGroup(string templateFolder, Project model, ADOConfiguration _deploymentGroup)
        {
            string path = GetJsonFilePath(model.IsPrivatePath, model.PrivateTemplatePath, model.SelectedTemplate, "DeploymentGroups\\CreateDeploymentGroup.json");
            //templateFolder + model.SelectedTemplate + "\\DeploymentGroups\\CreateDeploymentGroup.json";
            if (File.Exists(path))
            {
                string json = model.ReadJsonFile(path);
                if (!string.IsNullOrEmpty(json))
                {
                    DeploymentGroup deploymentGroup = new DeploymentGroup(_deploymentGroup);
                    bool isCreated = deploymentGroup.CreateDeploymentGroup(json);
                    if (isCreated) { }
                    else if (!string.IsNullOrEmpty(deploymentGroup.LastFailureMessage))
                    { model.id.ErrorId().AddMessage("Error while creating deployment group: " + deploymentGroup.LastFailureMessage); }
                }
            }
        }

        string GetTemplateMessage(string TemplateName)
        {
            try
            {
                string groupDetails = "";
                TemplateSelection.Templates templates = new TemplateSelection.Templates();
                string templatesPath = ""; templatesPath = Path.Combine(Directory.GetCurrentDirectory(), "Templates");
                if (File.Exists(templatesPath + "TemplateSetting.json"))
                {
                    groupDetails = File.ReadAllText(templatesPath + "\\TemplateSetting.json");
                    templates = JsonConvert.DeserializeObject<TemplateSelection.Templates>(groupDetails);
                    foreach (var template in templates.GroupwiseTemplates.FirstOrDefault().Template)
                    {
                        if (template.TemplateFolder.ToLower() == TemplateName.ToLower())
                        {
                            return template.Message;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return string.Empty;
        }

        bool AddUserToProject(ADOConfiguration con, Project model)
        {
            try
            {
                HttpServices httpService = new HttpServices(con);
                string PAT = string.Empty;
                string descriptorUrl = string.Format("_apis/graph/descriptors/{0}?api-version={1}", Convert.ToString(model.Environment.ProjectId), con.VersionNumber);
                var groups = httpService.Get(descriptorUrl);
                //dynamic obj = new dynamic();
                if (groups.IsSuccessStatusCode)
                {
                    dynamic obj = JsonConvert.DeserializeObject<dynamic>(groups.Content.ReadAsStringAsync().Result);
                    string getGroupDescriptor = string.Format("_apis/graph/groups?scopeDescriptor={0}&api-version={1}", Convert.ToString(obj.value), con.VersionNumber);
                    var getAllGroups = httpService.Get(getGroupDescriptor);
                    if (getAllGroups.IsSuccessStatusCode)
                    {
                        GetAllGroups.GroupList allGroups = JsonConvert.DeserializeObject<GetAllGroups.GroupList>(getAllGroups.Content.ReadAsStringAsync().Result);
                        foreach (var group in allGroups.value)
                        {
                            if (group.displayName.ToLower() == "project administrators")
                            {
                                string urpParams = string.Format("_apis/graph/users?groupDescriptors={0}&api-version={1}", Convert.ToString(group.descriptor), con.VersionNumber);
                                var json = CreatePrincipalReqBody(model.Email);
                                var response = httpService.Post(json, urpParams);
                            }
                            if (group.displayName.ToLower() == model.ProjectName.ToLower() + " team")
                            {
                                string urpParams = string.Format("_apis/graph/users?groupDescriptors={0}&api-version={1}", Convert.ToString(group.descriptor), con.VersionNumber);
                                var json = CreatePrincipalReqBody(model.Email);
                                var response = httpService.Post(json, urpParams);
                            }
                        }
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return false;
        }

        static string CreatePrincipalReqBody(string name)
        {
            return "{\"principalName\": \"" + name + "\"}";
        }
        #endregion

        void CreateVaribaleGroups(Project model, ADOConfiguration _variableGroups)
        {
            VariableGroups variableGroups = new VariableGroups(_variableGroups);
            model.Environment.variableGroups = new Dictionary<int, string>();
            string filePath = GetJsonFilePath(model.IsPrivatePath, model.PrivateTemplatePath, model.SelectedTemplate, "VariableGroups\\VariableGroup.json");
            if (File.Exists(filePath))
            {
                string jsonString = model.ReadJsonFile(filePath);
                GetVariableGroups.Groups groups = JsonConvert.DeserializeObject<GetVariableGroups.Groups>(jsonString);
                if (groups.count > 0)
                {
                    foreach (var group in groups.value)
                    {
                        GetVariableGroups.VariableGroupsCreateResponse response = variableGroups.PostVariableGroups(JsonConvert.SerializeObject(group));
                        if (!string.IsNullOrEmpty(response.name))
                        {
                            model.Environment.variableGroups.Add(response.id, response.name);
                        }
                    }
                }
            }
        }


        /// <summary>
        /// Checkign for template existance - if template is present in private path, return true else return false
        /// </summary>
        /// <param name="templatName"></param>
        /// <returns></returns>
        public bool WhereDoseTemplateBelongTo(string templatName)
        {

            string privatePath = Path.Combine(Directory.GetCurrentDirectory(), "PrivateTemplates");
            string privateTemplate = Path.Combine(privatePath, templatName);

            if (!Directory.Exists(privatePath))
            {
                Directory.CreateDirectory(privatePath);
            }
            string[] privatedirs = Directory.GetDirectories(privatePath);

            if (privatedirs.Contains(privateTemplate))
            {
                return true;
            }
            return false;
        }

        void CreateDeliveryPlans(Project model, ADOConfiguration _projectConfig)
        {
            try
            {
                Plans plans = new Plans(_projectConfig);
                string plansPath = GetJsonFilePath(model.IsPrivatePath, model.PrivateTemplatePath, templateUsed, "DeliveryPlans");
                if (Directory.Exists(plansPath))
                {
                    string[] files = Directory.GetFiles(plansPath);
                    if (files.Length > 0)
                    {
                        _projectConfig.ProjectId = model.Environment.ProjectId;
                        RestAPI.Extractor.ClassificationNodes nodes = new RestAPI.Extractor.ClassificationNodes(_projectConfig);
                        string defaultTeamID = string.Empty;
                        var teamsRes = nodes.GetTeams();
                        RootTeams rootTeams = new RootTeams();
                        if (teamsRes != null && teamsRes.IsSuccessStatusCode)
                        {
                            rootTeams = JsonConvert.DeserializeObject<RootTeams>(teamsRes.Content.ReadAsStringAsync().Result);
                        }
                        foreach (var dfile in files)
                        {
                            string content = File.ReadAllText(dfile);
                            foreach (var team in rootTeams.value)
                            {
                                content = content.Replace($"${team.name}$", team.id);
                            }
                            Dictionary<object, object> dict = new Dictionary<object, object>();
                            dict = JsonConvert.DeserializeObject<Dictionary<object, object>>(content);
                            var planCreated = plans.AddDeliveryPlan(content, _projectConfig.Project);
                            if (!string.IsNullOrEmpty(planCreated.id))
                            {
                                if (dict.ContainsKey("revision"))
                                {
                                    dict["revision"] = 1;
                                }
                                else
                                {
                                    dict.Add("revision", 1);
                                }
                                plans.UpdateDeliveryPlan(JsonConvert.SerializeObject(dict), _projectConfig.Project, planCreated.id);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);

            }
        }

        public bool CheckForInstalledExtensions(string extensionJsonFile, string token, string account)
        {
            bool ExtensionRequired = false;
            try
            {
                string accountName = account;
                string pat = token;
                string listedExtension = File.ReadAllText(extensionJsonFile);
                var template = JsonConvert.DeserializeObject<RequiredExtensions.Extension>(listedExtension);
                string requiresExtensionNames = string.Empty;
                string requiredMicrosoftExt = string.Empty;
                string requiredThirdPartyExt = string.Empty;
                string finalExtensionString = string.Empty;

                //Check for existing extensions
                if (template.Extensions.Count > 0)
                {
                    Dictionary<string, bool> dict = new Dictionary<string, bool>();
                    foreach (RequiredExtensions.ExtensionWithLink ext in template.Extensions)
                    {
                        dict.Add(ext.extensionName, false);
                    }
                    //pat = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes(string.Format("{0}:{1}", "", pat)));//ADOConfiguration.PersonalAccessToken;

                    //var connection = new VssConnection(new Uri(string.Format("https://{0}.visualstudio.com", accountName)), new Microsoft.VisualStudio.Services.OAuth.VssOAuthAccessTokenCredential(pat));// VssOAuthCredential(PAT));
                    var connection = new VssConnection(new Uri(string.Format("https://{0}.visualstudio.com", accountName)), new VssBasicCredential(string.Empty, pat));// VssOAuthCredential(PAT));

                    var client = connection.GetClient<ExtensionManagementHttpClient>();
                    var installed = client.GetInstalledExtensionsAsync().Result;
                    var extensions = installed.Where(x => x.Flags == 0).ToList();

                    var trustedFlagExtensions = installed.Where(x => x.Flags == ExtensionFlags.Trusted).ToList();
                    var builtInExtensions = installed.Where(x => x.Flags.ToString() == "BuiltIn, Trusted").ToList();

                    extensions.AddRange(trustedFlagExtensions);
                    extensions.AddRange(builtInExtensions);

                    foreach (var ext in extensions)
                    {
                        foreach (var extension in template.Extensions)
                        {
                            if (extension.extensionName.ToLower() == ext.ExtensionDisplayName.ToLower())
                            {
                                dict[extension.extensionName] = true;
                            }
                        }
                    }
                    var required = dict.Where(x => x.Value == false).ToList();
                    if (required.Count > 0)
                    {
                        ExtensionRequired = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);

                ExtensionRequired = false;
            }
            return ExtensionRequired;
        }
    }
}

