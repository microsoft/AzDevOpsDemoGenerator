using ADOGenerator.Models;

namespace ADOGenerator
{
    public static class ServiceExtensions
    {
        public static readonly object objLock = new object();

        public static string ReadJsonFile(this Project file, string filePath)
        {
            string fileContents = string.Empty;

            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (StreamReader sr = new StreamReader(fs))
                {
                    fileContents = sr.ReadToEnd();
                }
            }

            return fileContents;
        }

        public static string ErrorId(this string str)
        {
            str = str + "_Errors";
            return str;
        }

        public static void AddMessage(this string id, string message)
        {
            lock (objLock)
            {
                // Create Log floder
                if (!Directory.Exists("Log"))
                {
                    Directory.CreateDirectory("Log");
                }
                string logFilePath = Path.Combine(Directory.GetCurrentDirectory(), "Log");
                string fileName = $"{DateTime.Now.ToString("yyyy-MM-dd")}-{id}.txt";
                if (id.EndsWith("_Errors"))
                {
                    // Create the Errors folder if it does not exist
                    string errorsFolderPath = Path.Combine(logFilePath, "Errors");
                    if (!Directory.Exists(errorsFolderPath))
                    {
                        Directory.CreateDirectory(errorsFolderPath);
                    }
                    // Create Log file
                    string errorFilePath = Path.Combine(errorsFolderPath, fileName);
                    if (!File.Exists(errorFilePath))
                    {
                        File.Create(errorFilePath).Dispose();
                    }
                    File.AppendAllLines(errorFilePath, new string[] { message });

                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(message);
                    Console.ResetColor();
                }
                else
                {
                    if (!File.Exists(Path.Combine(logFilePath, fileName)))
                    {
                        File.Create(Path.Combine(logFilePath, fileName)).Dispose();
                    }
                    File.AppendAllLines(Path.Combine(logFilePath, fileName), new string[] { message });
                    // Create Log file
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine(message);
                    Console.ResetColor();
                }
            }
        }

    }
}
