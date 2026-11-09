# Stormbound Frame

A customization of [CleanFrame](https://github.com/nthnclrk/CleanFrame) for Samsung The Frame. This snapshot contains the 0.7.6 TV UI with the Forest mist wallpaper, the Netflix, YouTube, Spotify and Disney+ dock, and the animated Down panel for HDMI, Home preferences, Samsung settings and weather.

## Preview and package

Use Python 3.10 or newer and a supported Node.js release. From this directory, run `npm ci`, then `npm run dev:public`. Open http://localhost:4795/home/ and use the arrow keys, Enter and Escape. Hardware actions require the TV. Run `npm run check` for source validation and `npm run export:public` for an unsigned widget with redistributable fonts.

See [development and signing](docs/development.md) before installing. Keep the author certificate and package identity from your previous installation when updating an existing copy.

## Native Home button

The [2022 Frame profile](scripts/native-home/frame-2022/README.md) contains the Home assignment and Art Mode return source for the QE55LS03BAUXXH on T-PTMDEUC-1720.7. The widget itself installs separately. Native Home redirects the remote button to the installed launcher. Samsung's system UI and services remain on the TV.

See [upstream documentation](UPSTREAM.md), the [MIT license](LICENSE) and [third-party notices](THIRD_PARTY_NOTICES.md).
