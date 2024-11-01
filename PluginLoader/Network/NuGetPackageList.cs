﻿using ProtoBuf;
using System.Xml.Serialization;

namespace avaness.PluginLoader.Network
{
    [ProtoContract]
    public class NuGetPackageList
    {
        [ProtoMember(1)]
        public string Config { get; set; }

        [ProtoMember(2)]
        [XmlElement("PackageReference")]
        public NuGetPackageId[] PackageIds { get; set; }

        public string PackagesConfigNormalized => Config?.Replace('\\', '/').TrimStart('/');

        public bool HasPackages => !string.IsNullOrWhiteSpace(Config) || (PackageIds != null && PackageIds.Length > 0);
    }
}
