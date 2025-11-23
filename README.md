# spotify-controller

This is not currently deployed and can only be run locally.

## Requirements:
- Spotify account with Spotify premium.
- Device that can run Spotify.
- The code for this repository cloned to your computer.

## Setup

### Setup Spotify Developer Account
1. Create a Spotify developer account at `developer.spotify.com`.
2. Go to the Dashboard and create an app.
3. Set the redirect url to `http://127.0.0.1:5031/callback`.
4. Enable the Web API and Web Playback SDK.

### Setup Code
1. Create a file named `.env` in the root directory of the code you downloaded.
2. Fill the file with the following (the client ID and secret are on the Spotify developer account):
```
SPOTIFY_CLIENT_ID=<your-cliend-id>
SPOTIFY_CLIENT_SECRET=<your-client-secret>
SPOTIFY_REDIRECT_URI=http://127.0.0.1:5031/callback
```

### Use the Loop
1. Run the program on your local machine.
2. Open your browser and navigate to `http://127.0.0.1:5031/login`.
3. Sign in to Spotify with your credentials and wait for the redirect.
4. Queue songs on your device with Spotify.
5. Navigate to `http://127.0.0.1:5031/queue-loop`.
6. Wait and see the songs play!
