import { useAuth0 } from '@auth0/auth0-react';
import { NavLink } from 'react-router-dom';
import { useIsAdmin } from '../auth/useIsAdmin';
import { Button } from './ui/button';

export function NavBar() {
  const { user, logout } = useAuth0();
  const isAdmin = useIsAdmin();

  return (
    <nav className="navbar">
      <span className="navbar-wordmark">Larder</span>
      <div className="navbar-right">
        <div className="navbar-links">
          <NavLink to="/" end>
            Recipes
          </NavLink>
          <NavLink to="/pantry">Pantry</NavLink>
          {isAdmin && <NavLink to="/ingredients">Manage Ingredients</NavLink>}
        </div>
        <div className="navbar-user">
          {isAdmin && <span className="keeper-badge">Keeper</span>}
          {user?.email && <span>{user.email}</span>}
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={() => logout({ logoutParams: { returnTo: window.location.origin } })}
          >
            Log out
          </Button>
        </div>
      </div>
    </nav>
  );
}
