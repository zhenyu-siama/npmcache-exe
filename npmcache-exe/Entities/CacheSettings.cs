using System;
using System.Collections.Generic;
using System.Text;

namespace npmcache.Entities
{
    public class CacheSettings
    {
        public string CacheDirectory { get; set; }
        public string PackageManager { get; set; }
        public int CheckInterval { get; set; }
        public int Timeout { get; set; }
    }
}
