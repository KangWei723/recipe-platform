export interface RecipeSummary {
  id: number;
  title: string;
  description: string | null;
  servings: number | null;
  prepTimeMin: number | null;
  cookTimeMin: number | null;
  imageUrl: string | null;
}

export interface RecipeIngredient {
  id: number;
  ingredientId: number;
  ingredientName: string;
  quantity: number;
  unit: string;
  optional: boolean;
  inPantry: boolean;
}

export interface RecipeStep {
  id: number;
  stepNumber: number;
  instruction: string;
  timerSeconds: number | null;
}

export interface RecipeDetail {
  id: number;
  title: string;
  description: string | null;
  servings: number | null;
  prepTimeMin: number | null;
  cookTimeMin: number | null;
  imageUrl: string | null;
  steps: RecipeStep[];
  ingredients: RecipeIngredient[];
}

export interface Ingredient {
  id: number;
  name: string;
  category: string;
  defaultUnit: string;
  usageCount: number;
}

export interface MeasurementUnit {
  code: string;
  label: string;
  isFractionalFriendly: boolean;
}

export interface IngredientCategory {
  code: string;
  label: string;
}

export interface PantryItem {
  ingredientId: number;
  ingredientName: string;
  updatedAt: string;
}

export interface MissingMatchIngredient {
  ingredientId: number;
  ingredientName: string;
}

export interface RecipeMatch {
  recipe: RecipeSummary;
  requiredIngredientCount: number;
  matchedIngredientCount: number;
  missingIngredients: MissingMatchIngredient[];
}

export interface CreateRecipeStepInput {
  stepNumber: number;
  instruction: string;
  timerSeconds: number | null;
}

export interface CreateRecipeIngredientInput {
  ingredientId: number;
  quantity: number;
  unit: string;
  optional: boolean;
}

export interface StoreOffer {
  providerName: string;
  storeName: string;
  address: string | null;
  lat: number | null;
  lng: number | null;
  price: number | null;
  currency: string | null;
  isSimulated: boolean;
  placeId: string | null;
  // Populated only for confirmed, per-product results (Kroger) -- null for general locator
  // results (Google Places), which are stores, not individual products.
  storeId: string | null;
  productId: string | null;
  productName: string | null;
}
