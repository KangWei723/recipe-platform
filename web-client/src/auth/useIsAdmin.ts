import { useAuth0 } from '@auth0/auth0-react';
import { useEffect, useState } from 'react';

const ROLES_CLAIM = 'https://recipemate.api/roles';

// The roles claim only ever lands on the access token (not the ID token), so this decodes the
// JWT payload directly rather than reading useAuth0().user. This is a UI convenience check only
// -- RecipeService (and the Gateway's AdminOnly policy on admin-only mutations) is the real
// enforcement boundary, so a forged/stale value here can hide or show a button but can't grant
// access.
function decodeRoles(accessToken: string): string[] {
  const payload = accessToken.split('.')[1];
  if (!payload) return [];

  try {
    const json = atob(payload.replace(/-/g, '+').replace(/_/g, '/'));
    const claims = JSON.parse(json) as Record<string, unknown>;
    const roles = claims[ROLES_CLAIM];
    return Array.isArray(roles) ? roles.filter((r): r is string => typeof r === 'string') : [];
  } catch {
    return [];
  }
}

export function useIsAdmin(): boolean {
  const { isAuthenticated, getAccessTokenSilently } = useAuth0();
  const [isAdmin, setIsAdmin] = useState(false);

  useEffect(() => {
    if (!isAuthenticated) {
      setIsAdmin(false);
      return;
    }

    let cancelled = false;
    getAccessTokenSilently()
      .then((token) => {
        if (!cancelled) setIsAdmin(decodeRoles(token).includes('admin'));
      })
      .catch(() => {
        if (!cancelled) setIsAdmin(false);
      });

    return () => {
      cancelled = true;
    };
  }, [isAuthenticated, getAccessTokenSilently]);

  return isAdmin;
}
