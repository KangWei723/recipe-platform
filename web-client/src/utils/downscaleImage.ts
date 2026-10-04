const MAX_LONG_EDGE = 1600;
const JPEG_QUALITY = 0.85;

// Draws the file onto an offscreen canvas scaled down so its long edge is at most
// MAX_LONG_EDGE (never upscales a smaller image), then re-encodes as JPEG at JPEG_QUALITY. This
// is what actually keeps a modern phone photo (routinely 8-15 MB straight off the camera) under
// the server's 5 MB limit -- that limit stays the real, server-enforced backstop regardless;
// this is a client-side convenience, never trusted as a security control. Any failure (a format
// the browser can't decode, Canvas unsupported, etc.) falls back to the original file rather than
// blocking the upload -- the server's validation is the real gate either way.
export async function downscaleImage(file: File): Promise<File> {
  const objectUrl = URL.createObjectURL(file);
  try {
    const img = await loadImageElement(objectUrl);
    const scale = Math.min(1, MAX_LONG_EDGE / Math.max(img.naturalWidth, img.naturalHeight));
    const width = Math.round(img.naturalWidth * scale);
    const height = Math.round(img.naturalHeight * scale);

    const canvas = document.createElement('canvas');
    canvas.width = width;
    canvas.height = height;
    const ctx = canvas.getContext('2d');
    if (!ctx) return file;

    ctx.drawImage(img, 0, 0, width, height);

    const blob = await new Promise<Blob | null>((resolve) => canvas.toBlob(resolve, 'image/jpeg', JPEG_QUALITY));
    if (!blob) return file;

    const newName = file.name.replace(/\.[^.]+$/, '') + '.jpg';
    return new File([blob], newName, { type: 'image/jpeg' });
  } catch {
    return file;
  } finally {
    URL.revokeObjectURL(objectUrl);
  }
}

function loadImageElement(src: string): Promise<HTMLImageElement> {
  return new Promise((resolve, reject) => {
    const img = new Image();
    img.onload = () => resolve(img);
    img.onerror = () => reject(new Error('Failed to decode image'));
    img.src = src;
  });
}
