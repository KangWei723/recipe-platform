export interface KrogerProductEntry {
  productId: string;
  productName: string;
  price: number | null;
  currency: string | null;
}

export interface KrogerStoreGroup {
  storeId: string;
  storeName: string;
  lat: number | null;
  lng: number | null;
  distanceMiles: number | null;
  products: KrogerProductEntry[];
}

function mapsUrl(lat: number | null, lng: number | null): string | null {
  if (lat === null || lng === null) return null;
  return `https://www.google.com/maps/search/?api=1&query=${lat},${lng}`;
}

// Shared, accumulating display for confirmed Kroger finds -- fed by every per-ingredient
// ConfirmedStoreLookup click on the page (see RecipeDetailPage's krogerGroups state), grouped by
// store rather than repeating the store once per product. Kroger's product name has no real
// product-page URL to link to (confirmed against a real captured API response -- only image
// asset URLs exist, no browsable page), so product names are plain text; only the store heading
// links out, to Google Maps, same as the rest of this app's store links.
export function KrogerResultsPanel({ groups }: { groups: KrogerStoreGroup[] }) {
  if (groups.length === 0) return null;

  return (
    <div className="kroger-results-panel">
      <p className="panel-subtitle">Confirmed at Kroger</p>
      {groups.map((group) => {
        const url = mapsUrl(group.lat, group.lng);
        const heading = (
          <>
            {group.storeName}
            {group.distanceMiles !== null ? ` — ${group.distanceMiles.toFixed(1)} mi` : ''}
          </>
        );
        return (
          <div key={group.storeId} className="kroger-store-group">
            <div className="kroger-store-heading">
              {url ? (
                <a href={url} target="_blank" rel="noopener noreferrer">
                  {heading}
                </a>
              ) : (
                heading
              )}
            </div>
            <ul className="kroger-product-list">
              {group.products.map((product) => (
                <li key={product.productId}>
                  {product.productName}
                  {product.price !== null ? ` — ${product.currency ?? '$'}${product.price.toFixed(2)}` : ''}
                </li>
              ))}
            </ul>
          </div>
        );
      })}
    </div>
  );
}
