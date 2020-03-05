using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using Newtonsoft.Json.Linq;


namespace HSLauncher
{
    class Program
    {
        private const int Tries = 60;
        private const int Delay = 500;

        private const string json_pattern = @"^\[I \d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{4}\] Request GET \/(.+)\/hs_beta \r?\nResponse (\d+) \(\d+\.\d{4} ms\): ({(?:.*|\r?\n)+?}\r?\n)";
        private const string shutted_down_pattern = @"^\[I \d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{4}\] Agent is shutting down\r?\n";
        private const string log_name_pattern = @"Agent-(\d{8})T(\d{6}).log";

        [STAThread]
        static void Main()
        {
            var tries = Tries;
            bool SingleCopy;
            new Mutex(true, Marshal.GetTypeLibGuidForAssembly(Assembly.GetExecutingAssembly()).ToString(), out SingleCopy);
            if (SingleCopy && Process.GetProcessesByName("Hearthstone").Length == 0)
            {
                Process Agent,
                        BNET = Process.GetProcessesByName("Battle.net").FirstOrDefault();
                if (BNET == null)
                {
                    tries *= 2;
                    BNET = Process.Start(new ProcessStartInfo
                    {
                        FileName = "battlenet://",
                        UseShellExecute = true,
                        WorkingDirectory = Environment.SystemDirectory
                    });
                }
                Regex json_regex = new Regex(json_pattern, RegexOptions.Multiline | RegexOptions.Compiled);
                Regex shutted_down_regex = new Regex(shutted_down_pattern, RegexOptions.Compiled);
                //Regex log_name_regex = new Regex(log_name_pattern, RegexOptions.Compiled);
                for (var i = 0; i < tries; i++)
                {
                    if (Process.GetProcessesByName("Battle.net").Length >= 3 && (Agent = Process.GetProcessesByName("Agent").FirstOrDefault()) != null)
                    {
                        var logsDirectory = new DirectoryInfo(Path.Combine(Path.GetDirectoryName(Agent.MainModule.FileName), "Logs"));
                        if (logsDirectory.Exists)
                        {
                            var logs = logsDirectory.GetFiles("Agent-*.log", SearchOption.TopDirectoryOnly);
                            if (logs.Length > 0)
                            {
                                var text = File.ReadAllText(logs.OrderByDescending(x => x.LastWriteTime).First().FullName);

                                if (!shutted_down_regex.IsMatch(text))
                                {
                                    var jsons = json_regex.Matches(text);
                                    for (var j = jsons.Count - 1; j >= 0; j--)
                                    {
                                        if ((jsons[j].Groups[1].Value == "game" || jsons[j].Groups[1].Value == "version") && jsons[j].Groups[2].Value == "200")
                                        {
                                            var jObject = JObject.Parse(jsons[j].Groups[3].Value);
                                            if (jObject.ContainsKey("playable"))
                                            {
                                                if ((bool)jObject["playable"])
                                                {
                                                    Process.Start(new ProcessStartInfo
                                                    {
                                                        Arguments = "--exec=\"launch WTCG\"",
                                                        FileName = BNET.MainModule.FileName,
                                                        UseShellExecute = false
                                                    });
                                                }
                                                return;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    Thread.Sleep(Delay);
                }
            }
        }
    }
}
