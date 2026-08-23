import { useAuth0 } from '@auth0/auth0-react';
import { BrowserRouter, Route, Routes } from 'react-router-dom';
import { NavBar } from './components/NavBar';
import { Button } from './components/ui/button';
import { GraphQLProvider } from './graphql/client';
import { IngredientsPage } from './pages/IngredientsPage';
import { PantryPage } from './pages/PantryPage';
import { RecipeDetailPage } from './pages/RecipeDetailPage';
import { RecipeFormPage } from './pages/RecipeFormPage';
import { RecipeListPage } from './pages/RecipeListPage';

function App() {
  const { isLoading, isAuthenticated, loginWithRedirect } = useAuth0();

  if (isLoading) {
    return <p className="centered-message">Loading...</p>;
  }

  if (!isAuthenticated) {
    return (
      <div className="centered-message">
        <Button type="button" onClick={() => loginWithRedirect()}>
          Log in
        </Button>
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
            <Route path="/recipes/new" element={<RecipeFormPage />} />
            <Route path="/recipes/:id" element={<RecipeDetailPage />} />
            <Route path="/recipes/:id/edit" element={<RecipeFormPage />} />
            <Route path="/pantry" element={<PantryPage />} />
            <Route path="/ingredients" element={<IngredientsPage />} />
          </Routes>
        </main>
      </BrowserRouter>
    </GraphQLProvider>
  );
}

export default App;
