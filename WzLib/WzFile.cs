// This file is part of MSIT.
// 
// MSIT is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// MSIT is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MSIT.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using MSIT.WzLib.Util;
using MSIT.WzLib.WzProperties;

namespace MSIT.WzLib
{
    /// <summary>
    ///   A class that contains all the information of a wz file
    /// </summary>
    public class WzFile : IWzFile
    {
        #region Fields

        internal byte[] WzIv;
        internal short fileVersion;
        internal WzHeader header;
        internal WzMapleVersion mapleVersion;
        internal string name = "";
        internal string path;
        internal short version;
        internal uint versionHash;
        internal WzDirectory wzDir;
        private bool _namesEnc = false;

        #endregion

        public WzFile(short gameVersion, WzMapleVersion version, bool namesEnc)
        {
            Header = WzHeader.GetDefault();
            fileVersion = gameVersion;
            mapleVersion = version;
            WzIv = WzTool.GetIvByMapleVersion(version);
            _namesEnc = namesEnc;
        }

        /// <summary>
        ///   Open a wz file from a file on the disk
        /// </summary>
        /// <param name="filePath"> Path to the wz file </param>
        public WzFile(string filePath, WzMapleVersion version, bool namesEnc)
        {
            name = Path.GetFileName(filePath);
            path = filePath;
            fileVersion = -1;
            mapleVersion = version;
            if (version == WzMapleVersion.GetFromZlz)
            {
                FileStream zlzStream = File.OpenRead(Path.Combine(Path.GetDirectoryName(filePath), "ZLZ.dll"));
                WzIv = WzKeyGenerator.GetIvFromZlz(zlzStream);
                zlzStream.Close();
            }
            else WzIv = WzTool.GetIvByMapleVersion(version);
            _namesEnc = namesEnc;
        }

        /// <summary>
        ///   Open a wz file from a file on the disk
        /// </summary>
        /// <param name="filePath"> Path to the wz file </param>
        public WzFile(string filePath, short gameVersion, WzMapleVersion version, bool namesEnc)
        {
            name = Path.GetFileName(filePath);
            path = filePath;
            fileVersion = gameVersion;
            mapleVersion = version;
            if (version == WzMapleVersion.GetFromZlz)
            {
                FileStream zlzStream = File.OpenRead(Path.Combine(Path.GetDirectoryName(filePath), "ZLZ.dll"));
                WzIv = WzKeyGenerator.GetIvFromZlz(zlzStream);
                zlzStream.Close();
            }
            else WzIv = WzTool.GetIvByMapleVersion(version);
            _namesEnc = namesEnc;
        }

        /// <summary>
        ///   The parsed IWzDir after having called ParseWzDirectory(), this can either be a WzDirectory or a WzListDirectory
        /// </summary>
        public WzDirectory WzDirectory
        {
            get { return wzDir; }
        }

        /// <summary>
        ///   Name of the WzFile
        /// </summary>
        public override string Name
        {
            get { return name; }
        }

        /// <summary>
        ///   The WzObjectType of the file
        /// </summary>
        public override WzObjectType ObjectType
        {
            get { return WzObjectType.File; }
        }

        /// <summary>
        ///   Returns WzDirectory[name]
        /// </summary>
        /// <param name="name"> Name </param>
        /// <returns> WzDirectory[name] </returns>
        public IWzObject this[string name]
        {
            get { return WzDirectory[name]; }
        }

        public WzHeader Header
        {
            get { return header; }
            set { header = value; }
        }

        public short Version
        {
            get { return fileVersion; }
            set { fileVersion = value; }
        }

        public override string FilePath
        {
            get { return path; }
        }

        public override WzMapleVersion MapleVersion
        {
            get { return mapleVersion; }
            set { mapleVersion = value; }
        }

        public override IWzObject Parent
        {
            get { return null; }
            internal set { }
        }

        public override IWzFile WzFileParent
        {
            get { return this; }
        }

        public override void Dispose()
        {
            if (wzDir.reader == null) return;
            wzDir.reader.Close();
            Header = null;
            path = null;
            name = null;
            WzDirectory.Dispose();
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        /// <summary>
        ///   Parses the wz file, if the wz file is a list.wz file, WzDirectory will be a WzListDirectory, if not, it'll simply be a WzDirectory
        /// </summary>
        public void ParseWzFile()
        {
            if (mapleVersion == WzMapleVersion.Generate) throw new InvalidOperationException("Cannot call ParseWzFile() if WZ file type is GENERATE");
            ParseMainWzDirectory();
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        public void ParseWzFile(byte[] WzIv)
        {
            if (mapleVersion != WzMapleVersion.Generate) throw new InvalidOperationException("Cannot call ParseWzFile(byte[] generateKey) if WZ file type is not GENERATE");
            this.WzIv = WzIv;
            ParseMainWzDirectory();
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        private void ParseMainWzDirectory()
        {
            if (path == null)
            {
                throw new NullReferenceException("Path is null.");
            }

            WzBinaryReader reader = new WzBinaryReader(File.Open(path, FileMode.Open), WzIv);

            Header = new WzHeader();
            Header.Ident = reader.ReadString(4);
            Header.FSize = reader.ReadUInt64();
            Header.FStart = reader.ReadUInt32();
            Header.Copyright = reader.ReadNullTerminatedString();
            reader.ReadBytes((int) (Header.FStart - reader.BaseStream.Position));
            reader.Header = Header;
            version = reader.ReadInt16();
            if (fileVersion == -1)
            {
                for (int j = 0; j < short.MaxValue; j++)
                {
                    fileVersion = (short) j;
                    versionHash = GetVersionHash(version, fileVersion);
                    if (versionHash != 0)
                    {
                        reader.Hash = versionHash;
                        long position = reader.BaseStream.Position;
                        WzDirectory testDirectory = null;
                        try
                        {
                            testDirectory = new WzDirectory(reader, name, versionHash, WzIv, this, _namesEnc);
                            testDirectory.ParseDirectory();
                        }
                        catch
                        {
                            reader.BaseStream.Position = position;
                            continue;
                        }
                        WzImage testImage = testDirectory.GetChildImages()[0];

                        try
                        {
                            reader.BaseStream.Position = testImage.Offset;
                            byte checkByte = reader.ReadByte();
                            reader.BaseStream.Position = position;
                            testDirectory.Dispose();
                            switch (checkByte)
                            {
                                case 0x73:
                                case 0x1b:
                                    {
                                        WzDirectory directory = new WzDirectory(reader, name, versionHash, WzIv, this, _namesEnc);
                                        directory.ParseDirectory();
                                        wzDir = directory;
                                        return;
                                    }
                            }
                            reader.BaseStream.Position = position;
                        }
                        catch
                        {
                            reader.BaseStream.Position = position;
                        }
                    }
                }
                throw new Exception("Error with game version hash : The specified game version is incorrect and WzLib was unable to determine the version itself");
            }
            else
            {
                versionHash = GetVersionHash(version, fileVersion);
                reader.Hash = versionHash;
                WzDirectory directory = new WzDirectory(reader, name, versionHash, WzIv, this, _namesEnc);
                directory.ParseDirectory();
                wzDir = directory;
            }
        }

        private uint GetVersionHash(int encver, int realver)
        {
            int EncryptedVersionNumber = encver;
            int VersionNumber = realver;
            int VersionHash = 0;
            int DecryptedVersionNumber = 0;
            string VersionNumberStr;
            int a = 0, b = 0, c = 0, d = 0, l = 0;

            VersionNumberStr = VersionNumber.ToString();

            l = VersionNumberStr.Length;
            for (int i = 0; i < l; i++)
            {
                VersionHash = (32*VersionHash) + VersionNumberStr[i] + 1;
            }
            a = (VersionHash >> 24) & 0xFF;
            b = (VersionHash >> 16) & 0xFF;
            c = (VersionHash >> 8) & 0xFF;
            d = VersionHash & 0xFF;
            DecryptedVersionNumber = (0xff ^ a ^ b ^ c ^ d);

            if (EncryptedVersionNumber == DecryptedVersionNumber)
            {
                return Convert.ToUInt32(VersionHash);
            }
            else
            {
                return 0;
            }
        }

        /// <summary>
        ///   Returns an array of objects from a given path. Wild cards are supported For example : GetObjectsFromPath("Map.wz/Map0/*"); Would return all the objects (in this case images) from the sub directory Map0
        /// </summary>
        /// <param name="path"> The path to the object(s) </param>
        /// <returns> An array of IWzObjects containing the found objects </returns>
        public List<IWzObject> GetObjectsFromWildcardPath(string path)
        {
            if (path.ToLower() == name.ToLower()) return new List<IWzObject> {WzDirectory};
            else if (path == "*")
            {
                List<IWzObject> fullList = new List<IWzObject>();
                fullList.Add(WzDirectory);
                fullList.AddRange(GetObjectsFromDirectory(WzDirectory));
                return fullList;
            }
            else if (!path.Contains("*")) return new List<IWzObject> {GetObjectFromPath(path)};
            string[] seperatedNames = path.Split("/".ToCharArray());
            if (seperatedNames.Length == 2 && seperatedNames[1] == "*") return GetObjectsFromDirectory(WzDirectory);
            List<IWzObject> objList = new List<IWzObject>();
            foreach (WzImage img in WzDirectory.WzImages) foreach (string spath in GetPathsFromImage(img, name + "/" + img.Name)) if (strMatch(path, spath)) objList.Add(GetObjectFromPath(spath));
            foreach (WzDirectory dir in wzDir.WzDirectories) foreach (string spath in GetPathsFromDirectory(dir, name + "/" + dir.Name)) if (strMatch(path, spath)) objList.Add(GetObjectFromPath(spath));
            GC.Collect();
            GC.WaitForPendingFinalizers();
            return objList;
        }

        public List<IWzObject> GetObjectsFromRegexPath(string path)
        {
            if (path.ToLower() == name.ToLower()) return new List<IWzObject> {WzDirectory};
            List<IWzObject> objList = new List<IWzObject>();
            foreach (WzImage img in WzDirectory.WzImages) foreach (string spath in GetPathsFromImage(img, name + "/" + img.Name)) if (Regex.Match(spath, path).Success) objList.Add(GetObjectFromPath(spath));
            foreach (WzDirectory dir in wzDir.WzDirectories) foreach (string spath in GetPathsFromDirectory(dir, name + "/" + dir.Name)) if (Regex.Match(spath, path).Success) objList.Add(GetObjectFromPath(spath));
            GC.Collect();
            GC.WaitForPendingFinalizers();
            return objList;
        }

        public List<IWzObject> GetObjectsFromDirectory(WzDirectory dir)
        {
            List<IWzObject> objList = new List<IWzObject>();
            foreach (WzImage img in dir.WzImages)
            {
                objList.Add(img);
                objList.AddRange(GetObjectsFromImage(img));
            }
            foreach (WzDirectory subdir in dir.WzDirectories)
            {
                objList.Add(subdir);
                objList.AddRange(GetObjectsFromDirectory(subdir));
            }
            return objList;
        }

        public List<IWzObject> GetObjectsFromImage(WzImage img)
        {
            List<IWzObject> objList = new List<IWzObject>();
            foreach (IWzImageProperty prop in img.WzProperties)
            {
                objList.Add(prop);
                objList.AddRange(GetObjectsFromProperty(prop));
            }
            return objList;
        }

        public List<IWzObject> GetObjectsFromProperty(IWzImageProperty prop)
        {
            List<IWzObject> objList = new List<IWzObject>();
            switch (prop.PropertyType)
            {
                case WzPropertyType.Canvas:
                    foreach (IWzImageProperty canvasProp in (prop).WzProperties) objList.AddRange(GetObjectsFromProperty(canvasProp));
                    objList.Add(((WzCanvasProperty) prop).PngProperty);
                    break;
                case WzPropertyType.Convex:
                    foreach (IWzImageProperty exProp in (prop).WzProperties) objList.AddRange(GetObjectsFromProperty(exProp));
                    break;
                case WzPropertyType.SubProperty:
                    foreach (IWzImageProperty subProp in (prop).WzProperties) objList.AddRange(GetObjectsFromProperty(subProp));
                    break;
                case WzPropertyType.Vector:
                    objList.Add(((WzVectorProperty) prop).X);
                    objList.Add(((WzVectorProperty) prop).Y);
                    break;
            }
            return objList;
        }

        internal List<string> GetPathsFromDirectory(WzDirectory dir, string curPath)
        {
            List<string> objList = new List<string>();
            foreach (WzImage img in dir.WzImages)
            {
                objList.Add(curPath + "/" + img.Name);

                objList.AddRange(GetPathsFromImage(img, curPath + "/" + img.Name));
            }
            foreach (WzDirectory subdir in dir.WzDirectories)
            {
                objList.Add(curPath + "/" + subdir.Name);
                objList.AddRange(GetPathsFromDirectory(subdir, curPath + "/" + subdir.Name));
            }
            return objList;
        }

        internal List<string> GetPathsFromImage(WzImage img, string curPath)
        {
            List<string> objList = new List<string>();
            foreach (IWzImageProperty prop in img.WzProperties)
            {
                objList.Add(curPath + "/" + prop.Name);
                objList.AddRange(GetPathsFromProperty(prop, curPath + "/" + prop.Name));
            }
            return objList;
        }

        internal List<string> GetPathsFromProperty(IWzImageProperty prop, string curPath)
        {
            List<string> objList = new List<string>();
            switch (prop.PropertyType)
            {
                case WzPropertyType.Canvas:
                    foreach (IWzImageProperty canvasProp in (prop).WzProperties)
                    {
                        objList.Add(curPath + "/" + canvasProp.Name);
                        objList.AddRange(GetPathsFromProperty(canvasProp, curPath + "/" + canvasProp.Name));
                    }
                    objList.Add(curPath + "/PNG");
                    break;
                case WzPropertyType.Convex:
                    foreach (IWzImageProperty exProp in (prop).WzProperties)
                    {
                        objList.Add(curPath + "/" + exProp.Name);
                        objList.AddRange(GetPathsFromProperty(exProp, curPath + "/" + exProp.Name));
                    }
                    break;
                case WzPropertyType.SubProperty:
                    foreach (IWzImageProperty subProp in (prop).WzProperties)
                    {
                        objList.Add(curPath + "/" + subProp.Name);
                        objList.AddRange(GetPathsFromProperty(subProp, curPath + "/" + subProp.Name));
                    }
                    break;
                case WzPropertyType.Vector:
                    objList.Add(curPath + "/X");
                    objList.Add(curPath + "/Y");
                    break;
            }
            return objList;
        }

        public IWzObject GetObjectFromPath(string path)
        {
            string[] seperatedPath = path.Split('/');
            if (seperatedPath[0].ToLower() != wzDir.name.ToLower() && seperatedPath[0].ToLower() != wzDir.name.Substring(0, wzDir.name.Length - 3).ToLower()) return null;
            if (seperatedPath.Length == 1) return WzDirectory;
            IWzObject curObj = WzDirectory;
            for (int i = 1; i < seperatedPath.Length; i++)
            {
                if (curObj == null)
                {
                    return null;
                }
                switch (curObj.ObjectType)
                {
                    case WzObjectType.Directory:
                        curObj = ((WzDirectory) curObj)[seperatedPath[i]];
                        continue;
                    case WzObjectType.Image:
                        curObj = ((WzImage) curObj)[seperatedPath[i]];
                        continue;
                    case WzObjectType.Property:
                        switch (((IWzImageProperty) curObj).PropertyType)
                        {
                            case WzPropertyType.Canvas:
                                curObj = ((WzCanvasProperty) curObj)[seperatedPath[i]];
                                continue;
                            case WzPropertyType.Convex:
                                curObj = ((WzConvexProperty) curObj)[seperatedPath[i]];
                                continue;
                            case WzPropertyType.SubProperty:
                                curObj = ((WzSubProperty) curObj)[seperatedPath[i]];
                                continue;
                            case WzPropertyType.Vector:
                                if (seperatedPath[i] == "X") return ((WzVectorProperty) curObj).X;
                                else if (seperatedPath[i] == "Y") return ((WzVectorProperty) curObj).Y;
                                else return null;
                            default: // Wut?
                                return null;
                        }
                }
            }
            if (curObj == null)
            {
                return null;
            }
            return curObj;
        }

        internal bool strMatch(string strWildCard, string strCompare)
        {
            if (strWildCard.Length == 0) return strCompare.Length == 0;
            if (strCompare.Length == 0) return false;
            if (strWildCard[0] == '*' && strWildCard.Length > 1)
                for (int index = 0; index < strCompare.Length; index++)
                {
                    if (strMatch(strWildCard.Substring(1), strCompare.Substring(index))) return true;
                }
            else if (strWildCard[0] == '*') return true;
            else if (strWildCard[0] == strCompare[0]) return strMatch(strWildCard.Substring(1), strCompare.Substring(1));
            return false;
        }
    }
}