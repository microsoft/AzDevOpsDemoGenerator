using ADOGenerator.Models;
using Newtonsoft.Json.Linq;

namespace ADOGenerator.IServices
{
    public interface IProjectService
    {
        HttpResponseMessage GetprojectList(string accname, string pat);

        string GetJsonFilePath(bool IsPrivate, string TemplateFolder, string TemplateName, string FileName = "");

        bool CreateProjectEnvironment(Project model);
        // string[] CreateProjectEnvironment(string organizationName, string newProjectName, string token, string templateUsed, string templateFolder);

        public bool CheckForInstalledExtensions(string extensionJsonFile, string token, string account);

        public bool InstallExtensions(Project model, string accountName, string PAT);

        public bool WhereDoseTemplateBelongTo(string templatName);

    }
}
