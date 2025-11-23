using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class WeatherUIController : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text cityText;
    public TMP_Text weatherText;
    public TMP_Text temperatureText;
    public Button nextCityButton;
    public Button previousCityButton;

    private WeatherManager weatherManager;

    private void Start()
    {
        weatherManager = FindAnyObjectByType<WeatherManager>();

        if (weatherManager == null)
        {
            Debug.LogError("WeatherManager not found in scene!");
            return;
        }

        // Subscribe to the weather update event
        weatherManager.OnWeatherUpdated += OnWeatherDataUpdated;

        nextCityButton.onClick.AddListener(NextCity);
        previousCityButton.onClick.AddListener(PreviousCity);

        UpdateUI();
    }

    private void OnDestroy()
    {
        // Unsubscribe from the event when destroyed to prevent memory leaks
        if (weatherManager != null)
        {
            weatherManager.OnWeatherUpdated -= OnWeatherDataUpdated;
        }
    }

    private void NextCity()
    {
        weatherManager.NextCity();
        UpdateUI();
    }

    private void PreviousCity()
    {
        weatherManager.PreviousCity();
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (weatherManager != null && weatherManager.cities.Count > 0)
        {
            var currentCity = weatherManager.cities[weatherManager.currentCityIndex];
            cityText.text = currentCity.displayName;

            // Clear weather text while loading new data
            weatherText.text = "Loading...";
            temperatureText.text = "Loading...";
        }
    }

    public void OnWeatherDataUpdated(WeatherManager.WeatherData data)
    {
        if (weatherText != null)
            weatherText.text = $"{data.weatherMain} ({data.weatherDescription})";

        if (temperatureText != null)
            temperatureText.text = $"Temp: {data.temperature:0.0}°C\nHumidity: {data.humidity}%";
    }
}