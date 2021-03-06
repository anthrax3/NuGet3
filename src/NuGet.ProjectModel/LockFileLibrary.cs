// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.ProjectModel
{
    public class LockFileLibrary
    {
        public string Name { get; set; }

        public NuGetVersion Version { get; set; }

        public bool IsServiceable { get; set; }

        public string Sha512 { get; set; }

        public IList<string> Files { get; set; } = new List<string>();

        // Old stuff

        public string Sha { get; set; }

        public IList<LockFileFrameworkGroup> FrameworkGroups { get; set; } = new List<LockFileFrameworkGroup>();
    }

    public class LockFileTarget
    {
        public NuGetFramework TargetFramework { get; set; }

        public string RuntimeIdentifier { get; set; }

        public IList<LockFileTargetLibrary> Libraries { get; set; } = new List<LockFileTargetLibrary>();
    }

    public class LockFileTargetLibrary
    {
        public string Name { get; set; }

        public NuGetVersion Version { get; set; }

        public IList<PackageDependency> Dependencies { get; set; } = new List<PackageDependency>();

        public IList<string> FrameworkAssemblies { get; set; } = new List<string>();

        public IList<string> RuntimeAssemblies { get; set; } = new List<string>();

        public IList<string> CompileTimeAssemblies { get; set; } = new List<string>();

        public IList<string> NativeLibraries { get; set; } = new List<string>();
    }

    // Old stuff
    public class LockFileFrameworkGroup
    {
        public NuGetFramework TargetFramework { get; set; }

        public IList<PackageDependency> Dependencies { get; set; } = new List<PackageDependency>();

        public IList<string> FrameworkAssemblies { get; set; } = new List<string>();

        public IList<string> RuntimeAssemblies { get; set; } = new List<string>();

        public IList<string> CompileTimeAssemblies { get; set; } = new List<string>();

        public IList<string> NativeLibraries { get; set; } = new List<string>();
    }
}
