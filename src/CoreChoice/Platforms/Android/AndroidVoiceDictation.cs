using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Speech;
using CoreChoice.Application;

namespace CoreChoice.Platforms.Android;

/// <summary>
/// Speech-to-text for the dilemma fields, via Android's on-device <see cref="SpeechRecognizer"/>.
///
/// This is intentionally a thin adapter with no decision-making logic in it: which field a result
/// fills, and what happens when nothing came back, live in <see cref="Presentation.DilemmaViewModel"/>
/// where they are covered by tests. This file touches <c>Android.*</c> types throughout, so it
/// cannot be linked into <c>CoreChoice.App.Tests</c> (a plain <c>net10.0</c> project) at all — the
/// same reason it cannot be exercised on this machine, which has no device or emulator microphone
/// that represents a real phone. It is verified statically here and on-device at a later checkpoint.
///
/// One-shot by design: <see cref="ListenAsync"/> starts a single recognition session and returns
/// its best result, unlike <c>PurePrep</c>'s <c>VoiceCommandListener</c> which keeps re-arming
/// itself for continuous hands-free commands. A dilemma field is dictated once per tap, not
/// listened to continuously.
///
/// The microphone permission is requested here, at the moment <see cref="ListenAsync"/> is first
/// called — never at launch, and never merely because this class was constructed — because an app
/// that asks for the microphone on first open gets refused, and the refusal is permanent in
/// practice. <see cref="IsAvailable"/> reports whether the recogniser exists on this device at
/// all; it says nothing about permission, which is checked afresh on every call since it can be
/// revoked between them.
/// </summary>
public sealed class AndroidVoiceDictation : Java.Lang.Object, IVoiceDictation
{
    private static Context Context => global::Android.App.Application.Context;

    public bool IsAvailable => SpeechRecognizer.IsRecognitionAvailable(Context);

    public async Task<string?> ListenAsync(CancellationToken ct = default)
    {
        if (!IsAvailable)
            return null;

        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.Microphone>();
            if (status != PermissionStatus.Granted)
                status = await Permissions.RequestAsync<Permissions.Microphone>();
            if (status != PermissionStatus.Granted)
                return null;

            if (ct.IsCancellationRequested)
                return null;

            return await ListenOnMainThreadAsync(ct);
        }
        catch (Exception)
        {
            // Never throw into the view model: a denied permission, a missing recognition
            // service, or any platform quirk in between must hide the mic control's effect
            // rather than break the screen. The button itself is already hidden by
            // IsAvailable when the platform has no recogniser at all; this guards every other
            // way listening can go wrong.
            return null;
        }
    }

    private static Task<string?> ListenOnMainThreadAsync(CancellationToken ct)
    {
        var completion = new TaskCompletionSource<string?>();
        ct.Register(() => completion.TrySetResult(null));

        MainThread.BeginInvokeOnMainThread(() =>
        {
            SpeechRecognizer? recognizer = null;
            try
            {
                recognizer = SpeechRecognizer.CreateSpeechRecognizer(Context);
                if (recognizer is null)
                {
                    completion.TrySetResult(null);
                    return;
                }

                var listener = new OneShotListener(result =>
                {
                    completion.TrySetResult(result);
                    recognizer?.Destroy();
                });
                recognizer.SetRecognitionListener(listener);

                var intent = new Intent(RecognizerIntent.ActionRecognizeSpeech);
                intent.PutExtra(RecognizerIntent.ExtraLanguageModel, RecognizerIntent.LanguageModelFreeForm);
                intent.PutExtra(RecognizerIntent.ExtraLanguage, Java.Util.Locale.Default.ToLanguageTag());

                recognizer.StartListening(intent);
            }
            catch (Exception)
            {
                recognizer?.Destroy();
                completion.TrySetResult(null);
            }
        });

        return completion.Task;
    }

    /// <summary>A <see cref="IRecognitionListener"/> that reports exactly one outcome — the best
    /// recognised phrase, or null on cancellation, silence, or any recognition error — and never
    /// throws back into the caller.</summary>
    private sealed class OneShotListener(Action<string?> onDone) : Java.Lang.Object, IRecognitionListener
    {
        private bool _done;

        private void Complete(string? result)
        {
            if (_done)
                return;
            _done = true;
            onDone(result);
        }

        public void OnResults(Bundle? results)
        {
            var phrases = results?.GetStringArrayList(SpeechRecognizer.ResultsRecognition);
            Complete(phrases is { Count: > 0 } ? phrases[0] : null);
        }

        public void OnError([GeneratedEnum] SpeechRecognizerError error) => Complete(null);

        public void OnPartialResults(Bundle? partialResults) { }
        public void OnReadyForSpeech(Bundle? @params) { }
        public void OnBeginningOfSpeech() { }
        public void OnRmsChanged(float rmsdB) { }
        public void OnBufferReceived(byte[]? buffer) { }
        public void OnEndOfSpeech() { }
        public void OnEvent(int eventType, Bundle? @params) { }
    }
}
