using Web.Services;

namespace Web.Services
{
    public class BitbucketUrlService
    {
        // Base Bitbucket URL. In a real app, this might come from configuration.
        private const string BitbucketBaseUrl = "https://bitbucket.org";

        /// <summary>
        /// Composes a Bitbucket URL for a specific commit.
        /// </summary>
        /// <param name="workspaceSlug">The slug of the Bitbucket workspace.</param>
        /// <param name="repoSlug">The slug of the repository.</param>
        /// <param name="commitHash">The hash of the commit.</param>
        /// <returns>The full Bitbucket URL for the commit.</returns>
        public string GetCommitUrl(string workspaceSlug, string repoSlug, string commitHash)
        {
            if (string.IsNullOrEmpty(workspaceSlug))
            {
                throw new ArgumentNullException(nameof(workspaceSlug), "Workspace slug cannot be null or empty.");
            }
            if (string.IsNullOrEmpty(repoSlug))
            {
                throw new ArgumentNullException(nameof(repoSlug), "Repository slug cannot be null or empty.");
            }
            if (string.IsNullOrEmpty(commitHash))
            {
                throw new ArgumentNullException(nameof(commitHash), "Commit hash cannot be null or empty.");
            }

            return $"{BitbucketBaseUrl}/{workspaceSlug}/{repoSlug}/commits/{commitHash}";
        }

        /// <summary>
        /// Composes a Bitbucket URL for a specific pull request.
        /// </summary>
        /// <param name="workspaceSlug">The slug of the Bitbucket workspace.</param>
        /// <param name="repoSlug">The slug of the repository.</param>
        /// <param name="pullRequestId">The ID of the pull request.</param>
        /// <returns>The full Bitbucket URL for the pull request.</returns>
        public string GetPullRequestUrl(string workspaceSlug, string repoSlug, long pullRequestId)
        {
            if (string.IsNullOrEmpty(workspaceSlug))
            {
                throw new ArgumentNullException(nameof(workspaceSlug), "Workspace slug cannot be null or empty.");
            }
            if (string.IsNullOrEmpty(repoSlug))
            {
                throw new ArgumentNullException(nameof(repoSlug), "Repository slug cannot be null or empty.");
            }
            if (pullRequestId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pullRequestId), "Pull Request ID must be a positive number.");
            }

            return $"{BitbucketBaseUrl}/{workspaceSlug}/{repoSlug}/pull-requests/{pullRequestId}";
        }
    }
} 