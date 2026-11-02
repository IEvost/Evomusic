# Evomusic — Development Plan (v2)

Personal YouTube Music client for iPhone. Not for distribution.

## 1. Fixed decisions

| Topic | Decision |
|---|---|
| Device / OS | iPhone 18 Pro Max, iOS 27.0.1 only (no iPad, no older iOS) |
| App name | Evomusic, bundle ID `com.evomusic.app` |
| Dev machine | Windows only, no Mac; .NET SDK 10.0.403 + `maui` workload (iOS SDK pack 27.1) installed machine-wide in `C:\Program Files\dotnet` |
| Repo / CI | Public GitHub repo, GitHub Actions macOS runner (Xcode must match iOS SDK 27.1) |
| Apple account | Free Apple ID (7-day signing) |
| Language | C# only (code and UI); third-party libraries allowed; no Swift |
| UI framework | .NET MAUI (.NET 10 LTS), stock iOS look |
| Data source | Emulation of the YT Music web client (`WEB_REMIX`) over HTTP with saved Google cookies, so feeds and recommendations match music.youtube.com |
| YouTube Premium | No |
| Content | YT Music audio only (no video playback, no podcasts) |
| Offline downloads | Yes |
| Optional features | All that are technically possible (see §7); EQ and crossfade kept as experimental last steps |
| UI language | English |

## 2. Build and deploy pipeline (Windows-only)

You can't compile an iOS app on Windows, so the pipeline is:

```
Windows (code, unit tests, Windows-target UI preview)
   │ git push
   ▼
Cloud macOS CI (GitHub Actions or Codemagic)
   │ dotnet publish -f net10.0-ios -c Release -r ios-arm64 /p:EnableCodeSigning=false
   │ package → Evomusic.ipa (unsigned) → build artifact
   ▼
Windows: download .ipa → Impactor signs it with your free Apple ID → installs over USB cable
   ▼
iPhone (Developer Mode ON, trust developer profile in Settings)
```

Free Apple ID consequences:
- The app expires every **7 days**. Impactor ([CLARATION/Impactor](https://github.com/CLARATION/Impactor), open source; replaces Sideloadly, whose site is throttled on this network) can auto-refresh while the PC is on. It uses the iTunes USB drivers (iTunes + Apple Mobile Device Support installed). App data is kept if the bundle ID stays the same.
- Max 3 sideloaded apps at once, 10 App IDs per 7 days.
- **Not available:** CarPlay, push notifications, App Groups (so no widgets), iCloud. Background audio and Keychain still work.
- Visual Studio **Hot Restart** (debugging from Windows on a cabled device) requires a paid developer account, so no live debugger on the device. Instead:
  - Build an in-app **Debug Log** screen (ring buffer + export via the share sheet).
  - Optionally read device logs on Windows with `idevicesyslog` (libimobiledevice).
  - Keep as much logic as possible in `Evomusic.Core`, which is unit-tested on Windows.
  - Add a `net10.0-windows` MAUI target for fast UI iteration on the PC with a fake player (layout check only; the look differs from iOS).

CI notes:
- The CI macOS image must have an Xcode version that includes the iOS 27 SDK, and the .NET 10 iOS workload must support it. Verify in phase 0.
- GitHub Actions: unlimited for **public** repos. Private repos get 2,000 free min/month with a 10× macOS multiplier ≈ 200 macOS minutes ≈ 15–20 builds/month.
- Codemagic free tier: 500 macOS (M2) minutes/month, works with private repos.
- No secrets go into the repo (cookies live only on the phone).

## 3. Tech stack

| Area | Choice |
|---|---|
| App | .NET MAUI, `net10.0-ios` (+ `net10.0-windows` for dev preview) |
| MVVM | CommunityToolkit.Mvvm (source generators) |
| UI helpers | CommunityToolkit.Maui |
| DI / config | Microsoft.Extensions.DependencyInjection / Logging |
| HTTP | `HttpClient` + custom `CookieContainer`-backed handler |
| JSON | System.Text.Json (source-gen), `JsonNode` for InnerTube responses |
| Stream resolve | **YoutubeExplode** (with our cookies), behind `IStreamResolver` |
| Playback (iOS) | Custom C# service on `AVQueuePlayer` (Platforms/iOS), not MediaElement |
| System integration | `AVAudioSession`, `MPNowPlayingInfoCenter`, `MPRemoteCommandCenter`, `AVRoutePickerView` (AirPlay) |
| Storage | sqlite-net-pcl (library cache, history, queue, downloads index) |
| Secrets | MAUI `SecureStorage` (iOS Keychain) for cookies |
| Images | Disk + memory cache (FFImageLoading.Maui fork or custom) |
| Tests | xUnit on Windows |

## 4. Authentication (cookie-based)

1. **Sign-in screen:** a `WebView` (WKWebView) opens `accounts.google.com` → redirect to `music.youtube.com`. A desktop/mobile Safari User-Agent reduces the chance of Google's "This browser or app may not be secure" block.
2. After sign-in, read all `.youtube.com` and `.google.com` cookies from `WKHTTPCookieStore` (`SAPISID`, `__Secure-3PAPISID`, `SID`, `HSID`, `SSID`, `__Secure-1PSIDTS`, `__Secure-3PSIDTS`, …) and save them to the Keychain.
3. **Fallback:** if Google blocks WebView sign-in, a "Paste cookies" screen accepts a `Cookie:` header copied from a desktop browser's DevTools on music.youtube.com.
4. **Request signing:**
   - every InnerTube call sends `Cookie`, `Origin: https://music.youtube.com`, `X-Goog-AuthUser: 0`, `X-Origin`;
   - every InnerTube call sends `Authorization: SAPISIDHASH <ts>_<sha1(ts + " " + SAPISID + " " + origin)>`;
   - the client context (`WEB_REMIX`, current `clientVersion`, `visitorData`, `hl=en`, `gl`) is scraped from the `ytcfg` of the music.youtube.com homepage on startup.
5. **Cookie rotation:** persist every `Set-Cookie` from responses back to the Keychain (Google rotates `__Secure-*PSIDTS`). If the session becomes invalid, show "Session expired → Sign in again".
6. Sign out deletes cookies and caches.

## 5. YT Music client (InnerTube emulation)

Endpoints (`https://music.youtube.com/youtubei/v1/<name>?prettyPrint=false`):

| Feature | Endpoint |
|---|---|
| Home feed, Explore, Charts, Moods | `browse` (`FEmusic_home`, `FEmusic_explore`, …) + continuations |
| Library: playlists, songs, albums, artists, liked songs (`LM`) | `browse` (`FEmusic_library_*`, `VLLM`) |
| Album / Artist / Playlist pages | `browse` (browseId) |
| Search + suggestions | `search` (with filter params), `music/get_search_suggestions` |
| Up next / radio / autoplay queue | `next` (with `playlistId=RDAMVM<videoId>`) |
| Lyrics | `browse` (lyrics browseId from `next`) |
| Like / dislike / remove like | `like/like`, `like/dislike`, `like/removelike` |
| Playlist edit / create / delete | `browse/edit_playlist`, `playlist/create`, `playlist/delete` |
| History | `browse` (`FEmusic_history`) |
| Player info | `player` |

Design:
- `InnerTubeClient` builds the request context; separate **parsers** map raw JSON into `Track/Album/Artist/Playlist/Shelf` models. The parsers are the fragile part: they're unit-tested against saved real JSON fixtures, so a YouTube schema change shows up as a red test on Windows.
- **Playback reporting (behavior emulation):** after `player`, ping the `playbackTracking.videostatsPlaybackUrl` and `videostatsWatchtimeUrl` URLs like the web client does. Plays then land in your YT history and keep recommendations identical to the original. Can be turned off in Settings.

## 6. Streaming and playback

- `IStreamResolver` → YoutubeExplode (with our authenticated `HttpClient`) returns the direct googlevideo URLs.
- **Format:** `AVPlayer` can't play Opus/WebM, so use **AAC in MP4: itag 140 (~128 kbps)**. Without Premium, 256 kbps (itag 141) isn't available, so 140 is the best option. "Data saver" mode uses itag 139 (~48 kbps) on cellular.
- No Premium: direct streams contain **no ads**.
- Stream URLs expire after about 6 h: re-resolve on HTTP 403 or near expiry, transparently, mid-queue.
- `AVQueuePlayer` with the current track plus 1–2 preloaded items gives near-gapless playback. Prefetch to the cache on Wi-Fi.
- `AVAudioSession` category `.Playback`, `UIBackgroundModes=audio` in Info.plist; handle interruptions (calls, Siri) and route changes (pause when AirPods are removed).
- Lock screen, Control Center, Dynamic Island now-playing, AirPods controls: Now Playing info (title, artist, artwork, duration, elapsed, rate) plus remote commands (play, pause, next, previous, seek, like/dislike via feedback commands).
- Queue model (platform-independent, in Core): play now, play next, add to queue, reorder, remove, shuffle (keeping original order), repeat off/all/one, autoplay radio when the queue ends (`next` endpoint).

## 7. Feature set

**Core (MVP)**
- Sign in / out, account avatar
- Home feed, Search (with suggestions and filters), Library
- Album / Artist / Playlist pages
- Full playback, queue, shuffle, repeat, radio
- Mini player + full-screen Now Playing sheet
- Background audio, lock screen controls, AirPlay

**Extended (all feasible ones selected)**
| Feature | Status / approach |
|---|---|
| Offline downloads | Download itag 140 in chunked range requests (~10 MB, avoids throttling) to `Library/Application Support/Downloads` (excluded from backup); downloads queue, progress, storage usage screen, "Download playlist", "Downloaded" filter in Library, offline mode |
| Like / dislike / playlist editing | Synced to the account |
| Lyrics | YT Music lyrics; synced (time-coded) lyrics from **LRCLIB** (free API) when available; karaoke-style highlighting |
| Sleep timer | Minutes, or "end of track" |
| Playback speed | 0.5×–2× |
| Data saver | itag 139 on cellular |
| Listening history & stats | Local SQLite: top tracks/artists, minutes per day/week |
| Last.fm scrobbling | Last.fm API (needs your own API key) |
| Smart cache | LRU audio cache with a size limit; prefetch next tracks |
| Equalizer | **Experimental, late phase.** `MTAudioProcessingTap` on the AVPlayerItem with biquad filters written in C#. Real-time audio in managed code risks glitches, so it may be dropped if quality is poor. |
| Crossfade | **Experimental.** Two `AVPlayer`s with volume ramps; conflicts with gapless mode, so it's a toggle |
| CarPlay | **Not possible** (needs a paid account + Apple entitlement) |
| Widgets / Live Activities | **Not possible** (Swift-only extensions + App Groups need a paid account) |

## 8. UI (stock iPhone style, MAUI)

- `Shell` with a `TabBar` → native `UITabBarController`: **Home**, **Search**, **Library**, **Settings**.
- iOS platform-specifics: large titles (`PrefersLargeTitles`), translucent bars, `UseSafeArea`, page-sheet modals (`ModalPresentationStyle.PageSheet`) for Now Playing / Queue / Lyrics.
- Built with the iOS 27 SDK, the native tab bar and navigation bar get the system Liquid Glass look automatically.
- **Mini player:** a custom overlay docked above the tab bar (artwork, title, play/pause, next, progress line). Tapping or swiping up opens Now Playing.
- **Now Playing:** large artwork with a blurred dominant-color background, title/artist, like button, scrubber, controls, volume slider, AirPlay button (`AVRoutePickerView` via a custom handler), queue / lyrics / sleep-timer buttons.
- `CollectionView` for horizontal carousels (Home shelves) and lists; native context menus (MAUI `MenuFlyout` → `UIContextMenu`): Play next, Add to queue, Start radio, Add to playlist, Download, Like, Go to artist/album.
- SF Symbols (via a custom font/image source), Dynamic Type, Dark/Light mode following the system, haptics (`HapticFeedback`), pull-to-refresh.
- **Settings:** account, audio quality / data saver, cache and download size, playback reporting toggle, Last.fm, EQ (experimental), Debug Log, About.

## 9. Solution structure

```
Evomusic.sln
├─ src/Evomusic.Core/            net10.0 — all logic, no MAUI references
│   ├─ Auth/        CookieStore, SapisidHasher, SessionManager
│   ├─ InnerTube/   InnerTubeClient, ClientContext, Endpoints, Parsers/
│   ├─ Streams/     IStreamResolver, YoutubeExplodeResolver, FormatSelector
│   ├─ Playback/    QueueModel, ShuffleStrategy, RepeatMode, IPlayer (abstraction)
│   ├─ Downloads/   DownloadManager, ChunkedDownloader
│   ├─ Lyrics/      YtLyricsProvider, LrcLibProvider, LrcParser
│   ├─ Scrobbling/  LastFmClient
│   ├─ Storage/     Db, repositories, AudioCache
│   └─ Models/
├─ src/Evomusic.App/             MAUI app (net10.0-ios; net10.0-windows for preview)
│   ├─ Views/ ViewModels/ Controls/ (MiniPlayer, Artwork, ShelfView)
│   ├─ Platforms/iOS/            AvQueuePlayerService, NowPlayingService, AudioSessionService,
│   │                            WebViewCookieExtractor, RoutePickerHandler, Info.plist
│   └─ Platforms/Windows/        FakePlayer (dev preview only)
├─ tests/Evomusic.Core.Tests/    xUnit + JSON fixtures of real InnerTube responses
└─ .github/workflows/ios.yml     macOS build → unsigned .ipa artifact
```

## 10. Phases

| # | Phase | Done when |
|---|---|---|
| 0 | **Pipeline.** Repo, MAUI hello-world, CI workflow → unsigned IPA → Impactor → runs on the iPhone; verify iOS 27 SDK support | Hello-world icon on the phone |
| 1 | **Auth spike.** WebView Google sign-in → cookies → Keychain → SAPISIDHASH → authenticated `browse FEmusic_home` returns *your* feed; paste-cookie fallback | Your account name and home shelves are logged |
| 2 | **Playback core.** YoutubeExplode resolve (itag 140), AVQueuePlayer service, background audio, lock screen, remote commands, debug log screen | A track plays with the screen locked; lock screen controls work |
| 3 | **MVP UI.** Shell + tabs, Home, Search, Library, Album/Artist/Playlist pages, mini player, Now Playing, queue | Usable daily |
| 4 | **YT Music parity.** Radio/autoplay, like/dislike, playlist editing, history, playback reporting, lyrics (YT + LRCLIB) | Feels like YT Music, recommendations stay in sync |
| 5 | **Offline.** Downloads, cache, offline mode, data saver, URL-expiry handling, error/retry polish | Works in airplane mode with downloaded music |
| 6 | **Extras.** Sleep timer, speed, stats, Last.fm, then experimental EQ and crossfade | — |

## 11. Risks

| Risk | Mitigation |
|---|---|
| YouTube changes (player cipher, PO tokens, JSON schema) break playback or parsing | `IStreamResolver` abstraction; keep YoutubeExplode up to date; fixture-based parser tests; CI rebuild is all it takes to ship a fix |
| Google blocks WebView sign-in | Paste-cookie fallback |
| Session invalidation / cookie rotation | Persist `Set-Cookie`; clear re-login flow |
| Account flagging for automated use | Behave like the web client: real headers, normal request rate, no bulk scraping; personal usage volume is low risk but not zero (ToS violation) |
| 7-day expiry | Impactor auto-refresh; keep the bundle ID constant so data survives |
| No on-device debugger | Debug Log screen, `idevicesyslog`, heavy unit testing of Core |
| iOS 27 SDK / .NET MAUI support lag | Check in phase 0; pin CI image and workload versions |
| Limited macOS CI minutes | Public repo, or Codemagic, or batch changes per build |
