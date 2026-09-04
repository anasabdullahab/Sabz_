/**
 * Tier 2 of the Disease Camera validation pipeline: the AI Vision Leaf Guard.
 *
 * Every photo is sent to a vision model with a strict system instruction
 * that first classifies whether the image shows a plant leaf / crop / plant
 * part. Non-plant images (sky, rooms, faces, animals, cars...) are rejected
 * BEFORE any disease report or pesticide suggestion is generated; the caller
 * renders a rejection card instead of a diagnostic report.
 *
 * Provider chain (first success wins):
 *   1. Google Gemini direct — only when VITE_GEMINI_API_KEY is set.
 *   2. OpenRouter vision models (VITE_OPENROUTER_API_KEY), in order:
 *      "~google/gemini-flash-latest" (premium, best quality),
 *      "google/gemma-4-31b-it:free" (free tier) and "openrouter/free"
 *      (auto-routing last resort).
 *
 * All failures (network, timeout, quota, invalid key, malformed response)
 * throw — the caller maps every one of them to the same graceful message, so
 * raw technical errors never reach the farmer.
 */

const GEMINI_ENDPOINT =
  'https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent';
const OPENROUTER_ENDPOINT = 'https://openrouter.ai/api/v1/chat/completions';

/** Generous ceiling — vision calls normally return in a few seconds. */
const TIMEOUT_MS = 60_000;

/** Ordered OpenRouter fallbacks: premium Gemini first, free tiers after. */
const OPENROUTER_MODELS = [
  '~google/gemini-flash-latest',
  'google/gemma-4-31b-it:free',
  'openrouter/free',
];

const LEAF_GUARD_SYSTEM_PROMPT = `You are a strict agricultural computer vision classifier. Analyze the provided image. First, determine if the image contains a plant leaf, crop leaf, or agricultural plant part showing signs of health or disease.

Return ONLY a raw JSON object with NO markdown formatting using this exact schema:
{
  "isPlantLeaf": boolean,
  "rejectionReason": string or null,
  "diseaseName": string or null,
  "severity": string or null,
  "pesticide": string or null,
  "dosagePerAcre": string or null,
  "prevention": string or null
}`;

export interface LeafGuardResult {
  isPlantLeaf: boolean;
  rejectionReason: string | null;
  diseaseName: string | null;
  severity: string | null;
  pesticide: string | null;
  dosagePerAcre: string | null;
  prevention: string | null;
  /** Which provider produced the analysis (UI metadata, not part of the AI schema). */
  servedBy?: string;
}

/** Whether any AI vision provider is configured (Gemini direct or OpenRouter). */
export function isVisionAiConfigured(): boolean {
  return Boolean(import.meta.env.VITE_GEMINI_API_KEY || import.meta.env.VITE_OPENROUTER_API_KEY);
}

function fileToBase64(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => {
      const dataUrl = String(reader.result);
      resolve(dataUrl.slice(dataUrl.indexOf(',') + 1));
    };
    reader.onerror = () => reject(new Error('image-read-failed'));
    reader.readAsDataURL(file);
  });
}

async function fetchWithTimeout(url: string, init: RequestInit): Promise<Response> {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), TIMEOUT_MS);
  try {
    return await fetch(url, { ...init, signal: controller.signal });
  } finally {
    clearTimeout(timer);
  }
}

/** Gemini direct call (native API shape) — returns the raw text reply. */
async function callGemini(base64: string, mimeType: string, contextHint?: string): Promise<string> {
  const apiKey = import.meta.env.VITE_GEMINI_API_KEY;
  const response = await fetchWithTimeout(GEMINI_ENDPOINT, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', 'x-goog-api-key': apiKey ?? '' },
    body: JSON.stringify({
      systemInstruction: { parts: [{ text: LEAF_GUARD_SYSTEM_PROMPT }] },
      contents: [
        {
          role: 'user',
          parts: [
            { inline_data: { mime_type: mimeType, data: base64 } },
            { text: contextHint ? `Analyze this image. ${contextHint}` : 'Analyze this image.' },
          ],
        },
      ],
      generationConfig: { temperature: 0.1, responseMimeType: 'application/json' },
    }),
  });
  if (!response.ok) throw new Error(`gemini-${response.status}`);
  const data = await response.json();
  const text: string | undefined = data?.candidates?.[0]?.content?.parts?.[0]?.text;
  if (!text) throw new Error('empty-response');
  return text;
}

/** OpenRouter call (OpenAI-compatible shape) for one model — returns the raw text reply. */
async function callOpenRouter(
  model: string,
  base64: string,
  mimeType: string,
  contextHint?: string,
): Promise<{ text: string; servedBy: string }> {
  const apiKey = import.meta.env.VITE_OPENROUTER_API_KEY;
  const response = await fetchWithTimeout(OPENROUTER_ENDPOINT, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${apiKey ?? ''}` },
    body: JSON.stringify({
      model,
      messages: [
        { role: 'system', content: LEAF_GUARD_SYSTEM_PROMPT },
        {
          role: 'user',
          content: [
            { type: 'text', text: contextHint ? `Analyze this image. ${contextHint}` : 'Analyze this image.' },
            { type: 'image_url', image_url: { url: `data:${mimeType};base64,${base64}` } },
          ],
        },
      ],
      max_tokens: 1000,
      temperature: 0.1,
    }),
  });
  if (!response.ok) throw new Error(`openrouter-${response.status}`);
  const data = await response.json();
  const text: string | undefined = data?.choices?.[0]?.message?.content;
  if (!text || !text.trim()) throw new Error('empty-response');
  return { text, servedBy: `${data?.model ?? model} · OpenRouter` };
}

/**
 * Run the two-part leaf guard analysis on an uploaded photo.
 * Optional contextHint carries the farmer's selected crop / notes.
 * Throws on any failure — callers show a single graceful message.
 */
export async function analyzeLeafImage(file: File, contextHint?: string): Promise<LeafGuardResult> {
  const base64 = await fileToBase64(file);
  const mimeType = file.type || 'image/jpeg';

  // Ordered provider attempts: Gemini direct (optional), then OpenRouter models.
  const attempts: Array<() => Promise<{ text: string; servedBy?: string }>> = [];
  if (import.meta.env.VITE_GEMINI_API_KEY) {
    attempts.push(async () => ({ text: await callGemini(base64, mimeType, contextHint), servedBy: 'Gemini 2.0 Flash' }));
  }
  if (import.meta.env.VITE_OPENROUTER_API_KEY) {
    for (const model of OPENROUTER_MODELS) {
      attempts.push(() => callOpenRouter(model, base64, mimeType, contextHint));
    }
  }
  if (attempts.length === 0) throw new Error('no-vision-provider');

  let lastError: unknown = null;
  for (const attempt of attempts) {
    try {
      const { text, servedBy } = await attempt();
      const result = parseLeafGuardResult(text);
      return servedBy ? { ...result, servedBy } : result;
    } catch (err) {
      lastError = err;
    }
  }
  throw lastError ?? new Error('leaf-guard-failed');
}

/** Parse the model output into the strict schema; defensive against fences. */
function parseLeafGuardResult(text: string): LeafGuardResult {
  let cleaned = text.trim();
  if (cleaned.startsWith('```')) {
    const firstNewline = cleaned.indexOf('\n');
    if (firstNewline > 0) cleaned = cleaned.slice(firstNewline + 1);
    if (cleaned.endsWith('```')) cleaned = cleaned.slice(0, -3);
    cleaned = cleaned.trim();
  }

  const parsed = JSON.parse(cleaned) as Partial<LeafGuardResult>;
  return {
    isPlantLeaf: parsed.isPlantLeaf === true,
    rejectionReason: parsed.rejectionReason ?? null,
    diseaseName: parsed.diseaseName ?? null,
    severity: parsed.severity ?? null,
    pesticide: parsed.pesticide ?? null,
    dosagePerAcre: parsed.dosagePerAcre ?? null,
    prevention: parsed.prevention ?? null,
  };
}
