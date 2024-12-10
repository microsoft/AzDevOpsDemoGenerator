using ADOGenerator.IServices;
using ADOGenerator.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ADOGenerator.Services
{
    public class Init : IInitService
    {
        public bool CheckProjectName(string name)
        {
            try
            {
                List<string> reservedNames = new List<string>
                {
                    "AUX", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "COM10",
                    "CON", "DefaultCollection", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
                    "NUL", "PRN", "SERVER", "SignalR", "Web", "WEB",
                    "App_Browsers", "App_code", "App_Data", "App_GlobalResources", "App_LocalResources", "App_Themes", "App_WebResources", "bin", "web.config"
                };

                if (name.Length > 64)
                {
                    return false;
                }
                if (reservedNames.Contains(name))
                {
                    return false;
                }
                if (Regex.IsMatch(name, @"^_|^\.") || Regex.IsMatch(name, @"\.$"))
                {
                    return false;
                }
                if (Regex.IsMatch(name, @"[\x00-\x1F\x7F-\x9F\uD800-\uDFFF\\/:*?""<>;#$*{},+=\[\]|.@%&~`]"))
                {
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                PrintErrorMessage(ex.Message);
                return false;
            }
        }

       
        public string ExtractHref(string link)
        {
            var startIndex = link.IndexOf("href='") + 6;
            var endIndex = link.IndexOf("'", startIndex);
            return link.Substring(startIndex, endIndex - startIndex);
        }


        public void PrintErrorMessage(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: {message}");
            Console.ResetColor();
        }


        public string ReadSecret()
        {
            var secret = string.Empty;
            ConsoleKey key;
            do
            {
                var keyInfo = Console.ReadKey(intercept: true);
                key = keyInfo.Key;

                if (key == ConsoleKey.Backspace && secret.Length > 0)
                {
                    Console.Write("\b \b");
                    secret = secret[0..^1];
                }
                else if (!char.IsControl(keyInfo.KeyChar))
                {
                    Console.Write("*");
                    secret += keyInfo.KeyChar;
                }
            } while (key != ConsoleKey.Enter);
            Console.WriteLine();
            return secret;
        }
    }
}
