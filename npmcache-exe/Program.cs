using System;
using System.Text;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using npmcache.Utils;
using npmcache.Entities;
using Newtonsoft.Json;

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
                    if (cachedJsonFile.Exists)
                    {
                        string cachedJson = File.ReadAllText(cachedJsonFile.FullName);
                        if (cachedJson != packageJson)
                        {
                            Console.WriteLine("package-cached.json is different from package.json. current cache will be deleted");
                            cacheDirectory.Delete();
                            cacheDirectory.Create();
                        }
                        CreateLink(cacheDirectory.FullName);
                        Console.WriteLine($"{NodeModules} fold is linked to \"{cacheDirectory.FullName}\"");
                    }
                    else
                    {
                        throw new Exception("package-cached.json was not found in this folder. npmcache was unable to check difference.");
                    }
                }
                else
                {
                    if (reinstall)
                        cacheDirectory.Delete(true);
                    cacheDirectory.Create();
                    //store the file
                    File.Copy(filePackageJson.FullName, cachedJsonFile.FullName);
                    CreateLink(cacheDirectory.FullName);
                    Console.WriteLine($"{NodeModules} fold is linked to \"{cacheDirectory.FullName}\"");
                    NPMInstall();
                }


            }
            Console.WriteLine("npmcache install completed...");
        }

        static CacheSettings CreateCacheSetting(string filename)
        {
            var cacheSetting = new CacheSettings() { CacheDirectory = AppContext.BaseDirectory };
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

        static void NPMInstall()
        {
            Console.WriteLine($"Run npm install now:");
            ProcessStartInfo deleteLink = new ProcessStartInfo("cmd.exe")
            {
                Arguments = $"/c npm install",
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
