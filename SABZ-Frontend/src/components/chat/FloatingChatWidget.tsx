import { useEffect, useRef, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { askAgronomist } from '@/lib/agronomistChat';
import { isVoiceInputSupported, transcribeAudioBlob } from '@/lib/openrouterVoice';
import { farmApi } from '@/api/farmApi';
import { useAuth } from '@/hooks/useAuth';
import { t, isRtl } from '@/lib/i18n';
import { cn } from '@/lib/utils';
import { Bot, X, Send, Loader2, Sprout, Mic, Square } from 'lucide-react';
import type { FarmResponseDto } from '@/types';

interface ChatMessage {
  id: string;
  role: 'user' | 'assistant';
  question: string;
  answer: string;
  disclaimer?: string;
  error?: boolean;
}

const STORAGE_KEY = 'sabz.floatingChat';

interface PersistedChatState {
  open: boolean;
  farmId: string;
  messages: ChatMessage[];
}

function loadPersistedState(): PersistedChatState | null {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as PersistedChatState) : null;
  } catch {
    return null;
  }
}

/**
 * Floating AI agronomist chat available on every authenticated page.
 * Mounted once at the router level, so the conversation survives route
 * changes; a sessionStorage mirror also restores it after full page reloads.
 * Farm context: auto-selects the only farm; with several farms the header
 * shows a selector. Farmers without a farm get an "add farm first" hint.
 */
export function FloatingChatWidget() {
  const { pathname } = useLocation();
  const navigate = useNavigate();
  const { user } = useAuth();
  const userId = user?.id;

  const persisted = useRef<PersistedChatState | null>(loadPersistedState()).current;
  const [open, setOpen] = useState(persisted?.open ?? false);
  const [farms, setFarms] = useState<FarmResponseDto[] | null>(null);
  const [farmId, setFarmId] = useState(persisted?.farmId ?? '');
  const [messages, setMessages] = useState<ChatMessage[]>(persisted?.messages ?? []);
  const [input, setInput] = useState('');
  const [sending, setSending] = useState(false);
  const [recording, setRecording] = useState(false);
  const [transcribing, setTranscribing] = useState(false);
  const [voiceError, setVoiceError] = useState(false);
  const [voiceSupported] = useState(() => isVoiceInputSupported());
  const mediaRecorderRef = useRef<MediaRecorder | null>(null);
  const streamRef = useRef<MediaStream | null>(null);
  const chunksRef = useRef<Blob[]>([]);
  const autoStopRef = useRef<number | undefined>(undefined);
  const chatEnd = useRef<HTMLDivElement | null>(null);

  // The full-page agronomist already covers that route — the widget hides
  // there (checked after the hooks to keep hook order stable).
  const hideOnAgronomistPage = pathname.endsWith('/agronomist');

  // Load farms lazily on first open (no API call just for rendering the button).
  useEffect(() => {
    if (!open || farms !== null) return;
    farmApi.getAll()
      .then((f) => {
        setFarms(f);
        if (f.length > 0) setFarmId((prev) => prev || f[0].id);
      })
      .catch(() => setFarms([]));
  }, [open, farms]);

  useEffect(() => {
    if (open) chatEnd.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages, open]);

  // Mirror the conversation to sessionStorage so a full page reload keeps it.
  useEffect(() => {
    try {
      sessionStorage.setItem(STORAGE_KEY, JSON.stringify({ open, farmId, messages: messages.slice(-50) }));
    } catch {
      // Storage unavailable (private mode/quota) — chat still works in memory.
    }
  }, [open, farmId, messages]);

  // Clear the conversation when the signed-in farmer logs out (switching
  // accounts must never show the previous farmer's chat).
  const prevUserId = useRef<string | undefined>(undefined);
  useEffect(() => {
    const wasLoggedIn = prevUserId.current != null;
    prevUserId.current = userId;
    if (!wasLoggedIn || userId != null) return;
    setOpen(false);
    setMessages([]);
    setFarms(null);
    setFarmId('');
    setInput('');
    try {
      sessionStorage.removeItem(STORAGE_KEY);
    } catch {
      // ignore
    }
  }, [userId]);

  // Stop any live recording when the widget unmounts (route change, logout…).
  useEffect(() => {
    return () => {
      if (autoStopRef.current !== undefined) clearTimeout(autoStopRef.current);
      streamRef.current?.getTracks().forEach((track) => track.stop());
      if (mediaRecorderRef.current?.state === 'recording') mediaRecorderRef.current.stop();
    };
  }, []);

  if (!user || hideOnAgronomistPage) return null;

  const sendMessage = async (msg: string) => {
    if (!msg || !farmId || sending) return;
    setSending(true);

    const userMsg: ChatMessage = { id: crypto.randomUUID(), role: 'user', question: msg, answer: '' };
    setMessages((prev) => [...prev, userMsg]);

    // Prior exchanges give the Groq fallback conversation context.
    const history = messages
      .filter((m) => !m.error && m.answer)
      .map((m) => ({ question: m.question, answer: m.answer }));

    try {
      const result = await askAgronomist(farmId, msg, history);
      setMessages((prev) => [
        ...prev,
        {
          id: crypto.randomUUID(),
          role: 'assistant',
          question: result.question,
          answer: result.answer,
          disclaimer: result.disclaimer ?? undefined,
        },
      ]);
    } catch {
      setMessages((prev) => [
        ...prev,
        { id: crypto.randomUUID(), role: 'assistant', question: msg, answer: t('agronomist.error'), error: true },
      ]);
    } finally {
      setSending(false);
    }
  };

  const handleSend = () => {
    const msg = input.trim();
    if (!msg) return;
    setInput('');
    sendMessage(msg);
  };

  const startRecording = async () => {
    if (recording || transcribing || !voiceSupported) return;
    setVoiceError(false);
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
      const recorder = new MediaRecorder(stream);
      chunksRef.current = [];
      recorder.ondataavailable = (e) => {
        if (e.data.size > 0) chunksRef.current.push(e.data);
      };
      recorder.onstop = () => {
        void handleRecordingStopped();
      };
      mediaRecorderRef.current = recorder;
      streamRef.current = stream;
      recorder.start();
      setRecording(true);
      // Safety net: cap the clip so a forgotten mic can't produce a huge upload.
      autoStopRef.current = window.setTimeout(() => stopRecording(), 30_000);
    } catch {
      setVoiceError(true);
    }
  };

  const stopRecording = () => {
    if (mediaRecorderRef.current?.state === 'recording') mediaRecorderRef.current.stop();
  };

  const handleRecordingStopped = async () => {
    if (autoStopRef.current !== undefined) {
      clearTimeout(autoStopRef.current);
      autoStopRef.current = undefined;
    }
    setRecording(false);
    streamRef.current?.getTracks().forEach((track) => track.stop());
    streamRef.current = null;
    const mimeType = mediaRecorderRef.current?.mimeType;
    mediaRecorderRef.current = null;

    const blob = new Blob(chunksRef.current, { type: mimeType || 'audio/webm' });
    chunksRef.current = [];
    if (blob.size === 0) return;

    setTranscribing(true);
    try {
      const text = await transcribeAudioBlob(blob);
      if (!text) {
        setVoiceError(true);
      } else if (sending) {
        // Previous answer still streaming — leave the transcript for the farmer to send.
        setInput(text);
      } else {
        setInput('');
        void sendMessage(text);
      }
    } catch {
      setVoiceError(true);
    } finally {
      setTranscribing(false);
    }
  };

  return (
    <>
      {/* Chat panel */}
      {open && (
        <div className="fixed bottom-24 right-4 sm:right-5 z-50 w-[calc(100vw-2rem)] sm:w-96 h-[28rem] max-h-[70vh] flex flex-col rounded-2xl border border-gray-200 bg-white shadow-2xl overflow-hidden animate-fade-in">
          {/* Header */}
          <div className="shrink-0 bg-gradient-to-r from-indigo-500 to-purple-600 px-4 py-3 flex items-center gap-3">
            <div className="h-9 w-9 rounded-xl bg-white/20 flex items-center justify-center shrink-0">
              <Bot className="h-5 w-5 text-white" />
            </div>
            <div className="flex-1 min-w-0">
              <p className="text-sm font-bold text-white leading-tight">{t('chat.title')}</p>
              <p className="text-[10px] text-indigo-100 truncate">{t('chat.subtitle')}</p>
            </div>
            <button
              onClick={() => setOpen(false)}
              className="p-1.5 rounded-lg text-white/80 hover:bg-white/10 hover:text-white transition-colors shrink-0"
              aria-label={t('chat.close')}
            >
              <X className="h-4 w-4" />
            </button>
          </div>

          {/* Farm selector (only when several farms) */}
          {farms !== null && farms.length > 1 && (
            <div className="shrink-0 px-3 py-2 border-b border-gray-100 bg-gray-50/50">
              <div className="flex items-center gap-2">
                <span className="text-[10px] font-semibold text-gray-400 uppercase tracking-wider shrink-0">
                  {t('chat.farm')}
                </span>
                <select
                  value={farmId}
                  onChange={(e) => setFarmId(e.target.value)}
                  className="flex-1 min-w-0 text-xs bg-white border border-gray-200 rounded-lg px-2 py-1.5 text-gray-700 focus:outline-none focus:ring-1 focus:ring-primary-500"
                >
                  {farms.map((f) => (
                    <option key={f.id} value={f.id}>{f.farmName}</option>
                  ))}
                </select>
              </div>
            </div>
          )}

          {/* Messages / states */}
          <div className="flex-1 overflow-y-auto bg-gray-50 p-3 space-y-3">
            {farms !== null && farms.length === 0 ? (
              <div className="flex flex-col items-center justify-center h-full text-center px-4">
                <Sprout className="h-12 w-12 text-gray-300 mb-3" />
                <p className="text-xs text-gray-500 leading-relaxed">{t('chat.noFarms')}</p>
                <button
                  onClick={() => navigate('/farms/new')}
                  className="mt-3 px-3 py-1.5 rounded-full bg-primary-600 text-white text-xs font-medium hover:bg-primary-700 transition-colors"
                >
                  {t('dashboard.addFirstFarm')}
                </button>
              </div>
            ) : messages.length === 0 ? (
              <div className="flex flex-col items-center justify-center h-full text-center">
                <Bot className="h-12 w-12 text-gray-300 mb-3" />
                <p className="text-xs text-gray-500">{t('agronomist.noMessages')}</p>
                <div className="flex flex-col gap-2 mt-4 w-full max-w-[16rem]">
                  {[t('agronomist.qFertilizer'), t('agronomist.qPest'), t('agronomist.qWater')].map((prompt) => (
                    <button
                      key={prompt}
                      type="button"
                      onClick={() => sendMessage(prompt)}
                      disabled={sending || !farmId}
                      className="px-3 py-1.5 rounded-full bg-white border border-gray-200 text-xs text-gray-600 hover:border-primary-300 hover:text-primary-700 hover:bg-primary-50 transition-colors disabled:opacity-50"
                    >
                      {prompt}
                    </button>
                  ))}
                </div>
              </div>
            ) : (
              <>
                {messages.map((msg) => (
                  <div key={msg.id} className={cn('flex', msg.role === 'user' ? 'justify-end' : 'justify-start')}>
                    {msg.role === 'user' ? (
                      <div className="bg-primary-600 text-white rounded-2xl rounded-br-md px-3.5 py-2.5 max-w-[85%]">
                        <p className="text-sm whitespace-pre-wrap">{msg.question}</p>
                      </div>
                    ) : (
                      <div className="max-w-[85%] space-y-1">
                        <div className={cn(
                          'rounded-2xl rounded-bl-md px-3.5 py-2.5 border',
                          msg.error ? 'bg-red-50 border-red-100' : 'bg-white border-gray-100',
                        )}>
                          <div className="flex items-center gap-1.5 mb-1">
                            <Bot className="h-3 w-3 text-indigo-500" />
                            <span className="text-[10px] font-semibold text-indigo-600 uppercase tracking-wider">
                              {t('agronomist.aiAnswer')}
                            </span>
                          </div>
                          <p className={cn('text-sm whitespace-pre-wrap', msg.error ? 'text-red-600' : 'text-gray-700')}>
                            {msg.answer}
                          </p>
                        </div>
                        {msg.disclaimer && (
                          <p className="text-[10px] text-gray-400 px-2">{msg.disclaimer}</p>
                        )}
                      </div>
                    )}
                  </div>
                ))}
                {sending && (
                  <div className="flex justify-start">
                    <div className="bg-white rounded-2xl rounded-bl-md px-3.5 py-2.5 border border-gray-100">
                      <div className="flex items-center gap-2">
                        <Loader2 className="h-3.5 w-3.5 animate-spin text-indigo-500" />
                        <span className="text-xs text-gray-500">{t('agronomist.sending')}</span>
                      </div>
                    </div>
                  </div>
                )}
                <div ref={chatEnd} />
              </>
            )}
          </div>

          {/* Input */}
          {farms !== null && farms.length > 0 && (
            <>
              {(recording || transcribing) && (
                <div className="shrink-0 px-3 py-1.5 bg-red-50 border-t border-red-100 flex items-center gap-2">
                  <span className="h-2 w-2 rounded-full bg-red-500 animate-pulse shrink-0" />
                  <span className="text-[11px] font-medium text-red-600">
                    {recording ? t('chat.recording') : t('chat.transcribing')}
                  </span>
                </div>
              )}
              {voiceError && (
                <div className="shrink-0 px-3 py-1.5 bg-red-50 border-t border-red-100 flex items-center gap-2">
                  <Mic className="h-3 w-3 text-red-500 shrink-0" />
                  <span className="text-[11px] text-red-600">{t('chat.voiceError')}</span>
                </div>
              )}
              <div className="shrink-0 border-t border-gray-100 p-2.5 flex items-center gap-2 bg-white">
                <input
                  type="text"
                  value={input}
                  onChange={(e) => setInput(e.target.value)}
                  onKeyDown={(e) => { if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); handleSend(); } }}
                  placeholder={t('agronomist.askPlaceholder')}
                  disabled={sending}
                  className="flex-1 px-3 py-2 rounded-xl border border-gray-200 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-primary-500 focus:border-transparent disabled:opacity-50"
                />
                {voiceSupported && (
                  <button
                    type="button"
                    onClick={recording ? stopRecording : startRecording}
                    disabled={transcribing}
                    className={cn(
                      'h-9 w-9 rounded-xl flex items-center justify-center transition-colors shrink-0 disabled:opacity-50',
                      recording
                        ? 'bg-red-500 text-white hover:bg-red-600 animate-pulse'
                        : 'bg-gray-100 text-gray-500 hover:bg-gray-200 hover:text-gray-700',
                    )}
                    aria-label={recording ? t('chat.recording') : t('chat.voice')}
                    title={recording ? t('chat.recording') : t('chat.voice')}
                  >
                    {transcribing ? (
                      <Loader2 className="h-4 w-4 animate-spin" />
                    ) : recording ? (
                      <Square className="h-3.5 w-3.5 fill-current" />
                    ) : (
                      <Mic className="h-4 w-4" />
                    )}
                  </button>
                )}
                <button
                  onClick={handleSend}
                  disabled={sending || recording || transcribing || !input.trim() || !farmId}
                  className="h-9 w-9 rounded-xl bg-primary-600 flex items-center justify-center text-white hover:bg-primary-700 transition-colors disabled:opacity-50 shrink-0"
                  aria-label={t('agronomist.send')}
                >
                  <Send className="h-4 w-4" />
                </button>
              </div>
            </>
          )}
        </div>
      )}

      {/* Floating action button */}
      <button
        onClick={() => setOpen((v) => !v)}
        className={cn(
          'fixed bottom-5 right-5 z-50 h-14 w-14 rounded-full flex items-center justify-center text-white shadow-xl transition-all hover:scale-105',
          open
            ? 'bg-gray-700 hover:bg-gray-800'
            : 'bg-gradient-to-br from-indigo-500 to-purple-600 hover:from-indigo-600 hover:to-purple-700',
        )}
        aria-label={open ? t('chat.close') : t('chat.open')}
        title={open ? t('chat.close') : t('chat.open')}
      >
        {open ? <X className="h-6 w-6" /> : <Bot className="h-6 w-6" />}
        {!open && (
          <span className={`absolute -top-0.5 h-3 w-3 rounded-full bg-emerald-400 ring-2 ring-white ${isRtl() ? '-left-0.5' : '-right-0.5'}`} />
        )}
      </button>
    </>
  );
}
