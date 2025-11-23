using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class WeatherManager : MonoBehaviour
{
    [System.Serializable]
    public class CityData
    {
        public string name;
        public string countryCode;
        public string displayName;
    }

    [System.Serializable]
    public class WeatherData
    {
        public string cityName;
        public string weatherMain;
        public string weatherDescription;
        public float temperature;
        public float humidity;
        public long sunrise;
        public long sunset;
        public long timestamp;
        public int timezone;
    }

    [Header("Weather Settings")]
    public List<CityData> cities = new List<CityData>
    {
        new CityData { name = "Orlando", countryCode = "us", displayName = "Orlando, US" },
        new CityData { name = "Tokyo", countryCode = "jp", displayName = "Tokyo, JP" },
        new CityData { name = "London", countryCode = "gb", displayName = "London, UK" },
        new CityData { name = "Sydney", countryCode = "au", displayName = "Sydney, AU" },
        new CityData { name = "Cairo", countryCode = "eg", displayName = "Cairo, EG" }
    };

    public int currentCityIndex = 0;

    [Header("API Settings")]
    public string apiKey = "28bdfd69be7393bd86d7cdbec19ea26b";
    private string apiBaseUrl = "http://api.openweathermap.org/data/2.5/weather";

    [Header("Scene References")]
    public Light sunLight;
    public Material[] skyboxes; // Assign in inspector: Day, Night, Sunny, Rainy, Snowy

    // Event for UI updates
    public event Action<WeatherData> OnWeatherUpdated;

    public WeatherData currentWeather;
    private Dictionary<string, Texture2D> cachedImages = new Dictionary<string, Texture2D>();

    private void Start()
    {
        // Start with first city
        UpdateWeatherForCurrentCity();
    }

    public void UpdateWeatherForCurrentCity()
    {
        if (cities.Count == 0) return;

        CityData city = cities[currentCityIndex];
        string url = $"{apiBaseUrl}?q={city.name},{city.countryCode}&units=metric&appid={apiKey}";

        StartCoroutine(GetWeatherData(url, OnWeatherDataReceived));
    }

    private IEnumerator GetWeatherData(string url, Action<string> callback)
    {
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.ConnectionError)
            {
                Debug.LogError($"Network problem: {request.error}");
            }
            else if (request.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"Response error: {request.responseCode}");
            }
            else
            {
                callback(request.downloadHandler.text);
            }
        }
    }

    private void OnWeatherDataReceived(string jsonData)
    {
        try
        {
            // Parse JSON data
            WeatherJSONData data = JsonUtility.FromJson<WeatherJSONData>(jsonData);

            currentWeather = new WeatherData
            {
                cityName = data.name,
                weatherMain = data.weather[0].main,
                weatherDescription = data.weather[0].description,
                temperature = data.main.temp,
                humidity = data.main.humidity,
                sunrise = data.sys.sunrise,
                sunset = data.sys.sunset,
                timestamp = data.dt,
                timezone = data.timezone
            };
            // Log weather event to PlayFab
            LogWeatherAnalytics(currentWeather);

            // Notify UI that weather data has been updated
            OnWeatherUpdated?.Invoke(currentWeather);

            UpdateSceneBasedOnWeather();
        }
        catch (Exception e)
        {
            Debug.LogError($"Error parsing weather data: {e.Message}");
        }
    }

    private void LogWeatherAnalytics(WeatherData weatherData)
    {
        if (PlayFabAnalyticsManager.Instance != null)
        {
            var cityData = cities[currentCityIndex];

            // Log weather data
            PlayFabAnalyticsManager.Instance.LogWeatherEvent(
                cityData.displayName,
                weatherData.weatherMain,
                weatherData.temperature,
                weatherData.humidity
            );

            // Log city change
            PlayFabAnalyticsManager.Instance.LogCityChangeEvent(
                "Previous City", // You might want to track previous city
                cityData.displayName
            );
        }
    }

    private void UpdateSceneBasedOnWeather()
    {
        if (currentWeather == null) return;

        // Update skybox based on weather condition
        UpdateSkybox();

        // Update sun based on time of day
        UpdateSunSettings();
    }

    private void UpdateSkybox()
    {
        if (skyboxes == null || skyboxes.Length == 0) return;

        Material skyboxToUse = null;
        string weather = currentWeather.weatherMain.ToLower();
        string skyboxType = "";
        bool isDaytime = false;

        if (weather.Contains("clear"))
        {
            skyboxToUse = GetTimeBasedSkybox(skyboxes[0], skyboxes[1]);
            skyboxType = "Clear";
            isDaytime = IsDaytime();
        }
        else if (weather.Contains("cloud") || weather.Contains("overcast"))
        {
            skyboxToUse = skyboxes[2];
            skyboxType = "Cloudy";
            isDaytime = true;
        }
        else if (weather.Contains("rain") || weather.Contains("drizzle"))
        {
            skyboxToUse = skyboxes[3];
            skyboxType = "Rainy";
            isDaytime = true;
        }
        else if (weather.Contains("snow"))
        {
            skyboxToUse = skyboxes[4];
            skyboxType = "Snowy";
            isDaytime = true;
        }
        else
        {
            skyboxToUse = GetTimeBasedSkybox(skyboxes[0], skyboxes[1]);
            skyboxType = "Default";
            isDaytime = IsDaytime();
        }

        RenderSettings.skybox = skyboxToUse;
        DynamicGI.UpdateEnvironment();

        // Log skybox change
        if (PlayFabAnalyticsManager.Instance != null)
        {
            PlayFabAnalyticsManager.Instance.LogSkyboxChangeEvent(
                currentWeather.weatherMain,
                skyboxType,
                isDaytime
            );
        }
    }

    private bool IsDaytime()
    {
        DateTime utcNow = DateTime.UtcNow;
        DateTime cityTime = utcNow.AddSeconds(currentWeather.timezone);
        return cityTime.Hour >= 6 && cityTime.Hour < 18;
    }

    private Material GetTimeBasedSkybox(Material daySkybox, Material nightSkybox)
    {
        // Calculate local time for the city
        DateTime utcNow = DateTime.UtcNow;
        DateTime cityTime = utcNow.AddSeconds(currentWeather.timezone);

        // Simple day/night based on 6am-6pm
        bool isDaytime = cityTime.Hour >= 6 && cityTime.Hour < 18;

        return isDaytime ? daySkybox : nightSkybox;
    }

    private void UpdateSunSettings()
    {
        if (sunLight == null) return;

        DateTime utcNow = DateTime.UtcNow;
        DateTime cityTime = utcNow.AddSeconds(currentWeather.timezone);

        // Calculate sun position and intensity based on time
        float hour = cityTime.Hour + cityTime.Minute / 60f;

        // Simple day/night cycle
        if (hour >= 6 && hour <= 18) // Daytime
        {
            sunLight.intensity = Mathf.Lerp(0.3f, 1.2f,
                Mathf.Abs(hour - 12) / 6f); // Peak at noon
            sunLight.color = Color.Lerp(Color.red, Color.white,
                Mathf.Abs(hour - 12) / 6f);
        }
        else // Nighttime
        {
            sunLight.intensity = 0.1f;
            sunLight.color = Color.blue * 0.3f;
        }

        // Adjust for weather conditions
        string weather = currentWeather.weatherMain.ToLower();
        if (weather.Contains("rain") || weather.Contains("snow"))
        {
            sunLight.intensity *= 0.5f;
        }
        else if (weather.Contains("cloud"))
        {
            sunLight.intensity *= 0.7f;
        }
    }

    // UI method to change city
    public void ChangeCity(int newIndex)
    {
        if (newIndex >= 0 && newIndex < cities.Count)
        {
            currentCityIndex = newIndex;
            UpdateWeatherForCurrentCity();
        }
    }

    public void NextCity()
    {
        string previousCity = cities[currentCityIndex].displayName;
        currentCityIndex = (currentCityIndex + 1) % cities.Count;

        // Log city change before updating weather
        if (PlayFabAnalyticsManager.Instance != null)
        {
            PlayFabAnalyticsManager.Instance.LogCityChangeEvent(
                previousCity,
                cities[currentCityIndex].displayName
            );
        }

        UpdateWeatherForCurrentCity();
    }

    public void PreviousCity()
    {
        string previousCity = cities[currentCityIndex].displayName;
        currentCityIndex = (currentCityIndex - 1 + cities.Count) % cities.Count;

        // Log city change before updating weather
        if (PlayFabAnalyticsManager.Instance != null)
        {
            PlayFabAnalyticsManager.Instance.LogCityChangeEvent(
                previousCity,
                cities[currentCityIndex].displayName
            );
        }

        UpdateWeatherForCurrentCity();
    }

    // JSON data classes for parsing
    [System.Serializable]
    private class WeatherJSONData
    {
        public Weather[] weather;
        public Main main;
        public Sys sys;
        public string name;
        public long dt;
        public int timezone;
    }

    [System.Serializable]
    private class Weather
    {
        public string main;
        public string description;
    }

    [System.Serializable]
    private class Main
    {
        public float temp;
        public float humidity;
    }

    [System.Serializable]
    private class Sys
    {
        public long sunrise;
        public long sunset;
        public string country;
    }
}