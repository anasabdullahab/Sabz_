import {
  Sun, Cloud, CloudRain, CloudSnow, CloudDrizzle,
  CloudLightning, CloudFog, Wind, Eye,
  type LucideIcon,
} from 'lucide-react';

interface WeatherInfo {
  label: string;
  icon: LucideIcon;
  description: string;
}

/** WMO Weather interpretation code mappings */
const weatherCodeMap: Record<number, WeatherInfo> = {
  0:  { label: 'Clear Sky',        icon: Sun,            description: 'Clear sky conditions' },
  1:  { label: 'Mainly Clear',     icon: Sun,            description: 'Mainly clear sky' },
  2:  { label: 'Partly Cloudy',    icon: Cloud,          description: 'Partly cloudy' },
  3:  { label: 'Overcast',         icon: Cloud,          description: 'Overcast conditions' },
  45: { label: 'Fog',              icon: CloudFog,       description: 'Foggy conditions' },
  48: { label: 'Icy Fog',          icon: CloudFog,       description: 'Depositing rime fog' },
  51: { label: 'Light Drizzle',    icon: CloudDrizzle,   description: 'Light drizzle' },
  53: { label: 'Drizzle',          icon: CloudDrizzle,   description: 'Moderate drizzle' },
  55: { label: 'Heavy Drizzle',    icon: CloudDrizzle,   description: 'Dense drizzle' },
  56: { label: 'Freezing Drizzle', icon: CloudDrizzle,   description: 'Light freezing drizzle' },
  57: { label: 'Freezing Drizzle', icon: CloudDrizzle,   description: 'Dense freezing drizzle' },
  61: { label: 'Light Rain',       icon: CloudRain,      description: 'Slight rain' },
  63: { label: 'Rain',             icon: CloudRain,      description: 'Moderate rain' },
  65: { label: 'Heavy Rain',       icon: CloudRain,      description: 'Heavy rain' },
  66: { label: 'Freezing Rain',    icon: CloudRain,      description: 'Light freezing rain' },
  67: { label: 'Freezing Rain',    icon: CloudRain,      description: 'Heavy freezing rain' },
  71: { label: 'Light Snow',       icon: CloudSnow,      description: 'Slight snow fall' },
  73: { label: 'Snow',             icon: CloudSnow,      description: 'Moderate snow fall' },
  75: { label: 'Heavy Snow',       icon: CloudSnow,      description: 'Heavy snow fall' },
  77: { label: 'Snow Grains',      icon: CloudSnow,      description: 'Snow grains' },
  80: { label: 'Rain Showers',     icon: CloudRain,      description: 'Slight rain showers' },
  81: { label: 'Rain Showers',     icon: CloudRain,      description: 'Moderate rain showers' },
  82: { label: 'Violent Showers',  icon: CloudRain,      description: 'Violent rain showers' },
  85: { label: 'Snow Showers',     icon: CloudSnow,      description: 'Slight snow showers' },
  86: { label: 'Snow Showers',     icon: CloudSnow,      description: 'Heavy snow showers' },
  95: { label: 'Thunderstorm',     icon: CloudLightning, description: 'Thunderstorm' },
  96: { label: 'Thunderstorm Hail',icon: CloudLightning, description: 'Thunderstorm with slight hail' },
  99: { label: 'Severe Storm',     icon: CloudLightning, description: 'Thunderstorm with heavy hail' },
};

const defaultWeather: WeatherInfo = {
  label: 'Unknown',
  icon: Eye,
  description: 'Weather condition unknown',
};

export function getWeatherInfo(code: number | null | undefined): WeatherInfo {
  if (code == null) return defaultWeather;
  return weatherCodeMap[code] ?? defaultWeather;
}

export function getWeatherLabel(code: number | null | undefined): string {
  return getWeatherInfo(code).label;
}

export function getWeatherIcon(code: number | null | undefined): LucideIcon {
  return getWeatherInfo(code).icon;
}
