/**
 * Voice input for the AI agronomist chat via OpenRouter audio-input models.
 *
 * The browser records a short clip in whatever container the MediaRecorder
 * picks (usually webm/opus). The clip is then decoded, down-mixed to mono,
 * resampled to 16 kHz and re-encoded as plain PCM16 WAV — the one audio
 * format every provider in the chain accepts. Transcription itself is a
 * provider chain: if a model fails or returns nothing, the next one runs.
 */
const OPENROUTER_VOICE_MODELS = [
  '~google/gemini-flash-latest', // fast multimodal, accepts audio input
  'mistralai/voxtral-small-24b-2507', // audio-native fallback
];

const TRANSCRIBE_PROMPT =
  'Transcribe the spoken audio. The speaker is a Pakistani farmer and may speak ' +
  'Urdu, English or a mix of both. Return ONLY the transcript text — no quotes, ' +
  'no labels, no commentary. Preserve the original language(s) exactly as spoken.';

const TARGET_SAMPLE_RATE = 16_000;

export function isVoiceInputConfigured(): boolean {
  return Boolean(import.meta.env.VITE_OPENROUTER_API_KEY);
}

/** Browser can record + OpenRouter key present. */
export function isVoiceInputSupported(): boolean {
  return (
    typeof navigator !== 'undefined' &&
    !!navigator.mediaDevices?.getUserMedia &&
    typeof window.MediaRecorder !== 'undefined' &&
    typeof AudioContext !== 'undefined' &&
    isVoiceInputConfigured()
  );
}

/**
 * Converts any recorded audio blob into a 16 kHz mono PCM16 WAV data URL.
 * Works for webm/opus, mp4, ogg — anything the browser can decode.
 */
async function blobToWavDataUrl(blob: Blob): Promise<string> {
  const arrayBuffer = await blob.arrayBuffer();
  const audioContext = new AudioContext();
  const decoded = await audioContext.decodeAudioData(arrayBuffer);
  await audioContext.close();

  // Down-mix (any channel count -> mono) and resample in one offline pass.
  const length = Math.max(1, Math.ceil(decoded.duration * TARGET_SAMPLE_RATE));
  const offline = new OfflineAudioContext(1, length, TARGET_SAMPLE_RATE);
  const source = offline.createBufferSource();
  source.buffer = decoded;
  source.connect(offline.destination);
  source.start();
  const rendered = await offline.startRendering();

  return 'data:audio/wav;base64,' + arrayBufferToBase64(encodeWav(rendered.getChannelData(0), TARGET_SAMPLE_RATE));
}

/** Standard 44-byte RIFF/WAVE header + interleaved PCM16 samples. */
function encodeWav(samples: Float32Array, sampleRate: number): ArrayBuffer {
  const bytesPerSample = 2;
  const buffer = new ArrayBuffer(44 + samples.length * bytesPerSample);
  const view = new DataView(buffer);

  const writeString = (offset: number, value: string) => {
    for (let i = 0; i < value.length; i++) view.setUint8(offset + i, value.charCodeAt(i));
  };

  writeString(0, 'RIFF');
  view.setUint32(4, 36 + samples.length * bytesPerSample, true);
  writeString(8, 'WAVE');
  writeString(12, 'fmt ');
  view.setUint32(16, 16, true); // fmt chunk size
  view.setUint16(20, 1, true); // PCM
  view.setUint16(22, 1, true); // mono
  view.setUint32(24, sampleRate, true);
  view.setUint32(28, sampleRate * bytesPerSample, true); // byte rate
  view.setUint16(32, bytesPerSample, true); // block align
  view.setUint16(34, 16, true); // bits per sample
  writeString(36, 'data');
  view.setUint32(40, samples.length * bytesPerSample, true);

  let offset = 44;
  for (let i = 0; i < samples.length; i++) {
    const s = Math.max(-1, Math.min(1, samples[i]));
    view.setInt16(offset, s < 0 ? s * 0x8000 : s * 0x7fff, true);
    offset += 2;
  }
  return buffer;
}

function arrayBufferToBase64(buffer: ArrayBuffer): string {
  const bytes = new Uint8Array(buffer);
  let binary = '';
  const chunk = 0x8000;
  for (let i = 0; i < bytes.length; i += chunk) {
    binary += String.fromCharCode(...bytes.subarray(i, i + chunk));
  }
  return btoa(binary);
}

/** Sends the recording to OpenRouter and returns the transcript text. */
export async function transcribeAudioBlob(blob: Blob): Promise<string> {
  const apiKey = import.meta.env.VITE_OPENROUTER_API_KEY;
  if (!apiKey) throw new Error('OpenRouter API key is not configured.');

  const dataUrl = await blobToWavDataUrl(blob);

  let lastError: Error | null = null;
  for (const model of OPENROUTER_VOICE_MODELS) {
    try {
      const res = await fetch('https://openrouter.ai/api/v1/chat/completions', {
        method: 'POST',
        headers: {
          Authorization: `Bearer ${apiKey}`,
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          model,
          max_tokens: 300,
          messages: [
            {
              role: 'user',
              content: [
                { type: 'text', text: TRANSCRIBE_PROMPT },
                { type: 'input_audio', input_audio: { data: dataUrl, format: 'wav' } },
              ],
            },
          ],
        }),
        signal: AbortSignal.timeout(60_000),
      });
      if (!res.ok) throw new Error(`HTTP ${res.status}`);

      const data = await res.json();
      const text: unknown = data?.choices?.[0]?.message?.content;
      if (typeof text === 'string' && text.trim()) return text.trim();
      throw new Error('empty transcription');
    } catch (err) {
      lastError = err instanceof Error ? err : new Error(String(err));
    }
  }

  throw lastError ?? new Error('Voice transcription failed.');
}
