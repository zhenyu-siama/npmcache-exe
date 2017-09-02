using System;
using System.IO;
using System.Text.RegularExpressions;

namespace npmcache.Utils
{
    public static class IOExtensions
    {
        public static string RelativePathTo(this FileInfo fileInfo, DirectoryInfo baseDirectory)
        {
            string file = fileInfo.FullName.TrimPath();
            string baseDir = baseDirectory.FullName.TrimPath();

            if (file.StartsWith(file, StringComparison.CurrentCultureIgnoreCase))
                return file.Substring(baseDir.Length);
            else
                return null;
        }

        public static string RelativePathTo(this DirectoryInfo directoryInfo, DirectoryInfo baseDirectory)
        {
            string directory = directoryInfo.FullName.TrimPath();
            string baseDir = baseDirectory.FullName.TrimPath();

            if (directory.StartsWith(directory, StringComparison.CurrentCultureIgnoreCase))
                return directory.Substring(baseDir.Length);
            else
                return null;
        }

        public static FileInfo AppendRelativeFile(this DirectoryInfo baseDirectory, string relativePath)
        {
            return new FileInfo($"{baseDirectory.FullName}\\{relativePath.TrimPath()}");
        }

        public static DirectoryInfo AppendRelativeDirectory(this DirectoryInfo baseDirectory, string relativePath)
        {
            return new DirectoryInfo($"{baseDirectory.FullName}\\{relativePath.TrimPath()}");
        }

        static Regex regexParentFolder = new Regex(@"[^\*^\.^""^\\^\/^\[^\]^\:^;^\|^=^\,^\?^<^>]+\\\.\.\\");
        static Regex regexCurrentFolder = new Regex(@"\.\\");
        static Regex regexSlashBase = new Regex(@"^\\");

        public static string TrimPath(this string path)
        {


            path = path.Replace(@"/", @"\");

            path = path.Replace(@"\\", @"\");

            path = regexCurrentFolder.Replace(path, "");

            path = regexParentFolder.Replace(path, "");

            path = regexSlashBase.Replace(path, "");

            return path;
        }
    }
}
