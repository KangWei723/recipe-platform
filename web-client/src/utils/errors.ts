import type { CombinedError } from 'urql';

const GRAPHQL_PREFIX = '[GraphQL] ';

// urql's CombinedError.message prefixes the first GraphQL error with "[GraphQL] " -- strip it
// so server-supplied messages (e.g. "Cannot delete ingredient: used in 3 recipes") read as plain
// text instead of looking like a raw error dump.
export function formatMutationError(error: CombinedError, fallback: string): string {
  const message = error.graphQLErrors[0]?.message ?? error.message;
  return message.startsWith(GRAPHQL_PREFIX) ? message.slice(GRAPHQL_PREFIX.length) : message || fallback;
}
