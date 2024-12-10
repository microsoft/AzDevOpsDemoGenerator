using ADOGenerator.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADOGenerator.IServices
{
    public interface IInitService
    {
        string ExtractHref(string link);

        string ReadSecret();

        void PrintErrorMessage(string message);

        bool CheckProjectName(string name);
    }
}
