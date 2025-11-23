using System;
using System.Collections.Generic;
using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;

public class PlayFabAnalyticsManager : MonoBehaviour
{
    [Header("PlayFab Settings")]
    public string titleId = "YOUR_TITLE_ID";

    [Header("Analytics Settings")]
    public bool enableAnalytics = true;

    private static PlayFabAnalyticsManager _instance;
    public static PlayFabAnalyticsManager Instance => _instance;

    private string playerId;
    private bool isInitialized = false;

    // Events for other scripts to know when analytics is ready
    public event Action OnAnalyticsReady;
    public event Action<string> OnAnalyticsError;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        InitializePlayFab();
    }

    private void InitializePlayFab()
    {
        if (string.IsNullOrEmpty(titleId) || titleId == "YOUR_TITLE_ID")
        {
            Debug.LogError("PlayFab Title ID is not set! Please set it in the inspector.");
            OnAnalyticsError?.Invoke("Title ID not set");
            return;
        }

        PlayFabSettings.staticSettings.TitleId = titleId;

        // Use the simplest login method first
        LoginWithCustomID();
    }

    private void LoginWithCustomID()
    {
        // Generate a consistent device ID
        string deviceId = GetDeviceId();

        var request = new LoginWithCustomIDRequest
        {
            CustomId = deviceId,
            CreateAccount = true
        };

        Debug.Log($"Attempting PlayFab login with device ID: {deviceId}");

        PlayFabClientAPI.LoginWithCustomID(request, OnLoginSuccess, OnLoginFailure);
    }

    private string GetDeviceId()
    {
        // Use device unique identifier, fallback to custom generated ID
        if (!string.IsNullOrEmpty(SystemInfo.deviceUniqueIdentifier) &&
            SystemInfo.deviceUniqueIdentifier != "Unknown")
        {
            return "Device_" + SystemInfo.deviceUniqueIdentifier;
        }

        // Fallback: generate ID based on system info
        return "Generated_" + SystemInfo.deviceModel.GetHashCode() + "_" +
               SystemInfo.operatingSystem.GetHashCode();
    }

    private void OnLoginSuccess(LoginResult result)
    {
        playerId = result.PlayFabId;
        isInitialized = true;

        Debug.Log("PlayFab login successful! Player ID: " + playerId);

        // Notify other scripts that analytics is ready
        OnAnalyticsReady?.Invoke();

        // Log initial game start event
        LogGameEvent("game_start", new Dictionary<string, object>
        {
            {"device_model", SystemInfo.deviceModel},
            {"platform", Application.platform.ToString()},
            {"unity_version", Application.unityVersion},
            {"playfab_player_id", playerId}
        });
    }

    private void OnLoginFailure(PlayFabError error)
    {
        string errorMessage = $"PlayFab login failed: {error.ErrorMessage}";
        Debug.LogError(errorMessage);

        // Provide specific guidance based on error
        if (error.ErrorMessage.Contains("Player creations have been disabled"))
        {
            errorMessage += "\n\nSOLUTION: Go to PlayFab Dashboard → Title Settings → API Features " +
                          "and UNCHECK 'Disable player creations' and related options.";
        }
        else if (error.ErrorMessage.Contains("TitleId is not set"))
        {
            errorMessage += "\n\nSOLUTION: Set your Title ID in the PlayFabAnalyticsManager inspector.";
        }

        OnAnalyticsError?.Invoke(errorMessage);
        isInitialized = false;
    }

    // Method to check if analytics is ready
    public bool IsReady()
    {
        return isInitialized && enableAnalytics;
    }

    // Method to log weather-related events
    public void LogWeatherEvent(string cityName, string weatherCondition, float temperature, float humidity)
    {
        if (!IsReady())
        {
            Debug.LogWarning("Analytics not ready - skipping weather event");
            return;
        }

        var eventData = new Dictionary<string, object>
        {
            {"city_name", cityName},
            {"weather_condition", weatherCondition},
            {"temperature", temperature},
            {"humidity", humidity},
            {"event_time", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")} // Changed from 'timestamp'
        };

        LogGameEvent("weather_data_received", eventData);
    }

    public void LogCityChangeEvent(string fromCity, string toCity)
    {
        if (!IsReady()) return;

        var eventData = new Dictionary<string, object>
        {
            {"from_city", fromCity},
            {"to_city", toCity},
            {"event_time", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")} // Changed from 'timestamp'
        };

        LogGameEvent("city_changed", eventData);
    }

    public void LogSkyboxChangeEvent(string weatherCondition, string skyboxType, bool isDaytime)
    {
        if (!IsReady()) return;

        var eventData = new Dictionary<string, object>
        {
            {"weather_condition", weatherCondition},
            {"skybox_type", skyboxType},
            {"is_daytime", isDaytime},
            {"event_time", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")} // Changed from 'timestamp'
        };

        LogGameEvent("skybox_changed", eventData);
    }

    public void LogImageDownloadEvent(string imageUrl, bool success, string errorMessage = "")
    {
        if (!IsReady()) return;

        var eventData = new Dictionary<string, object>
        {
            {"image_url", imageUrl},
            {"success", success},
            {"event_time", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")} // Changed from 'timestamp'
        };

        if (!string.IsNullOrEmpty(errorMessage))
        {
            eventData.Add("error_message", errorMessage);
        }

        LogGameEvent("image_download", eventData);
    }

    // Generic method to log any game event
    private void LogGameEvent(string eventName, Dictionary<string, object> eventData)
    {
        if (!IsReady()) return;

        PlayFabClientAPI.WritePlayerEvent(new WriteClientPlayerEventRequest
        {
            EventName = eventName,
            Body = eventData
        },
        result => {
            Debug.Log($"PlayFab event '{eventName}' logged successfully");
        },
        error => {
            Debug.LogError($"Failed to log PlayFab event '{eventName}': {error.GenerateErrorReport()}");
        });
    }

    // Method to manually retry initialization
    public void RetryInitialization()
    {
        if (!isInitialized)
        {
            Debug.Log("Retrying PlayFab initialization...");
            InitializePlayFab();
        }
    }
}