﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Framework.Logging;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Protocol.Core.Types;
using NuGet.RuntimeModel;
using NuGet.Versioning;

namespace NuGet.DependencyResolver
{
    public class SourceRepositoryDependencyProvider : IRemoteDependencyProvider
    {
        private readonly SourceRepository _sourceRepository;
        private readonly ILogger _logger;
        private readonly bool _noCache;
        private FindPackageByIdResource _findPackagesByIdResource;

        public SourceRepositoryDependencyProvider(
            SourceRepository sourceRepository,
            ILoggerFactory loggerFactory)
            : this(sourceRepository, loggerFactory, noCache: false)
        {

        }

        public SourceRepositoryDependencyProvider(
            SourceRepository sourceRepository,
            ILoggerFactory loggerFactory,
            bool noCache)
        {
            var logger = loggerFactory.CreateLogger(
                    typeof(SourceRepository).FullName + ":" + sourceRepository.PackageSource.Source);
            _logger = loggerFactory.CreateLogger<SourceRepositoryDependencyProvider>();

            _sourceRepository = sourceRepository;
            _noCache = noCache;
        }

        public bool IsHttp => _sourceRepository.PackageSource.IsHttp;

        public async Task<LibraryIdentity> FindLibraryAsync(LibraryRange libraryRange, NuGetFramework targetFramework, CancellationToken cancellationToken)
        {
            await EnsureResource();

            var packageVersions = await _findPackagesByIdResource.GetAllVersionsAsync(libraryRange.Name, cancellationToken);

            var packageVersion = packageVersions.FindBestMatch(libraryRange.VersionRange, version => version);

            if (packageVersion != null)
            {
                return new LibraryIdentity
                {
                    Name = libraryRange.Name,
                    Version = packageVersion,
                    Type = LibraryTypes.Package
                };
            }

            return null;
        }

        public async Task<IEnumerable<LibraryDependency>> GetDependenciesAsync(LibraryIdentity match, NuGetFramework targetFramework, CancellationToken cancellationToken)
        {
            await EnsureResource();

            var nuspecReader = await _findPackagesByIdResource.GetNuspecReaderAsync(match.Name, match.Version, cancellationToken);

            return GetDependencies(nuspecReader, targetFramework);
        }

        public async Task CopyToAsync(LibraryIdentity identity, Stream stream, CancellationToken cancellationToken)
        {
            await EnsureResource();

            using (var nupkgStream = await _findPackagesByIdResource.GetNupkgStreamAsync(identity.Name, identity.Version, cancellationToken))
            {
                await nupkgStream.CopyToAsync(stream, bufferSize: 8192, cancellationToken: cancellationToken);
            }
        }

        private IEnumerable<LibraryDependency> GetDependencies(NuspecReader nuspecReader, NuGetFramework targetFramework)
        {
            var dependencies = NuGetFrameworkUtility.GetNearest(nuspecReader.GetDependencyGroups(),
                                                      targetFramework,
                                                      item => item.TargetFramework);

            var frameworkAssemblies = NuGetFrameworkUtility.GetNearest(nuspecReader.GetFrameworkReferenceGroups(),
                                                             targetFramework,
                                                             item => item.TargetFramework);

            return GetDependencies(targetFramework, dependencies, frameworkAssemblies);
        }

        private static IList<LibraryDependency> GetDependencies(NuGetFramework targetFramework,
                                                                PackageDependencyGroup dependencies,
                                                                FrameworkSpecificGroup frameworkAssemblies)
        {
            var libraryDependencies = new List<LibraryDependency>();

            if (dependencies != null)
            {
                foreach (var d in dependencies.Packages)
                {
                    libraryDependencies.Add(new LibraryDependency
                    {
                        LibraryRange = new LibraryRange
                        {
                            Name = d.Id,
                            VersionRange = d.VersionRange
                        }
                    });
                }
            }

            if (frameworkAssemblies == null)
            {
                return libraryDependencies;
            }

            if (frameworkAssemblies.TargetFramework.AnyPlatform && !targetFramework.IsDesktop())
            {
                // REVIEW: This isn't 100% correct since none *can* mean 
                // any in theory, but in practice it means .NET full reference assembly
                // If there's no supported target frameworks and we're not targeting
                // the desktop framework then skip it.

                // To do this properly we'll need all reference assemblies supported
                // by each supported target framework which isn't always available.
                return libraryDependencies;
            }

            foreach (var name in frameworkAssemblies.Items)
            {
                libraryDependencies.Add(new LibraryDependency
                {
                    LibraryRange = new LibraryRange
                    {
                        Name = name,
                        TypeConstraint = LibraryTypes.Reference
                    }
                });
            }

            return libraryDependencies;
        }

        private async Task EnsureResource()
        {
            if (_findPackagesByIdResource == null)
            {
                _findPackagesByIdResource = await _sourceRepository.GetResourceAsync<FindPackageByIdResource>();
                _findPackagesByIdResource.Logger = _logger;
                _findPackagesByIdResource.NoCache = _noCache;
            }
        }
    }
}
