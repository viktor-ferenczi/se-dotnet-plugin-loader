﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace avaness.PluginLoader
{
    public class AssemblyResolver
    {
        private readonly HashSet<string> allowedAssemblyNames = new HashSet<string>();
        private readonly HashSet<string> allowedAssemblyFiles = new HashSet<string>();
        private readonly List<string> sourceFolders = new List<string>();
        private readonly Dictionary<string, string> assemblies = new Dictionary<string, string>();
        private bool enabled;

        public event Action<string> AssemblyResolved;

        public AssemblyResolver() 
        {

        }

        /// <summary>
        /// Adds an assembly to the list of assemblies that are allowed to request from this resolver.
        /// </summary>
        public void AddAllowedAssemblyName(string assemblyName)
        {
            allowedAssemblyNames.Add(assemblyName);
        }

        /// <summary>
        /// Adds an assembly to the list of assemblies that are allowed to request from this resolver.
        /// </summary>
        public void AddAllowedAssemblyFile(string assemblyFile)
        {
            allowedAssemblyFiles.Add(Path.GetFullPath(assemblyFile));
        }

        /// <summary>
        /// Adds a folder of assemblies to resolve.
        /// </summary>
        public void AddSourceFolder(string folder, SearchOption fileSearch = SearchOption.TopDirectoryOnly)
        {
            if (!Directory.Exists(folder))
                return;

            sourceFolders.Add(Path.GetFullPath(folder));
            foreach (string name in Directory.EnumerateFiles(folder, "*.dll", fileSearch))
            {
                if (!Path.GetExtension(name).Equals(".dll", StringComparison.OrdinalIgnoreCase))
                    continue;
                string assemblyName = Path.GetFileNameWithoutExtension(name);
                if (!assemblies.ContainsKey(assemblyName))
                {
                    assemblies.Add(assemblyName, name);
                    if(!enabled)
                    {
                        AppDomain.CurrentDomain.AssemblyResolve += Resolve;
                        enabled = true;
                    }
                }
            }
        }

        private Assembly Resolve(object sender, ResolveEventArgs args)
        {
            if (!IsAllowedRequest(args.RequestingAssembly))
                return null;

            AssemblyName targetAssembly = new AssemblyName(args.Name);
            if (assemblies.TryGetValue(targetAssembly.Name, out string targetPath) && File.Exists(targetPath))
            {
                Assembly a = Assembly.LoadFile(targetPath);
                if (AssemblyResolved != null)
                    AssemblyResolved.Invoke(targetPath);
                LogFile.WriteLine($"Resolved {targetAssembly} as {a.GetName()} for {args.RequestingAssembly.GetName()}");
                return a;
            }
            return null;
        }

        private bool IsAllowedRequest(Assembly requestingAssembly)
        {
            if (requestingAssembly == null)
                return false;

            string name = requestingAssembly.GetName().Name;

            if (string.IsNullOrWhiteSpace(requestingAssembly.Location))
                return allowedAssemblyNames.Contains(name);

            string location = Path.GetFullPath(requestingAssembly.Location);

            if (allowedAssemblyFiles.Contains(location))
                return true;

            if (sourceFolders.Any(x => location.StartsWith(x, StringComparison.OrdinalIgnoreCase)))
                return true;

            return allowedAssemblyNames.Contains(name);
        }
    }
}
