using System;
using System.IO;
using System.Reflection;
using Microsoft.TeamFoundation.Build.Workflow.Activities;

namespace ClassLibrary1
{
    // By default pdbstr.exe used internally by IndexSources is resolved relative to the host process, which for MSBuild will be a folder only writable by Admins
    class IndexSourcesToolPathOverrideScope : IDisposable
    {
        private readonly FieldInfo _toolPathField;
        private string _originalToolPath;

        public IndexSourcesToolPathOverrideScope(string overridePath)
        {
            const string PdbStrFileName = "pdbstr.exe";
            var indexSourcesType = typeof(IndexSources);

            _toolPathField = indexSourcesType.GetField("m_toolPath", BindingFlags.NonPublic | BindingFlags.Static);
            if (_toolPathField == null) throw new NotSupportedException("Cannot access private tool path field.");

            var toolPathProperty = indexSourcesType.GetProperty("ToolPath", BindingFlags.NonPublic | BindingFlags.Static);
            if (toolPathProperty == null) throw new NotSupportedException("Cannot access private tool path property.");
            _originalToolPath = (string)toolPathProperty.GetValue(null);
            var currentPdbStrPath = Path.Combine(_originalToolPath, PdbStrFileName);

            if (File.Exists(currentPdbStrPath))
            {
                // pdbstr exists at expected location, override not required.
                return;
            }

            var newPdbStrPath = Path.Combine(overridePath, PdbStrFileName);
            if (!File.Exists(newPdbStrPath))
            {
                throw new FileNotFoundException("Expected pdbstr tool not found.", newPdbStrPath);
            }
            SetToolPath(overridePath);
        }

        private void SetToolPath(string toolPath)
        {
            _toolPathField.SetValue(null, toolPath);
        }

        public void Dispose()
        {
            SetToolPath(_originalToolPath);
        }
    }
}