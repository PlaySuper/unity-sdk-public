# PS UPI Test Game

Minimal Unity project to device-test the **UPI Intent fix** in the PlaySuper Unity SDK
(the SDK is referenced from the local checkout at `../ps-unity-sdk/unity-sdk-public`,
so it includes the uncommitted `Runtime/WebView.cs` changes).

Bundled: GPM WebView 2.2.0 + GPM Common 2.4.0 + GPM Communicator 1.1.1 (vendored in `Assets/GPM`).

## One-time setup (~20 min, mostly downloads)

1. Install **Unity Hub** → https://unity.com/download
2. In Hub → Installs → Install Editor → **Unity 2022.3 LTS** (any patch; 2022.3.38f1 matches CI).
   Tick **Android Build Support** including *OpenJDK* and *Android SDK & NDK Tools*.
3. Hub → Projects → **Add** → select this folder (`ps-upi-test-game`) → open. First import takes a few minutes.

## Build the APK

1. Menu bar → **PlaySuper → Build UPI Test APK**.
   (Creates the test scene, switches to Android, builds `Builds/ps-upi-test.apk`, opens Finder.)
2. Install on the phone: `adb install Builds/ps-upi-test.apk`, or AirDrop/copy the APK and open it.

## Test on device

1. Open the app → paste a **production game API key** (from the console — e.g. the Ludo test game).
   ⚠️ Use prod: Cashfree **sandbox does not launch real UPI apps**.
2. Tap **1) Init + Guest Login** → wait for "Guest login OK".
3. Tap **2) Open Store** → the store opens in the GPM WebView (same path real games use).
4. Add any low-price product to cart → checkout → Cashfree modal opens.

### What to verify

| Check | Expected with the fix |
|---|---|
| UPI section in checkout | "Pay Via UPI" row with app buttons (Paytm / PhonePe / GPay / Any UPI) — not "Click to see QR" only |
| Tap a UPI app | The real app opens full-screen, amount + payee pre-filled |
| Approve with PIN, return | Game comes back with the Cashfree modal still open → flips to success |
| Order | Store routes to the order page; order finalizes (webhook) |

If the QR-only screen still appears, grab `adb logcat -s Unity` — the harness and SDK log
the patched user agent and every intercepted scheme (`[PlaySuper] Launching external payment app …`).

## Notes

- The SDK is a `file:` package reference — edits in `../ps-unity-sdk/unity-sdk-public` are picked up
  on the next domain reload/build; no re-import needed.
- Harness UI is IMGUI built at runtime; no scene wiring. `PlaySuper → Create UPI Test Scene`
  regenerates the scene if needed.
- Min SDK 23, IL2CPP, ARM64+ARMv7 (installs on 64-bit-only phones).
