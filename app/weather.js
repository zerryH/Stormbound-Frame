/* Open-Meteo forecast for the city shown in Controls. Fetch on entry, never on Home. */
(function () {
  'use strict';

  var endpoint = 'https://api.open-meteo.com/v1/forecast?latitude=60.17&longitude=24.94'
    + '&current=temperature_2m,weather_code&hourly=temperature_2m,weather_code&timezone=Europe%2FHelsinki&forecast_days=2';
  var cacheMs = 15 * 60 * 1000, retryMs = 30 * 1000;
  var state = { data: null, error: false }, checked = 0, listeners = [];

  function condition(code) {
    if (code === 0) return 'Clear';
    if (code === 1 || code === 2) return 'Partly cloudy';
    if (code === 3) return 'Overcast';
    if (code === 45 || code === 48) return 'Fog';
    if (code >= 95) return 'Thunderstorms';
    if (code >= 71 && code <= 77 || code >= 85 && code <= 86) return 'Snow';
    if (code >= 51 && code <= 82) return 'Rain';
    return 'Conditions unavailable';
  }
  function parse(payload) {
    var now = payload.current, hourly = payload.hourly, hours = [], i, time, temp;
    if (!now || typeof now.temperature_2m !== 'number' || !isFinite(now.temperature_2m) || typeof now.time !== 'string') throw new Error('Incomplete current weather');
    if (hourly && Array.isArray(hourly.time) && Array.isArray(hourly.temperature_2m) && Array.isArray(hourly.weather_code)) {
      for (i = 0; i < hourly.time.length && hours.length < 6; i++) {
        time = hourly.time[i]; temp = hourly.temperature_2m[i];
        if (typeof time === 'string' && /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/.test(time) && time >= now.time && typeof temp === 'number' && isFinite(temp)) {
          hours.push({ time: time.slice(11), temp: Math.round(temp), condition: condition(hourly.weather_code[i]) });
        }
      }
    }
    return { temp: Math.round(now.temperature_2m), condition: condition(now.weather_code), hours: hours };
  }
  function finish(data) {
    var pending = listeners;
    listeners = [];
    checked = Date.now();
    state.error = !data;
    if (data) state.data = data;
    pending.forEach(function (done) { done(); });
  }
  function load(done) {
    var request;
    if (Date.now() - checked < (state.error ? retryMs : cacheMs)) return;
    listeners.push(done);
    if (listeners.length > 1) return;
    try {
      request = new window.XMLHttpRequest();
      request.open('GET', endpoint);
      request.timeout = 5500;
      request.onload = function () {
        var data;
        try { if (request.status === 200) data = parse(JSON.parse(request.responseText)); }
        catch (error) { /* Keep the last valid reading. */ }
        finish(data);
      };
      request.onerror = request.ontimeout = function () { finish(null); };
      request.send();
    } catch (error) { finish(null); }
  }
  window.FrameWeather = { load: load, state: function () { return state; } };
}());
