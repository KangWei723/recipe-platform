// Derived from the existing Gateway GraphQL endpoint rather than a second env var -- the upload
// route is a plain REST sibling of /graphql on the same Gateway origin.
export function gatewayUploadUrl(): string {
  return `${import.meta.env.VITE_GATEWAY_URL.replace(/\/graphql\/?$/, '')}/api/images/upload`;
}

// XMLHttpRequest, not fetch -- fetch has no upload-progress event, and
// XMLHttpRequest.upload.onprogress is the only way to drive a real progress bar without adding a
// library.
export function uploadImage(
  file: File | Blob,
  fileName: string,
  token: string,
  onProgress: (percent: number) => void,
): Promise<string> {
  return new Promise((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    xhr.open('POST', gatewayUploadUrl());
    xhr.setRequestHeader('Authorization', `Bearer ${token}`);

    xhr.upload.onprogress = (event) => {
      if (event.lengthComputable) {
        onProgress(Math.round((event.loaded / event.total) * 100));
      }
    };

    xhr.onload = () => {
      if (xhr.status >= 200 && xhr.status < 300) {
        try {
          const body = JSON.parse(xhr.responseText) as { url: string };
          resolve(body.url);
        } catch {
          reject(new Error('Upload succeeded but the response could not be read.'));
        }
      } else {
        reject(new Error(extractErrorMessage(xhr) ?? `Upload failed (${xhr.status}).`));
      }
    };

    xhr.onerror = () => reject(new Error('Upload failed: network error.'));

    const formData = new FormData();
    formData.append('file', file, fileName);
    xhr.send(formData);
  });
}

// Gateway's /api/images/upload returns a ProblemDetails body ({title, status, detail}) for a
// validation failure forwarded from recipe-service -- same shape the GraphQL error path already
// surfaces for other mutations. A 401/403 from the authorization middleware itself has no body,
// which the caller's generic fallback message covers.
function extractErrorMessage(xhr: XMLHttpRequest): string | null {
  try {
    const body = JSON.parse(xhr.responseText) as { detail?: string; title?: string };
    return body.detail ?? body.title ?? null;
  } catch {
    return null;
  }
}
