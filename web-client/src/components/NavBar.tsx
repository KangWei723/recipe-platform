import { useAuth0 } from '@auth0/auth0-react';
import { NavLink } from 'react-router-dom';
import { useIsAdmin } from '../auth/useIsAdmin';

export function NavBar() {
  const { user, logout } = useAuth0();
  const isAdmin = useIsAdmin();

  return (
    <nav className="navbar">
      <div className="navbar-links">
        <NavLink to="/" end>
          Recipes
        </NavLink>
        {isAdmin && <NavLink to="/recipes/new">Add Recipe</NavLink>}
        <NavLink to="/pantry">Pantry</NavLink>
      </div>
      <div className="navbar-user">
        {user?.email && <span>{user.email}</span>}
        <button
          type="button"
          className="btn btn-outline btn-sm"
          onClick={() => logout({ logoutParams: { returnTo: window.location.origin } })}
        >
          Log out
        </button>
      </div>
    </nav>
  );
}
