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
        var session = new RecognizerSession();

        // One exit for every outcome — recognised, errored, or cancelled — so the recogniser is
        // torn down exactly once no matter which happens first.
        void Complete(string? result)
        {
            completion.TrySetResult(result);
            session.Close();
        }

        var registration = ct.Register(() => Complete(null));
        completion.Task.ContinueWith(_ => registration.Dispose(), TaskScheduler.Default);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            SpeechRecognizer? recognizer = null;
            try
            {
                recognizer = SpeechRecognizer.CreateSpeechRecognizer(Context);
                if (recognizer is null)
                {
                    Complete(null);
                    return;
                }

                // Cancellation can arrive before this block runs — the person navigates away in
                // the instant between tapping and the main thread getting here. Handing the
                // recogniser to the session is therefore a handover that can be REFUSED: if the
                // session has already closed, this instance is destroyed now and never started,
                // instead of listening on with nothing left holding a reference to it.
                if (!session.TryAdopt(recognizer))
                {
                    RecognizerSession.Release(recognizer);
                    return;
                }

                recognizer.SetRecognitionListener(new OneShotListener(Complete));

                var intent = new Intent(RecognizerIntent.ActionRecognizeSpeech);
                intent.PutExtra(RecognizerIntent.ExtraLanguageModel, RecognizerIntent.LanguageModelFreeForm);
                intent.PutExtra(RecognizerIntent.ExtraLanguage, Java.Util.Locale.Default.ToLanguageTag());

                recognizer.StartListening(intent);
            }
            catch (Exception)
            {
                if (recognizer is not null)
                    RecognizerSession.Release(recognizer);
                Complete(null);
            }
        });

        return completion.Task;
    }

    /// <summary>
    /// Owns the one <see cref="SpeechRecognizer"/> of a listening attempt and guarantees it is
    /// released exactly once. Without this, cancelling mid-dictation resolved the waiting task but
    /// left the recogniser running: the microphone stayed open, and on Android the recording
    /// indicator stays lit with it. The recogniser is created inside a main-thread callback, so
    /// nothing outside that closure could reach it to shut it down.
    /// </summary>
    private sealed class RecognizerSession
    {
        private readonly Lock _gate = new();
        private SpeechRecognizer? _recognizer;
        private bool _closed;

        /// <summary>Takes ownership, unless the session has already closed — in which case the
        /// caller must release the instance itself, because nothing else ever will.</summary>
        public bool TryAdopt(SpeechRecognizer recognizer)
        {
            lock (_gate)
            {
                if (_closed) return false;
                _recognizer = recognizer;
                return true;
            }
        }

        public void Close()
        {
            SpeechRecognizer? toRelease;
            lock (_gate)
            {
                if (_closed) return;
                _closed = true;
                toRelease = _recognizer;
                _recognizer = null;
            }

            if (toRelease is null) return;

            // SpeechRecognizer is main-thread only; calling Destroy from the cancellation callback's
            // thread is itself a crash.
            MainThread.BeginInvokeOnMainThread(() => Release(toRelease));
        }

        /// <summary>Stops and destroys a recogniser, swallowing everything. This runs on teardown
        /// paths — including cancellation — where a throw would replace a clean exit with a crash.</summary>
        public static void Release(SpeechRecognizer recognizer)
        {
            try { recognizer.Cancel(); } catch (Exception) { }
            try { recognizer.Destroy(); } catch (Exception) { }
            try { recognizer.Dispose(); } catch (Exception) { }
        }
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
