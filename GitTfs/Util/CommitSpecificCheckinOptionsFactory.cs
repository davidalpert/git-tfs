using System;
using System.IO;
using Sep.Git.Tfs.Commands;
using System.Text.RegularExpressions;

namespace Sep.Git.Tfs.Util
{
    /// <summary>
    /// Creates a new <see cref="CheckinOptions"/> that is customized based 
    /// on extracting special git-tfs commands from a git commit message.
    /// </summary>
    /// <remarks>
    /// This class handles the pre-checkin commit message parsing that
    /// enables special git-tfs commands: 
    /// https://github.com/git-tfs/git-tfs/wiki/Special-actions-in-commit-messages
    /// </remarks>
    public class CommitSpecificCheckinOptionsFactory
    {
        public CheckinOptions BuildCommitSpecificCheckinOptions(CheckinOptions sourceCheckinOptions, string commitMessage)
        {
            var customCheckinOptions = Clone(sourceCheckinOptions);

            customCheckinOptions.CheckinComment = commitMessage;

            ProcessWorkItemCommands(customCheckinOptions);

            ProcessForceCommand(customCheckinOptions);

            return customCheckinOptions;
        }

        private CheckinOptions Clone(CheckinOptions source)
        {
            CheckinOptions clone = new CheckinOptions();

            clone.CheckinComment = source.CheckinComment;
            clone.NoGenerateCheckinComment = source.NoGenerateCheckinComment;
            clone.NoMerge = source.NoMerge;
            clone.OverrideReason = source.OverrideReason;
            clone.Force = source.Force;
            clone.OverrideGatedCheckIn = source.OverrideGatedCheckIn;
            clone.WorkItemsToAssociate.AddRange(source.WorkItemsToAssociate);
            clone.WorkItemsToResolve.AddRange(source.WorkItemsToResolve);

            return clone;
        }

        private void ProcessWorkItemCommands(CheckinOptions checkinOptions)
        {
            MatchCollection workitemMatches;
            if ((workitemMatches = GitTfsConstants.TfsWorkItemRegex.Matches(checkinOptions.CheckinComment)).Count > 0)
            {
                foreach (Match match in workitemMatches)
                {
                    switch (match.Groups["action"].Value)
                    {
                        case "associate":
                            checkinOptions.WorkItemsToAssociate.Add(match.Groups["item_id"].Value);
                            break;
                        case "resolve":
                            checkinOptions.WorkItemsToResolve.Add(match.Groups["item_id"].Value);
                            break;
                    }
                }
                checkinOptions.CheckinComment = GitTfsConstants.TfsWorkItemRegex.Replace(checkinOptions.CheckinComment, "").Trim(' ', '\r', '\n');
            }
        }

        private void ProcessForceCommand(CheckinOptions checkinOptions)
        {
            MatchCollection workitemMatches;
            if ((workitemMatches = GitTfsConstants.TfsForceRegex.Matches(checkinOptions.CheckinComment)).Count == 1)
            {
                string overrideReason = workitemMatches[0].Groups["reason"].Value;

                if (!string.IsNullOrWhiteSpace(overrideReason))
                {
                    checkinOptions.Force = true;
                    checkinOptions.OverrideReason = overrideReason;
                }
                checkinOptions.CheckinComment = GitTfsConstants.TfsForceRegex.Replace(checkinOptions.CheckinComment, "").Trim(' ', '\r', '\n');
            }
        }
    }
}
