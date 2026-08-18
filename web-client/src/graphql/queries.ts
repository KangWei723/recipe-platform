import { gql } from 'urql';

export const RECIPES_QUERY = gql`
  query Recipes {
    recipes {
      id
      title
      description
      servings
      prepTimeMin
      cookTimeMin
      imageUrl
    }
  }
`;

export const RECIPE_QUERY = gql`
  query Recipe($id: Long!) {
    recipe(id: $id) {
      id
      title
      description
      servings
      prepTimeMin
      cookTimeMin
      imageUrl
      steps {
        id
        stepNumber
        instruction
        timerSeconds
      }
      ingredients {
        id
        ingredientId
        ingredientName
        quantity
        unit
        optional
        inPantry
        substitutions {
          substituteName
          ratio
          confidence
          contexts
        }
      }
    }
  }
`;

export const INGREDIENTS_QUERY = gql`
  query Ingredients {
    ingredients {
      id
      name
      category
      defaultUnit
    }
  }
`;

export const PANTRY_ITEMS_QUERY = gql`
  query PantryItems {
    pantryItems {
      id
      ingredientId
      ingredientName
      quantity
      unit
      expiryDate
      updatedAt
    }
  }
`;

export const UPSERT_PANTRY_ITEM_MUTATION = gql`
  mutation UpsertPantryItem(
    $ingredientId: Long!
    $quantity: Decimal!
    $unit: String!
    $expiryDate: LocalDate
  ) {
    upsertPantryItem(
      ingredientId: $ingredientId
      quantity: $quantity
      unit: $unit
      expiryDate: $expiryDate
    ) {
      id
      ingredientId
      ingredientName
      quantity
      unit
      expiryDate
      updatedAt
    }
  }
`;

export const REMOVE_PANTRY_ITEM_MUTATION = gql`
  mutation RemovePantryItem($itemId: Long!) {
    removePantryItem(itemId: $itemId)
  }
`;

export const CREATE_RECIPE_MUTATION = gql`
  mutation CreateRecipe(
    $title: String!
    $description: String
    $servings: Int
    $prepTimeMin: Int
    $cookTimeMin: Int
    $steps: [CreateRecipeStepInput!]!
    $ingredients: [CreateRecipeIngredientInput!]!
  ) {
    createRecipe(
      title: $title
      description: $description
      servings: $servings
      prepTimeMin: $prepTimeMin
      cookTimeMin: $cookTimeMin
      steps: $steps
      ingredients: $ingredients
    ) {
      id
      title
    }
  }
`;

export const UPDATE_RECIPE_MUTATION = gql`
  mutation UpdateRecipe(
    $recipeId: Long!
    $title: String!
    $description: String
    $servings: Int
    $prepTimeMin: Int
    $cookTimeMin: Int
    $steps: [CreateRecipeStepInput!]!
    $ingredients: [CreateRecipeIngredientInput!]!
  ) {
    updateRecipe(
      recipeId: $recipeId
      title: $title
      description: $description
      servings: $servings
      prepTimeMin: $prepTimeMin
      cookTimeMin: $cookTimeMin
      steps: $steps
      ingredients: $ingredients
    ) {
      id
      title
    }
  }
`;

export const DELETE_RECIPE_MUTATION = gql`
  mutation DeleteRecipe($recipeId: Long!) {
    deleteRecipe(recipeId: $recipeId)
  }
`;

export const CREATE_INGREDIENT_MUTATION = gql`
  mutation CreateIngredient($name: String!, $category: String, $defaultUnit: String!) {
    createIngredient(name: $name, category: $category, defaultUnit: $defaultUnit) {
      id
      name
      category
      defaultUnit
    }
  }
`;

export const UPDATE_INGREDIENT_MUTATION = gql`
  mutation UpdateIngredient($ingredientId: Long!, $name: String!, $category: String, $defaultUnit: String!) {
    updateIngredient(ingredientId: $ingredientId, name: $name, category: $category, defaultUnit: $defaultUnit) {
      id
      name
      category
      defaultUnit
    }
  }
`;

export const DELETE_INGREDIENT_MUTATION = gql`
  mutation DeleteIngredient($ingredientId: Long!) {
    deleteIngredient(ingredientId: $ingredientId)
  }
`;

export const NEARBY_STORES_QUERY = gql`
  query NearbyStores($ingredientName: String!, $lat: Float!, $lng: Float!) {
    nearbyStores(ingredientName: $ingredientName, lat: $lat, lng: $lng) {
      providerName
      storeName
      address
      lat
      lng
      price
      currency
      isSimulated
    }
  }
`;
