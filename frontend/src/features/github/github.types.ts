/** GitHub integration v0.10: public repositories only, scoped to a Project. */
export interface GithubRepositoryLink {
  id: string
  projectId: string
  repositoryUrl: string
  defaultBranch: string | null
  lastCommitAt: string | null
  createdAt: string
}
export interface GithubCommit {
  sha: string
  message: string
  author: string
  committedAt: string | null
  htmlUrl: string
}
export interface GithubCommitResult {
  id: string
  projectId: string
  repositoryUrl: string
  items: GithubCommit[]
}
