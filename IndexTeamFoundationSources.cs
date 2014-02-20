using System;
using System.Activities;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using ClassLibrary1.Tools;
using Microsoft.Build.Utilities;
using Microsoft.TeamFoundation.Build.Workflow.Activities;
using Microsoft.TeamFoundation.Client;
using Microsoft.Win32;

namespace ClassLibrary1
{
    // NOTE: dbghelp.dll used internally by IndexSources appears to be resolved relative to this assembly
    public class IndexTeamFoundationSources : Task
    {
        public string TeamFoundationServerUrl { get; set; }
        public string BinariesDirectory { get; set; }
        public string TeamBuildWorkflowAssemblyDirectory { get; set; }
        public string PdbStrToolDirectory { get; set; }

        public override bool Execute()
        {
            var url = GetCollectionUri();
            var binDir = GetBinariesDirectory();
            var teamBuildWorkflowAssemblyPath = GetTeamBuildWorkflowAssemblyDirectory();
            var pdbStrToolDirectory = GetPdbStrToolDirectory();

            LoadBuildWorkflowAssembly(teamBuildWorkflowAssemblyPath);

            LoadDbgHelpLibrary();
            ExtractPdbSrcExe(pdbStrToolDirectory);

            var collection = TfsTeamProjectCollectionFactory.GetTeamProjectCollection(new Uri(url));

            var inputParameters = new Dictionary<string, object> { { "BinariesDirectory", binDir } };

            using (new IndexSourcesToolPathOverrideScope(pdbStrToolDirectory))
            {
                var workflow = new RunIndexSources();
                var invoker = new WorkflowInvoker(workflow);
                invoker.Extensions.Add(collection);
                invoker.Invoke(inputParameters);
            }

            return true;
        }

        private void ExtractPdbSrcExe(string pdbStrToolDirectory)
        {
            const string PdbStrFile = "pdbstr.exe";
            var pdbStrExePath = Path.Combine(pdbStrToolDirectory, PdbStrFile);
            if (File.Exists(pdbStrExePath)) return;
            EmbeddedResource.Extract(PdbStrFile, pdbStrExePath);
        }

        private void LoadDbgHelpLibrary()
        {
            const string DbgHelpFile = "dbghelp.dll";
            var workflowFolder = Path.GetDirectoryName(typeof(IndexSources).Assembly.Location);
            if (File.Exists(Path.Combine(workflowFolder, DbgHelpFile))) return;

            var processFolder = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            if (File.Exists(Path.Combine(processFolder, DbgHelpFile))) return;

            // TODO check debug tools folder, perhaps
            var myFolder = GetTaskDirectory();
            var localDbgHelpFile = Path.Combine(myFolder, DbgHelpFile);
            if (!File.Exists(localDbgHelpFile))
            {
                EmbeddedResource.Extract(DbgHelpFile, localDbgHelpFile);
                // TODO return a useful error message (or try another folder) if write access is denied.
            }

            LoadLibrary(localDbgHelpFile);
        }

        private static string GetTaskDirectory()
        {
            return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern int LoadLibrary(string lpFileName);

        private void LoadBuildWorkflowAssembly(string teamBuildWorkflowAssemblyPath)
        {
            const string assemblyName = "Microsoft.TeamFoundation.Build.Workflow";
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assemblyName.Equals(assembly.GetName().Name, StringComparison.InvariantCultureIgnoreCase))
                {
                    // already loaded
                    return;
                }
            }
            var assemblyPath = Path.Combine(teamBuildWorkflowAssemblyPath, assemblyName + ".dll");
            if (!File.Exists(assemblyPath))
            {
                throw new FileNotFoundException("Team Build Workflow assembly not found at expected location.", teamBuildWorkflowAssemblyPath);
            }
            Assembly.LoadFrom(assemblyPath);
        }

        private string GetTeamBuildWorkflowAssemblyDirectory()
        {
            var installPath = TeamBuildWorkflowAssemblyDirectory;
            if (!string.IsNullOrEmpty(installPath) && installPath.EndsWith(".dll"))
            {
                installPath = Path.GetDirectoryName(installPath);
            }
            if (string.IsNullOrEmpty(installPath))
            {
                installPath = GetTeamBuildInstallPath();
            }
            if (string.IsNullOrEmpty(installPath))
            {
                installPath = GetVisualStudioReferenceAssembliesPath();
            }
            if (string.IsNullOrEmpty(installPath))
            {
                throw new Exception("TeamBuildWorkflowAssemblyDirectory could not be inferred, specify the value explicitly.");
            }
            return installPath;
        }

        private string GetPdbStrToolDirectory()
        {
            var pdbStrPath = PdbStrToolDirectory;
            if (string.IsNullOrEmpty(pdbStrPath))
            {
                pdbStrPath = GetTeamBuildInstallPath();
            }
            if (string.IsNullOrEmpty(pdbStrPath))
            {
                pdbStrPath = GetTaskDirectory();
            }
            return pdbStrPath;
        }

        private static string GetVisualStudioReferenceAssembliesPath()
        {
            const string assembliesKeyPath = @"SOFTWARE\Microsoft\.NETFramework\v2.0.50727\AssemblyFoldersEx\Visual Studio v12.0 Reference Assemblies";
            var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32).OpenSubKey(assembliesKeyPath, writable: false);
            if (key == null) return string.Empty;
            return (string)key.GetValue(null);
        }

        private static string GetTeamBuildInstallPath()
        {
            const string teamBuildKeyPath = @"SOFTWARE\Microsoft\TeamFoundationServer\12.0\InstalledComponents\TeamBuild";
            var key = Registry.LocalMachine.OpenSubKey(teamBuildKeyPath, writable: false);
            if (key == null) return string.Empty;
            return (string) key.GetValue("InstallPath");
        }

        private string GetBinariesDirectory()
        {
            var binDir = BinariesDirectory;
            if (string.IsNullOrEmpty(binDir))
            {
                binDir = Environment.GetEnvironmentVariable("TF_BUILD_BINARIESDIRECTORY");
            }
            if (string.IsNullOrEmpty(binDir))
            {
                throw new Exception("BinariesDirectory could not be inferred, specify the value explicitly.");
            }
            return binDir;
        }

        private string GetCollectionUri()
        {
            var url = TeamFoundationServerUrl;
            if (string.IsNullOrEmpty(url))
            {
                url = Environment.GetEnvironmentVariable("TeamFoundationServerUrl");
            }
            // TODO try get from workspace
            if (string.IsNullOrEmpty(url))
            {
                throw new Exception("TeamFoundationServerUrl could not be inferred, specify the value explicitly.");
            }
            return url;
        }
    }
}