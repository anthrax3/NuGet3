﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text;

namespace NuGet.LibraryModel
{
    public class LibraryDependency
    {
        public LibraryRange LibraryRange { get; set; }

        public LibraryDependencyType Type { get; set; } = LibraryDependencyType.Default;

        public string Name
        {
            get
            {
                return LibraryRange.Name;
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(LibraryRange);
            sb.Append(" ");

            sb.Append(Type);
            return sb.ToString();
        }

        public bool HasFlag(LibraryDependencyTypeFlag flag)
        {
            return Type.Contains(flag);
        }
    }
}
