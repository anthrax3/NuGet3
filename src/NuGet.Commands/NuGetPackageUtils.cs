﻿using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using DefaultPackagePathResolver = NuGet.Packaging.DefaultPackagePathResolver;
using Microsoft.Framework.Logging;
using NuGet.Common;

namespace NuGet.Commands
{
    internal static class NuGetPackageUtils
    {
        private const string ManifestExtension = ".nuspec";

        internal static async Task InstallFromStream(
            Stream stream,
            LibraryIdentity library,
            string packagesDirectory)
        {
            var packagePathResolver = new DefaultPackagePathResolver(packagesDirectory);

            var targetPath = packagePathResolver.GetInstallPath(library.Name, library.Version);
            var targetNuspec = packagePathResolver.GetManifestFilePath(library.Name, library.Version);
            var targetNupkg = packagePathResolver.GetPackageFilePath(library.Name, library.Version);
            var hashPath = packagePathResolver.GetHashPath(library.Name, library.Version);

            // Acquire the lock on a nukpg before we extract it to prevent the race condition when multiple
            // processes are extracting to the same destination simultaneously
            await ConcurrencyUtilities.ExecuteWithFileLocked(targetNupkg, async createdNewLock =>
            {
                // If this is the first process trying to install the target nupkg, go ahead
                // After this process successfully installs the package, all other processes
                // waiting on this lock don't need to install it again.
                if (createdNewLock && !File.Exists(targetNupkg))
                {
                    Directory.CreateDirectory(targetPath);
                    using (var nupkgStream = new FileStream(
                        targetNupkg,
                        FileMode.Create,
                        FileAccess.ReadWrite,
                        FileShare.ReadWrite | FileShare.Delete,
                        bufferSize: 4096,
                        useAsync: true))
                    {
                        await stream.CopyToAsync(nupkgStream);
                        nupkgStream.Seek(0, SeekOrigin.Begin);

                        ExtractPackage(targetPath, nupkgStream);
                    }

                    // DNU REFACTORING TODO: delete the hacky FixNuSpecIdCasing() and uncomment logic below after we
                    // have implementation of NuSpecFormatter.Read()
                    // Fixup the casing of the nuspec on disk to match what we expect
                    var nuspecFile = Directory.EnumerateFiles(targetPath, "*" + ManifestExtension).Single();
                    FixNuSpecIdCasing(nuspecFile, targetNuspec, library.Name);

                    /*var actualNuSpecName = Path.GetFileName(nuspecFile);
                    var expectedNuSpecName = Path.GetFileName(targetNuspec);

                    if (!string.Equals(actualNuSpecName, expectedNuSpecName, StringComparison.Ordinal))
                    {
                        MetadataBuilder metadataBuilder = null;
                        var nuspecFormatter = new NuSpecFormatter();
                        using (var nuspecStream = File.OpenRead(nuspecFile))
                        {
                            metadataBuilder = nuspecFormatter.Read(nuspecStream);
                            // REVIEW: any way better hardcoding "id"?
                            metadataBuilder.SetMetadataValue("id", library.Name);
                        }

                        // Delete the previous nuspec file
                        File.Delete(nuspecFile);

                        // Write the new manifest
                        using (var targetNuspecStream = File.OpenWrite(targetNuspec))
                        {
                            nuspecFormatter.Save(metadataBuilder, targetNuspecStream);
                        }
                    }*/

                    stream.Seek(0, SeekOrigin.Begin);
                    string packageHash;
                    using (var sha512 = SHA512.Create())
                    {
                        packageHash = Convert.ToBase64String(sha512.ComputeHash(stream));
                    }

                    // Note: PackageRepository relies on the hash file being written out as the final operation as part of a package install
                    // to assume a package was fully installed.
                    File.WriteAllText(hashPath, packageHash);
                }

                return 0;
            });
        }

        // DNU REFACTORING TODO: delete this temporary workaround after we have NuSpecFormatter.Read()
        private static void FixNuSpecIdCasing(string nuspecFile, string targetNuspec, string correctedId)
        {
            var actualNuSpecName = Path.GetFileName(nuspecFile);
            var expectedNuSpecName = Path.GetFileName(targetNuspec);

            if (!string.Equals(actualNuSpecName, expectedNuSpecName, StringComparison.Ordinal))
            {
                var xDoc = System.Xml.Linq.XDocument.Parse(File.ReadAllText(nuspecFile),
                    System.Xml.Linq.LoadOptions.PreserveWhitespace);
                var metadataNode = xDoc.Root.Elements().Where(e => StringComparer.Ordinal.Equals(e.Name.LocalName, "metadata")).First();
                var node = metadataNode.Elements(System.Xml.Linq.XName.Get("id", metadataNode.GetDefaultNamespace().NamespaceName)).First();
                node.Value = correctedId;

                File.Delete(nuspecFile);

                using (var stream = File.OpenWrite(targetNuspec))
                {
                    xDoc.Save(stream);
                }
            }
        }

        internal static LibraryIdentity CreateLibraryFromNupkg(string nupkgPath)
        {
            using (var fileStream = File.OpenRead(nupkgPath))
            using (var archive = new ZipArchive(fileStream))
            {
                foreach (var entry in archive.Entries)
                {
                    if (!entry.Name.EndsWith(ManifestExtension, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    using (var entryStream = entry.Open())
                    {
                        var reader = new NuspecReader(entryStream);
                        return new LibraryIdentity()
                        {
                            Name = reader.GetId(),
                            Version = reader.GetVersion(),
                            Type = LibraryTypes.Package
                        };
                    }
                }

                throw new FormatException(
                    string.Format("{0} doesn't contain {1} entry", nupkgPath, ManifestExtension));
            }
        }

        private static void ExtractPackage(string targetPath, FileStream stream)
        {
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                ExtractNupkg(archive, targetPath);
            }
        }

        private static void ExtractNupkg(ZipArchive archive, string targetPath)
        {
            ExtractFiles(
                archive, 
                targetPath, 
                shouldInclude: NupkgFilter);
        }

        private static bool NupkgFilter(string fullName)
        {
            var fileName = Path.GetFileName(fullName);
            if (fileName != null)
            {
                if (fileName == ".rels")
                {
                    return false;
                }
                if (fileName == "[Content_Types].xml")
                {
                    return false;
                }
            }

            var extension = Path.GetExtension(fullName);
            if (extension == ".psmdcp")
            {
                return false;
            }

            return true;
        }

        public static void ExtractFiles(ZipArchive archive, string targetPath, Func<string, bool> shouldInclude)
        {
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                var entryFullName = entry.FullName;
                if (entryFullName.StartsWith("/", StringComparison.Ordinal))
                {
                    entryFullName = entryFullName.Substring(1);
                }
                entryFullName = Uri.UnescapeDataString(entryFullName.Replace('/', Path.DirectorySeparatorChar));


                var targetFile = Path.Combine(targetPath, entryFullName);
                if (!targetFile.StartsWith(targetPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!shouldInclude(entryFullName))
                {
                    continue;
                }

                if (Path.GetFileName(targetFile).Length == 0)
                {
                    Directory.CreateDirectory(targetFile);
                }
                else
                {
                    var targetEntryPath = Path.GetDirectoryName(targetFile);
                    if (!Directory.Exists(targetEntryPath))
                    {
                        Directory.CreateDirectory(targetEntryPath);
                    }

                    using (var entryStream = entry.Open())
                    {
                        using (var targetStream = new FileStream(targetFile, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            entryStream.CopyTo(targetStream);
                        }
                    }
                }
            }
        }

    }
}
