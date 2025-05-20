using ADOGenerator.IServices;
using ADOGenerator.Models;
using log4net;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using RestAPI.DeliveryPlans;
using RestAPI.ExtensionManagement;
using RestAPI.Extractor;
using RestAPI.ProjectsAndTeams;
using RestAPI.Viewmodel.Extractor;
using RestAPI.Viewmodel.GitHub;
using RestAPI.Viewmodel.Plans;
using RestAPI.Viewmodel.ProjectAndTeams;
using RestAPI;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static RestAPI.Viewmodel.Extractor.GetServiceEndpoints;
using static RestAPI.Viewmodel.Plans.DeliveryPlans;
using Configuration = RestAPI.ADOConfiguration;
using RestAPI.QueriesAndWidgets;
using Microsoft.Extensions.Configuration;
using static ADOGenerator.Models.TemplateSelection;
//using ADOProjectConfigurations = ADOGenerator.Models.ADOProjectConfigurations;

namespace ADOGenerator.Services
{
    public class ExtractorService : IExtractorService
    {
        #region STATIC DECLARATIONS
        private readonly IConfiguration _config;
        public static ILog logger = LogManager.GetLogger("ErrorLog");
        public static readonly object objLock = new object();
        public static Dictionary<string, string> statusMessages;
        public List<string> errorMessages = new List<string>();
        public static string extractedTemplatePath = string.Empty;
        private ProjectProperties.Properties projectProperties = new ProjectProperties.Properties();
        public static string currentPath = Directory.GetCurrentDirectory().Replace("bin\\Debug\\net8.0", "").Replace("bin\\Release\\net8.0", "").Replace("bin\\Debug", "").Replace("bin\\Release", "");
        
        public ExtractorService(IConfiguration config)
        {
            _config = config;
            if (currentPath.EndsWith("ADOGenerator"))
                currentPath = currentPath + "\\";
        }
        #endregion  STATIC DECLARATIONS

        #region ANALYSIS - GET COUNTS
        public ProjectConfigurations ProjectConfiguration(Project model)
        {
            string repoVersion = _config["AppSettings:RepoVersion"];
            string buildVersion = _config["AppSettings:BuildVersion"];
            string releaseVersion = _config["AppSettings:ReleaseVersion"];
            string wikiVersion = _config["AppSettings:WikiVersion"];
            string boardVersion = _config["AppSettings:BoardVersion"];
            string workItemsVersion = _config["AppSettings:WorkItemsVersion"];
            string releaseHost = _config["AppSettings:ReleaseHost"];
            string defaultHost = _config["AppSettings:DefaultHost"];
            string extensionHost = _config["AppSettings:ExtensionHost"];
            string getReleaseVersion = _config["AppSettings:GetRelease"];
            string agentQueueVersion = _config["AppSettings:AgentQueueVersion"];
            string extensionVersion = _config["AppSettings:ExtensionVersion"];
            string endpointVersion = _config["AppSettings:EndPointVersion"];
            string queriesVersion = _config["AppSettings:QueriesVersion"];
            string variableGroupsApiVersion = _config["AppSettings:VariableGroupsApiVersion"];
            ProjectConfigurations projectConfig = new ProjectConfigurations();

            projectConfig.AgentQueueConfig = new Configuration() { UriString = defaultHost + model.accountName + "/", PersonalAccessToken = model.accessToken, Project = model.ProjectName, AccountName = model.accountName, Id = model.id, VersionNumber = wikiVersion, _adoAuthScheme = model.adoAuthScheme };
            projectConfig.WorkItemConfig = new Configuration() { UriString = defaultHost + model.accountName + "/", PersonalAccessToken = model.accessToken, Project = model.ProjectName, AccountName = model.accountName, Id = model.id, VersionNumber = wikiVersion, _adoAuthScheme = model.adoAuthScheme };
            projectConfig.BuildDefinitionConfig = new Configuration() { UriString = defaultHost + model.accountName + "/", PersonalAccessToken = model.accessToken, Project = model.ProjectName, AccountName = model.accountName, Id = model.id, VersionNumber = buildVersion, _adoAuthScheme = model.adoAuthScheme };
            projectConfig.ReleaseDefinitionConfig = new Configuration() { UriString = releaseHost + model.accountName + "/", PersonalAccessToken = model.accessToken, Project = model.ProjectName, AccountName = model.accountName, Id = model.id, VersionNumber = releaseVersion, _adoAuthScheme = model.adoAuthScheme };
            projectConfig.RepoConfig = new Configuration() { UriString = defaultHost + model.accountName + "/", PersonalAccessToken = model.accessToken, Project = model.ProjectName, AccountName = model.accountName, Id = model.id, VersionNumber = repoVersion, _adoAuthScheme = model.adoAuthScheme };
            projectConfig.BoardConfig = new Configuration() { UriString = defaultHost + model.accountName + "/", PersonalAccessToken = model.accessToken, Project = model.ProjectName, AccountName = model.accountName, Id = model.id, VersionNumber = boardVersion, _adoAuthScheme = model.adoAuthScheme };
            projectConfig.Config = new Configuration() { UriString = defaultHost + model.accountName + "/", PersonalAccessToken = model.accessToken, Project = model.ProjectName, AccountName = model.accountName, Id = model.id, _adoAuthScheme = model.adoAuthScheme };
            projectConfig.GetReleaseConfig = new Configuration() { UriString = releaseHost + model.accountName + "/", PersonalAccessToken = model.accessToken, Project = model.ProjectName, AccountName = model.accountName, Id = model.id, VersionNumber = getReleaseVersion, _adoAuthScheme = model.adoAuthScheme };
            projectConfig.ExtensionConfig = new Configuration() { UriString = extensionHost + model.accountName + "/", PersonalAccessToken = model.accessToken, Project = model.ProjectName, AccountName = model.accountName, Id = model.id, VersionNumber = extensionVersion, _adoAuthScheme = model.adoAuthScheme };
            projectConfig.EndpointConfig = new Configuration() { UriString = defaultHost + model.accountName + "/", PersonalAccessToken = model.accessToken, Project = model.ProjectName, AccountName = model.accountName, Id = model.id, VersionNumber = endpointVersion, _adoAuthScheme = model.adoAuthScheme };
            projectConfig.QueriesConfig = new Configuration() { UriString = defaultHost + model.accountName + "/", PersonalAccessToken = model.accessToken, Project = model.ProjectName, AccountName = model.accountName, Id = model.id, VersionNumber = queriesVersion, _adoAuthScheme = model.adoAuthScheme };
            projectConfig.VariableGroupConfig = new Configuration() { UriString = defaultHost + model.accountName + "/", PersonalAccessToken = model.accessToken, Project = model.ProjectName, AccountName = model.accountName, Id = model.id, VersionNumber = variableGroupsApiVersion, _adoAuthScheme = model.adoAuthScheme };

            return projectConfig;
        }
        public bool IsTemplateExists(string templateName)
        {
            string templatesDirectory = currentPath + @"Templates\";
            string templateSettingsPath = templatesDirectory + "TemplateSetting.json";
            if (!File.Exists(templateSettingsPath))
            {
                return false;
            }
            string templateSettingsContent = File.ReadAllText(templateSettingsPath);
            JObject templateSettings = JObject.Parse(templateSettingsContent);
            // Check if the template name exists in the settings by looping the GroupwiseTemplates
            bool templateExists = false;
            string templateFolder = string.Empty;
            foreach (var group in templateSettings["GroupwiseTemplates"])
            {
                var templates = group["Template"];
                foreach (var template in templates)
                {
                    if (template["Name"].ToString() == templateName)
                    {
                        templateExists = true;
                        templateFolder = template["TemplateFolder"].ToString();
                        break;
                    }
                }
                if (templateExists)
                {
                    break;
                }
            }
            if(!templateExists)
            {
                return false;
            }
            string templatePath = templatesDirectory + templateFolder;
            if (Directory.Exists(templatePath))
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        public int GetTeamsCount(ProjectConfigurations appConfig)
        {
            RestAPI.Extractor.ClassificationNodes nodes = new RestAPI.Extractor.ClassificationNodes(appConfig.BoardConfig);
            TeamList teamList = nodes.ExportTeamList("");
            int count = 0;
            if (teamList.value != null)
            {
                count = teamList.value.Count;
            }
            return count;
        }
        public int GetIterationsCount(ProjectConfigurations appConfig)
        {
            RestAPI.Extractor.ClassificationNodes nodes = new RestAPI.Extractor.ClassificationNodes(appConfig.BoardConfig);
            GetINumIteration.Iterations iterations = new GetINumIteration.Iterations();
            iterations = nodes.GetiterationCount();
            if (iterations.count > 0)
            {
                return iterations.count;
            }
            else
            {
                if (!(string.IsNullOrEmpty(nodes.LastFailureMessage)))
                {
                    errorMessages.Add("Error while fetching iteration(s) count: " + nodes.LastFailureMessage);
                }
                return 0;
            }
        }
        public int GetBuildDefinitionCount(ProjectConfigurations appConfig)
        {
            int BuildDefCount = 0;
            BuildandReleaseDefs buildandReleaseDefs = new BuildandReleaseDefs(appConfig.BuildDefinitionConfig);
            GetBuildDefResponse.BuildDef buildDef = new GetBuildDefResponse.BuildDef();
            buildDef = buildandReleaseDefs.GetBuildDefCount();
            if (buildDef.count > 0)
            {
                BuildDefCount = buildDef.count;
            }
            else if (!string.IsNullOrEmpty(buildandReleaseDefs.LastFailureMessage))
            {
                errorMessages.Add("Error while fetching build definition count: " + buildandReleaseDefs.LastFailureMessage);
            }
            return BuildDefCount;
        }
        public int GetReleaseDefinitionCount(ProjectConfigurations appConfig)
        {
            int ReleaseDefCount = 0;
            BuildandReleaseDefs buildandReleaseDefs = new BuildandReleaseDefs(appConfig.ReleaseDefinitionConfig);
            GetReleaseDefResponse.ReleaseDef releaseDef = new GetReleaseDefResponse.ReleaseDef();
            releaseDef = buildandReleaseDefs.GetReleaseDefCount();
            if (releaseDef.count > 0)
            {
                ReleaseDefCount = releaseDef.count;
            }
            else if (!string.IsNullOrEmpty(buildandReleaseDefs.LastFailureMessage))
            {
                errorMessages.Add("Error while fetching release definition count: " + buildandReleaseDefs.LastFailureMessage);
            }
            return ReleaseDefCount;
        }
        public Dictionary<string, int> GetWorkItemsCount(ProjectConfigurations appConfig)
        {
            string[] workItemtypes = GetAllWorkItemsName(appConfig);//{ "Epic", "Feature", "Product Backlog Item", "Task", "Test Case", "Bug", "User Story", "Test Suite", "Test Plan", "Issue" };
            GetWorkItemsCount itemsCount = new GetWorkItemsCount(appConfig.WorkItemConfig);
            Dictionary<string, int> fetchedWorkItemsCount = new Dictionary<string, int>();
            if (workItemtypes.Length > 0)
            {
                foreach (var workItem in workItemtypes)
                {
                    itemsCount.LastFailureMessage = "";
                    WorkItemFetchResponse.WorkItems WITCount = itemsCount.GetWorkItemsfromSource(workItem);
                    if (WITCount.count > 0)
                    {
                        fetchedWorkItemsCount.Add(workItem, WITCount.count);
                    }
                    else if (!string.IsNullOrEmpty(itemsCount.LastFailureMessage))
                    {
                        errorMessages.Add(string.Format("Error while querying work item - {0}: {1}", workItem, itemsCount.LastFailureMessage));
                    }
                }
            }

            return fetchedWorkItemsCount;
        }
        #endregion ANALYSIS - GET COUNTS

        #region GENERATE ARTIFACTS
        public string[] GenerateTemplateArifacts(Project model)
        {
            model.id.AddMessage(Environment.NewLine+"Template Generation Started");
            extractedTemplatePath = currentPath + @"Templates\";
            string extractedFolderName = extractedTemplatePath + $"CT-{model.ProjectName.Replace(" ", "-")}";
            if (Directory.Exists(extractedFolderName))
            {
                Directory.Delete(extractedFolderName, true);
            }
            ProjectConfigurations appConfig = ProjectConfiguration(model);

            GetInstalledExtensions(appConfig, extractedFolderName);

            ExportQuries(appConfig, extractedFolderName);
            ExportTeams(appConfig.BoardConfig, model, extractedFolderName);

            if (ExportIterations(appConfig, extractedFolderName))
            {
                model.id.AddMessage("Iterations Definition Exported");
            }
            string filePathToRead = currentPath + @"\\PreSetting";

            string projectSetting = "";
            projectSetting = filePathToRead + "\\ProjectSettings.json";
            projectSetting = File.ReadAllText(projectSetting);
            projectSetting = projectSetting.Replace("$type$", model.ProcessTemplate).Replace("$id$", projectProperties.value.Where(x => x.name == "System.ProcessTemplateType").FirstOrDefault().value);
            File.WriteAllText(extractedFolderName + "\\ProjectSettings.json", projectSetting);

            string projectTemplate = "";
            projectTemplate = filePathToRead + "\\ProjectTemplate.json";
            projectTemplate = File.ReadAllText(projectTemplate);
            File.WriteAllText(extractedFolderName + "\\ProjectTemplate.json", projectTemplate);

            string teamArea = "";
            teamArea = filePathToRead + "\\TeamArea.json";
            teamArea = File.ReadAllText(teamArea);
            File.WriteAllText(extractedFolderName + "\\TeamArea.json", teamArea);
            model.id.AddMessage("Team Areas Exported");

            if(ExportWorkItems(appConfig, extractedFolderName))
                model.id.AddMessage("Work Items Exported");

            if(ExportDeliveryPlans(appConfig, extractedFolderName))
                model.id.AddMessage("Delivery Plans Exported");

            if(ExportRepositoryList(appConfig, extractedFolderName))
                model.id.AddMessage("Repository Exported");

            GetServiceEndpoints(appConfig, extractedFolderName);
            int count = GetBuildDefinitions(appConfig, extractedFolderName);
            if (count >= 1)
            {
                model.id.AddMessage("Build Definition Exported");
            }

            int relCount = GeneralizingGetReleaseDefinitions(appConfig, extractedFolderName);
            if (relCount >= 1)
            {
                model.id.AddMessage("Release Definition Exported");
            }

            // Generate custom template JSON
            var customTemplateJson = new
            {
                Groups = new[]
                {
                    "Custom Templates"
                },
                GroupwiseTemplates = new[]
                {
                    new
                    {
                        Groups = "Custom Templates",
                        Template = new[]
                        {
                            new
                            {
                                Name = model.ProjectName,
                                TemplateFolder = $"CT-{model.ProjectName.Replace(" ", "-")}",
                                Description = $"This is a custom template for the project '{model.ProjectName}'.",
                            }
                        }
                    }
                }
            };
            return new string[] { model.id, JsonConvert.SerializeObject(customTemplateJson, Formatting.Indented) , extractedFolderName };
        }
        public List<RequiredExtensions.ExtensionWithLink> GetInstalledExtensions(ProjectConfigurations appConfig,string extractedFolderName)
        {
            try
            {
                GetListExtenison listExtenison = new GetListExtenison(appConfig.ExtensionConfig);
                List<RequiredExtensions.ExtensionWithLink> extensionList = new List<RequiredExtensions.ExtensionWithLink>();
                GetExtensions.ExtensionsList returnExtensionsList = listExtenison.GetInstalledExtensions();
                if (returnExtensionsList != null && returnExtensionsList.count > 0)
                {
                    List<GetExtensions.Value> builtInExtensions = returnExtensionsList.value.Where(x => x.flags == null).ToList();
                    List<GetExtensions.Value> trustedExtensions = returnExtensionsList.value.Where(x => x.flags != null && x.flags.ToString() == "trusted").ToList();
                    builtInExtensions.AddRange(trustedExtensions);
                    returnExtensionsList.value = builtInExtensions;

                    foreach (GetExtensions.Value data in returnExtensionsList.value)
                    {
                        RequiredExtensions.ExtensionWithLink extension = new RequiredExtensions.ExtensionWithLink();
                        if (data.extensionName.ToLower() != "analytics")
                        {
                            extension.extensionId = data.extensionId;
                            extension.extensionName = data.extensionName;
                            extension.publisherId = data.publisherId;
                            extension.publisherName = data.publisherName;
                            extension.link = "<a href='" + string.Format("https://marketplace.visualstudio.com/items?itemName={0}.{1}", data.publisherId, data.extensionId) + "' target='_blank'><b>" + data.extensionName + "</b></a>";
                            extension.License = "<a href='" + string.Format("https://marketplace.visualstudio.com/items?itemName={0}.{1}", data.publisherId, data.extensionId) + "' target='_blank'>License Terms</a>";
                            extensionList.Add(extension);
                        }
                    }
                    RequiredExtensions.listExtension listExtension = new RequiredExtensions.listExtension();
                    if (extensionList.Count > 0)
                    {
                        listExtension.Extensions = extensionList;
                        if (!Directory.Exists(extractedFolderName))
                        {
                            Directory.CreateDirectory(extractedFolderName);
                        }
                        string fetchedJson = JsonConvert.SerializeObject(listExtension, Formatting.Indented);

                        File.WriteAllText(extractedFolderName + "\\Extensions.json", JsonConvert.SerializeObject(listExtension, Formatting.Indented));
                        appConfig.ExtensionConfig.Id.AddMessage("Extensions Exported");
                    }
                }
                else if (!string.IsNullOrEmpty(listExtenison.LastFailureMessage))
                {
                    appConfig.ExtensionConfig.Id.ErrorId().AddMessage("Some error occured while fetching extensions"+Environment.NewLine+listExtenison.LastFailureMessage);
                }
                return extensionList;
            }
            catch (Exception ex)
            {
                appConfig.ExtensionConfig.Id.ErrorId().AddMessage(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + ex.Message + "\n" + ex.StackTrace + "\n");
            }
            return new List<RequiredExtensions.ExtensionWithLink>();
        }
        public void ExportQuries(ProjectConfigurations appConfig, string extractedFolderName)
        {
            try
            {
                Queries queries = new Queries(appConfig.QueriesConfig);
                GetQueries.Queries listQueries = queries.GetQueriesWiql();
                if (listQueries.count > 0)
                {
                    foreach (var _queries in listQueries.value)
                    {
                        if (_queries.hasChildren)
                        {
                            foreach (var query in _queries.children)
                            {
                                if (!query.hasChildren)
                                {
                                    if (query.wiql != null)
                                    {
                                        query.wiql = query.wiql.Replace(appConfig.QueriesConfig.Project, "$projectId$");
                                        JObject jobj = new JObject();
                                        jobj["name"] = query.name;
                                        jobj["wiql"] = query.wiql;
                                        if (!Directory.Exists(extractedFolderName + "\\Dashboard\\Queries"))
                                        {
                                            Directory.CreateDirectory(extractedFolderName + "\\Dashboard");
                                            File.WriteAllText(extractedFolderName + "\\Dashboard\\Dashboard.json", JsonConvert.SerializeObject("text", Formatting.Indented));
                                        }
                                        if (!Directory.Exists(extractedFolderName + "\\Dashboard\\Queries"))
                                        {
                                            Directory.CreateDirectory(extractedFolderName + "\\Dashboard\\Queries");
                                            File.WriteAllText(extractedFolderName + "\\Dashboard\\Queries\\" + query.name + ".json", JsonConvert.SerializeObject(jobj, Formatting.Indented));
                                        }
                                        else
                                        {
                                            File.WriteAllText(extractedFolderName + "\\Dashboard\\Queries\\" + query.name + ".json", JsonConvert.SerializeObject(jobj, Formatting.Indented));
                                        }
                                    }
                                }
                                else
                                {
                                    foreach (var child1 in query.children)
                                    {
                                        if (child1.wiql != null)
                                        {
                                            child1.wiql = child1.wiql.Replace(appConfig.QueriesConfig.Project, "$projectId$");
                                            JObject jobj = new JObject();
                                            jobj["name"] = child1.name;
                                            jobj["wiql"] = child1.wiql;
                                            if (!Directory.Exists(extractedFolderName + "\\Dashboard\\Queries"))
                                            {
                                                Directory.CreateDirectory(extractedFolderName + "\\Dashboard\\Queries");

                                                File.WriteAllText(extractedFolderName + "\\Dashboard\\Queries\\" + child1.name + ".json", JsonConvert.SerializeObject(jobj, Formatting.Indented));
                                            }
                                            else
                                            {
                                                File.WriteAllText(extractedFolderName + "\\Dashboard\\Queries\\" + child1.name + ".json", JsonConvert.SerializeObject(jobj, Formatting.Indented));
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    appConfig.QueriesConfig.Id.AddMessage("Queries Exported");
                }
                else if (!string.IsNullOrEmpty(queries.LastFailureMessage))
                {
                    appConfig.QueriesConfig.Id.ErrorId().AddMessage("Error while fetching queries");
                }
            }
            catch (Exception ex)
            {
                appConfig.QueriesConfig.Id.ErrorId().AddMessage(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + ex.Message + "\n" + ex.StackTrace + "\n");
            }

        }
        public bool ExportTeams(Configuration con, Project model,string extractedFolderName)
        {
            try
            {
                string defaultTeamID = string.Empty;
                RestAPI.Extractor.ClassificationNodes nodes = new RestAPI.Extractor.ClassificationNodes(con);
                TeamList _team = new TeamList();

                string defaultHost = _config["AppSettings:DefaultHost"];
                string ProjectPropertyVersion = _config["AppSettings:ProjectPropertyVersion"];
                ADOConfiguration config = new ADOConfiguration() { AccountName = model.accountName, PersonalAccessToken = model.accessToken, UriString = defaultHost + model.accountName, VersionNumber = ProjectPropertyVersion, ProjectId = model.ProjectId, _adoAuthScheme = model.adoAuthScheme };
                                
                Projects projects = new Projects(config);
                projectProperties = projects.GetProjectProperties();

                if (projectProperties.count > 0)
                {
                    defaultTeamID = projectProperties.value.Where(x => x.name == "System.Microsoft.TeamFoundation.Team.Default").FirstOrDefault().value;
                }
                _team = nodes.ExportTeamList(defaultTeamID);
                if (_team.value != null)
                {
                    con.Id.AddMessage("Teams");

                    string fetchedJson = JsonConvert.SerializeObject(_team.value, Formatting.Indented);
                    if (fetchedJson != "")
                    {
                        if (!Directory.Exists(extractedFolderName + "\\Teams"))
                        {
                            Directory.CreateDirectory(extractedFolderName + "\\Teams");
                        }
                        File.WriteAllText(extractedFolderName + "\\Teams\\Teams.json", fetchedJson);

                        List<string> boardTypes = new List<string>();
                        boardTypes.Add("Epics");
                        if (model.ProcessTemplate.ToLower() == "agile")
                        {
                            boardTypes.Add("Features");
                            boardTypes.Add("Stories");
                        }
                        else if (model.ProcessTemplate.ToLower() == "basic")
                        {
                            boardTypes.Add("Issues");
                        }
                        else if (model.ProcessTemplate.ToLower() == "scrum")
                        {
                            boardTypes.Add("Features");
                            boardTypes.Add("Backlog Items");
                        }

                        foreach (var team in _team.value)
                        {
                            List<BoardColumnResponseScrum.ColumnResponse> columnResponsesScrum = new List<BoardColumnResponseScrum.ColumnResponse>();
                            List<BoardColumnResponseAgile.ColumnResponse> columnResponsesAgile = new List<BoardColumnResponseAgile.ColumnResponse>();
                            List<BoardColumnResponseBasic.ColumnResponse> columnResponsesBasic = new List<BoardColumnResponseBasic.ColumnResponse>();
                            List<ExportBoardRows.Rows> boardRows = new List<ExportBoardRows.Rows>();

                            ExportTeamSetting.Setting listTeamSetting = new ExportTeamSetting.Setting();

                            List<JObject> jObjCardFieldList = new List<JObject>();
                            List<JObject> jObjcardStyleList = new List<JObject>();
                            string teamFolderPath = extractedFolderName + "\\Teams\\" + team.name;
                            if (!Directory.Exists(teamFolderPath))
                            {
                                Directory.CreateDirectory(teamFolderPath);
                            }
                            //Export Board Colums for each team
                            con.Team = team.name;

                            ClassificationNodes teamNodes = new ClassificationNodes(con);
                            foreach (var boardType in boardTypes)
                            {
                                var response = teamNodes.ExportBoardColums(boardType);
                                if (response.IsSuccessStatusCode && response.StatusCode == System.Net.HttpStatusCode.OK)
                                {
                                    if (model.ProcessTemplate.ToLower() == "scrum")
                                    {
                                        string res = response.Content.ReadAsStringAsync().Result;
                                        BoardColumnResponseScrum.ColumnResponse scrumColumns = JsonConvert.DeserializeObject<BoardColumnResponseScrum.ColumnResponse>(res);
                                        scrumColumns.BoardName = boardType;
                                        columnResponsesScrum.Add(scrumColumns);
                                    }
                                    else if (model.ProcessTemplate.ToLower() == "agile")
                                    {
                                        string res = response.Content.ReadAsStringAsync().Result;
                                        BoardColumnResponseAgile.ColumnResponse agileColumns = JsonConvert.DeserializeObject<BoardColumnResponseAgile.ColumnResponse>(res);
                                        agileColumns.BoardName = boardType;
                                        columnResponsesAgile.Add(agileColumns);
                                    }
                                    else if (model.ProcessTemplate.ToLower() == "basic")
                                    {
                                        string res = response.Content.ReadAsStringAsync().Result;
                                        BoardColumnResponseBasic.ColumnResponse basicColumns = JsonConvert.DeserializeObject<BoardColumnResponseBasic.ColumnResponse>(res);
                                        basicColumns.BoardName = boardType;
                                        columnResponsesBasic.Add(basicColumns);
                                    }
                                    con.Id.AddMessage("Board Columns Exported for "+ boardType);
                                    Thread.Sleep(2000);
                                }
                                else
                                {
                                    var errorMessage = response.Content.ReadAsStringAsync();
                                    string error = RestAPI.Utility.GeterroMessage(errorMessage.Result.ToString());
                                    teamNodes.LastFailureMessage = error;
                                    con.Id.ErrorId().AddMessage("Error occured while exporting Board Columns: " + teamNodes.LastFailureMessage);
                                }

                                //Export board rows for each team
                                ExportBoardRows.Rows rows = teamNodes.ExportBoardRows(boardType);
                                if (rows.value != null && rows.value.Count > 0)
                                {
                                    rows.BoardName = boardType;
                                    boardRows.Add(rows);
                                    con.Id.AddMessage("Board Rows Exported for "+boardType);
                                    Thread.Sleep(2000);
                                }
                                else if (!string.IsNullOrEmpty(teamNodes.LastFailureMessage))
                                {
                                    con.Id.ErrorId().AddMessage("Error occured while exporting Board Rows: " + teamNodes.LastFailureMessage);
                                }


                                //Export Card Fields for each team
                                var cardFieldResponse = teamNodes.ExportCardFields(boardType);
                                if (cardFieldResponse.IsSuccessStatusCode && cardFieldResponse.StatusCode == System.Net.HttpStatusCode.OK)
                                {
                                    string res = cardFieldResponse.Content.ReadAsStringAsync().Result;
                                    JObject jObj = JsonConvert.DeserializeObject<JObject>(res);
                                    jObj["BoardName"] = boardType;
                                    jObjCardFieldList.Add(jObj);
                                    con.Id.AddMessage("Card fields Definition Exported for "+ boardType);

                                }
                                else
                                {
                                    var errorMessage = cardFieldResponse.Content.ReadAsStringAsync();
                                    string error = RestAPI.Utility.GeterroMessage(errorMessage.Result.ToString());
                                    teamNodes.LastFailureMessage = error;
                                    con.Id.ErrorId().AddMessage("Error occured while exporting Card Fields: " + teamNodes.LastFailureMessage);
                                }

                                // Export card styles for each team
                                var cardStyleResponse = teamNodes.ExportCardStyle(boardType);
                                if (cardStyleResponse.IsSuccessStatusCode && cardStyleResponse.StatusCode == System.Net.HttpStatusCode.OK)
                                {
                                    string res = cardStyleResponse.Content.ReadAsStringAsync().Result;
                                    res = res.Replace(con.Project, "$ProjectName$");
                                    JObject jObj = JsonConvert.DeserializeObject<JObject>(res);
                                    jObj["BoardName"] = boardType;
                                    var style = jObj;
                                    style["url"] = "";
                                    style["_links"] = "{}";
                                    var tagStyle = style["rules"]["tagStyle"];
                                    if (tagStyle == null)
                                    {
                                        style["rules"]["tagStyle"] = new JArray();
                                    }
                                    jObjcardStyleList.Add(jObj);
                                    con.Id.AddMessage("Card style exported for "+ boardType);

                                }
                                else
                                {
                                    var errorMessage = cardStyleResponse.Content.ReadAsStringAsync();
                                    string error = RestAPI.Utility.GeterroMessage(errorMessage.Result.ToString());
                                    teamNodes.LastFailureMessage = error;
                                    con.Id.ErrorId().AddMessage("Error occured while exporting Card Styles: " + teamNodes.LastFailureMessage);
                                }
                            }
                            //Export Team Setting for each team
                            if (model.ProcessTemplate.ToLower() != "basic")
                            {
                                ExportTeamSetting.Setting teamSetting = teamNodes.ExportTeamSetting();
                                if (teamSetting.backlogVisibilities != null)
                                {
                                    listTeamSetting = teamSetting;
                                    con.Id.AddMessage("Team Settings Definition Exported");
                                }
                            }
                            else if (!string.IsNullOrEmpty(teamNodes.LastFailureMessage))
                            {
                                con.Id.ErrorId().AddMessage("Error occured while exporting Team Setting: " + teamNodes.LastFailureMessage);
                            }

                            if (columnResponsesAgile.Count > 0)
                            {
                                File.WriteAllText(teamFolderPath + "\\BoardColumns.json", JsonConvert.SerializeObject(columnResponsesAgile, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }));
                            }
                            if (columnResponsesScrum.Count > 0)
                            {
                                File.WriteAllText(teamFolderPath + "\\BoardColumns.json", JsonConvert.SerializeObject(columnResponsesScrum, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }));
                            }
                            if (columnResponsesBasic.Count > 0)
                            {
                                File.WriteAllText(teamFolderPath + "\\BoardColumns.json", JsonConvert.SerializeObject(columnResponsesBasic, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }));
                            }
                            if (boardRows.Count > 0)
                            {
                                File.WriteAllText(teamFolderPath + "\\BoardRows.json", JsonConvert.SerializeObject(boardRows, Formatting.Indented));
                            }
                            if (!string.IsNullOrEmpty(listTeamSetting.bugsBehavior))
                            {
                                File.WriteAllText(teamFolderPath + "\\TeamSetting.json", JsonConvert.SerializeObject(listTeamSetting, Formatting.Indented));
                            }
                            if (jObjCardFieldList.Count > 0)
                            {
                                File.WriteAllText(teamFolderPath + "\\CardFields.json", JsonConvert.SerializeObject(jObjCardFieldList, Formatting.Indented));
                            }
                            if (jObjcardStyleList.Count > 0)
                            {
                                File.WriteAllText(teamFolderPath + "\\CardStyles.json", JsonConvert.SerializeObject(jObjcardStyleList, Formatting.Indented));
                            }
                        }

                        return true;
                    }
                    else if (!string.IsNullOrEmpty(nodes.LastFailureMessage))
                    {
                        con.Id.ErrorId().AddMessage(nodes.LastFailureMessage);
                        string error = nodes.LastFailureMessage;
                        return false;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    con.Id.ErrorId().AddMessage(nodes.LastFailureMessage);
                    return false;
                }
            }
            catch (Exception ex)
            {
                con.Id.ErrorId().AddMessage(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + ex.Message + "\n" + ex.StackTrace + "\n");
            }
            return false;
        }
        public bool ExportIterations(ProjectConfigurations appConfig,string extractedFolderName)
        {
            try
            {
                ClassificationNodes nodes = new RestAPI.Extractor.ClassificationNodes(appConfig.BoardConfig);
                ExportedIterations.Iterations viewModel = nodes.ExportIterationsToSave();
                string fetchedJson = JsonConvert.SerializeObject(viewModel, Formatting.Indented);
                if (fetchedJson != "")
                {
                    if (!Directory.Exists(extractedFolderName))
                    {
                        Directory.CreateDirectory(extractedFolderName);
                    }
                    File.WriteAllText(extractedFolderName + "\\Iterations.json", fetchedJson);
                    return true;
                }
                else
                {
                    string error = nodes.LastFailureMessage;
                    appConfig.BoardConfig.Id.ErrorId().AddMessage(error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                appConfig.BoardConfig.Id.ErrorId().AddMessage(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + ex.Message + "\n" + ex.StackTrace + "\n");
            }
            return false;
        }
        public bool ExportWorkItems(ProjectConfigurations appConfig, string extractedFolderName)
        {
            bool isWorkItemExported = false;
            try
            {
                string[] workItemtypes = GetAllWorkItemsName(appConfig);//{ "Epic", "Feature", "Product Backlog Item", "Task", "Test Case", "Bug", "User Story", "Test Suite", "Test Plan", "Issue" };
                if (!Directory.Exists(extractedFolderName))
                {
                    Directory.CreateDirectory(extractedFolderName);
                }

                if (workItemtypes.Length > 0)
                {
                    foreach (var WIT in workItemtypes)
                    {
                        GetWorkItemsCount WorkitemsCount = new GetWorkItemsCount(appConfig.WorkItemConfig);
                        WorkItemFetchResponse.WorkItems fetchedWorkItem = WorkitemsCount.GetWorkItemsfromSource(WIT);
                        string workItemJson = JsonConvert.SerializeObject(fetchedWorkItem, Formatting.Indented);
                        if (fetchedWorkItem.count > 0)
                        {
                            workItemJson = workItemJson.Replace(appConfig.WorkItemConfig.Project + "\\", "$ProjectName$\\");
                            string item = WIT;
                            if (!Directory.Exists(extractedFolderName + "\\WorkItems"))
                            {
                                Directory.CreateDirectory(extractedFolderName + "\\WorkItems");
                            }
                            File.WriteAllText(extractedFolderName + "\\WorkItems\\" + item + ".json", workItemJson);
                        }
                        else if (!string.IsNullOrEmpty(WorkitemsCount.LastFailureMessage))
                        {
                            appConfig.WorkItemConfig.Id.ErrorId().AddMessage(WorkitemsCount.LastFailureMessage);
                        }
                    }
                    isWorkItemExported = true;
                }
            }
            catch (Exception ex)
            {
                appConfig.WorkItemConfig.Id.ErrorId().AddMessage(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + ex.Message + "\n" + ex.StackTrace + "\n");
            }
            return isWorkItemExported;
        }
        public bool ExportRepositoryList(ProjectConfigurations appConfig, string extractedFolderName)
        {
            bool isRepoExported = false;
            try
            {
                BuildandReleaseDefs repolist = new BuildandReleaseDefs(appConfig.RepoConfig);
                RepositoryList.Repository repos = repolist.GetRepoList();
                if (repos.count > 0)
                {
                    foreach (var repo in repos.value)
                    {
                        string preSettingPath = currentPath + @"\PreSetting";
                        string host = appConfig.RepoConfig.UriString + appConfig.RepoConfig.Project;
                        string sourceCodeJson = File.ReadAllText(preSettingPath + "\\ImportSourceCode.json");
                        sourceCodeJson = sourceCodeJson.Replace("$Host$", host).Replace("$Repo$", repo.name);
                        string endPointJson = File.ReadAllText(preSettingPath + "\\ServiceEndPoint.json");
                        endPointJson = endPointJson.Replace("$Host$", host).Replace("$Repo$", repo.name);
                        if (!Directory.Exists(extractedFolderName + "\\ImportSourceCode"))
                        {
                            Directory.CreateDirectory(extractedFolderName + "\\ImportSourceCode");
                            File.WriteAllText(extractedFolderName + "\\ImportSourceCode\\" + repo.name + ".json", sourceCodeJson);
                        }
                        else
                        {
                            File.WriteAllText(extractedFolderName + "\\ImportSourceCode\\" + repo.name + ".json", sourceCodeJson);
                        }
                        if (!Directory.Exists(extractedFolderName + "\\ServiceEndpoints"))
                        {
                            Directory.CreateDirectory(extractedFolderName + "\\ServiceEndpoints");
                            File.WriteAllText(extractedFolderName + "\\ServiceEndpoints\\" + repo.name + "-code.json", endPointJson);
                        }
                        else
                        {
                            File.WriteAllText(extractedFolderName + "\\ServiceEndpoints\\" + repo.name + "-code.json", endPointJson);
                        }
                    }
                    isRepoExported = true;
                }
            }
            catch (Exception ex) 
            {
                appConfig.RepoConfig.Id.ErrorId().AddMessage(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + ex.Message + "\n" + ex.StackTrace + "\n");
            }
            return isRepoExported;
        }
        /// <summary>
        /// Get the Build definitions to write into file
        /// </summary>
        /// <param name="appConfig"></param>
        /// <returns></returns>
        public int GetBuildDefinitions(ProjectConfigurations appConfig, string extractedFolderName)
        {
            try
            {
                BuildandReleaseDefs buildandReleaseDefs = new BuildandReleaseDefs(appConfig.BuildDefinitionConfig);
                List<JObject> builds = buildandReleaseDefs.ExportBuildDefinitions();
                BuildandReleaseDefs repoDefs = new BuildandReleaseDefs(appConfig.RepoConfig);
                Dictionary<string, string> variableGroupNameId = GetVariableGroups(appConfig, extractedFolderName);
                RepositoryList.Repository repo = repoDefs.GetRepoList();
                if (builds.Count > 0)
                {
                    int count = 1;
                    //creating ImportCode Json file
                    foreach (JObject def in builds)
                    {
                        string repoID = "";
                        var buildName = def["name"];
                        string fileName = buildName.ToString().Replace(".", "") + ".json";
                        var repoName = def["repository"]["name"];
                        var type = def["repository"]["type"];
                        foreach (var re in repo.value)
                        {
                            if (re.name == repoName.ToString())
                            {
                                repoID = re.id;
                            }
                        }
                        def["authoredBy"] = "{}";
                        def["project"] = "{}";
                        def["url"] = "";
                        def["uri"] = "";
                        def["id"] = "";
                        if (def["queue"]["pool"].HasValues)
                        {
                            def["queue"]["pool"]["id"] = "";
                        }
                        def["_links"] = "{}";
                        def["createdDate"] = "";
                        if (def["variableGroups"] != null)
                        {
                            var variableGroup = def["variableGroups"].HasValues ? def["variableGroups"].ToArray() : new JToken[0];
                            if (variableGroup.Length > 0)
                            {
                                foreach (var groupId in variableGroup)
                                {
                                    groupId["id"] = "$" + variableGroupNameId.Where(x => x.Key == groupId["id"].ToString()).FirstOrDefault().Value + "$";
                                }
                            }
                        }
                        var yamalfilename = def["process"]["yamlFilename"];

                        #region YML PIPELINES OF TYPE AZURE REPOS
                        if (yamalfilename != null && type.ToString().ToLower() == "tfsgit")
                        {
                            count = YmlWithAzureRepos(appConfig, count, extractedFolderName, def, fileName, type);
                        }
                        #endregion

                        #region YML PIPELINE WITH GITHUB
                        else if (yamalfilename != null && type.ToString().ToLower() == "github")
                        {
                            count = YmlWithGitHub(appConfig, count, extractedFolderName, def, fileName, type);
                        }
                        #endregion

                        #region OTHER
                        else if (yamalfilename == null)
                        {
                            count = NormalPipeline(appConfig, count, extractedFolderName, def, fileName, repoName, type);
                        }
                        #endregion
                    }
                    return count;
                }
            }
            catch (Exception ex)
            {
                logger.Info(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + ex.Message + "\n" + ex.StackTrace + "\n");
            }
            return 0;
        }
        /// <summary>
        /// Normal Build pipeline, which could be either pointing from Azure Repos or GitHub
        /// </summary>
        /// <param name="appConfig"></param>
        /// <param name="count"></param>
        /// <param name="templatePath"></param>
        /// <param name="def"></param>
        /// <param name="fileName"></param>
        /// <param name="repoName"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        private static int NormalPipeline(ProjectConfigurations appConfig, int count, string templatePath, JObject def, string fileName, JToken repoName, JToken type)
        {
            try
            {
                def["queue"]["id"] = "";
                def["queue"]["url"] = "";
                def["queue"]["_links"] = "{}";
                def["queue"]["pool"]["id"] = "";
                def["_links"] = "{}";
                def["createdDate"] = "";

                var process = def["process"];
                if (process != null)
                {
                    var phases = process["phases"];
                    if (phases != null)
                    {
                        foreach (var phase in phases)
                        {
                            phase["target"]["queue"] = "{}";
                            var steps = phase["steps"];
                            if (steps != null)
                            {
                                foreach (var step in steps)
                                {
                                    string keyConfig = File.ReadAllText(currentPath + @"\\Templates\EndpointKeyConfig.json");
                                    KeyConfig.Keys keyC = new KeyConfig.Keys();
                                    keyC = JsonConvert.DeserializeObject<KeyConfig.Keys>(keyConfig);
                                    foreach (var key in keyC.keys)
                                    {
                                        string keyVal = step[key] != null ? step[key].ToString() : "";
                                        if (!string.IsNullOrEmpty(keyVal))
                                        {
                                            step[key] = "";
                                        }
                                    }
                                    foreach (var key in keyC.keys)
                                    {
                                        string keyVal = step["inputs"][key] != null ? step["inputs"][key].ToString() : "";
                                        if (!string.IsNullOrEmpty(keyVal))
                                        {
                                            step["inputs"][key] = "";
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                if (type.ToString().ToLower() == "github")
                {

                    Guid g = Guid.NewGuid();
                    string randStr = g.ToString().Substring(0, 8);
                    def["repository"]["type"] = "Git";
                    def["repository"]["properties"]["fullName"] = "repository";
                    def["repository"]["properties"]["connectedServiceId"] = "$GitHub_" + randStr + "$";
                    def["repository"]["name"] = "repository";
                    string url = def["repository"]["url"].ToString();
                    if (url != "")
                    {
                        string endPointString = File.ReadAllText(currentPath + @"PreSetting\\GitHubEndPoint.json");
                        endPointString = endPointString.Replace("$GitHubURL$", url).Replace("$Name$", "GitHub_" + randStr);

                        if (!Directory.Exists(extractedTemplatePath + appConfig.RepoConfig.Project + "\\ServiceEndpoints"))
                        {
                            Directory.CreateDirectory(extractedTemplatePath + appConfig.RepoConfig.Project + "\\ServiceEndpoints");
                            File.WriteAllText(extractedTemplatePath + appConfig.RepoConfig.Project + "\\ServiceEndpoints\\GitHub" + randStr + "-EndPoint.json", endPointString);
                        }
                        else
                        {
                            File.WriteAllText(extractedTemplatePath + appConfig.RepoConfig.Project + "\\ServiceEndpoints\\GitHub" + randStr + "-EndPoint.json", endPointString);
                        }
                    }
                }
                else if (type.ToString().ToLower() == "git")
                {
                    Guid g = Guid.NewGuid();
                    string randStr = g.ToString().Substring(0, 8);
                    string url = def["repository"]["url"].ToString();
                    string endPointString = File.ReadAllText(currentPath + @"PreSetting\\GitHubEndPoint.json");
                    endPointString = endPointString.Replace("$GitHubURL$", url).Replace("$Name$", "GitHub_" + randStr);

                    if (!Directory.Exists(extractedTemplatePath + appConfig.RepoConfig.Project + "\\ServiceEndpoints"))
                    {
                        Directory.CreateDirectory(extractedTemplatePath + appConfig.RepoConfig.Project + "\\ServiceEndpoints");
                        File.WriteAllText(extractedTemplatePath + appConfig.RepoConfig.Project + "\\ServiceEndpoints\\GitHub_" + randStr + "-EndPoint.json", endPointString);
                    }
                    else
                    {
                        File.WriteAllText(extractedTemplatePath + appConfig.RepoConfig.Project + "\\ServiceEndpoints\\GitHub_" + randStr + "-EndPoint.json", endPointString);
                    }
                    def["repository"]["properties"]["connectedServiceId"] = "$GitHub_" + randStr + "$";
                }
                else
                {
                    def["repository"]["id"] = "$" + repoName + "$";
                    def["repository"]["url"] = "";
                    def["repository"]["properties"]["connectedServiceId"] = "";
                }
                var input = def["processParameters"]["inputs"];
                if (input != null)
                {
                    if (input.HasValues)
                    {
                        foreach (var i in input)
                        {
                            i["defaultValue"] = "";

                        }
                    }
                }
                var build = def["build"];
                if (build != null)
                {
                    if (build.HasValues)
                    {
                        foreach (var b in build)
                        {
                            b["inputs"]["serverEndpoint"] = "";
                        }
                    }
                }
                count++;
                if (!Directory.Exists(templatePath + "\\BuildDefinitions"))
                {
                    Directory.CreateDirectory(templatePath + "\\BuildDefinitions");
                    File.WriteAllText(templatePath + "\\BuildDefinitions\\" + fileName, JsonConvert.SerializeObject(def, Formatting.Indented));
                }
                else
                {
                    File.WriteAllText(templatePath + "\\BuildDefinitions\\" + fileName, JsonConvert.SerializeObject(def, Formatting.Indented));
                }

                return count;
            }
            catch (Exception ex)
            {
                logger.Info(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "Exporting normalPipeline \t" + ex.Message + "\n" + ex.StackTrace + "\n");
            }
            return count;
        }
        /// <summary>
        /// YAML pipeline which is pointing to GitHub
        /// </summary>
        /// <param name="appConfig"></param>
        /// <param name="count"></param>
        /// <param name="templatePath"></param>
        /// <param name="def"></param>
        /// <param name="fileName"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        private static int YmlWithGitHub(ProjectConfigurations appConfig, int count, string templatePath, JObject def, string fileName, JToken type)
        {
            try
            {
                Guid g = Guid.NewGuid();
                string randStr = g.ToString().Substring(0, 8);
                var ymlRepoUrl = def["repository"]["url"].ToString();
                if (!Directory.Exists(templatePath + "\\ImportSourceCode"))
                {
                    Directory.CreateDirectory(templatePath + "\\ImportSourceCode");
                }
                if (type.ToString().ToLower() == "github")
                {
                    string gitHubRepo = def["repository"]["id"].ToString();
                    string[] gitHubIdSplit = gitHubRepo.Split('/');
                    gitHubIdSplit[0] = "$username$";
                    gitHubRepo = string.Join("/", gitHubIdSplit);

                    ForkRepos.Fork gitHubRepoList = new ForkRepos.Fork();
                    gitHubRepoList.repositories = new List<ForkRepos.Repository>();
                    if (File.Exists(templatePath + "\\ImportSourceCode\\GitRepository.json"))
                    {
                        string readrepo = File.ReadAllText(templatePath + "\\ImportSourceCode\\GitRepository.json");
                        gitHubRepoList = JsonConvert.DeserializeObject<ForkRepos.Fork>(readrepo);
                    }
                    ForkRepos.Repository repoName = new ForkRepos.Repository
                    {
                        fullName = def["repository"]["id"].ToString(),
                        endPointName = "GitHub_" + randStr
                    };
                    gitHubRepoList.repositories.Add(repoName);

                    File.WriteAllText(templatePath + "\\ImportSourceCode\\GitRepository.json", JsonConvert.SerializeObject(gitHubRepoList, Formatting.Indented));

                    def["repository"]["properties"]["apiUrl"] = "https://api.github.com/repos/" + gitHubRepo;
                    def["repository"]["properties"]["branchesUrl"] = "https://api.github.com/repos/" + gitHubRepo + "/branches";
                    def["repository"]["properties"]["cloneUrl"] = "https://github.com/" + gitHubRepo + ".git";
                    def["repository"]["properties"]["fullName"] = "repository";
                    def["repository"]["properties"]["manageUrl"] = "https://github.com/" + gitHubRepo;
                    def["repository"]["properties"]["connectedServiceId"] = "$GitHub_" + randStr + "$";
                    def["repository"]["name"] = gitHubRepo;
                    def["repository"]["url"] = "https://github.com/" + gitHubRepo + ".git";
                    def["repository"]["id"] = gitHubRepo;
                }
                if (ymlRepoUrl != "")
                {
                    string endPointString = File.ReadAllText(currentPath + @"PreSetting\\GitHubEndPoint.json");
                    endPointString = endPointString.Replace("$GitHubURL$", ymlRepoUrl).Replace("$Name$", "GitHub_" + randStr);

                    if (!Directory.Exists(templatePath + "\\ServiceEndpoints"))
                    {
                        Directory.CreateDirectory(templatePath + "\\ServiceEndpoints");
                        File.WriteAllText(templatePath + "\\ServiceEndpoints\\GitHub_" + randStr + "-EndPoint.json", endPointString);
                    }
                    else
                    {
                        File.WriteAllText(templatePath + "\\ServiceEndpoints\\GitHub_" + randStr + "-EndPoint.json", endPointString);
                    }
                }
                count = count + 1;
                if (!Directory.Exists(templatePath + "\\BuildDefinitionGitHub"))
                {
                    Directory.CreateDirectory(templatePath + "\\BuildDefinitionGitHub");
                    File.WriteAllText(templatePath + "\\BuildDefinitionGitHub\\" + fileName, JsonConvert.SerializeObject(def, Formatting.Indented));
                }
                else
                {
                    File.WriteAllText(templatePath + "\\BuildDefinitionGitHub\\" + fileName, JsonConvert.SerializeObject(def, Formatting.Indented));
                }

                return count;
            }
            catch (Exception ex)
            {
                logger.Info(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "Exporting ymlWithGitHub \t" + ex.Message + "\n" + ex.StackTrace + "\n");
            }
            return count;
        }
        /// <summary>
        /// YAML pipeline which is pointing to Azure Repos
        /// </summary>
        /// <param name="appConfig"></param>
        /// <param name="count"></param>
        /// <param name="templatePath"></param>
        /// <param name="def"></param>
        /// <param name="fileName"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        private static int YmlWithAzureRepos(ProjectConfigurations appConfig, int count, string templatePath, JObject def, string fileName, JToken type)
        {
            try
            {
                Guid g = Guid.NewGuid();
                string randStr = g.ToString().Substring(0, 8);
                // def["triggers"] = new JArray();
                if (type.ToString().ToLower() == "github")
                {
                    def["repository"]["properties"]["fullName"] = "repository";
                    def["repository"]["properties"]["connectedServiceId"] = "$GitHub_" + randStr + "$";
                    def["repository"]["name"] = "repository";
                }
                var ymlRepoUrl = def["repository"]["url"].ToString();
                if (ymlRepoUrl != "")
                {
                    string endPointString = File.ReadAllText(currentPath + @"PreSetting\\GitHubEndPoint.json");
                    endPointString = endPointString.Replace("$GitHubURL$", ymlRepoUrl).Replace("$Name$", "GitHub_" + randStr);
                    if (!Directory.Exists(templatePath + "\\ServiceEndpoints"))
                    {
                        Directory.CreateDirectory(templatePath + "\\ServiceEndpoints");
                        File.WriteAllText(templatePath + "\\ServiceEndpoints\\GitHub-" + randStr + "-EndPoint.json", endPointString);
                    }
                    else
                    {
                        File.WriteAllText(templatePath + "\\ServiceEndpoints\\GitHub-" + randStr + "-EndPoint.json", endPointString);
                    }
                }
                string[] splitYmlRepoUrl = ymlRepoUrl.Split('/');
                if (splitYmlRepoUrl.Length > 0)
                {
                    splitYmlRepoUrl[2] = "$Organization$@dev.azure.com";
                    splitYmlRepoUrl[3] = "$Organization$";
                    splitYmlRepoUrl[4] = "$ProjectName$";
                    ymlRepoUrl = string.Join("/", splitYmlRepoUrl);
                    def["repository"]["url"] = ymlRepoUrl;
                    def["repository"]["properties"]["cloneUrl"] = ymlRepoUrl;
                }
                def["repository"]["properties"]["safeRepository"] = string.Format("${0}$", def["repository"]["name"].ToString());
                def["repository"]["id"] = string.Format("${0}$", def["repository"]["name"].ToString());
                var queueHref = def["queue"]["_links"]["self"]["href"].ToString();
                if (queueHref != "")
                {
                    string[] splitQhref = queueHref.Split('/');
                    if (splitQhref.Length > 0)
                    {
                        splitQhref[3] = "$Organization$";
                        splitQhref[splitQhref.Length - 1] = "$" + def["queue"]["name"].ToString() + "$";
                        def["queue"]["_links"]["self"]["href"] = string.Join("/", splitQhref);
                    }
                    def["queue"]["id"] = "$" + def["queue"]["name"] + "$";
                    def["queue"]["url"] = string.Join("/", splitQhref);
                }
                count = count + 1;
                if (!Directory.Exists(templatePath + "\\BuildDefinitions"))
                {
                    Directory.CreateDirectory(templatePath + "\\BuildDefinitions");
                    File.WriteAllText(templatePath + "\\BuildDefinitions\\" + fileName, JsonConvert.SerializeObject(def, Formatting.Indented));
                }
                else
                {
                    File.WriteAllText(templatePath + "\\BuildDefinitions\\" + fileName, JsonConvert.SerializeObject(def, Formatting.Indented));
                }

                return count;
            }
            catch (Exception ex)
            {
                logger.Info(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "Exporting ymlWithAzureRepos \t" + ex.Message + "\n" + ex.StackTrace + "\n");
            }
            return count;
        }
        /// <summary>
        /// Generalizing the release definition method to make it work for All kind of Release definition
        /// </summary>
        /// <param name="appConfig"></param>
        /// <returns></returns>
        public int GeneralizingGetReleaseDefinitions(ProjectConfigurations appConfig, string extractedFolderName)
        {
            try
            {
                BuildandReleaseDefs releaseDefs = new BuildandReleaseDefs(appConfig.ReleaseDefinitionConfig);
                List<JObject> releases = releaseDefs.GetReleaseDefs();
                string rells = JsonConvert.SerializeObject(releases);
                BuildandReleaseDefs agent = new BuildandReleaseDefs(appConfig.AgentQueueConfig);
                Dictionary<string, string> variableGroupNameId = GetVariableGroups(appConfig, extractedFolderName);
                Dictionary<string, int> queue = agent.GetQueues();
                int releasecount = 1;
                if (releases.Count > 0)
                {
                    foreach (JObject rel in releases)
                    {
                        var name = rel["name"];
                        rel["id"] = "";
                        rel["url"] = "";
                        rel["_links"] = "{}";
                        rel["createdBy"] = "{}";
                        rel["createdOn"] = "";
                        rel["modifiedBy"] = "{}";
                        rel["modifiedOn"] = "";

                        var variableGroup = rel["variableGroups"].HasValues ? rel["variableGroups"].ToArray() : new JToken[0];
                        if (variableGroup.Length > 0)
                        {
                            foreach (var groupId in variableGroup)
                            {
                                rel["variableGroups"] = new JArray("$" + variableGroupNameId.Where(x => x.Key == groupId.ToString()).FirstOrDefault().Value + "$");
                            }
                        }
                        else
                        {
                            rel["variableGroups"] = new JArray();
                        }
                        var env = rel["environments"];
                        foreach (var e in env)
                        {
                            e["badgeUrl"] = "";
                            var envVariableGroup = e["variableGroups"].HasValues ? e["variableGroups"].ToArray() : new JToken[0];
                            if (envVariableGroup.Length > 0)
                            {
                                foreach (var envgroupId in envVariableGroup)
                                {
                                    e["variableGroups"] = new JArray("$" + variableGroupNameId.Where(x => x.Key == envgroupId.ToString()).FirstOrDefault().Value + "$");
                                }
                            }
                            else
                            {
                                e["variableGroups"] = new JArray();
                            }
                            var owner = e["owner"];
                            owner["id"] = "$OwnerId$";
                            owner["displayName"] = "$OwnerDisplayName$";
                            owner["uniqueName"] = "$OwnerUniqueName$";
                            owner["url"] = "";
                            owner["_links"] = "{}";
                            owner["imageUrl"] = "";
                            owner["descriptor"] = "";

                            var deployPhases = e["deployPhases"];
                            if (deployPhases.HasValues)
                            {
                                foreach (var dep in deployPhases)
                                {

                                    var deploymentInput = dep["deploymentInput"];
                                    var queueID = deploymentInput["queueId"];
                                    string queueName = "";
                                    if (queue != null)
                                    {
                                        if (queue.Count > 0)
                                        {
                                            var q = queue.ContainsValue(Convert.ToInt32(queueID));
                                            if (q)
                                            {
                                                var agenetName = queue.Where(x => x.Value.ToString() == queueID.ToString()).FirstOrDefault();
                                                if (agenetName.Key != null)
                                                {
                                                    queueName = agenetName.Key.ToString();
                                                }
                                                else
                                                {
                                                    queueName = "";
                                                }
                                            }
                                        }
                                    }
                                    if (queueName != "")
                                    {
                                        deploymentInput["queueId"] = "$" + queueName + "$";
                                    }
                                    else
                                    {
                                        deploymentInput["queueId"] = "";
                                    }

                                    var workflow = dep["workflowTasks"];
                                    if (workflow.HasValues)
                                    {
                                        foreach (var flow in workflow)
                                        {
                                            var input = flow["inputs"];
                                            string keyConfig = File.ReadAllText(currentPath + @"\\Templates\EndpointKeyConfig.json");
                                            KeyConfig.Keys keyC = new KeyConfig.Keys();
                                            keyC = JsonConvert.DeserializeObject<KeyConfig.Keys>(keyConfig);
                                            foreach (var key in keyC.keys)
                                            {
                                                string keyVal = input[key] != null ? input[key].ToString() : "";
                                                if (!string.IsNullOrEmpty(keyVal))
                                                {
                                                    input[key] = "";
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        var artifact = rel["artifacts"];
                        if (artifact.HasValues)
                        {
                            foreach (var art in artifact)
                            {
                                string buildName = art["definitionReference"]["definition"]["name"].ToString();
                                string type = art["type"].ToString();
                                if (type.ToLower() == "build")
                                {
                                    art["sourceId"] = "$ProjectId$:" + "$" + buildName + "-id$";
                                    art["definitionReference"]["definition"]["id"] = "$" + buildName + "-id$";
                                    art["definitionReference"]["project"]["id"] = "$ProjectId$";
                                    art["definitionReference"]["project"]["name"] = "$ProjectName$";
                                    art["definitionReference"]["artifactSourceDefinitionUrl"] = "{}";
                                }
                                if (type.ToLower() == "azurecontainerrepository")
                                {
                                    art["sourceId"] = "$ProjectId$:" + "$" + buildName + "-id$";
                                    art["definitionReference"]["connection"]["id"] = "";
                                    art["definitionReference"]["definition"]["id"] = "";
                                    art["definitionReference"]["definition"]["name"] = "";
                                    art["definitionReference"]["registryurl"]["id"] = "";
                                    art["definitionReference"]["registryurl"]["name"] = "";
                                    art["definitionReference"]["resourcegroup"]["id"] = "";
                                    art["definitionReference"]["resourcegroup"]["name"] = "";
                                }
                            }
                        }
                        if (!(Directory.Exists(extractedFolderName + "\\ReleaseDefinitions")))
                        {
                            Directory.CreateDirectory(extractedFolderName + "\\ReleaseDefinitions");
                            File.WriteAllText(extractedFolderName + "\\ReleaseDefinitions\\" + name + ".json", JsonConvert.SerializeObject(rel, Formatting.Indented));
                        }
                        else
                        {
                            File.WriteAllText(extractedFolderName + "\\ReleaseDefinitions\\" + name + ".json", JsonConvert.SerializeObject(rel, Formatting.Indented));
                        }
                        releasecount++;
                    }
                }
                return releasecount;
            }
            catch (Exception ex)
            {
                logger.Info(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + ex.Message + "\n" + ex.StackTrace + "\n");
                appConfig.ReleaseDefinitionConfig.Id.ErrorId().AddMessage(ex.Message + Environment.NewLine + ex.StackTrace);
            }
            return 0;
        }
        /// <summary>
        /// Get different kinds of service endpoints and format it into POST json format
        /// </summary>
        /// <param name="appConfig"></param>
        public void GetServiceEndpoints(ProjectConfigurations appConfig, string extractedFolderName)
        {
            try
            {
                RestAPI.Service.ServiceEndPoint serviceEndPoint = new RestAPI.Service.ServiceEndPoint(appConfig.EndpointConfig);
                ServiceEndPoint getServiceEndPoint = serviceEndPoint.GetServiceEndPoints();
                if (getServiceEndPoint.count > 0)
                {
                    foreach (RestAPI.Viewmodel.Extractor.GetServiceEndpoints.Value endpoint in getServiceEndPoint.value)
                    {
                        switch (endpoint.authorization.scheme)
                        {
                            case "OAuth":
                            case "InstallationToken":
                                switch (endpoint.type)
                                {
                                    case "github":
                                    case "GitHub":
                                        if (endpoint.authorization.parameters == null)
                                        {
                                            endpoint.authorization.parameters = new RestAPI.Viewmodel.Extractor.GetServiceEndpoints.Parameters
                                            {
                                                AccessToken = "AccessToken"
                                            };
                                        }
                                        else
                                        {
                                            endpoint.authorization.parameters.AccessToken = endpoint.authorization.parameters.AccessToken ?? "AccessToken";
                                        }
                                        break;
                                }
                                break;
                            case "UsernamePassword":
                                endpoint.authorization.parameters.username = endpoint.authorization.parameters.username ?? "username";
                                endpoint.authorization.parameters.password = endpoint.authorization.parameters.password ?? "password";
                                break;
                            case "ManagedServiceIdentity":
                                if (endpoint.authorization.parameters == null)
                                {
                                    endpoint.authorization.parameters = new RestAPI.Viewmodel.Extractor.GetServiceEndpoints.Parameters
                                    {
                                        tenantId = Guid.NewGuid().ToString()
                                    };
                                }
                                else
                                {
                                    endpoint.authorization.parameters.tenantId = endpoint.authorization.parameters.tenantId ?? Guid.NewGuid().ToString();
                                }
                                break;
                            case "ServicePrincipal":
                                switch (endpoint.type)
                                {
                                    case "devCenter":
                                        endpoint.authorization.parameters.servicePrincipalKey = endpoint.authorization.parameters.servicePrincipalKey ?? "P2ssw0rd@123";
                                        break;
                                    case "azurerm":
                                        endpoint.authorization.parameters.url = null;
                                        endpoint.authorization.parameters.servicePrincipalId = endpoint.authorization.parameters.servicePrincipalId ?? Guid.NewGuid().ToString();
                                        endpoint.authorization.parameters.authenticationType = endpoint.authorization.parameters.authenticationType ?? "spnKey";
                                        endpoint.authorization.parameters.tenantId = endpoint.authorization.parameters.tenantId ?? Guid.NewGuid().ToString();
                                        endpoint.authorization.parameters.servicePrincipalKey = endpoint.authorization.parameters.servicePrincipalKey ?? "spnKey";
                                        switch (endpoint.data.scopeLevel)
                                        {
                                            case "ManagementGroup":
                                                endpoint.data.managementGroupId = endpoint.data.managementGroupId ?? "managedgroup";
                                                endpoint.data.managementGroupName = endpoint.data.managementGroupName ?? "groupname";
                                                break;
                                        }
                                        break;
                                }
                                break;
                            case "Certificate":
                                switch (endpoint.type)
                                {
                                    case "dockerhost":
                                        if (endpoint.authorization.parameters == null)
                                        {
                                            endpoint.authorization.parameters = new RestAPI.Viewmodel.Extractor.GetServiceEndpoints.Parameters();
                                            endpoint.authorization.parameters.cacert = endpoint.authorization.parameters.cacert ?? "cacert";
                                            endpoint.authorization.parameters.cert = endpoint.authorization.parameters.cert ?? "cert";
                                            endpoint.authorization.parameters.key = endpoint.authorization.parameters.key ?? "key";
                                        }
                                        else
                                        {
                                            endpoint.authorization.parameters.cacert = endpoint.authorization.parameters.cacert ?? "cacert";
                                            endpoint.authorization.parameters.cert = endpoint.authorization.parameters.cert ?? "cert";
                                            endpoint.authorization.parameters.key = endpoint.authorization.parameters.key ?? "key";
                                        }
                                        break;

                                    case "azure":
                                        if (endpoint.authorization.parameters == null)
                                        {
                                            endpoint.authorization.parameters = new RestAPI.Viewmodel.Extractor.GetServiceEndpoints.Parameters
                                            {
                                                certificate = "certificate"
                                            };
                                        }
                                        else
                                        {
                                            endpoint.authorization.parameters.certificate = endpoint.authorization.parameters.certificate ?? "certificate";
                                        }
                                        break;
                                }
                                break;
                            case "Token":
                                if (endpoint.authorization.parameters == null)
                                {
                                    endpoint.authorization.parameters = new RestAPI.Viewmodel.Extractor.GetServiceEndpoints.Parameters
                                    {
                                        apitoken = "apitoken"
                                    };
                                }
                                else
                                {
                                    endpoint.authorization.parameters.apitoken = endpoint.authorization.parameters.apitoken ?? "apitoken";
                                }
                                break;
                            case "None":
                                switch (endpoint.type)
                                {
                                    case "AzureServiceBus":
                                        if (endpoint.authorization.parameters == null)
                                        {
                                            endpoint.authorization.parameters = new RestAPI.Viewmodel.Extractor.GetServiceEndpoints.Parameters
                                            {
                                                serviceBusConnectionString = "connectionstring"
                                            };
                                        }
                                        else
                                        {
                                            endpoint.authorization.parameters.serviceBusConnectionString = endpoint.authorization.parameters.serviceBusConnectionString ?? "connectionstring";
                                        }
                                        break;
                                    case "externalnugetfeed":
                                        if (endpoint.authorization.parameters == null)
                                        {
                                            endpoint.authorization.parameters = new RestAPI.Viewmodel.Extractor.GetServiceEndpoints.Parameters
                                            {
                                                nugetkey = "nugetkey"
                                            };
                                        }
                                        else
                                        {
                                            endpoint.authorization.parameters.nugetkey = endpoint.authorization.parameters.nugetkey ?? "nugetkey";
                                        }
                                        break;
                                }
                                break;

                        }
                        string endpointString = JsonConvert.SerializeObject(endpoint);
                        if (!Directory.Exists(extractedFolderName + "\\ServiceEndpoints"))
                        {
                            Directory.CreateDirectory(extractedFolderName + "\\ServiceEndpoints");
                            File.WriteAllText(extractedFolderName + "\\ServiceEndpoints\\", JsonConvert.SerializeObject(endpoint, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }));
                        }
                        else
                        {
                            File.WriteAllText(extractedFolderName + "\\ServiceEndpoints\\" + endpoint.name + ".json", JsonConvert.SerializeObject(endpoint, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }));
                        }
                    }
                    appConfig.EndpointConfig.Id.AddMessage("Service endpoints exported");
                }
                else if (!string.IsNullOrEmpty(serviceEndPoint.LastFailureMessage))
                {
                    appConfig.EndpointConfig.Id.ErrorId().AddMessage("Error occured while fetching service endpoints");
                }
            }
            catch (Exception ex)
            {
                appConfig.EndpointConfig.Id.ErrorId().AddMessage(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + ex.Message + "\n" + ex.StackTrace + "\n");
            }
        }
        /// <summary>
        /// Get All work item names
        /// </summary>
        /// <param name="appConfig"></param>
        /// <returns></returns>
        private string[] GetAllWorkItemsName(ProjectConfigurations appConfig)
        {
            GetWorkItemsCount getWorkItems = new GetWorkItemsCount(appConfig.WorkItemConfig);
            WorkItemNames.Names workItems = getWorkItems.GetAllWorkItemNames();
            List<string> workItemNames = new List<string>();
            if (workItems.count > 0)
            {
                foreach (var workItem in workItems.value)
                {
                    workItemNames.Add(workItem.name);
                }
            }
            return workItemNames.ToArray();
        }
        private Dictionary<string, string> GetVariableGroups(ProjectConfigurations appConfig, string extractedFolderName)
        {
            VariableGroups variableGroups = new VariableGroups(appConfig.VariableGroupConfig);
            GetVariableGroups.Groups groups = variableGroups.GetVariableGroups();
            Dictionary<string, string> varibaleGroupDictionary = new Dictionary<string, string>();
            if (groups.count > 0)
            {
                if (!(Directory.Exists(extractedFolderName + "\\VariableGroups")))
                {
                    Directory.CreateDirectory(extractedFolderName + "\\VariableGroups");
                    File.WriteAllText(extractedFolderName + "\\VariableGroups\\VariableGroup.json", JsonConvert.SerializeObject(groups, Formatting.Indented));
                }
                else
                {
                    File.WriteAllText(extractedFolderName + "\\VariableGroups\\VariableGroup.json", JsonConvert.SerializeObject(groups, Formatting.Indented));
                }
                foreach (var vg in groups.value)
                {
                    if (!varibaleGroupDictionary.ContainsKey(vg.id))
                    {
                        varibaleGroupDictionary.Add(vg.id, vg.name);
                    }
                }
            }
            return varibaleGroupDictionary;
        }
        public bool ExportDeliveryPlans(ProjectConfigurations appConfig, string extractedFolderName)
        {
            bool isDeliveryPlanExported = false;
            try
            {
                Plans plans = new Plans(appConfig.WorkItemConfig);
                GetPlans.Root plansList = plans.GetDeliveryPlans(appConfig.WorkItemConfig.AccountName, appConfig.WorkItemConfig.Project);
                if (plansList.count > 0)
                {
                    RestAPI.Extractor.ClassificationNodes nodes = new RestAPI.Extractor.ClassificationNodes(appConfig.BoardConfig);
                    string defaultTeamID = string.Empty;

                    var teamsRes = nodes.GetTeams();
                    RootTeams rootTeams = new RootTeams();
                    if (teamsRes != null && teamsRes.IsSuccessStatusCode)
                    {
                        rootTeams = JsonConvert.DeserializeObject<RootTeams>(teamsRes.Content.ReadAsStringAsync().Result);
                    }

                    foreach (var plan in plansList.value)
                    {
                        APlan.Root aplan = plans.GetAPlan(appConfig.WorkItemConfig.AccountName, appConfig.WorkItemConfig.Project, plan.id);
                        if (aplan.properties?.teamBacklogMappings != null)
                        {
                            foreach (var team in aplan.properties?.teamBacklogMappings)
                            {
                                if (rootTeams.count > 0)
                                {
                                    foreach (var teams in rootTeams.value)
                                    {
                                        if (team.teamId == teams.id)
                                        {
                                            team.teamId = $"${teams.name}$";
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        if (!string.IsNullOrEmpty(aplan.id))
                        {
                            if (!(Directory.Exists(extractedFolderName + "\\DeliveryPlans")))
                            {
                                Directory.CreateDirectory(extractedFolderName + "\\DeliveryPlans");
                                File.WriteAllText(extractedFolderName + $"\\DeliveryPlans\\{aplan.name}.json", JsonConvert.SerializeObject(aplan, Formatting.Indented));
                            }
                            else
                            {
                                File.WriteAllText(extractedFolderName + $"\\DeliveryPlans\\{aplan.name}.json", JsonConvert.SerializeObject(aplan, Formatting.Indented));
                            }
                        }
                    }
                    isDeliveryPlanExported = true;
                }
            }
            catch (Exception ex)
            {
                appConfig.WorkItemConfig.Id.ErrorId().AddMessage(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + ex.Message + "\n" + ex.StackTrace + "\n");
            }
            return isDeliveryPlanExported;
        }
        #endregion END GENERATE ARTIFACTS       
    }
}
