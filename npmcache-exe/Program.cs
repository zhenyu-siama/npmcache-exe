using System;
using System.Text;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Runtime.InteropServices;
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

            DirectoryInfo workingDirectory = new DirectoryInfo(Directory.GetCurrentDirectory());

            FileInfo filePackageJson = new FileInfo($"{workingDirectory.FullName}\\package.json");

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

                var reinstall = args != null && args.Length > 0 && args[0].ToLower() == @"\r";

                var whiteList = args.Where(arg => !arg.StartsWith(@"\")).ToList();

                if (cacheSetting.HardLinkWhiteList != null)
                    whiteList.AddRange(cacheSetting.HardLinkWhiteList);

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
                            CreateLink(workingDirectory, cacheDirectory, whiteList);
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
                    if (reinstall && Directory.Exists(cacheDirectory.FullName))
                        cacheDirectory.Delete(true);
                    cacheDirectory.Create();
                    //store the file

                    RunInstall(cacheSetting, cacheDirectory, filePackageJson, cachedJsonFile);
                    CreateLink(workingDirectory, cacheDirectory, whiteList);
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

            //copy the package.json to the cache directory

            File.Copy(filePackageJson.FullName, cacheDirectory.AppendRelativeFile(filePackageJson.Name).FullName);

            // run npm install here

            Console.WriteLine($"{NodeModules} fold is linked to \"{cacheDirectory.FullName}\"");
            NPMInstall(cacheSetting.PackageManager, cacheDirectory);

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

        /// <summary>
        /// Create Folders and File Links in the Target Folder
        /// </summary>
        /// <param name="current">the current folder under the source folder</param>
        /// <param name="source">the source root folder</param>
        /// <param name="target">the target root folder</param>
        public static void LinkFolderWithFileHardLink(DirectoryInfo current, DirectoryInfo source, DirectoryInfo target, IEnumerable<string> hardlinkWhitelist = null)
        {
            if(hardlinkWhitelist == null)
            {
                //sync files

                foreach (var fi in current.GetFiles())
                {
                    var relative = fi.RelativePathTo(source);
                    if (relative != null)
                    {
                        var file = target.AppendRelativeFile(relative);
                        CreateHardLink(file.FullName, fi.FullName, IntPtr.Zero);
                    }
                }

                //sync dirs

                foreach (var di in current.GetDirectories())
                {
                    var relative = di.RelativePathTo(source);
                    if (relative != null)
                    {
                        var directory = target.AppendRelativeDirectory(relative);
                        if (!Directory.Exists(directory.FullName))
                            Directory.CreateDirectory(directory.FullName);
                        LinkFolderWithFileHardLink(di, source, target);
                    }
                }
            }

            else
            {
                foreach (var fi in current.GetFiles())
                {
                    var relative = fi.RelativePathTo(source);
                    if (relative != null)
                    {
                        var file = target.AppendRelativeFile(relative);
                        CreateHardLink(file.FullName, fi.FullName, IntPtr.Zero);
                    }
                }

                //sync dirs

                foreach (var di in current.GetDirectories())
                {
                    var relative = di.RelativePathTo(source);
                    if (relative != null)
                    {
                        var directory = target.AppendRelativeDirectory(relative);
                        if(hardlinkWhitelist.Any(path => path == di.Name))
                        {
                            if (!Directory.Exists(directory.FullName))
                                Directory.CreateDirectory(directory.FullName);
                            int dirNameLength = di.Name.Length + 1;
                            LinkFolderWithFileHardLink(di, source, target);
                        }
                        else
                        {
                            var found = hardlinkWhitelist.Where(path => path == di.Name || path.StartsWith($"{di.Name}\\"));
                            if (found.Any())
                            {
                                if (!Directory.Exists(directory.FullName))
                                    Directory.CreateDirectory(directory.FullName);
                                int dirNameLength = di.Name.Length + 1;
                                LinkFolderWithFileHardLink(di, source, target, found.Select(path => path.Substring(dirNameLength)));
                            }
                            else
                            {
                                CreateSymbolicLink(directory.FullName, di.FullName, SymbolicLinkTargetIsADirectory);
                            }
                        }
                    }

                }

            }


        }

        [DllImport("Kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool CreateSymbolicLink(string lpSymlinkFileName, string lpTargetFileName, int dwFlags);

        private const int SymbolicLinkTargetIsAFile = 0;

        private const int SymbolicLinkTargetIsADirectory = 1;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="targetDirectory">The directory where node_moduels should be created. It is not the node_modules folder.</param>
        /// <param name="sourceDirectory">The sourceDirectory where cached node_modules can be found. It is not the cached node_modules folder itself.</param>
        static void CreateLink(DirectoryInfo targetDirectory, DirectoryInfo sourceDirectory, List<string> whiteList)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            DirectoryInfo target_node_modules = targetDirectory.AppendRelativeDirectory("node_modules");

            if (Directory.Exists(target_node_modules.FullName))
            {
                target_node_modules.Delete(true);
            }
            Directory.CreateDirectory(target_node_modules.FullName);

            DirectoryInfo source_node_modules = sourceDirectory.AppendRelativeDirectory("node_modules");

            LinkFolderWithFileHardLink(source_node_modules, source_node_modules, target_node_modules, whiteList);
                //new List<string>() { "@angular",  "applicationinsights-js", "angular2-busy"}); //"@ngtools", "webpack",

            stopwatch.Stop();
            Console.WriteLine($"Hard Link Time Consumed: {stopwatch.ElapsedMilliseconds}ms.");
        }

        static void NPMInstall(string packageManager, DirectoryInfo workingDirectory)
        {

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            string root = workingDirectory.Root.FullName.Replace(@"\", "");

            Console.WriteLine($"Run {packageManager} now:");
            ProcessStartInfo deleteLink = new ProcessStartInfo("cmd.exe")
            {
                Arguments = $"/c {root} & cd {workingDirectory.FullName} & {packageManager}",
                UseShellExecute = false,
                RedirectStandardError = false,
                RedirectStandardInput = false,
                RedirectStandardOutput = false
            };
            Process action = Process.Start(deleteLink);
            action.WaitForExit();

            stopwatch.Stop();
            Console.WriteLine($"Package Installation Time Consumed: {stopwatch.ElapsedMilliseconds}ms.");
        }

    }
}
