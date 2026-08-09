import { useAuth0 } from '@auth0/auth0-react';
import { useMemo, type ReactNode } from 'react';
import { Provider, cacheExchange, createClient, fetchExchange } from 'urql';

export function GraphQLProvider({ children }: { children: ReactNode }) {
  const { getAccessTokenSilently } = useAuth0();

  const client = useMemo(
    () =>
      createClient({
        url: import.meta.env.VITE_GATEWAY_URL,
        exchanges: [cacheExchange, fetchExchange],
        fetch: async (input, init) => {
          const token = await getAccessTokenSilently();
          return fetch(input, {
            ...init,
            headers: { ...init?.headers, Authorization: `Bearer ${token}` },
          });
        },
      }),
    [getAccessTokenSilently],
  );

  return <Provider value={client}>{children}</Provider>;
}
