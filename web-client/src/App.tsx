import { useAuth0 } from '@auth0/auth0-react';
import { BrowserRouter, Route, Routes } from 'react-router-dom';
import { NavBar } from './components/NavBar';
import { GraphQLProvider } from './graphql/client';
import { CreateRecipePage } from './pages/CreateRecipePage';
import { PantryPage } from './pages/PantryPage';
import { RecipeDetailPage } from './pages/RecipeDetailPage';
import { RecipeListPage } from './pages/RecipeListPage';

function App() {
  const { isLoading, isAuthenticated, loginWithRedirect } = useAuth0();

  if (isLoading) {
    return <p className="centered-message">Loading...</p>;
  }

  if (!isAuthenticated) {
    return (
      <div className="centered-message">
        <button type="button" className="btn btn-primary" onClick={() => loginWithRedirect()}>
          Log in
        </button>
      </div>
    );
  }

  return (
    <GraphQLProvider>
      <BrowserRouter>
        <NavBar />
        <main className="page">
          <Routes>
            <Route path="/" element={<RecipeListPage />} />
            <Route path="/recipes/new" element={<CreateRecipePage />} />
            <Route path="/recipes/:id" element={<RecipeDetailPage />} />
            <Route path="/pantry" element={<PantryPage />} />
          </Routes>
        </main>
      </BrowserRouter>
    </GraphQLProvider>
  );
}

export default App;
