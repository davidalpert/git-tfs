﻿using System;
using System.ComponentModel;
using System.IO;
using NDesk.Options;
using StructureMap;
using Sep.Git.Tfs.Core;

namespace Sep.Git.Tfs.Commands
{
    [Pluggable("info")]
    [Description("info")]
    [RequiresValidGitRepository]
    public class Info : GitTfsCommand
    {
        Globals globals;
        TextWriter stdout;
        IGitTfsVersionProvider versionProvider;

        public Info(Globals globals, TextWriter stdout, IGitTfsVersionProvider versionProvider)
        {
            this.globals = globals;
            this.stdout = stdout;
            this.versionProvider = versionProvider;
        }

        public OptionSet OptionSet { get { return globals.OptionSet; } }

        public int Run()
        {
            stdout.WriteLine(versionProvider.GetVersionString());

            var changeset = globals.Repository.GetLastParentTfsCommits("HEAD").FirstOr(null);

            if (changeset != null)
            {
                stdout.WriteLine("remote tfs id '{0}' maps to {1} {2}", globals.RemoteId, changeset.Remote.TfsUrl, changeset.Remote.TfsRepositoryPath);
            }

            return GitTfsExitCodes.OK;
        }
    }
}