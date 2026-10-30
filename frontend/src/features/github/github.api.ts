import { request } from '../../api/client'
import type { GithubRepositoryLink, GithubCommitResult } from './github.types'

export const githubApi = {
  list: (projectId: string) => request<GithubRepositoryLink[]>(`/v10/projects/${projectId}/repositories`),
  link: (projectId: string, repositoryUrl: string) =>
    request<GithubRepositoryLink>(`/v10/projects/${projectId}/repositories`, {
      method: 'POST', body: JSON.stringify({ repositoryUrl }),
    }),
  unlink: (repositoryId: string) =>
    request<void>(`/v10/repositories/${repositoryId}`, { method: 'DELETE' }),
  branches: (repositoryId: string) =>
    request<{ id: string; items: string[] }>(`/v10/repositories/${repositoryId}/branches`),
  commits: (repositoryId: string) =>
    request<GithubCommitResult>(`/v10/repositories/${repositoryId}/commits`),
}
