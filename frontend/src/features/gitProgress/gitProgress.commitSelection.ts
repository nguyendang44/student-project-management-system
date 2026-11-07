import type { GithubCommit } from '../github/github.types'

/** GitHub returns newest commits first; preserve order and use complete SHA values. */
export function latestTwentyValidCommits(items: readonly GithubCommit[]): GithubCommit[] {
  return items.filter(c => /^[0-9a-f]{40}$/i.test(c.sha)).slice(0, 20)
}

/** A descending GitHub list permits Base (older) only after Head (newer). */
export function isCommitOrderValid(commits: readonly GithubCommit[], baseSha: string, headSha: string): boolean {
  const baseIndex = commits.findIndex(c => c.sha === baseSha)
  const headIndex = commits.findIndex(c => c.sha === headSha)
  return headIndex >= 0 && baseIndex > headIndex
}

export function formatCommitChoice(commit: GithubCommit): string {
  const message = commit.message.split('\n')[0].trim().slice(0, 65) || '(Không có mô tả)'
  const author = commit.author?.trim() || 'Không rõ tác giả'
  const when = commit.committedAt && !Number.isNaN(new Date(commit.committedAt).getTime())
    ? new Date(commit.committedAt).toLocaleDateString('vi-VN') : 'Không rõ ngày'
  return `${commit.sha.slice(0, 8)} · ${message} · ${author} · ${when}`
}
