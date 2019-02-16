﻿using ForgeModGenerator.SoundGenerator.Models;
using ForgeModGenerator.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ForgeModGenerator.Tests
{
    [TestClass]
    public class PathTests
    {
        [TestMethod]
        public void GetUniqueName()
        {
            Assert.AreEqual("smth(5)", IOExtensions.GetUniqueName("smth", (name) => name == "smth(5)"));
            HashSet<string> strings = new HashSet<string> { "test", "test(1)", "test(2)" };
            Assert.AreEqual("test(3)", IOExtensions.GetUniqueName("test", (name) => !strings.Contains(name)));
        }

        [TestMethod]
        public void Subpaths()
        {
            Assert.IsTrue(IOExtensions.IsSubPathOf(@"c:\foo", @"c:"));
            Assert.IsTrue(IOExtensions.IsSubPathOf(@"c:\foo", @"c:\"));
            Assert.IsTrue(IOExtensions.IsSubPathOf(@"c:\foo", @"c:\foo"));
            Assert.IsTrue(IOExtensions.IsSubPathOf(@"c:\foo", @"c:\foo\"));
            Assert.IsTrue(IOExtensions.IsSubPathOf(@"c:\foo\", @"c:\foo"));
            Assert.IsTrue(IOExtensions.IsSubPathOf(@"c:\foo\bar\", @"c:\foo\"));
            Assert.IsTrue(IOExtensions.IsSubPathOf(@"c:\foo\bar", @"c:\foo\"));
            Assert.IsTrue(IOExtensions.IsSubPathOf(@"c:\foo\a.txt", @"c:\foo"));
            Assert.IsTrue(IOExtensions.IsSubPathOf(@"c:\FOO\a.txt", @"c:\foo"));
            Assert.IsTrue(IOExtensions.IsSubPathOf(@"c:/foo/a.txt", @"c:\foo"));
            Assert.IsTrue(IOExtensions.IsSubPathOf(@"c:\foo\..\bar\baz", @"c:\bar"));
            Assert.IsFalse(IOExtensions.IsSubPathOf(@"c:\foobar", @"c:\foo"));
            Assert.IsFalse(IOExtensions.IsSubPathOf(@"c:\foobar\a.txt", @"c:\foo"));
            Assert.IsFalse(IOExtensions.IsSubPathOf(@"c:\foobar\a.txt", @"c:\foo\"));
            Assert.IsFalse(IOExtensions.IsSubPathOf(@"c:\foo\a.txt", @"c:\foobar"));
            Assert.IsFalse(IOExtensions.IsSubPathOf(@"c:\foo\a.txt", @"c:\foobar\"));
            Assert.IsFalse(IOExtensions.IsSubPathOf(@"c:\foo\..\bar\baz", @"c:\foo"));
            Assert.IsFalse(IOExtensions.IsSubPathOf(@"c:\foo\..\bar\baz", @"c:\barr"));
        }

        [TestMethod]
        public void SoundPath()
        {
            Assert.AreEqual("craftpolis", Sound.GetModidFromSoundName("craftpolis:entity/jump"));
            Assert.AreEqual("entity/jump", Sound.GetRelativePathFromSoundName("craftpolis:entity/jump"));
        }

        [TestMethod]
        public void PathValidation()
        {
            Assert.IsTrue(IOExtensions.IsDirectoryPath(@"C:\Dev\ForgeModGenerator\ForgeModGenerator\mods\Craftpolis\src\main\resources"));
            Assert.IsTrue(IOExtensions.IsDirectoryPath(@"\ForgeModGenerator\ForgeModGenerator\mods\Craftpolis"));
            Assert.IsTrue(IOExtensions.IsDirectoryPath(@"C:\Dev\ForgeModGenerator\ForgeModGenerator\mods\Craftpolis\src\main\resources"));
            Assert.IsTrue(IOExtensions.IsDirectoryPath(@"Craftpolis"));

            Assert.IsFalse(IOExtensions.IsDirectoryPath(@"C:\Dev\ForgeModGenerator\ForgeModGenerator\mods\Craftpolis\src\main\resources.smth"));
            Assert.IsFalse(IOExtensions.IsDirectoryPath(@"Craftpolis.lol"));

            Assert.IsFalse(IOExtensions.IsDirectoryPath(@"C:\Dev\ForgeModGenerator\ForgeModGenerator\mods\Craftpolis\src\main\resou:;<>rcessmth"));
            Assert.IsFalse(IOExtensions.IsDirectoryPath(@"<>?:32"));

            Assert.IsTrue(IOExtensions.IsFilePath(@"C:\Dev\ForgeModGenerator\ForgeModGenerator\mods\Craftpolis\src\main\resources\smth.png"));
            Assert.IsTrue(IOExtensions.IsFilePath(@"\ForgeModGenerator\ForgeModGenerator\mods\Craftpolis.png"));
            Assert.IsTrue(IOExtensions.IsFilePath(@"Craftpolis.png"));

            Assert.IsFalse(IOExtensions.IsFilePath(@"C:\Dev\ForgeModGenerator\ForgeModGenerator\mods\Craftpolis\src\main\resou:;<>rces.smth"));
            Assert.IsFalse(IOExtensions.IsFilePath(@"<>?:32.png"));

            Assert.IsTrue(IOExtensions.IsPathValid(@"C:\Dev\ForgeModGenerator\ForgeModGenerator\mods\Craftpolis\src\main\resources"));
            Assert.IsTrue(IOExtensions.IsPathValid(@"\ForgeModGenerator\ForgeModGenerator\mods\Craftpolis"));
        }

        [TestMethod]
        public void GetModidFromPath()
        {
            string path = @"C:\Dev\ForgeModGenerator\ForgeModGenerator\mods\Craftpolis\src\main\resources";
            string path1 = @"C:\Dev\ForgeModGenerator\ForgeModGenerator\mods\Craftpolis\src\main\resources\assets\craftpolis";
            string path2 = @"craftpolis:entity/something/either";
            string path3 = @"craftpolis:entity.something.either";
            string path4 = @"C:\Dev\ForgeModGenerator\ForgeModGenerator\mods\Craftpolis";
            string path5 = @"\ForgeModGenerator\ForgeModGenerator\mods\Craftpolis";

            string resultModid = GetModidFromPath(path);
            string resultModid1 = GetModidFromPath(path1);
            string resultModid2 = GetModidFromPath(path2);
            string resultModid3 = GetModidFromPath(path3);
            string resultModid4 = GetModidFromPath(path4);
            string resultModid5 = GetModidFromPath(path5);

            Assert.AreEqual("craftpolis", resultModid);
            Assert.AreEqual("craftpolis", resultModid1);
            Assert.AreEqual("craftpolis", resultModid2);
            Assert.AreEqual("craftpolis", resultModid3);
            Assert.AreEqual("craftpolis", resultModid4);
            Assert.AreEqual(null, resultModid5);
        }

        [TestMethod]
        public void GetModnameFromPath()
        {
            string path = @"C:\Dev\ForgeModGenerator\ForgeModGenerator\mods\Craftpolis\src\main\resources";
            string path1 = @"C:\Dev\ForgeModGenerator\ForgeModGenerator\mods\Craftpolis\src\main\resources\assets\craftpolis";
            string path2 = @"C:\Dev\ForgeModGenerator\ForgeModGenerator\mods\Craftpolis";
            string path3 = @"\ForgeModGenerator\ForgeModGenerator\mods\Craftpolis";
            string path4 = "C:\\Dev\\ForgeModGenerator\\ForgeModGenerator\\mods\\Craftpolis\\src\\main\\resources\\assets\\craftpolis\\sounds\\test.ogg";

            string resultModid = GetModnameFromPath(path);
            string resultModid1 = GetModnameFromPath(path1);
            string resultModid2 = GetModnameFromPath(path2);
            string resultModid3 = GetModnameFromPath(path3);
            string resultModid4 = GetModnameFromPath(path4);

            Assert.AreEqual("Craftpolis", resultModid);
            Assert.AreEqual("Craftpolis", resultModid1);
            Assert.AreEqual("Craftpolis", resultModid2);
            Assert.AreEqual(null, resultModid3);
            Assert.AreEqual("Craftpolis", resultModid4);
        }

        public static string GetModidFromPath(string path)
        {
            path = path.Replace("\\", "/");
            int index = !path.Contains(":/") ? path.IndexOf(':') : -1;
            if (index >= 1)
            {
                return path.Substring(0, index);
            }
            string modname = GetModnameFromPath(path);
            if (modname != null)
            {
                string assetsPath = $@"C:\Dev\ForgeModGenerator\ForgeModGenerator\mods\{modname}\src\main\resources\assets"; // in assets folder there should be always folder with modid
                int assetsPathLength = assetsPath.Length;
                try
                {
                    string directory = Directory.EnumerateDirectories(assetsPath).First();
                    string dir = directory.Replace("\\", "/");
                    return dir.Remove(0, assetsPathLength + 1);
                }
                catch (System.Exception) { }
            }
            return null;
        }

        public static string GetModnameFromPath(string path)
        {
            if (!IOExtensions.IsPathValid(path))
            {
                return null;
            }
            path = path.NormalizePath();
            string modsPath = @"C:/Dev/ForgeModGenerator/ForgeModGenerator/mods".NormalizePath();
            int length = modsPath.Length;
            if (!path.StartsWith(modsPath))
            {
                return null;
            }
            try
            {
                string sub = path.Remove(0, length + 1);
                int index = sub.IndexOf("/");
                return index >= 1 ? sub.Substring(0, index) : sub;
            }
            catch (System.Exception)
            {
                return null;
            }
        }
    }
}
