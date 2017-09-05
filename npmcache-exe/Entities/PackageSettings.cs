using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using npmcache.Utils;

namespace npmcache.Entities
{
    public class PackageSettings
    {

        public Dictionary<string, string> dependencies { get; set; }
        public Dictionary<string, string> devDependencies { get; set; }

        public string ComputeDependencyHashCode()
        {
            List<string> modules = new List<string>();

            if(dependencies != null)
            {
                foreach(var kvp in dependencies)
                {
                    modules.Add($"{kvp.Key}:{kvp.Value}");
                }
            }

            if (devDependencies != null)
            {
                foreach (var kvp in devDependencies)
                {
                    modules.Add($"%{kvp.Key}:{kvp.Value}");
                }
            }

            var md5 = MD5.Create();

            modules.Sort();
            string all_modules = string.Join(",", modules);

            return md5.ComputeHash(Encoding.UTF8.GetBytes(all_modules)).ToHexString();
        }
    }
}
