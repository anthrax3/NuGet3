﻿using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Versioning;

namespace NuGet.Protocol.Core.v3.MetadataClient
{
    internal static class Utils
    {
        public static VersionRange CreateVersionRange(string stringToParse, bool includePrerelease)
        {
            VersionRange range = VersionRange.Parse(string.IsNullOrEmpty(stringToParse) ? "[0.0.0-alpha,)" : stringToParse);
            return new VersionRange(range.MinVersion, range.IsMinInclusive, range.MaxVersion, range.IsMaxInclusive, includePrerelease);
        }

        public static async Task<JObject> GetJObjectAsync(HttpClient httpClient, Uri registrationUri)
        {
            string json = await httpClient.GetStringAsync(registrationUri);
            return JObject.Parse(json);
        }

        public static VersionRange SetIncludePrerelease(VersionRange range, bool includePrerelease)
        {
            return new VersionRange(range.MinVersion, range.IsMinInclusive, range.MaxVersion, range.IsMaxInclusive, includePrerelease);
        }

        public static string Indent(int depth)
        {
            return new string(Enumerable.Repeat(' ', depth).ToArray());
        }

    }
}
