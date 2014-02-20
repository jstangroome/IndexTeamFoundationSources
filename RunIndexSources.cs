using System.Activities;
using System.Activities.Statements;
using System.Collections.Generic;
using System.IO;
using Microsoft.TeamFoundation.Build.Workflow.Activities;

namespace ClassLibrary1
{
    class RunIndexSources : Activity
    {
        public RunIndexSources()
        {
            base.Implementation = CreateBody;
        }

        public InArgument<string> BinariesDirectory { get; set; }

        private Activity CreateBody()
        {
            var pdbFilesVariable = new Variable<IEnumerable<string>>();
            return new Sequence
            {
                Variables = { pdbFilesVariable },
                Activities =
                {
                    new FindMatchingFiles
                    {
                        MatchPattern = new InArgument<string>(context => Path.Combine(BinariesDirectory.Get(context), @"**\*.pdb")),
                        Result = pdbFilesVariable
                    },
                    new IndexSources
                    {
                        FileList = pdbFilesVariable
                    }
                }
            };
        }

    }
}