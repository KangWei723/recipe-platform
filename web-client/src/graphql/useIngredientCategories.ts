import { useQuery } from 'urql';
import { INGREDIENT_CATEGORIES_QUERY } from './queries';
import type { IngredientCategory } from './types';

export function useIngredientCategories() {
  const [{ data, fetching }] = useQuery<{ ingredientCategories: IngredientCategory[] }>({
    query: INGREDIENT_CATEGORIES_QUERY,
  });
  const categories = data?.ingredientCategories ?? [];

  function getLabel(code: string): string {
    return categories.find((c) => c.code === code)?.label ?? code;
  }

  return { categories, fetching, getLabel };
}
