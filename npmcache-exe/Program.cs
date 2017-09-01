using System;
using System.Text;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using npmcache.Utils;
using npmcache.Entities;
using Newtonsoft.Json;
using System.Threading;
using System.Threading.Tasks;

namespace npmcache
{
    class Program
    {
        const string NodeModules = "node_modules";
        static void Main(string[] args)
        {
            Console.WriteLine($"using NPM Cached @ \"{AppContext.BaseDirectory}\"");

            FileInfo cacheSettingJson = new FileInfo($"{AppContext.BaseDirectory}\\npmcache.json");

            CacheSettings cacheSetting = null;
            if (!cacheSettingJson.Exists)
            {
                cacheSetting = CreateCacheSetting(cacheSettingJson.FullName);
            }
            else
            {
                try
                {
                    cacheSetting = JsonConvert.DeserializeObject<CacheSettings>(File.ReadAllText(cacheSettingJson.FullName));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.ToString()}");
                    cacheSetting = CreateCacheSetting(cacheSettingJson.FullName);
                }
            }

            CancellationTokenSource timeout = SetupTimeout(cacheSetting.Timeout);

            Console.WriteLine($"NPM Cache Folders are located in @ \"{cacheSetting.CacheDirectory}\"");

            FileInfo filePackageJson = new FileInfo($"{Directory.GetCurrentDirectory()}\\package.json");

            if (filePackageJson.Exists)
            {
                var md5 = MD5.Create();
                var packageJson = File.ReadAllText(filePackageJson.FullName);
                List<byte> bytes = new List<byte>();
                bytes.AddRange(Encoding.UTF8.GetBytes(packageJson));
                var code = md5.ComputeHash(bytes.ToArray()).ToHexString();
                Console.WriteLine($"Md5: {code}");

                DirectoryInfo cacheBaseDirectory = new DirectoryInfo(cacheSetting.CacheDirectory);

                if (!cacheBaseDirectory.Exists)
                    cacheBaseDirectory.Create();

                DirectoryInfo cacheDirectory = new DirectoryInfo($"{cacheSetting.CacheDirectory}\\{code}");

                FileInfo cachedJsonFile = new FileInfo($"{cacheDirectory.FullName}\\package-cached.json");

                DeleteLink();

                var reinstall = args != null && args.Length > 0 && args[0].ToLower() == "r";

                if (cacheDirectory.Exists && !reinstall)
                {
                    while (!File.Exists(cachedJsonFile.FullName))
                    {
                        //wait until another process is done with the installation
                        Thread.Sleep(1000 * cacheSetting.CheckInterval);
                        Console.WriteLine("Wait until previous installation is done...");
                    }

                    if (File.Exists(cachedJsonFile.FullName))
                    {
                        string cachedJson = File.ReadAllText(cachedJsonFile.FullName);
                        if (cachedJson != packageJson)
                        {
                            Console.WriteLine("package-cached.json is different from package.json. current cache will be deleted");
                            cacheDirectory.Delete(true);
                            RunInstall(cacheSetting, cacheDirectory, filePackageJson, cachedJsonFile);
                        }
                        else
                        {
                            Console.WriteLine("package-cached.json is the same as package.json. a link will be created");
                            CreateLink(cacheDirectory.FullName);
                        }
                    }
                    else
                    {
                        Console.WriteLine("package-cached.json was not found in this folder. npmcache was unable to check difference.");
                        Console.WriteLine("npmcache will reinstall the package");
                        cacheDirectory.Delete(true);

                        RunInstall(cacheSetting, cacheDirectory, filePackageJson, cachedJsonFile);
                    }
                }
                else
                {
                    if (reinstall)
                        cacheDirectory.Delete(true);
                    cacheDirectory.Create();
                    //store the file

                    RunInstall(cacheSetting, cacheDirectory, filePackageJson, cachedJsonFile);
                }
            }
            Console.WriteLine("npmcache install completed...");

            //cancel the timeout to shutdown the process
            timeout.Cancel();
        }

        static CancellationTokenSource SetupTimeout(int count)
        {
            int total = count;
            CancellationTokenSource source = new CancellationTokenSource();
            CancellationToken token = source.Token;
            Task timeout = new Task(() =>
            {
                while (!token.IsCancellationRequested)
                {
                    Thread.Sleep(1000);
                    total -= 1;
                    if (total <= 0 && !token.IsCancellationRequested)
                    {
                        Environment.Exit(1);
                    }
                }
            });
            timeout.Start();
            return source;
        }

        static void RunInstall(CacheSettings cacheSetting, DirectoryInfo cacheDirectory, FileInfo filePackageJson, FileInfo cachedJsonFile)
        {
            CreateLink(cacheDirectory.FullName);
            Console.WriteLine($"{NodeModules} fold is linked to \"{cacheDirectory.FullName}\"");
            NPMInstall(cacheSetting.PackageManager);

            Console.WriteLine($"Copy package.json as package-cached.json");
            File.Copy(filePackageJson.FullName, cachedJsonFile.FullName);
        }

        static CacheSettings CreateCacheSetting(string filename)
        {
            var cacheSetting = new CacheSettings() { CacheDirectory = AppContext.BaseDirectory, PackageManager = "npm install", CheckInterval = 1, Timeout = 300 };
            File.WriteAllText(filename, JsonConvert.SerializeObject(cacheSetting));
            Console.WriteLine($"Cache Setting Created!");
            return cacheSetting;
        }

        static void DeleteLink()
        {
            if(Directory.Exists(NodeModules))
                Directory.Delete(NodeModules, true);
        }

        static void CreateLink(string path)
        {
            ProcessStartInfo deleteLink = new ProcessStartInfo("cmd.exe")
            {
                Arguments = $"/c mklink /J {NodeModules} \"{path}\"",
                UseShellExecute = false,
                RedirectStandardError = false,
                RedirectStandardInput = false,
                RedirectStandardOutput = false
            };
            Process action = Process.Start(deleteLink);
            action.WaitForExit();
        }

        static void NPMInstall(string packageManager)
        {
            Console.WriteLine($"Run {packageManager} now:");
            ProcessStartInfo deleteLink = new ProcessStartInfo("cmd.exe")
            {
                Arguments = $"/c {packageManager}",
                UseShellExecute = false,
                RedirectStandardError = false,
                RedirectStandardInput = false,
                RedirectStandardOutput = false
            };
            Process action = Process.Start(deleteLink);
            action.WaitForExit();
        }

    }
}
