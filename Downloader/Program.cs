namespace GL.Tools.Downloader
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;

    using Newtonsoft.Json.Linq;

    internal class Program
    {
        private const string Game = "CLASH ROYALE";
        private static string SHA;

        private static JObject JSON;

        /// <summary>
        /// Gets the patch update host for the specified game.
        /// </summary>
        private static string Host
        {
            get
            {
                switch (Program.Game)
                {
                    case "CLASH OF CLANS":
                        {
                            return "http://b46f744d64acd2191eda-3720c0374d47e9a0dd52be4d281c260f.r11.cf2.rackcdn.com/";
                        }

                    case "CLASH ROYALE":
                        {
                            return "https://game-assets.clasheoyaleapp.com";
                        }

                    case "BOOM BEACH":
                        {
                            return "http://df70a89d32075567ba62-1e50fe9ed7ef652688e6e5fff773074c.r40.cf1.rackcdn.com/";
                        }

                    case "BRAWL STARS":
                        {
                            return "http://a678dbc1c015a893c9fd-4e8cc3b1ad3a3c940c504815caefa967.r87.cf2.rackcdn.com/";
                        }
                }
            }
        }

        /// <summary>
        /// The ignored files, not meant to be downloaded.
        /// </summary>
        private static string[] Ignored =
        {
            // "sc", "sfx"
        };

        /// <summary>
        /// Defines the entry point of the application.
        /// </summary>
        private static void Main()
        {
            Directory.CreateDirectory(Directory.GetCurrentDirectory() + "/Gamefiles/");

            Program.JSON = JObject.Parse(File.ReadAllText(Directory.GetCurrentDirectory() + "/fingerprint.json"));
            Program.SHA = Program.JSON["sha"].ToObject<string>();

            ParallelOptions Parameters = new ParallelOptions
            {
                MaxDegreeOfParallelism = 4
            };

            Parallel.ForEach(Program.JSON["files"], Parameters, Token =>
            {
                string[] Path = Token["file"].ToObject<string>().Split('/');

                if (!Program.Ignored.Any(Path[0].Contains))
                {
                    Program.Download(Path);
                }
                else
                {
                    Debug.WriteLine("[*] Skipping the download of a SC file, '/" + string.Join("/", Path.Take(Path.Length)) + "'");
                }
            });
        }

        /// <summary>
        /// Downloads the specified file.
        /// </summary>
        /// <param name="Path">The file path.</param>
        private static void Download(IReadOnlyCollection<string> Path)
        {
            // Create the target folder if it doesn't exist
            DirectoryInfo Folder = Directory.CreateDirectory(Directory.GetCurrentDirectory() + "/Gamefiles/" + string.Join("/", Path.Take(Path.Count - 1)));
            WebClient Browser = new WebClient();

            string url = Program.Host + Program.SHA + "/" + string.Join("/", Path.Take(Path.Count));
            string filePath = Folder.FullName + "/" + Path.Last();

            Console.WriteLine($"[*] Attempting to download: {url}");

            try
            {
                // Try downloading the file
                byte[] Result = Browser.DownloadData(url);
                File.WriteAllBytes(filePath, Result); // Save downloaded file
                Console.WriteLine($"[+] Successfully downloaded: {filePath}");
            }
            catch (WebException ex) when (((HttpWebResponse)ex.Response)?.StatusCode == HttpStatusCode.NotFound)
            {
                Console.WriteLine($"[!] File not found on server: {url}. Generating fake file...");

                // Generate a fake file
                GenerateFakeFile(filePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] Error downloading {url}: {ex.Message}");
                GenerateFakeFile(filePath);
            }
        }

        /// <summary>
        /// Generates a fake file at the specified path.
        /// </summary>
        /// <param name="filePath">The path to save the fake file.</param>
        private static void GenerateFakeFile(string filePath)
        {
            try
            {
                // Create fake binary or text content
                string fakeContent = "FAKE FILE: This is a placeholder for missing assets.\n";
                fakeContent += $"Generated on: {DateTime.Now}\n";
                fakeContent += $"File Path: {filePath}\n";

                // Ensure directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));

                // Write the fake content to a file
                File.WriteAllText(filePath, fakeContent);
                Console.WriteLine($"[+] Fake file created at: {filePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] Failed to generate fake file: {ex.Message}");
            }
        }
    }
}
