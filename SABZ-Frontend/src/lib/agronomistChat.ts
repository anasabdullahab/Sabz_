import { agronomistApi } from '@/api/agronomistApi';
import { t } from '@/lib/i18n';
import { groqChat, isGroqConfigured, type ChatTurn } from './groqChat';

/** One prior exchange, used to give the AI conversation context. */
export interface AgronomistTurn {
  question: string;
  answer: string;
}

export interface AgronomistAnswer {
  question: string;
  answer: string;
  disclaimer: string | null;
}

/** Backend constant from AgronomistAssistantService (SourceAiProvider). */
const ANSWER_SOURCE_AI_PROVIDER = 'AiProvider';

/**
 * Ask the SABZ agronomist with a provider chain:
 *
 *  1. SABZ backend endpoint — primary; it weaves in the farm context and
 *     answers from its real AI provider when one is configured server-side.
 *  2. Groq direct — secondary chat provider, used when the backend answered
 *     from its offline knowledge base or was unreachable. If Groq fails too,
 *     the backend's local answer (when available) is returned.
 *
 * Throws only when every provider fails — callers map that to their friendly
 * error state.
 */
export async function askAgronomist(
  farmId: string,
  message: string,
  history: AgronomistTurn[],
): Promise<AgronomistAnswer> {
  let localAnswer: AgronomistAnswer | null = null;

  try {
    const res = await agronomistApi.chat(farmId, message);
    if (res.answerSource === ANSWER_SOURCE_AI_PROVIDER) {
      return { question: res.question, answer: res.answer, disclaimer: res.disclaimer };
    }
    // Offline mode: keep the local knowledge-base answer as a fallback.
    localAnswer = { question: res.question, answer: res.answer, disclaimer: res.disclaimer };
  } catch {
    // Backend unreachable — Groq (if configured) gets a chance below.
  }

  if (isGroqConfigured()) {
    try {
      const turns: ChatTurn[] = [];
      for (const h of history.slice(-8)) {
        if (!h.question || !h.answer) continue;
        turns.push({ role: 'user', content: h.question });
        turns.push({ role: 'assistant', content: h.answer });
      }
      turns.push({ role: 'user', content: message });

      const answer = await groqChat(turns);
      return { question: message, answer, disclaimer: t('agronomist.disclaimer') };
    } catch {
      // Groq failed — fall back to the backend's local answer when present.
    }
  }

  if (localAnswer) return localAnswer;
  throw new Error('agronomist-unavailable');
}
