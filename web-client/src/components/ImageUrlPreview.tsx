import { useAuth0 } from '@auth0/auth0-react';
import { useEffect, useRef, useState, type ChangeEvent } from 'react';
import { downscaleImage } from '../utils/downscaleImage';
import { uploadImage } from '../utils/uploadImage';
import { Button } from './ui/button';
import { Label } from './ui/label';

interface ImageUrlPreviewProps {
  id: string;
  label: string;
  value: string;
  // Owned by the caller (RecipeFormPage's heroImageFileName / StepRow.imageFileName) -- NOT this
  // component's own state. It has to live in the same place as `value` and move with it through
  // drag-reorder (arrayMove) and deletion (filter) exactly the way `value` already does; state
  // local to this component is tied to this component *instance*, not to "this step", so it
  // doesn't reliably follow a step through those operations. See onChange below.
  fileName: string | null;
  onChange: (value: string, fileName: string | null) => void;
}

type PreviewStatus = 'empty' | 'loading' | 'loaded' | 'error';

const ACCEPTED_TYPES = 'image/jpeg,image/png,image/webp';

// Shared by the hero image field and each step's image field. Upload-only -- no URL text entry,
// so the only way to set or change the value is picking a file (downscaled client-side, pushed
// to Cloudinary via Gateway, server validates the actual bytes), and the only way to clear it is
// the Remove button below. The raw URL is never shown; only a thumbnail plus a label.
export function ImageUrlPreview({ id, label, value, fileName, onChange }: ImageUrlPreviewProps) {
  const { getAccessTokenSilently } = useAuth0();
  const fileInputRef = useRef<HTMLInputElement>(null);

  const [status, setStatus] = useState<PreviewStatus>(value.trim() ? 'loading' : 'empty');
  const [uploading, setUploading] = useState(false);
  const [uploadProgress, setUploadProgress] = useState<number | null>(null);
  // Separate from the preview's own "couldn't load this image" error above -- an admin needs to
  // tell "the upload itself failed" apart from "the URL doesn't resolve to an image". Fine to
  // keep as local state (unlike fileName above) -- it's transient per-interaction UI feedback,
  // never persisted data that needs to survive a reorder.
  const [uploadError, setUploadError] = useState<string | null>(null);

  // Reset to loading (or empty) whenever the value itself changes -- otherwise a stale
  // loaded/error state from the previous image would stick around while the new one is fetching.
  useEffect(() => {
    setStatus(value.trim() ? 'loading' : 'empty');
  }, [value]);

  async function handleFileSelected(e: ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    e.target.value = ''; // allows re-selecting the same file later and still firing this handler
    if (!file) return;

    setUploadError(null);
    setUploading(true);
    setUploadProgress(0);
    try {
      const downscaled = await downscaleImage(file);
      const token = await getAccessTokenSilently();
      const url = await uploadImage(downscaled, downscaled.name, token, setUploadProgress);
      onChange(url, file.name); // file.name: the original name, not the downscaled/renamed blob's
    } catch (err) {
      setUploadError(err instanceof Error ? err.message : 'Upload failed.');
    } finally {
      setUploading(false);
      setUploadProgress(null);
    }
  }

  // Only clears the field's value -- never deletes anything at Cloudinary. This doesn't take
  // effect until the form is saved (same as every other field), so Cancel leaves the saved
  // recipe's image untouched; an immediate Cloudinary-side delete here would destroy the asset
  // even if the admin then cancels instead of saving.
  function handleRemove() {
    setUploadError(null);
    onChange('', null);
  }

  const hasImage = value.trim().length > 0;

  return (
    <div className="image-url-field">
      <Label htmlFor={id}>{label}</Label>
      <div className="image-url-field-row">
        {hasImage && (
          <div className="image-url-preview" data-status={status}>
            <img
              src={value}
              alt={label}
              className="image-url-preview-img"
              onLoad={() => setStatus('loaded')}
              onError={() => setStatus('error')}
            />
            {status === 'error' && <span className="image-url-preview-error">Couldn't load this image</span>}
          </div>
        )}
        <div className="image-upload-controls">
          {hasImage && <span className="image-upload-filename">{fileName ?? 'Image uploaded'}</span>}
          <div className="image-upload-buttons">
            <Button
              type="button"
              variant="outline"
              size="sm"
              disabled={uploading}
              onClick={() => fileInputRef.current?.click()}
            >
              {uploading ? `Uploading... ${uploadProgress ?? 0}%` : hasImage ? 'Replace image' : 'Upload image'}
            </Button>
            {hasImage && (
              <Button type="button" variant="ghost" size="sm" disabled={uploading} onClick={handleRemove}>
                Remove image
              </Button>
            )}
          </div>
        </div>
        <input
          ref={fileInputRef}
          id={id}
          type="file"
          accept={ACCEPTED_TYPES}
          className="sr-only"
          aria-label={`${label} file picker`}
          onChange={handleFileSelected}
        />
      </div>
      {uploadError && <p className="image-upload-error">{uploadError}</p>}
    </div>
  );
}
