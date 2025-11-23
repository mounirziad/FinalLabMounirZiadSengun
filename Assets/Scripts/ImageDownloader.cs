using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class ImageDownloader : MonoBehaviour
{
    [System.Serializable]
    public class WebImage
    {
        public string url;
        public string displayName;
    }

    [Header("Image Settings")]
    public List<WebImage> webImages = new List<WebImage>
    {
        new WebImage {
            url = "https://picsum.photos/800/600?random=1",
            displayName = "Random Image 1"
        },
        new WebImage {
            url = "https://picsum.photos/800/600?random=2",
            displayName = "Random Image 2"
        },
        new WebImage {
            url = "https://picsum.photos/800/600?random=3",
            displayName = "Random Image 3"
        }
    };

    [Header("Billboard Settings")]
    public GameObject billboardPrefab;
    public Vector3 billboardSpacing = new Vector3(3, 0, 0);

    private Dictionary<string, Texture2D> imageCache = new Dictionary<string, Texture2D>();
    private Dictionary<string, List<Action<Texture2D>>> pendingCallbacks = new Dictionary<string, List<Action<Texture2D>>>();

    private void Start()
    {
        CreateBillboards();
    }

    private void CreateBillboards()
    {
        Vector3 position = Vector3.zero;

        foreach (WebImage webImage in webImages)
        {
            GameObject billboard = Instantiate(billboardPrefab, position, Quaternion.identity);
            billboard.name = $"Billboard_{webImage.displayName}";
            StartCoroutine(SetupBillboardTexture(billboard, webImage.url));
            position += billboardSpacing;
        }
    }

    private IEnumerator SetupBillboardTexture(GameObject billboard, string imageUrl)
    {
        yield return StartCoroutine(GetWebImage(imageUrl, (texture) =>
        {
            if (texture != null && billboard != null)
            {
                Renderer renderer = billboard.GetComponent<Renderer>();
                if (renderer != null)
                {
                    // Create unlit texture material to avoid lighting issues
                    Material material = new Material(Shader.Find("Unlit/Texture"));
                    material.mainTexture = texture;
                    renderer.material = material;

                    Debug.Log($"Successfully applied texture to {billboard.name} - Size: {texture.width}x{texture.height}");
                }
            }
            else
            {
                Debug.LogWarning($"Failed to load texture for {billboard.name}");
            }
        }));
    }

    public IEnumerator GetWebImage(string url, Action<Texture2D> callback)
    {
        if (imageCache.ContainsKey(url))
        {
            callback?.Invoke(imageCache[url]);
            yield break;
        }

        if (pendingCallbacks.ContainsKey(url))
        {
            pendingCallbacks[url].Add(callback);
            yield break;
        }

        pendingCallbacks[url] = new List<Action<Texture2D>> { callback };

        yield return StartCoroutine(DownloadImage(url, (texture) =>
        {
            if (texture != null)
            {
                imageCache[url] = texture;
                Debug.Log($"Successfully downloaded and cached: {url}");
            }

            if (pendingCallbacks.ContainsKey(url))
            {
                foreach (var cb in pendingCallbacks[url])
                {
                    cb?.Invoke(texture);
                }
                pendingCallbacks.Remove(url);
            }
        }));
    }

    private IEnumerator DownloadImage(string url, Action<Texture2D> callback)
    {
        Debug.Log($"Starting download from: {url}");

        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(request);

                // Ensure texture is readable and apply settings
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Bilinear;

                Debug.Log($"Download successful: {texture.width}x{texture.height}, Format: {texture.format}");

                // Log successful image download
                if (PlayFabAnalyticsManager.Instance != null)
                {
                    PlayFabAnalyticsManager.Instance.LogImageDownloadEvent(url, true);
                }

                callback?.Invoke(texture);
            }
            else
            {
                Debug.LogError($"Failed to download image: {request.error}");

                // Log failed image download
                if (PlayFabAnalyticsManager.Instance != null)
                {
                    PlayFabAnalyticsManager.Instance.LogImageDownloadEvent(url, false, request.error);
                }

                callback?.Invoke(null);
            }
        }
    }

    public void ClearImageCache()
    {
        foreach (var texture in imageCache.Values)
        {
            Destroy(texture);
        }
        imageCache.Clear();
        pendingCallbacks.Clear();
    }
}