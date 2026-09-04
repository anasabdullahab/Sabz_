/**
 * Secondary chat provider for the AI agronomist: Groq (OpenAI-compatible API).
 *
 * The SABZ backend endpoint stays the primary source (it weaves in the farm
 * context); this module powers the fallback when the backend answers from its
 * offline knowledge base or is unreachable, so the farmer always gets a real
 * conversational answer. All failures throw — the chat UI falls back to the
 * backend's local answer or a friendly error message.
 */

const GROQ_ENDPOINT = 'https://api.groq.com/openai/v1/chat/completions';

/** Ordered model fallbacks: qwen3.8 first (clean output, strong Urdu). */
const MODELS = ['qwen/qwen3.8-27b', 'openai/gpt-oss-120b'];

const TIMEOUT_MS = 45_000;
const MAX_TOKENS = 700;

const SYSTEM_PROMPT = `You are SABZ, a friendly AI agronomist assistant for smallholder farmers in Pakistan.
- Give practical, specific, actionable advice for Pakistani crops (wheat, rice, cotton, sugarcane, maize, mung bean, chickpea, potato, citrus).
- Reply in plain text only — no markdown symbols like **, ### or tables. Use short paragraphs and simple dashes for lists.
- Keep answers under 150 words unless the farmer asks for more detail.
- Use metric units, acres, and PKR for costs.
- Reply in the same language the farmer uses (English or Urdu).
- Never invent pesticide names or dosages you are unsure about; for critical decisions advise consulting the local agriculture extension office.`;

export interface ChatTurn {
  role: 'user' | 'assistant';
  content: string;
}

/** Whether the Groq fallback is configured (VITE_GROQ_API_KEY present). */
export function isGroqConfigured(): boolean {
  return Boolean(import.meta.env.VITE_GROQ_API_KEY);
}

/** Remove stray inline reasoning blocks some models emit. */
function cleanContent(text: string): string {
  return text.replace(/<think>[\s\S]*?<\/think>/gi, '').trim();
}

/**
 * Send a conversation to Groq and return the assistant reply.
 * Tries each model in order; throws when all of them fail.
 */
export async function groqChat(turns: ChatTurn[]): Promise<string> {
  const apiKey = import.meta.env.VITE_GROQ_API_KEY;
  let lastError: unknown = null;

  for (const model of MODELS) {
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), TIMEOUT_MS);
    try {
      const response = await fetch(GROQ_ENDPOINT, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${apiKey ?? ''}` },
        signal: controller.signal,
        body: JSON.stringify({
          model,
          messages: [{ role: 'system', content: SYSTEM_PROMPT }, ...turns],
          max_tokens: MAX_TOKENS,
          temperature: 0.4,
        }),
      });
      if (!response.ok) throw new Error(`groq-${response.status}`);
      const data = await response.json();
      const content: string | undefined = data?.choices?.[0]?.message?.content;
      const cleaned = content ? cleanContent(content) : '';
      if (!cleaned) throw new Error('empty-response');
      return cleaned;
    } catch (err) {
      lastError = err;
    } finally {
      clearTimeout(timer);
    }
  }
  throw lastError ?? new Error('groq-failed');
}
