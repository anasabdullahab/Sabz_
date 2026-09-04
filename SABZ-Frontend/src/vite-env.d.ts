/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_API_BASE_URL: string;
  /** OpenRouter key — vision provider chain for the Disease Camera leaf guard. */
  readonly VITE_OPENROUTER_API_KEY?: string;
  /** Groq key — secondary chat provider for the AI agronomist chatbot. */
  readonly VITE_GROQ_API_KEY?: string;
  /** Google AI (Gemini) key for direct Disease Camera vision. Optional — OpenRouter is used when absent. */
  readonly VITE_GEMINI_API_KEY?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
