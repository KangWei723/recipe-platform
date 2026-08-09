import { useAuth0 } from '@auth0/auth0-react';
import { NavLink } from 'react-router-dom';

export function NavBar() {
  const { user, logout } = useAuth0();

  return (
    <nav className="navbar">
      <div className="navbar-links">
        <NavLink to="/" end>
          Recipes
        </NavLink>
        <NavLink to="/recipes/new">Add Recipe</NavLink>
        <NavLink to="/pantry">Pantry</NavLink>
      </div>
      <div className="navbar-user">
        {user?.email && <span>{user.email}</span>}
        <button type="button" onClick={() => logout({ logoutParams: { returnTo: window.location.origin } })}>
          Log out
        </button>
      </div>
    </nav>
  );
}
